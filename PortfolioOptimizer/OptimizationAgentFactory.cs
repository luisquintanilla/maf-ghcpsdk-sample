using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace PortfolioOptimizer;

/// <summary>
/// Factory for the portfolio optimization sub-agent.
///
/// This agent is NOT user-facing — the orchestrator calls it via AsAIFunction().
/// It owns the Z3 solver, TensorPrimitives math, and Plotly.NET charting tools.
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
                "Explain the trade-offs between risk and return clearly.");
    }
}
