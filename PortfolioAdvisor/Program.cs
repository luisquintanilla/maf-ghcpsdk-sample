using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using PortfolioAdvisor;

bool verbose = args.Contains("--verbose");
bool debug = args.Contains("--debug");

// ─── Sub-agent: Portfolio Analysis────────────────────────────────────────────
//
// This agent owns the PowerShell-hosted tools for crunching portfolio data.
// It is NOT user-facing — the orchestrator calls it as a tool via AsAIFunction().

await using var analysisClient = new CopilotClient();
AIAgent analysisAgent = AnalysisAgentFactory.Create(analysisClient);

// Wrap the sub-agent as an AIFunction so the orchestrator can invoke it.
AIFunction analysisFunction = analysisAgent.AsAIFunction(
    new AIFunctionFactoryOptions
    {
        Name = "portfolio_analyst",
        Description =
            "Delegates to a specialist portfolio analyst agent that can retrieve " +
            "portfolio summaries, sector breakdowns, and top holdings. " +
            "Send it a natural-language analysis request."
    });

// ─── Orchestrator agent ──────────────────────────────────────────────────────
//
// User-facing agent backed by a separate CopilotClient.  It sees the analysis
// sub-agent as a tool and decides when to delegate to it.

await using var orchestratorClient = new CopilotClient();

AIAgent orchestrator = orchestratorClient.AsAIAgent(
    new SessionConfig
    {
        Tools = [analysisFunction],
        SystemMessage = new SystemMessageConfig
        {
            Content =
                "You are a friendly, knowledgeable personal portfolio advisor. " +
                "When users ask about their portfolio, holdings, sectors, or performance, " +
                "delegate to the portfolio_analyst tool to get real data — never make up numbers. " +
                "Present the results in a clear, conversational way. " +
                "Offer actionable observations (e.g., concentration risk, underperformers). " +
                "When you spot something noteworthy, ask the user if they'd like to explore it further. " +
                "Keep responses concise but insightful."
        },
        OnPermissionRequest = PermissionHandler.ApproveAll,
    },
    name: "Portfolio Advisor",
    description: "A personal investment portfolio advisor that helps users understand their holdings");

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
Console.WriteLine("║  💼  Portfolio Advisor — Multi-Agent MVP                  ║");
Console.WriteLine("║      GitHub Copilot SDK + MAF + PowerShell               ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════╣");
Console.WriteLine("║  Try: 'Show me my portfolio summary'                     ║");
Console.WriteLine("║       'What does my sector breakdown look like?'          ║");
Console.WriteLine("║       'What are my top 5 holdings?'                       ║");
Console.WriteLine("║  Press Ctrl+C to exit.                                   ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.WriteLine();

var toolStatusMap = new Dictionary<string, string>
{
    ["portfolio_analyst"] = "📊 Analyzing your portfolio...",
    ["get_portfolio_summary"] = "📊 Retrieving portfolio summary...",
    ["get_sector_breakdown"] = "📊 Calculating sector breakdown...",
    ["get_top_holdings"] = "📊 Finding top holdings...",
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

static string FormatArgs(IDictionary<string, object?>? arguments)
{
    if (arguments is null || arguments.Count == 0) return "{}";
    return "{" + string.Join(", ", arguments.Select(kvp => $"{kvp.Key}: {kvp.Value}")) + "}";
}
