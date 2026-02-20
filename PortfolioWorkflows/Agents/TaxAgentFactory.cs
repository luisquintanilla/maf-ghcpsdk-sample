using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace PortfolioWorkflows;

/// <summary>
/// Factory for the tax optimization sub-agent.
/// Owns Z3-based tools for asset location and tax-loss harvesting.
/// </summary>
internal static class TaxAgentFactory
{
    public static AIAgent Create(CopilotClient client)
    {
        AIFunction assetLocationTool = AIFunctionFactory.Create(
            (Func<string>)TaxTools.OptimizeAssetLocation,
            name: "optimize_asset_location",
            description: "Uses Z3 constraint solver to recommend optimal account type for each holding to minimize tax drag");

        AIFunction harvestTool = AIFunctionFactory.Create(
            (Func<double, string>)TaxTools.FindHarvestCandidates,
            name: "find_harvest_candidates",
            description: "Uses Z3 to find tax-loss harvesting candidates while respecting wash sale rules. Pass target harvest amount or 0 to maximize.");

        AIFunction savingsTool = AIFunctionFactory.Create(
            (Func<string>)TaxTools.ComputeTaxSavings,
            name: "compute_tax_savings",
            description: "Computes estimated annual tax savings from optimized asset location using TensorPrimitives");

        AIFunction chartTool = AIFunctionFactory.Create(
            (Func<string>)TaxTools.RenderTaxChart,
            name: "render_tax_chart",
            description: "Renders a Plotly waterfall chart of tax optimization savings and saves it as an HTML file");

        return client.AsAIAgent(
            name: "Tax Optimization Agent",
            description: "Optimises portfolio tax efficiency using Z3 constraint solving for asset location and tax-loss harvesting",
            tools: [assetLocationTool, harvestTool, savingsTool, chartTool],
            instructions:
                "You are a tax optimization specialist. Use your tools to analyse " +
                "tax efficiency and recommend improvements. Always show concrete numbers. " +
                "When presenting tax-loss harvesting candidates, clearly list each lot with " +
                "its potential loss and any wash sale warnings. Never fabricate data. " +
                "Write for someone who is NOT a financial expert. Use everyday language; avoid jargon. " +
                "When you must use a technical term, explain it in parentheses " +
                "(e.g., \"diversification (spreading investments to reduce risk)\"). " +
                "Frame numbers in terms of real-world impact " +
                "(e.g., \"This could save you about $3,200 per year\" not " +
                "\"The tax alpha is 32 basis points\").");
    }
}
