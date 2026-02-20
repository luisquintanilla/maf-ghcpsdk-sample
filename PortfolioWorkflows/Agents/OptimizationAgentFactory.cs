using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace PortfolioWorkflows;

/// <summary>
/// Factory for the portfolio optimization sub-agent.
/// Owns the Z3 solver, TensorPrimitives math, and Plotly.NET charting tools.
/// </summary>
internal static class OptimizationAgentFactory
{
    public static AIAgent Create(CopilotClient client)
    {
        AIFunction optimizeTool = AIFunctionFactory.Create(
            (Func<string, string>)OptimizationTools.OptimizeAllocation,
            name: "optimize_allocation",
            description: "Runs Z3 constraint solver to find optimal portfolio weights given risk tolerance");

        AIFunction statsTool = AIFunctionFactory.Create(
            (Func<string, string>)OptimizationTools.ComputePortfolioStats,
            name: "compute_portfolio_stats",
            description: "Computes portfolio statistics (return, volatility, Sharpe) for given asset weights using TensorPrimitives");

        AIFunction chartTool = AIFunctionFactory.Create(
            (Func<string>)OptimizationTools.RenderFrontierChart,
            name: "render_frontier_chart",
            description: "Generates an efficient frontier chart as interactive HTML using Plotly.NET");

        return client.AsAIAgent(
            name: "Portfolio Optimization Agent",
            description: "Optimizes portfolio allocation using Z3 constraint solving, computes statistics with TensorPrimitives, and renders charts with Plotly.NET",
            tools: [optimizeTool, statsTool, chartTool],
            instructions:
                "You are a quantitative portfolio optimizer. Use the optimize_allocation tool to find " +
                "optimal portfolio weights under constraints. Use compute_portfolio_stats to analyse " +
                "specific weight vectors. Use render_frontier_chart to visualize the efficient frontier. " +
                "Always present results with precise numbers from the tools — never fabricate data. " +
                "Explain the trade-offs between risk and return clearly. " +
                "Write for someone who is NOT a financial expert. Use everyday language; avoid jargon. " +
                "When you must use a technical term, explain it in parentheses " +
                "(e.g., \"diversification (spreading investments to reduce risk)\"). " +
                "Frame numbers in terms of real-world impact " +
                "(e.g., \"This could save you about $3,200 per year\" not " +
                "\"The tax alpha is 32 basis points\").");
    }
}
