using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace PortfolioTaxAdvisor;

/// <summary>
/// Factory for the portfolio analysis sub-agent (reused from PortfolioAdvisor).
/// </summary>
internal static class AnalysisAgentFactory
{
    public static AIAgent Create(CopilotClient client)
    {
        AIFunction summaryTool = AIFunctionFactory.Create(
            (Func<string>)PowerShellTools.GetPortfolioSummary,
            name: "get_portfolio_summary",
            description: "Returns total portfolio value, cost basis, gain/loss, and holding count");

        AIFunction sectorTool = AIFunctionFactory.Create(
            (Func<string>)PowerShellTools.GetSectorBreakdown,
            name: "get_sector_breakdown",
            description: "Returns portfolio breakdown by sector with value and weight percentages");

        AIFunction topHoldingsTool = AIFunctionFactory.Create(
            (Func<int, string>)PowerShellTools.GetTopHoldings,
            name: "get_top_holdings",
            description: "Returns the top N holdings by current market value with gain/loss details");

        return client.AsAIAgent(
            name: "Portfolio Analysis Agent",
            description: "Analyses portfolio holdings data including summaries, sector breakdowns, and top holdings",
            tools: [summaryTool, sectorTool, topHoldingsTool],
            instructions:
                "You are a portfolio data analyst. When asked to analyse a portfolio, " +
                "use your tools to retrieve the data and present it clearly. " +
                "Always include actual numbers from the tools — never fabricate data. " +
                "Format currency values with dollar signs and two decimal places. " +
                "Keep responses factual and data-driven.");
    }
}
