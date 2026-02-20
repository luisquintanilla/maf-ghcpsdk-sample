using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace PortfolioWorkflows;

internal static class RetirementAgentFactory
{
    public static AIAgent Create(CopilotClient client)
    {
        AIFunction projectionTool = AIFunctionFactory.Create(
            (Func<double, string>)RetirementTools.ProjectRetirement,
            name: "project_retirement",
            description: "Projects retirement balance using compound growth based on current portfolio, contributions, and expected return");

        return client.AsAIAgent(
            name: "Retirement Projection Agent",
            description: "Projects retirement outcomes using compound growth calculations",
            tools: [projectionTool],
            instructions:
                "You are a retirement projection specialist. Use project_retirement to calculate " +
                "future portfolio values. Present results clearly with dollar amounts and whether " +
                "the user is on track for their retirement goal. Never fabricate data. " +
                "Write for someone who is NOT a financial expert. Use everyday language; avoid jargon. " +
                "When you must use a technical term, explain it in parentheses " +
                "(e.g., \"diversification (spreading investments to reduce risk)\"). " +
                "Frame numbers in terms of real-world impact " +
                "(e.g., \"This could save you about $3,200 per year\" not " +
                "\"The tax alpha is 32 basis points\").");
    }
}
