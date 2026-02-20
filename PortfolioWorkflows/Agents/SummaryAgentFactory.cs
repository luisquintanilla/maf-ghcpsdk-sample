using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;

namespace PortfolioWorkflows;

internal static class SummaryAgentFactory
{
    public static AIAgent Create(CopilotClient client)
    {
        return client.AsAIAgent(
            name: "Summary Agent",
            description: "Synthesizes analysis results from multiple agents into a unified plan",
            tools: [],
            instructions:
                "You are a portfolio planning summarizer. You receive analysis results from multiple " +
                "specialist agents (portfolio analysis, optimization, tax, retirement). Synthesize " +
                "their outputs into a clear, unified rebalancing plan. Present each section with " +
                "the key findings and recommended actions. At the end, ask the user for approval " +
                "before considering the plan accepted. Be concise but thorough.");
    }
}
