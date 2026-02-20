using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace PortfolioRetirement;

/// <summary>
/// Factory for the retirement planning sub-agent.
///
/// This agent is NOT user-facing — the orchestrator calls it via AsAIFunction().
/// It owns the Monte Carlo simulation, withdrawal strategy, Social Security,
/// and charting tools.
/// </summary>
internal static class RetirementAgentFactory
{
    public static AIAgent Create(CopilotClient client)
    {
        AIFunction monteCarloTool = AIFunctionFactory.Create(
            (Func<double, double, int, int, string>)RetirementTools.RunMonteCarlo,
            name: "run_monte_carlo",
            description: "Runs a Monte Carlo simulation of portfolio growth and returns percentile outcomes, probability of reaching the $2M goal, and mean final value");

        AIFunction withdrawalTool = AIFunctionFactory.Create(
            (Func<double, double, double, string>)RetirementTools.CompareWithdrawalStrategies,
            name: "compare_withdrawal_strategies",
            description: "Compares three withdrawal strategies (4% Rule, Dynamic Percentage, Guardrails) over a 30-year retirement via Monte Carlo simulation");

        AIFunction ssTool = AIFunctionFactory.Create(
            (Func<string>)RetirementTools.OptimizeSocialSecurity,
            name: "optimize_social_security",
            description: "Optimizes Social Security claiming ages for primary and spouse to maximize total lifetime benefits");

        AIFunction coneTool = AIFunctionFactory.Create(
            (Func<double, double, int, string>)RetirementTools.RenderProbabilityCone,
            name: "render_probability_cone",
            description: "Renders a probability cone chart showing Monte Carlo portfolio projection percentiles and saves it as an HTML file");

        AIFunction strategyChartTool = AIFunctionFactory.Create(
            (Func<string, string>)RetirementTools.RenderStrategyComparison,
            name: "render_strategy_comparison",
            description: "Renders a grouped bar chart comparing withdrawal strategies on success rate, median income, and median remaining balance");

        return client.AsAIAgent(
            name: "Retirement Planning Agent",
            description: "Provides retirement planning analysis including Monte Carlo simulations, withdrawal strategy comparisons, Social Security optimization, and visualizations",
            tools: [monteCarloTool, withdrawalTool, ssTool, coneTool, strategyChartTool],
            instructions:
                "You are a retirement planning specialist. Use your tools to provide " +
                "data-driven retirement analysis. Always include actual simulation results " +
                "from the tools — never fabricate numbers. When presenting withdrawal strategies, " +
                "clearly explain the trade-offs of each approach. " +
                "Format currency values with dollar signs and two decimal places. " +
                "Keep responses factual and insightful.");
    }
}
