using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoveInPlanner.Data;
using MoveInPlanner.Models.Enums;
using MoveInPlanner.Models.ViewModels;

namespace MoveInPlanner.Controllers;

public class DashboardController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var items = await db.HouseholdItems
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.ProductChoices)
            .Include(x => x.SelectedProductChoice)
            .OrderByDescending(x => x.IsEssentialForMoveIn)
            .ThenByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToListAsync();

        var model = new DashboardViewModel
        {
            TotalItems = items.Count,
            PurchasedItems = items.Count(x => x.Status == PurchaseStatus.Purchased),
            EssentialOutstanding = items.Count(x => x.IsEssentialForMoveIn && x.Status != PurchaseStatus.Purchased),
            ItemsWithoutChoices = items.Count(x => x.ProductChoices.Count == 0),
            PlannedBudget = items.Sum(x => x.TargetBudget ?? 0),
            PurchasedSpend = items.Sum(x => x.ActualPurchasePrice ?? 0),
            LoggedOptionsValue = items.Sum(item => item.ProductChoices.Sum(choice => choice.Price * choice.Quantity)),
            CurrentPlanValue = items.Sum(item =>
            {
                var preferredChoices = item.ProductChoices
                    .Where(choice => choice.IsPreferred)
                    .ToList();

                if (preferredChoices.Count > 0)
                    return preferredChoices.Sum(choice => choice.Price * choice.Quantity);

                var budgetFallback = item.ProductChoices
                    .OrderBy(choice => choice.Price * choice.Quantity)
                    .FirstOrDefault();

                return budgetFallback is null ? 0 : budgetFallback.Price * budgetFallback.Quantity;
            }),
            Categories = items.GroupBy(x => x.Category.Name)
                .OrderBy(x => x.Key)
                .Select(group => new CategoryDashboardGroup
                {
                    Name = group.Key,
                    Total = group.Count(),
                    Purchased = group.Count(x => x.Status == PurchaseStatus.Purchased),
                    Items = group.Take(6).ToList()
                }).ToList(),
            NextActions = items
                .Where(x => x.Status != PurchaseStatus.Purchased)
                .OrderByDescending(x => x.IsEssentialForMoveIn)
                .ThenByDescending(x => x.Priority)
                .ThenBy(x => x.NeededBy ?? DateTime.MaxValue)
                .Take(6).ToList()
        };

        return View(model);
    }
}
