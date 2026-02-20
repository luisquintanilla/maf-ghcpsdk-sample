using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
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

// ─── Build workflows using AgentWorkflowBuilder ────────────────────────────
//
// Sequential: Analysis → Optimization → Tax → Summary
// Each agent's output becomes the next agent's input.
Workflow sequentialWorkflow = AgentWorkflowBuilder.BuildSequential(
    "Rebalancing Pipeline",
    new[] { analysisAgent, optimizationAgent, taxAgent, summaryAgent });

// Concurrent: Analysis ∥ Tax ∥ Retirement
// All agents receive the same input and run in parallel.
Workflow concurrentWorkflow = AgentWorkflowBuilder.BuildConcurrent(
    "Annual Portfolio Review",
    new[] { analysisAgent, taxAgent, retirementAgent });

// ─── Ctrl+C handling ─────────────────────────────────────────────────────
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

// ─── REPL loop ────────────────────────────────────────────────────────────
Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║  🔄  Portfolio Workflows — Sequential + Concurrent       ║");
Console.WriteLine("║      GitHub Copilot SDK + MAF + AgentWorkflowBuilder     ║");
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

    Workflow selectedWorkflow;
    string workflowName;

    if (input.Contains("rebalance", StringComparison.OrdinalIgnoreCase))
    {
        selectedWorkflow = sequentialWorkflow;
        workflowName = "Rebalancing Pipeline (Sequential)";
    }
    else if (input.Contains("review", StringComparison.OrdinalIgnoreCase))
    {
        selectedWorkflow = concurrentWorkflow;
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
        string? lastExecutorId = null;

        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
            selectedWorkflow,
            new List<ChatMessage> { new(ChatRole.User, input) });

        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (cts.Token.IsCancellationRequested) break;

            if (evt is AgentResponseUpdateEvent e)
            {
                if (e.ExecutorId != lastExecutorId)
                {
                    lastExecutorId = e.ExecutorId;
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine($"── {e.ExecutorId} ──");
                    Console.ResetColor();
                }

                if (!string.IsNullOrEmpty(e.Update.Text))
                    Console.Write(e.Update.Text);
            }
            else if (evt is WorkflowOutputEvent)
            {
                break;
            }
            else if (evt is WorkflowErrorEvent error)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[Workflow error: {error.Exception?.Message}]");
                Console.ResetColor();
            }
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
