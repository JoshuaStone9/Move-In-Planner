using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MoveInPlanner.Data;
using MoveInPlanner.Models.Entities;
using MoveInPlanner.Models.ViewModels;

namespace MoveInPlanner.Controllers;

public class ItemsController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? search, int? categoryId, MoveInPlanner.Models.Enums.PurchaseStatus? status)
    {
        var query = db.HouseholdItems
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.ProductChoices)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmedSearch = search.Trim();
            query = query.Where(x => x.Name.Contains(trimmedSearch));
        }

        if (categoryId.HasValue)
            query = query.Where(x => x.CategoryId == categoryId);

        if (status.HasValue)
            query = query.Where(x => x.Status == status);

        var items = await query
            .OrderBy(x => x.Category.Name)
            .ThenBy(x => x.Name)
            .ToListAsync();

        ViewBag.Categories = new SelectList(
            await db.Categories.AsNoTracking().OrderBy(x => x.Name).ToListAsync(),
            "Id", "Name", categoryId);

        static decimal? CheapestOptionValue(HouseholdItem item)
        {
            if (item.ProductChoices.Count == 0)
                return null;

            return item.ProductChoices.Min(choice => choice.Price * choice.Quantity);
        }

        static decimal? PreferredPlanValue(HouseholdItem item)
        {
            var preferred = item.ProductChoices.Where(choice => choice.IsPreferred).ToList();
            return preferred.Count == 0
                ? null
                : preferred.Sum(choice => choice.Price * choice.Quantity);
        }

        static decimal CurrentPlanValue(HouseholdItem item)
            => PreferredPlanValue(item) ?? CheapestOptionValue(item) ?? 0;

        var model = new ItemsIndexViewModel
        {
            Search = search,
            CategoryId = categoryId,
            Status = status,
            TotalItems = items.Count,
            PurchasedItems = items.Count(x => x.Status == MoveInPlanner.Models.Enums.PurchaseStatus.Purchased),
            CurrentPlanValue = items.Sum(CurrentPlanValue),
            PurchasedValue = items.Sum(item =>
                item.ActualPurchasePrice
                ?? item.ProductChoices.Where(choice => choice.IsPurchased)
                    .Sum(choice => choice.Price * choice.Quantity)),
            Categories = items
                .GroupBy(item => new { item.CategoryId, item.Category.Name })
                .OrderBy(group => group.Key.Name)
                .Select(group => new ItemCategorySectionViewModel
                {
                    CategoryId = group.Key.CategoryId,
                    Name = group.Key.Name,
                    TotalItems = group.Count(),
                    PurchasedItems = group.Count(item => item.Status == MoveInPlanner.Models.Enums.PurchaseStatus.Purchased),
                    CurrentPlanValue = group.Sum(CurrentPlanValue),
                    Items = group
                        .OrderBy(item => item.Name)
                        .Select(item => new ItemListCardViewModel
                        {
                            Id = item.Id,
                            Name = item.Name,
                            GeneralNotes = item.GeneralNotes,
                            Status = item.Status,
                            Priority = item.Priority,
                            IsEssentialForMoveIn = item.IsEssentialForMoveIn,
                            QuantityRequired = item.QuantityRequired,
                            TargetBudget = item.TargetBudget,
                            PurchaseOptionCount = item.ProductChoices.Count,
                            PreferredOptionCount = item.ProductChoices.Count(choice => choice.IsPreferred),
                            PreferredPlanValue = PreferredPlanValue(item),
                            CheapestOptionValue = CheapestOptionValue(item)
                        })
                        .ToList()
                })
                .ToList()
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var item = await db.HouseholdItems
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.ProductChoices)
            .Include(x => x.SelectedProductChoice)
            .SingleOrDefaultAsync(x => x.Id == id);

        if (item is null)
            return NotFound();

        var optionTotals = item.ProductChoices
            .Select(choice => choice.Price * choice.Quantity)
            .ToList();

        var preferredChoices = item.ProductChoices
            .Where(choice => choice.IsPreferred)
            .ToList();

        return View(new ItemDetailsViewModel
        {
            Item = item,
            LoggedOptionsValue = optionTotals.Sum(),
            CheapestOptionValue = optionTotals.Count == 0 ? null : optionTotals.Min(),
            HighestOptionValue = optionTotals.Count == 0 ? null : optionTotals.Max(),
            PreferredOptionValue = preferredChoices.Count == 0
                ? null
                : preferredChoices.Sum(choice => choice.Price * choice.Quantity),
            PreferredOptionCount = preferredChoices.Count
        });
    }

    public async Task<IActionResult> Create()
    {
        var model = new ItemFormViewModel();
        await PopulateCategories(model);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ItemFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCategories(model);
            return View(model);
        }

        var item = new HouseholdItem();
        ApplyForm(item, model);

        db.Add(item);
        await db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"{item.Name} was added. Add a purchase option when you are ready.";
        return RedirectToAction(nameof(Details), new { id = item.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await db.HouseholdItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (item is null)
            return NotFound();

        var model = new ItemFormViewModel
        {
            Id = item.Id,
            Name = item.Name,
            CategoryId = item.CategoryId,
            ChoiceType = item.ChoiceType,
            Priority = item.Priority,
            Status = item.Status,
            QuantityRequired = item.QuantityRequired,
            TargetBudget = item.TargetBudget,
            IsEssentialForMoveIn = item.IsEssentialForMoveIn,
            NeededBy = item.NeededBy,
            GeneralNotes = item.GeneralNotes,
            DecisionReason = item.DecisionReason,
            FullComparisonResponse = item.FullComparisonResponse
        };

        await PopulateCategories(model);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ItemFormViewModel model)
    {
        if (model.Id != id)
            return BadRequest();

        if (!ModelState.IsValid)
        {
            await PopulateCategories(model);
            return View(model);
        }

        var item = await db.HouseholdItems.SingleOrDefaultAsync(x => x.Id == id);
        if (item is null)
            return NotFound();

        ApplyForm(item, model);
        item.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"{item.Name} was updated.";
        return RedirectToAction(nameof(Details), new { id = item.Id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var item = await db.HouseholdItems
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.ProductChoices)
            .SingleOrDefaultAsync(x => x.Id == id);

        return item is null ? NotFound() : View(item);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await db.HouseholdItems.SingleOrDefaultAsync(x => x.Id == id);
        if (item is null)
            return NotFound();

        var name = item.Name;

        // Break the optional selected-choice reference before deleting the item.
        // Product choices are then removed by the configured cascade relationship.
        if (item.SelectedProductChoiceId.HasValue)
        {
            item.SelectedProductChoiceId = null;
            await db.SaveChangesAsync();
        }

        db.Remove(item);
        await db.SaveChangesAsync();
        TempData["SuccessMessage"] = $"{name} and its purchase options were deleted.";
        return RedirectToAction(nameof(Index));
    }

    private static void ApplyForm(HouseholdItem item, ItemFormViewModel model)
    {
        item.Name = model.Name.Trim();
        item.CategoryId = model.CategoryId;
        item.ChoiceType = model.ChoiceType;
        item.Priority = model.Priority;
        item.Status = model.Status;
        item.QuantityRequired = model.QuantityRequired;
        item.TargetBudget = model.TargetBudget;
        item.IsEssentialForMoveIn = model.IsEssentialForMoveIn;
        item.NeededBy = model.NeededBy;
        item.GeneralNotes = string.IsNullOrWhiteSpace(model.GeneralNotes) ? null : model.GeneralNotes.Trim();
        item.DecisionReason = string.IsNullOrWhiteSpace(model.DecisionReason) ? null : model.DecisionReason.Trim();
        item.FullComparisonResponse = string.IsNullOrWhiteSpace(model.FullComparisonResponse) ? null : model.FullComparisonResponse.Trim();
    }

    private async Task PopulateCategories(ItemFormViewModel model)
    {
        model.Categories = await db.Categories.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync();
    }
}
