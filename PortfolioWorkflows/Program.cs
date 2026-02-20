using System.Text;
using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using PortfolioWorkflows;

bool verbose = args.Contains("--verbose");
bool debug = args.Contains("--debug");

// ─── Create specialist agents──────────────────────────────────────────────
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

// ─── Intent classification agent ──────────────────────────────────────────
await using var triageClient = new CopilotClient();
AIAgent triageAgent = triageClient.AsAIAgent(
    name: "Intent Classifier",
    description: "Classifies user intent into workflow categories",
    tools: Array.Empty<AIFunction>(),
    instructions:
        "You classify user requests into exactly one category. " +
        "Respond with ONLY the category name — no explanation, no punctuation.\n\n" +
        "Categories:\n" +
        "REBALANCE — requests about portfolio rebalancing, optimization, " +
        "allocation changes, improving portfolio, adjusting weights\n" +
        "REVIEW — requests about annual review, portfolio health check, " +
        "comprehensive assessment, combined analysis across areas\n" +
        "QUESTION — general questions, greetings, clarifications, or " +
        "requests that don't clearly fit REBALANCE or REVIEW");

// ─── Conversational fallback agent ────────────────────────────────────────
await using var chatClient = new CopilotClient();
AIAgent chatAgent = chatClient.AsAIAgent(
    name: "Portfolio Assistant",
    description: "Answers general portfolio questions",
    tools: Array.Empty<AIFunction>(),
    instructions:
        "You are a friendly portfolio assistant. Answer general questions about " +
        "the user's portfolio. If the user seems to want a full rebalancing or " +
        "comprehensive review, let them know they can ask for that. " +
        "Keep responses concise and helpful.");

// ─── Ctrl+C handling─────────────────────────────────────────────────────
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

AgentSession triageSession = await triageAgent.CreateSessionAsync();
AgentSession chatSession = await chatAgent.CreateSessionAsync();

// ─── REPL loop ────────────────────────────────────────────────────────────
Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║  🔄  Portfolio Workflows — Sequential + Concurrent       ║");
Console.WriteLine("║      GitHub Copilot SDK + MAF + AgentWorkflowBuilder     ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════╣");
Console.WriteLine("║      Ask anything about your portfolio.                   ║");
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

    Console.WriteLine();

    try
    {
        // Classify intent
        string intent = "";
        await foreach (var update in triageAgent.RunStreamingAsync(input, triageSession, cancellationToken: cts.Token))
        {
            if (update.ResponseId is null) intent += update.Text;
        }
        intent = intent.Trim().ToUpperInvariant();

        var reportContent = new StringBuilder();

        if (intent.Contains("REBALANCE"))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n▶ Running: Rebalancing Pipeline (Sequential)");
            Console.ResetColor();
            await RunWorkflowAsync(sequentialWorkflow, input, verbose, debug, cts.Token, reportContent);
        }
        else if (intent.Contains("REVIEW"))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n▶ Running: Annual Portfolio Review (Concurrent)");
            Console.ResetColor();
            await RunWorkflowAsync(concurrentWorkflow, input, verbose, debug, cts.Token, reportContent);
        }
        else
        {
            // Conversational fallback
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\nAdvisor: ");
            Console.ResetColor();
            await foreach (var update in chatAgent.RunStreamingAsync(input, chatSession, cancellationToken: cts.Token))
            {
                if (update.ResponseId is null && update.Text.Length > 0)
                {
                    Console.Write(update.Text);
                    reportContent.Append(update.Text);
                }
            }
            Console.WriteLine("\n");
        }

        if (reportContent.Length > 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write("💾 Save report? (y/n): ");
            Console.ResetColor();
            var answer = Console.ReadLine()?.Trim();
            if (answer?.Equals("y", StringComparison.OrdinalIgnoreCase) == true)
            {
                var reportTitle = intent.Contains("REBALANCE") ? "Portfolio Rebalancing Report" :
                                  intent.Contains("REVIEW") ? "Annual Portfolio Review" :
                                  "Portfolio Analysis";
                var htmlPath = ReportTools.GenerateHtmlReport(reportTitle, reportContent.ToString());
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  📄 HTML report: {htmlPath}");
                Console.ResetColor();

                var pdfPath = ReportTools.GeneratePdfReport(reportTitle, reportContent.ToString());
                if (pdfPath != null)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  📄 PDF report:  {pdfPath}");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("  ℹ️  PDF generation skipped (requires pandoc + typst)");
                    Console.ResetColor();
                }
            }
            Console.WriteLine();
        }
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

static async Task RunWorkflowAsync(Workflow workflow, string input, bool verbose, bool debug, CancellationToken ct, StringBuilder? reportCapture = null)
{
    string? lastExecutorId = null;

    await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
        workflow,
        new List<ChatMessage> { new(ChatRole.User, input) });

    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

    await foreach (WorkflowEvent evt in run.WatchStreamAsync())
    {
        if (ct.IsCancellationRequested) break;

        if (evt is AgentResponseUpdateEvent e)
        {
            if ((verbose || debug) && e.ExecutorId != lastExecutorId)
            {
                lastExecutorId = e.ExecutorId;
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"\n── {e.ExecutorId} ──");
                Console.ResetColor();
            }

            if (!string.IsNullOrEmpty(e.Update.Text))
            {
                Console.Write(e.Update.Text);
                if (reportCapture != null) reportCapture.Append(e.Update.Text);
            }
        }
        else if (evt is ExecutorCompletedEvent ec)
        {
            if (verbose || debug)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"\n✅ {ec.ExecutorId} complete");
                Console.ResetColor();
            }
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
