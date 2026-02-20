using System.Text;
using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using PortfolioRetirement;

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
        "Offer actionable observations and keep responses concise but insightful. " +
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

var toolStatusMap = new Dictionary<string, string>
{
    ["portfolio_analyst"] = "📊 Analyzing your portfolio...",
    ["retirement_planner"] = "🏖️  Running retirement analysis...",
    ["get_portfolio_summary"] = "📊 Retrieving portfolio summary...",
    ["get_sector_breakdown"] = "📊 Calculating sector breakdown...",
    ["get_top_holdings"] = "📊 Finding top holdings...",
    ["run_monte_carlo"] = "🎲 Running 10,000 retirement simulations...",
    ["compare_withdrawal_strategies"] = "📊 Comparing withdrawal strategies...",
    ["optimize_social_security"] = "🏛️  Optimizing Social Security timing...",
    ["render_probability_cone"] = "🎨 Generating probability chart...",
    ["render_strategy_comparison"] = "🎨 Generating strategy comparison chart...",
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

        if (reportContent.Length > 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write("💾 Save report? (y/n): ");
            Console.ResetColor();
            var answer = Console.ReadLine()?.Trim();
            if (answer?.Equals("y", StringComparison.OrdinalIgnoreCase) == true)
            {
                var htmlPath = ReportTools.GenerateHtmlReport("Retirement Planning Report", reportContent.ToString());
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  📄 HTML report: {htmlPath}");
                Console.ResetColor();

                var pdfPath = ReportTools.GeneratePdfReport("Retirement Planning Report", reportContent.ToString());
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
