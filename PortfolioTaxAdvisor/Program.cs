using System.Text;
using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using PortfolioTaxAdvisor;

bool verbose = args.Contains("--verbose");
bool debug = args.Contains("--debug");

// ─── Sub-agent: Portfolio Analysis────────────────────────────────────────────

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

var toolStatusMap = new Dictionary<string, string>
{
    ["portfolio_analyst"] = "📊 Analyzing your portfolio...",
    ["tax_optimizer"] = "💰 Analyzing tax optimization...",
    ["get_portfolio_summary"] = "📊 Retrieving portfolio summary...",
    ["get_sector_breakdown"] = "📊 Calculating sector breakdown...",
    ["get_top_holdings"] = "📊 Finding top holdings...",
    ["optimize_asset_location"] = "⚙️  Optimizing where investments are held...",
    ["find_harvest_candidates"] = "🔍 Finding tax-saving opportunities...",
    ["compute_tax_savings"] = "💰 Calculating potential tax savings...",
    ["render_tax_chart"] = "🎨 Generating tax savings chart...",
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
                var htmlPath = ReportTools.GenerateHtmlReport("Tax Optimization Report", reportContent.ToString());
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  📄 HTML report: {htmlPath}");
                Console.ResetColor();

                var pdfPath = ReportTools.GeneratePdfReport("Tax Optimization Report", reportContent.ToString());
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
