using System.Text;
using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using PortfolioOptimizer;

bool verbose = args.Contains("--verbose");
bool debug = args.Contains("--debug");

// ─── Sub-agent: Portfolio Analysis────────────────────────────────────────────
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
        "Keep responses concise but insightful. " +
        "Write for someone who is NOT a financial expert. Use everyday language; avoid jargon. " +
        "When you must use a technical term, explain it in parentheses " +
        "(e.g., \"diversification (spreading investments to reduce risk)\"). " +
        "Frame numbers in terms of real-world impact " +
        "(e.g., \"This could save you about $3,200 per year\" not " +
        "\"The tax alpha is 32 basis points\"). " +
        "Structure your response with these sections when providing a comprehensive analysis: " +
        "1. **At a Glance** — 3-bullet executive summary. " +
        "2. **Your Portfolio Today** — current state in plain language. " +
        "3. **What We Recommend** — specific actions with expected dollar impact. " +
        "4. **Things to Watch** — risks explained simply. " +
        "5. **Next Steps** — concrete actions to take.");

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

var toolStatusMap = new Dictionary<string, string>
{
    ["portfolio_analyst"] = "📊 Analyzing your portfolio...",
    ["portfolio_optimizer"] = "⚙️  Running portfolio optimization...",
    ["get_portfolio_summary"] = "📊 Retrieving portfolio summary...",
    ["get_sector_breakdown"] = "📊 Calculating sector breakdown...",
    ["get_top_holdings"] = "📊 Finding top holdings...",
    ["optimize_allocation"] = "⚙️  Solving optimal allocation (Z3)...",
    ["compute_portfolio_stats"] = "📈 Computing portfolio statistics...",
    ["render_frontier_chart"] = "🎨 Generating efficient frontier chart...",
};

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
        var reportContent = new StringBuilder();
        await foreach (AgentResponseUpdate update in
            orchestrator.RunStreamingAsync(input, session, cancellationToken: cts.Token))
        {
            foreach (var content in update.Contents)
            {
                if (content is FunctionCallContent call)
                {
                    if (debug)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"\n  [Call: {call.Name}({FormatArgs(call.Arguments)})]");
                        Console.ResetColor();
                    }
                    else
                    {
                        string status = toolStatusMap.GetValueOrDefault(call.Name, "⏳ Processing...");
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.Write($"\n  {status}");
                        Console.ResetColor();
                    }
                }
                else if (content is FunctionResultContent)
                {
                    if (debug)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine("  [Result received]");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGreen;
                        Console.WriteLine(" ✅");
                        Console.ResetColor();
                    }
                }
            }

            if (update.ResponseId is null && update.Text.Length > 0)
            {
                Console.Write(update.Text);
                reportContent.Append(update.Text);
            }
        }

        Console.WriteLine("\n");

        // Offer to save report
        if (reportContent.Length > 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write("💾 Save report? (y/n): ");
            Console.ResetColor();
            var answer = Console.ReadLine()?.Trim();
            if (answer?.Equals("y", StringComparison.OrdinalIgnoreCase) == true)
            {
                var htmlPath = ReportTools.GenerateHtmlReport("Portfolio Optimization Report", reportContent.ToString());
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  📄 HTML report: {htmlPath}");
                Console.ResetColor();

                var pdfPath = ReportTools.GeneratePdfReport("Portfolio Optimization Report", reportContent.ToString());
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

static string FormatArgs(IDictionary<string, object?>? arguments)
{
    if (arguments is null || arguments.Count == 0) return "{}";
    return "{" + string.Join(", ", arguments.Select(kvp => $"{kvp.Key}: {kvp.Value}")) + "}";
}
