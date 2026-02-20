using GitHub.Copilot.SDK;          // CopilotClient + CopilotClient.AsAIAgent() extension
using HelloWorldAgent;             // GreetingTools
using Microsoft.Agents.AI;        // AIAgent, AgentSession, AgentResponseUpdate
using Microsoft.Extensions.AI;    // AIFunctionFactory, TextContent

bool verbose = args.Contains("--verbose");
bool debug = args.Contains("--debug");

// ─── Tool registration────────────────────────────────────────────────────────
//
// AIFunctionFactory.Create wraps a delegate into an AIFunction.  It uses
// reflection on the underlying MethodInfo to extract [Description] attributes
// from the method and its parameters, which the LLM uses to decide when/how to
// call each tool.
//
// We cast to a typed Func<> so the compiler resolves the correct MethodInfo
// (required for reflection-based attribute discovery on a static method group).

AIFunction greetTool = AIFunctionFactory.Create(
    (Func<string, string>)GreetingTools.GetGreeting,
    name: "get_greeting",
    description: "Greets a person by name with a warm, friendly message");

AIFunction timeTool = AIFunctionFactory.Create(
    (Func<string>)GreetingTools.GetCurrentTime,
    name: "get_current_time",
    description: "Returns the current local date and time");

// ─── Agent construction ───────────────────────────────────────────────────────
//
// CopilotClient.AsAIAgent() is an extension method from the
// Microsoft.Agents.AI.GitHub.Copilot bridge package.  It wraps the client in a
// GitHubCopilotAgent — a first-party implementation of the AIAgent abstraction
// from Microsoft Agent Framework.
//
// Key architectural point: the CopilotClient IS the LLM backend.  Auth comes
// from the gh CLI (logged-in user), so no API key or extra config is needed.
//
// Future expansion: swap `client.AsAIAgent(...)` for a multi-agent orchestrator
// that delegates to specialist ChatClientAgent instances as sub-tools.

await using var client = new CopilotClient();

AIAgent agent = client.AsAIAgent(
    name: "Greeting Agent",
    description: "A friendly assistant that greets people and reports the current time",
    tools: [greetTool, timeTool],
    instructions:
        "You are a friendly greeting assistant. " +
        "Use the get_greeting tool whenever asked to greet or say hello to someone. " +
        "Use the get_current_time tool whenever asked about the current time or date. " +
        "Keep your responses concise and warm. " +
        "Write for someone who is NOT a financial expert. Use everyday language; avoid jargon. " +
        "When you must use a technical term, explain it in parentheses " +
        "(e.g., \"diversification (spreading investments to reduce risk)\"). " +
        "Frame numbers in terms of real-world impact " +
        "(e.g., \"This could save you about $3,200 per year\" not " +
        "\"The tax alpha is 32 basis points\").");

// ─── Session ──────────────────────────────────────────────────────────────────
//
// A single AgentSession is reused across all REPL turns so the agent retains
// conversation history.  GitHubCopilotAgent maps this to a CopilotSession with
// infinite-session context compaction enabled by default.

AgentSession session = await agent.CreateSessionAsync();

// ─── Ctrl+C handling ─────────────────────────────────────────────────────────

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // prevent process kill; let the loop exit cleanly
    cts.Cancel();
};

// ─── REPL loop ────────────────────────────────────────────────────────────────

Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║  \ud83e\udd16  Greeting Agent — Hello-World Sample                    ║");
Console.WriteLine("║      GitHub Copilot SDK + Microsoft Agent Framework      ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════╣");
Console.WriteLine("║  Try: 'Say hello to Alice'  or  'What time is it?'       ║");
Console.WriteLine("║  Press Ctrl+C to exit.                                   ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.WriteLine();

var toolStatusMap = new Dictionary<string, string>
{
    ["get_greeting"] = "👋 Preparing a greeting...",
    ["get_current_time"] = "🕐 Checking the time...",
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
    Console.Write("Agent: ");
    Console.ResetColor();

    try
    {
        // RunStreamingAsync re-uses `session` so the agent remembers prior turns.
        //
        // The GitHubCopilotAgent emits two kinds of AgentResponseUpdate:
        //   • Deltas  (ResponseId == null)  — incremental tokens as they arrive
        //   • Complete (ResponseId != null) — the full assembled message
        //
        // We print only deltas to display text as it streams, avoiding duplication.
        await foreach (AgentResponseUpdate update in
            agent.RunStreamingAsync(input, session, cancellationToken: cts.Token))
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

Console.WriteLine("\nGoodbye! \ud83d\udc4b");

static string FormatArgs(IDictionary<string, object?>? arguments)
{
    if (arguments is null || arguments.Count == 0) return "{}";
    return "{" + string.Join(", ", arguments.Select(kvp => $"{kvp.Key}: {kvp.Value}")) + "}";
}
