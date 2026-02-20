using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using PortfolioWorkflows;

// ─── Create specialist agents ──────────────────────────────────────────────
await using var analysisClient = new CopilotClient();
await using var optimizationClient = new CopilotClient();
await using var taxClient = new CopilotClient();
await using var retirementClient = new CopilotClient();
await using var summaryClient = new CopilotClient();

AIAgent analysisAgent = AnalysisAgentFactory.Create(analysisClient);
AIAgent optimizationAgent = OptimizationAgentFactory.Create(optimizationClient);
AIAgent taxAgent = TaxAgentFactory.Create(taxClient);
AIAgent retirementAgent = RetirementAgentFactory.Create(retirementClient);
AIAgent summaryAgent = SummaryAgentFactory.Create(summaryClient);

// ─── Wrap sub-agents as tools for orchestration ────────────────────────────
//
// The MAF workflow packages are not yet publicly available, so we use the
// proven agent-as-tool pattern: each specialist agent is exposed as an
// AIFunction that the orchestrator can invoke.  The orchestrator's system
// prompt enforces sequential or concurrent execution order.

AIFunction analysisFunction = analysisAgent.AsAIFunction(
    new AIFunctionFactoryOptions
    {
        Name = "portfolio_analyst",
        Description =
            "Delegates to a specialist portfolio analyst that retrieves portfolio " +
            "summaries, sector breakdowns, and top holdings."
    });

AIFunction optimizationFunction = optimizationAgent.AsAIFunction(
    new AIFunctionFactoryOptions
    {
        Name = "portfolio_optimizer",
        Description =
            "Delegates to a specialist portfolio optimizer that uses Z3 constraint " +
            "solving to find optimal allocation weights."
    });

AIFunction taxFunction = taxAgent.AsAIFunction(
    new AIFunctionFactoryOptions
    {
        Name = "tax_advisor",
        Description =
            "Delegates to a specialist tax advisor that optimizes asset location " +
            "and finds tax-loss harvesting candidates."
    });

AIFunction retirementFunction = retirementAgent.AsAIFunction(
    new AIFunctionFactoryOptions
    {
        Name = "retirement_projector",
        Description =
            "Delegates to a retirement projection specialist that calculates " +
            "future portfolio values using compound growth."
    });

AIFunction summaryFunction = summaryAgent.AsAIFunction(
    new AIFunctionFactoryOptions
    {
        Name = "plan_summarizer",
        Description =
            "Delegates to a summary agent that synthesizes results from multiple " +
            "specialist agents into a unified rebalancing plan."
    });

// ─── Sequential workflow orchestrator ──────────────────────────────────────
//
// Enforces a strict pipeline: Analysis → Optimization → Tax → Summary.
// The system prompt tells the LLM the exact order to call each tool.

await using var sequentialClient = new CopilotClient();
AIAgent sequentialOrchestrator = sequentialClient.AsAIAgent(
    name: "Rebalancing Pipeline",
    description: "Runs a sequential rebalancing pipeline: analysis, optimization, tax, and summary",
    tools: [analysisFunction, optimizationFunction, taxFunction, summaryFunction],
    instructions:
        "You are a portfolio rebalancing pipeline orchestrator. " +
        "Execute these steps IN STRICT ORDER — do not skip or reorder:\n" +
        "1. Call portfolio_analyst to get a full portfolio overview.\n" +
        "2. Call portfolio_optimizer to find optimal allocation weights.\n" +
        "3. Call tax_advisor to analyse tax implications.\n" +
        "4. Call plan_summarizer with all the results above to produce a unified plan.\n" +
        "After each step, briefly relay the key findings before moving to the next step. " +
        "Present the final summary plan to the user.");

// ─── Concurrent workflow orchestrator ──────────────────────────────────────
//
// Runs Analysis, Tax, and Retirement in parallel (the LLM can call all
// three tools in a single turn), then presents the combined report.

await using var concurrentClient = new CopilotClient();
AIAgent concurrentOrchestrator = concurrentClient.AsAIAgent(
    name: "Annual Portfolio Review",
    description: "Runs a concurrent annual portfolio review: analysis, tax, and retirement in parallel",
    tools: [analysisFunction, taxFunction, retirementFunction],
    instructions:
        "You are a portfolio review orchestrator. " +
        "Call ALL THREE tools in a SINGLE turn to run them concurrently:\n" +
        "  - portfolio_analyst: get the full portfolio overview\n" +
        "  - tax_advisor: analyse tax efficiency and harvesting opportunities\n" +
        "  - retirement_projector: project retirement balance\n" +
        "After receiving results from all three, present a unified annual " +
        "portfolio review report with sections for each area. " +
        "Highlight the most important findings and recommended actions.");

// ─── Ctrl+C handling ─────────────────────────────────────────────────────
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

// ─── Sessions ────────────────────────────────────────────────────────────
AgentSession sequentialSession = await sequentialOrchestrator.CreateSessionAsync();
AgentSession concurrentSession = await concurrentOrchestrator.CreateSessionAsync();

// ─── REPL loop ────────────────────────────────────────────────────────────
Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║  🔄  Portfolio Workflows — Sequential + Concurrent       ║");
Console.WriteLine("║      GitHub Copilot SDK + MAF + Agent-as-Tool Pipelines   ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════╣");
Console.WriteLine("║  Commands:                                                ║");
Console.WriteLine("║    'rebalance' — Run sequential rebalancing pipeline      ║");
Console.WriteLine("║    'review'    — Run concurrent annual portfolio review   ║");
Console.WriteLine("║  Press Ctrl+C to exit.                                    ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.WriteLine();

while (!cts.Token.IsCancellationRequested)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("You: ");
    Console.ResetColor();

    string? input = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(input)) continue;

    AIAgent selectedAgent;
    AgentSession selectedSession;
    string workflowName;

    if (input.Contains("rebalance", StringComparison.OrdinalIgnoreCase))
    {
        selectedAgent = sequentialOrchestrator;
        selectedSession = sequentialSession;
        workflowName = "Rebalancing Pipeline (Sequential)";
    }
    else if (input.Contains("review", StringComparison.OrdinalIgnoreCase))
    {
        selectedAgent = concurrentOrchestrator;
        selectedSession = concurrentSession;
        workflowName = "Annual Portfolio Review (Concurrent)";
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Please type 'rebalance' or 'review' to run a workflow.\n");
        Console.ResetColor();
        continue;
    }

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"▶ Running: {workflowName}");
    Console.ResetColor();
    Console.WriteLine();

    try
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("Advisor: ");
        Console.ResetColor();

        await foreach (AgentResponseUpdate update in
            selectedAgent.RunStreamingAsync(input, selectedSession, cancellationToken: cts.Token))
        {
            if (update.ResponseId is null && update.Text.Length > 0)
                Console.Write(update.Text);
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n\n✅ Workflow complete.");
        Console.ResetColor();
        Console.WriteLine();
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
