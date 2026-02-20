using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using PortfolioTaxAdvisor;

// ─── Sub-agent: Portfolio Analysis ────────────────────────────────────────────

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

// ─── Sub-agent: Tax Optimization ──────────────────────────────────────────────

await using var taxClient = new CopilotClient();
AIAgent taxAgent = TaxAgentFactory.Create(taxClient);

AIFunction taxFunction = taxAgent.AsAIFunction(
    new AIFunctionFactoryOptions
    {
        Name = "tax_optimizer",
        Description =
            "Delegates to a specialist tax optimization agent that can optimise " +
            "asset location across account types, find tax-loss harvesting opportunities, " +
            "compute tax savings, and render tax charts. " +
            "Send it a natural-language tax optimization request."
    });

// ─── Orchestrator agent ──────────────────────────────────────────────────────

await using var orchestratorClient = new CopilotClient();

AIAgent orchestrator = orchestratorClient.AsAIAgent(
    name: "Portfolio Tax Advisor",
    description: "A personal investment portfolio advisor with tax optimization capabilities",
    tools: [analysisFunction, taxFunction],
    instructions:
        "You are a friendly, knowledgeable portfolio advisor with deep tax optimization expertise. " +
        "When users ask about their portfolio, holdings, sectors, or performance, " +
        "delegate to the portfolio_analyst tool to get real data — never make up numbers. " +
        "When users ask about tax optimization, asset location, tax-loss harvesting, or tax savings, " +
        "delegate to the tax_optimizer tool. " +
        "When presenting tax-loss harvesting candidates, always ask the user for approval " +
        "before considering the trades accepted. If wash sale warnings exist, highlight them prominently. " +
        "Present results in a clear, conversational way. " +
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
Console.WriteLine("║  💼  Portfolio Tax Advisor — Multi-Agent + Z3             ║");
Console.WriteLine("║      GitHub Copilot SDK + MAF + Z3 + TensorPrimitives    ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════╣");
Console.WriteLine("║  Try: 'Show me my portfolio summary'                     ║");
Console.WriteLine("║       'Optimize my asset location for taxes'              ║");
Console.WriteLine("║       'Find tax-loss harvesting opportunities'            ║");
Console.WriteLine("║       'Compute my potential tax savings'                  ║");
Console.WriteLine("║       'Generate a tax savings chart'                      ║");
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
