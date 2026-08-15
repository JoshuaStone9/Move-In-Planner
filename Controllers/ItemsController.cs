using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MoveInPlanner.Data;
using MoveInPlanner.Models.Entities;
using MoveInPlanner.Models.Enums;
using MoveInPlanner.Models.ViewModels;
using MoveInPlanner.Services;

namespace MoveInPlanner.Controllers;

public class ItemsController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? search, int? categoryId, PurchaseStatus? status)
    {
        var query = db.HouseholdItems
            .AsNoTracking()
            .Include(item => item.Category)
            .Include(item => item.ProductChoices)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmedSearch = search.Trim();
            query = query.Where(item => item.Name.Contains(trimmedSearch));
        }

        if (categoryId.HasValue)
            query = query.Where(item => item.CategoryId == categoryId);

        if (status.HasValue)
            query = query.Where(item => item.Status == status);

        var items = await query
            .OrderBy(item => item.Category.Name)
            .ThenBy(item => item.Name)
            .ToListAsync();

        ViewBag.Categories = new SelectList(
            await db.Categories
                .AsNoTracking()
                .OrderBy(category => category.Name)
                .ToListAsync(),
            "Id",
            "Name",
            categoryId);

        var model = new ItemsIndexViewModel
        {
            Search = search,
            CategoryId = categoryId,
            Status = status,
            TotalItems = items.Count,

            PurchasedItems = items.Count(item => item.Status == PurchaseStatus.Purchased),

            CurrentPlanValue = items.Sum(HouseholdItemValueCalculator.CurrentPlanValue),

            PurchasedValue = items.Sum(HouseholdItemValueCalculator.PurchasedValue),

            Categories = items
                .GroupBy(item => new
                {
                    item.CategoryId,
                    item.Category.Name
                })
                .OrderBy(group => group.Key.Name)
                .Select(group => new ItemCategorySectionViewModel
                {
                    CategoryId = group.Key.CategoryId,
                    Name = group.Key.Name,
                    TotalItems = group.Count(),

                    PurchasedItems = group.Count(item => item.Status == PurchaseStatus.Purchased),

                    CurrentPlanValue = group.Sum(HouseholdItemValueCalculator.CurrentPlanValue),

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
                            PreferredPlanValue = HouseholdItemValueCalculator.PreferredPlanValue(item),
                            CheapestOptionValue = HouseholdItemValueCalculator.CheapestOptionValue(item)
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
            .Include(item => item.Category)
            .Include(item => item.ProductChoices)
            .Include(item => item.SelectedProductChoice)
            .SingleOrDefaultAsync(item => item.Id == id);

        if (item is null)
            return NotFound();

        var optionTotals = item.ProductChoices
            .Select(choice => choice.Price * choice.Quantity)
            .ToList();

        return View(new ItemDetailsViewModel
        {
            Item = item,
            LoggedOptionsValue = HouseholdItemValueCalculator.LoggedOptionsValue(item),
            CheapestOptionValue = HouseholdItemValueCalculator.CheapestOptionValue(item),
            HighestOptionValue = optionTotals.Count == 0 ? null : optionTotals.Max(),
            PreferredOptionValue = HouseholdItemValueCalculator.PreferredPlanValue(item),
            PreferredOptionCount = item.ProductChoices.Count(choice => choice.IsPreferred)
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
        var item = await db.HouseholdItems
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id);

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

        var item = await db.HouseholdItems
            .SingleOrDefaultAsync(item => item.Id == id);

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
            .Include(item => item.Category)
            .Include(item => item.ProductChoices)
            .SingleOrDefaultAsync(item => item.Id == id);

        return item is null ? NotFound() : View(item);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await db.HouseholdItems
            .SingleOrDefaultAsync(item => item.Id == id);

        if (item is null)
            return NotFound();

        var name = item.Name;

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
        model.Categories = await db.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new SelectListItem(
                category.Name,
                category.Id.ToString()))
            .ToListAsync();
    }
}