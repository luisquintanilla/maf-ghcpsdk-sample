# Hello-World Agent

A minimal C# sample demonstrating how to build an interactive CLI agent using:

- **[GitHub Copilot SDK](https://github.com/github/copilot-sdk)** — provides the CLI runtime, auth (via `gh`), model access, and tool dispatch
- **[Microsoft Agent Framework](https://github.com/microsoft/agent-framework)** — provides the `AIAgent` abstraction, session management, and streaming API
- **[Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai)** — shared primitive (`AIFunctionFactory`) for defining tools

The agent responds to natural language and calls two tools: `get_greeting` and `get_current_time`.

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

### Integration seam

The `Microsoft.Agents.AI.GitHub.Copilot` bridge package provides a
`CopilotClient.AsAIAgent(tools, instructions)` extension method that wraps
`CopilotClient` as a `GitHubCopilotAgent` — a first-party `AIAgent` implementation.
This means:

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

## Key NuGet packages

| Package | Version | Role |
|---|---|---|
| `GitHub.Copilot.SDK` | 0.1.25 | `CopilotClient`, tool dispatch, CLI process management |
| `Microsoft.Agents.AI.GitHub.Copilot` | 1.0.0-preview | Bridge: `AsAIAgent()` extension method |
| *(transitive)* `Microsoft.Agents.AI.Abstractions` | 1.0.0-preview | `AIAgent`, `AgentSession`, `AgentResponseUpdate` |
| *(transitive)* `Microsoft.Extensions.AI.Abstractions` | 10.3.0 | `AIFunctionFactory`, `AIFunction`, `TextContent` |

> **Note:** Both SDKs are in preview / technical preview and may have breaking changes.

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

### Future: multi-agent architecture

The end-goal architecture this sample points toward:

```
GitHubCopilotAgent  (user-facing orchestrator — this file)
  ├─ Tool: GreetingAgent.AsAgentTool()   ← dedicated ChatClientAgent
  ├─ Tool: WeatherAgent.AsAgentTool()    ← dedicated ChatClientAgent
  └─ Tool: SearchAgent  (A2A remote)     ← hosted separately via MapA2A()
```

Agent Framework supports three escalation patterns:
- **Agent-as-tool** (local): wrap any `AIAgent` as an `AIFunction` for another agent to call
- **A2A protocol** (remote): host agents over HTTP with `MapA2A()`, call via `A2AAgent`
- **Workflow API**: compose agents sequentially or concurrently with checkpointing
