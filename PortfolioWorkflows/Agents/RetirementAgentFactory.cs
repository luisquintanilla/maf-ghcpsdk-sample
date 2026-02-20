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
                "the user is on track for their retirement goal. Never fabricate data.");
    }
}
