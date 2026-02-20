using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PortfolioWorkflows;

internal static class RetirementTools
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");

    [Description("Projects retirement balance using compound growth formula")]
    public static string ProjectRetirement(
        [Description("Expected annual return (e.g. 0.07)")] double expectedReturn = 0.07)
    {
        var profile = JsonNode.Parse(File.ReadAllText(Path.Combine(DataDir, "investor_profile.json")))!;
        double totalBalance =
            profile["accounts"]!["taxable"]!["balance"]!.GetValue<double>() +
            profile["accounts"]!["roth"]!["balance"]!.GetValue<double>() +
            profile["accounts"]!["traditional"]!["balance"]!.GetValue<double>();
        double monthlyContribution = profile["monthlyContribution"]!.GetValue<double>();
        int currentAge = profile["age"]!.GetValue<int>();
        int retirementAge = profile["retirementAge"]!.GetValue<int>();
        int years = retirementAge - currentAge;
        double annualContribution = monthlyContribution * 12;

        // FV = PV × (1+r)^n + PMT × ((1+r)^n - 1) / r
        double growthFactor = Math.Pow(1 + expectedReturn, years);
        double projectedBalance = totalBalance * growthFactor +
            annualContribution * (growthFactor - 1) / expectedReturn;

        double goal = profile["goals"]![0]!["targetAmount"]!.GetValue<double>();
        double gap = goal - projectedBalance;

        var result = new
        {
            currentBalance = Math.Round(totalBalance, 2),
            projectedBalance = Math.Round(projectedBalance, 2),
            yearsToRetirement = years,
            expectedReturn = expectedReturn,
            annualContribution = annualContribution,
            retirementGoal = goal,
            projectedGap = Math.Round(gap, 2),
            onTrack = projectedBalance >= goal
        };
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }
}
