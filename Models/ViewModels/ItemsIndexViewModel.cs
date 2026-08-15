using MoveInPlanner.Models.Enums;

namespace MoveInPlanner.Models.ViewModels;

/// <summary>
/// Read model for the grouped All Items page. The page is deliberately grouped
/// by category so the shopping list remains usable as the number of items grows.
/// </summary>
public sealed class ItemsIndexViewModel
{
    public string? Search { get; init; }
    public int? CategoryId { get; init; }
    public PurchaseStatus? Status { get; init; }

    public int TotalItems { get; init; }
    public int PurchasedItems { get; init; }
    public decimal CurrentPlanValue { get; init; }
    public decimal PurchasedValue { get; init; }

    public IReadOnlyList<ItemCategorySectionViewModel> Categories { get; init; } = [];

    public decimal CompletionPercentage => TotalItems == 0
        ? 0
        : Math.Round((decimal)PurchasedItems / TotalItems * 100, 0);
}

public sealed class ItemCategorySectionViewModel
{
    public int CategoryId { get; init; }
    public required string Name { get; init; }
    public int TotalItems { get; init; }
    public int PurchasedItems { get; init; }
    public decimal CurrentPlanValue { get; init; }
    public IReadOnlyList<ItemListCardViewModel> Items { get; init; } = [];

    public decimal CompletionPercentage => TotalItems == 0
        ? 0
        : Math.Round((decimal)PurchasedItems / TotalItems * 100, 0);
}

public sealed class ItemListCardViewModel
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public string? GeneralNotes { get; init; }
    public PurchaseStatus Status { get; init; }
    public ItemPriority Priority { get; init; }
    public bool IsEssentialForMoveIn { get; init; }
    public int QuantityRequired { get; init; }
    public decimal? TargetBudget { get; init; }
    public int PurchaseOptionCount { get; init; }
    public int PreferredOptionCount { get; init; }
    public decimal? PreferredPlanValue { get; init; }
    public decimal? CheapestOptionValue { get; init; }

    /// <summary>
    /// Preferred options are used when present; otherwise the cheapest option is
    /// displayed as the current planning fallback, matching the dashboard logic.
    /// </summary>
    public decimal? DisplayPlanValue => PreferredPlanValue ?? CheapestOptionValue;
}
