using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using PortfolioOptimizer;

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

// ─── Sub-agent: Portfolio Optimization ────────────────────────────────────────
//
// This agent owns the Z3 solver, TensorPrimitives math, and Plotly.NET charting.
// It is NOT user-facing — the orchestrator calls it as a tool via AsAIFunction().

await using var optimizationClient = new CopilotClient();
AIAgent optimizationAgent = OptimizationAgentFactory.Create(optimizationClient);

AIFunction optimizationFunction = optimizationAgent.AsAIFunction(
    new AIFunctionFactoryOptions
    {
        Name = "portfolio_optimizer",
        Description =
            "Delegates to a specialist portfolio optimization agent that can " +
            "optimize allocation using Z3 constraint solving, compute portfolio " +
            "statistics with TensorPrimitives, and render efficient frontier charts. " +
            "Send it a natural-language optimization request."
    });

// ─── Orchestrator agent ──────────────────────────────────────────────────────
//
// User-facing agent backed by a separate CopilotClient.  It sees the analysis
// and optimization sub-agents as tools and decides when to delegate to each.

await using var orchestratorClient = new CopilotClient();

AIAgent orchestrator = orchestratorClient.AsAIAgent(
    name: "Portfolio Optimizer",
    description: "A portfolio optimization advisor that analyses holdings and finds optimal allocations",
    tools: [analysisFunction, optimizationFunction],
    instructions:
        "You are a portfolio optimization advisor that helps users understand and improve " +
        "their investment allocations. You have two specialist agents at your disposal: " +
        "1) portfolio_analyst — for retrieving current portfolio data (summaries, sectors, holdings). " +
        "2) portfolio_optimizer — for running Z3 constraint optimization, computing portfolio statistics, " +
        "and generating efficient frontier charts. " +
        "When users ask about their current portfolio, delegate to portfolio_analyst. " +
        "When users ask for optimization, risk analysis, or charts, delegate to portfolio_optimizer. " +
        "After running an optimization, always present the recommended allocation to the user " +
        "and ask for confirmation before considering it accepted. " +
        "Present results in a clear, conversational way with actual numbers. " +
        "Offer actionable observations and explain risk-return trade-offs. " +
        "Keep responses concise but insightful.");

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
Console.WriteLine("║  📊  Portfolio Optimizer — Multi-Agent + Z3              ║");
Console.WriteLine("║      GitHub Copilot SDK + MAF + Z3 + TensorPrimitives    ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════╣");
Console.WriteLine("║  Try: 'Optimize my portfolio for moderate risk'          ║");
Console.WriteLine("║       'Show me the efficient frontier'                   ║");
Console.WriteLine("║       'What are my current holdings?'                    ║");
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
