namespace Meridian.Application.Common;

using Meridian.Application.Exceptions;

public static class BillingClassificationRules
{
    public static readonly string[] Classifications = ["Billable", "NonBillable", "PartialBillable"];

    public static readonly IReadOnlyDictionary<string, string[]> AllowedCategories = new Dictionary<string, string[]>
    {
        ["Billable"] = ["AMS", "T&M", "FB"],
        ["NonBillable"] = ["OH"],
        ["PartialBillable"] = [],
    };

    public static void Validate(string classification, string? billingCategory)
    {
        if (!Classifications.Contains(classification))
            throw new BusinessRuleException($"'{classification}' is not a valid classification. Use Billable, NonBillable, or PartialBillable.");

        if (billingCategory is null) return;

        var allowed = AllowedCategories[classification];
        if (!allowed.Contains(billingCategory))
        {
            throw new BusinessRuleException(
                allowed.Length == 0
                    ? $"'{classification}' does not take a billing category — leave it blank."
                    : $"'{billingCategory}' is not valid for '{classification}'. Allowed: {string.Join(", ", allowed)}.");
        }
    }
}
