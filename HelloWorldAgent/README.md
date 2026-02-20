# Hello-World Agent

> **📍 Start here** — this is the first sample in the learning path. It teaches the fundamentals of building a single agent with tools. Once you're comfortable with this pattern, move on to [PortfolioAdvisor](../PortfolioAdvisor/) for multi-agent orchestration.

A minimal C# sample demonstrating how to build an interactive CLI agent using:

- **[GitHub Copilot SDK](https://github.com/github/copilot-sdk)** — provides the CLI runtime, auth (via `gh`), model access, and tool dispatch
- **[Microsoft Agent Framework (MAF)](https://github.com/microsoft/agent-framework)** — provides the `AIAgent` abstraction, session management, and streaming API
- **[Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai)** — shared primitives (`AIFunctionFactory`, `AIFunction`) for defining tools

The agent responds to natural language and calls two tools: `get_greeting` and `get_current_time`.

## What you'll learn

This sample covers the foundational concepts you need before tackling multi-agent patterns:

| Concept | What you'll see |
|---|---|
| **CopilotClient** | How to create an LLM-backed client that authenticates via `gh` CLI — no API keys |
| **AIAgent** | The core abstraction from Microsoft Agent Framework that wires LLM + tools + instructions together |
| **AIFunction / AIFunctionFactory** | How to wrap plain C# methods as tools the LLM can call, with description metadata extracted via reflection |
| **Tool dispatch** | The LLM decides when to call your tools based on user input and tool descriptions |
| **Streaming responses** | How `RunStreamingAsync` yields `AgentResponseUpdate` objects for real-time token-by-token output |
| **Session management** | How `AgentSession` preserves conversation history across REPL turns |

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  User (REPL — Console.ReadLine)                             │
└────────────────────┬────────────────────────────────────────┘
                     │ input string
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  GitHubCopilotAgent  (AIAgent — Microsoft Agent Framework)  │
│                                                             │
│  Backed by: CopilotClient (GitHub Copilot SDK)              │
│  Auth:      gh CLI logged-in user                           │
│  Model:     gpt-4.1 (or whatever Copilot selects)           │
│                                                             │
│  ┌─────────────────┐   ┌──────────────────────────────┐    │
│  │ get_greeting    │   │ get_current_time             │    │
│  │ (AIFunction)    │   │ (AIFunction)                 │    │
│  │                 │   │                              │    │
│  │ GreetingTools   │   │ GreetingTools                │    │
│  │ .GetGreeting()  │   │ .GetCurrentTime()            │    │
│  └─────────────────┘   └──────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
                     │ streaming AgentResponseUpdates
                     ▼
┌─────────────────────────────────────────────────────────────┐
│  Console output (streamed token-by-token)                   │
└─────────────────────────────────────────────────────────────┘
```

### How it works

1. **Tool registration**: `AIFunctionFactory.Create()` wraps plain C# methods (static functions with `[Description]` attributes) into `AIFunction` objects. The descriptions become tool metadata the LLM uses to decide when and how to call each tool.

2. **Agent construction**: `CopilotClient.AsAIAgent()` (from the `Microsoft.Agents.AI.GitHub.Copilot` bridge package) wraps the client as a `GitHubCopilotAgent` — a first-party implementation of the `AIAgent` abstraction. You pass in the tools and a system prompt (instructions).

3. **Session**: `agent.CreateSessionAsync()` creates an `AgentSession` that preserves conversation history. The session is reused across REPL turns so the agent remembers prior messages.

4. **Streaming**: `agent.RunStreamingAsync()` yields two kinds of `AgentResponseUpdate`:
   - **Deltas** (`ResponseId == null`) — incremental tokens as they stream from the LLM
   - **Complete** (`ResponseId != null`) — the full assembled message

   The REPL prints only deltas to display text as it arrives, avoiding duplication.

### Integration seam

The `Microsoft.Agents.AI.GitHub.Copilot` bridge package provides the
`CopilotClient.AsAIAgent(tools, instructions)` extension method. This means:

- Only **two NuGet packages** are needed (everything else is transitive)
- Auth is handled entirely by the `gh` CLI — no API keys to manage
- The result IS a proper `AIAgent`, so it participates in Agent Framework's
  multi-agent patterns out of the box

## Prerequisites

| Requirement | Details |
|---|---|
| [.NET 8 SDK](https://dot.net) or later | `dotnet --version` |
| [GitHub CLI](https://cli.github.com) | `gh --version` |
| GitHub Copilot subscription | Required for model access |

Authenticate the CLI before running:

```bash
gh auth login
```

## Run

```bash
cd HelloWorldAgent
dotnet run
```

## Example session

```
╔══════════════════════════════════════════════════════════╗
║  🤖  Greeting Agent — Hello-World Sample                  ║
║      GitHub Copilot SDK + Microsoft Agent Framework      ║
╠══════════════════════════════════════════════════════════╣
║  Try: 'Say hello to Alice'  or  'What time is it?'       ║
║  Press Ctrl+C to exit.                                   ║
╚══════════════════════════════════════════════════════════╝

You: Say hello to Alice

Agent: Hello, Alice! 👋 Great to meet you!

You: What time is it?

Agent: It's 3:27 PM on Wednesday, February 19, 2026.

You: Now greet Bob too

Agent: Hello, Bob! 👋 Great to meet you!
```

## Project structure

```
HelloWorldAgent/
├── HelloWorldAgent.csproj   — .NET 8 console app, NuGet references
├── Program.cs               — Agent wiring + interactive REPL loop
└── GreetingTools.cs         — Pure tool implementations (no AI dependencies)
```

### Design note: separation of concerns

The tool implementations in `GreetingTools.cs` are **pure functions with no AI dependencies**. They don't know about `CopilotClient`, `AIAgent`, or any LLM concepts. This separation means:

- Tools are easy to unit-test (they're just static methods)
- You can swap tool implementations without touching agent wiring
- The same tools could be registered with a different agent or LLM provider

This pattern carries forward into more complex samples like [PortfolioAdvisor](../PortfolioAdvisor/), where tools become more sophisticated (e.g., running PowerShell pipelines) but remain decoupled from the agent layer.

## Key NuGet packages

| Package | Version | Role |
|---|---|---|
| [`GitHub.Copilot.SDK`](https://github.com/github/copilot-sdk) | 0.1.18 | `CopilotClient` — LLM backend, auth via `gh` CLI |
| [`Microsoft.Agents.AI.GitHub.Copilot`](https://github.com/microsoft/agent-framework) | 1.0.0-preview | Bridge: `AsAIAgent()` extension method |
| *(transitive)* `Microsoft.Agents.AI.Abstractions` | 1.0.0-preview | `AIAgent`, `AgentSession`, `AgentResponseUpdate` |
| *(transitive)* `Microsoft.Extensions.AI.Abstractions` | 10.3.0 | `AIFunctionFactory`, `AIFunction`, `TextContent` |

> **Note:** Both SDKs are in preview and may have breaking changes.

## Extending this sample

### Add a new tool

1. Add a method to `GreetingTools.cs` (or a new `*Tools.cs` class):

```csharp
[Description("Tells a programming joke")]
public static string TellJoke() => "Why do programmers prefer dark mode? Because light attracts bugs! 🐛";
```

2. Register it in `Program.cs`:

```csharp
AIFunction jokeTool = AIFunctionFactory.Create(
    (Func<string>)GreetingTools.TellJoke,
    name: "tell_joke",
    description: "Tells a programming joke");
```

3. Pass it to `AsAIAgent(tools: [greetTool, timeTool, jokeTool], ...)`.

## Next step: multi-agent

Once you're comfortable with this single-agent pattern, the [PortfolioAdvisor](../PortfolioAdvisor/) sample shows how to:

- Build **multiple agents** with separate tools and instructions
- Use the **agent-as-tool** pattern (`AsAIFunction()`) so an orchestrator can delegate to specialists
- Host **PowerShell pipelines in-process** as agent tools
- Implement **human-in-the-loop** (HITL) via conversational follow-ups

## Further reading

- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) — the `AIAgent` abstraction and multi-agent patterns
- [GitHub Copilot SDK](https://github.com/github/copilot-sdk) — the LLM backend for these samples
- [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) — the shared AI abstractions for .NET (`AIFunction`, `IChatClient`, etc.)
