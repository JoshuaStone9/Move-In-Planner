using MoveInPlanner.Models.Entities;
using MoveInPlanner.Models.Enums;

namespace MoveInPlanner.Services;

public static class HouseholdItemValueCalculator
{
    private static decimal ChoiceValue(ProductChoice choice) => choice.Price * choice.Quantity;

    public static decimal? CheapestOptionValue(HouseholdItem item) => item.ProductChoices.Count == 0 ? null : item.ProductChoices.Min(ChoiceValue);

    public static decimal? PreferredPlanValue(HouseholdItem item)
    {
        var preferredChoices = item.ProductChoices
            .Where(choice => choice.IsPreferred)
            .ToList();

        return preferredChoices.Count == 0 ? null : preferredChoices.Sum(ChoiceValue);
    }

    public static decimal CurrentPlanValue(HouseholdItem item)
    {
        var preferredPlanValue = PreferredPlanValue(item);

        if (preferredPlanValue.HasValue)
            return preferredPlanValue.Value;

        return item.Status == PurchaseStatus.Purchased ? 0 : CheapestOptionValue(item) ?? 0;
    }

    public static decimal PurchasedValue(HouseholdItem item) => item.Status == PurchaseStatus.Purchased ? PreferredPlanValue(item) ?? 0 : 0;

    public static decimal LoggedOptionsValue(HouseholdItem item) => item.ProductChoices.Sum(ChoiceValue);
}