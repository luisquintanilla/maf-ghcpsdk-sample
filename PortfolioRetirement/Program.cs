using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using PortfolioRetirement;

// ─── Sub-agent: Portfolio Analysis ────────────────────────────────────────────
//
// This agent owns the PowerShell-hosted tools for crunching portfolio data.
// It is NOT user-facing — the orchestrator calls it as a tool via AsAIFunction().

await using var analysisClient = new CopilotClient();
AIAgent analysisAgent = AnalysisAgentFactory.Create(analysisClient);

AIFunction analysisFunction = analysisAgent.AsAIFunction(
    new AIFunctionFactoryOptions
    {
        Name = "portfolio_analyst",
        Description =
            "Delegates to a specialist portfolio analyst agent that can retrieve " +
            "portfolio summaries, sector breakdowns, and top holdings. " +
            "Send it a natural-language analysis request."
    });

// ─── Sub-agent: Retirement Planning ──────────────────────────────────────────
//
// This agent owns Monte Carlo simulations, withdrawal strategies, Social Security
// optimization, and charting tools.

await using var retirementClient = new CopilotClient();
AIAgent retirementAgent = RetirementAgentFactory.Create(retirementClient);

AIFunction retirementFunction = retirementAgent.AsAIFunction(
    new AIFunctionFactoryOptions
    {
        Name = "retirement_planner",
        Description =
            "Delegates to a specialist retirement planning agent that can run " +
            "Monte Carlo simulations, compare withdrawal strategies, optimize " +
            "Social Security claiming ages, and render charts. " +
            "Send it a natural-language retirement planning request."
    });

// ─── Orchestrator agent ──────────────────────────────────────────────────────
//
// User-facing agent backed by a separate CopilotClient.  It sees both sub-agents
// as tools and decides when to delegate to each.

await using var orchestratorClient = new CopilotClient();

AIAgent orchestrator = orchestratorClient.AsAIAgent(
    name: "Retirement Portfolio Advisor",
    description: "A personal retirement planning advisor that helps users plan for retirement with portfolio analysis and simulations",
    tools: [analysisFunction, retirementFunction],
    instructions:
        "You are a friendly, knowledgeable retirement planning advisor. " +
        "When users ask about their portfolio, holdings, sectors, or performance, " +
        "delegate to the portfolio_analyst tool to get real data — never make up numbers. " +
        "When users ask about retirement projections, Monte Carlo simulations, " +
        "withdrawal strategies, Social Security optimization, or charts, " +
        "delegate to the retirement_planner tool. " +
        "Present the results in a clear, conversational way. " +
        "When presenting withdrawal strategy comparisons, always present all options " +
        "and ask the user which strategy they prefer before proceeding. " +
        "Offer actionable observations and keep responses concise but insightful.");

// ─── Session ──────────────────────────────────────────────────────────────────

AgentSession session = await orchestrator.CreateSessionAsync();

// ─── Ctrl+C handling ─────────────────────────────────────────────────────────

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// ─── REPL loop ────────────────────────────────────────────────────────────────

Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║  🏦  Retirement Portfolio Advisor — Multi-Agent          ║");
Console.WriteLine("║      GitHub Copilot SDK + MAF + TensorPrimitives         ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════╣");
Console.WriteLine("║  Try: 'Run a Monte Carlo simulation for my retirement'   ║");
Console.WriteLine("║       'Compare withdrawal strategies'                    ║");
Console.WriteLine("║       'Optimize my Social Security claiming age'         ║");
Console.WriteLine("║       'Show me my portfolio summary'                     ║");
Console.WriteLine("║  Press Ctrl+C to exit.                                   ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.WriteLine();

while (!cts.Token.IsCancellationRequested)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("You: ");
    Console.ResetColor();

    string? input = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(input))
        continue;

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("Advisor: ");
    Console.ResetColor();

    try
    {
        await foreach (AgentResponseUpdate update in
            orchestrator.RunStreamingAsync(input, session, cancellationToken: cts.Token))
        {
            if (update.ResponseId is null && update.Text.Length > 0)
                Console.Write(update.Text);
        }

        Console.WriteLine("\n");
    }
    catch (OperationCanceledException)
    {
        break;
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n[Error: {ex.Message}]\n");
        Console.ResetColor();
    }
}

Console.WriteLine("\nGoodbye! 👋");
