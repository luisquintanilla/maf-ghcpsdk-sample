# Portfolio Advisor — Multi-Agent MVP

A line-of-business sample demonstrating the **multi-agent** pattern: an orchestrator agent delegates to a specialist sub-agent whose tools run **in-process PowerShell** pipelines against mock portfolio data.

This sample goes beyond the [HelloWorldAgent](../HelloWorldAgent/) single-agent pattern to show how multiple agents — each with their own tools, instructions, and responsibilities — can collaborate to handle complex, domain-specific tasks.

## Why multi-agent?

A single agent with many tools works fine for simple scenarios, but real business problems often involve distinct areas of expertise. Consider an investment advisor that needs to:

- Analyse portfolio holdings and compute performance metrics
- Assess risk exposure and run stress tests
- Optimise tax strategies across account types
- Model retirement projections

Giving one agent all of these tools creates a bloated context and makes it harder for the LLM to choose the right tool at the right time. The **multi-agent** pattern solves this by giving each specialist agent a focused instruction set and a small, curated toolset. An orchestrator agent acts as the user-facing layer and decides when to delegate to which specialist.

This is the same principle behind how teams work in the real world: a manager (orchestrator) delegates to specialists (sub-agents) who each have their own skills and resources (tools).

## What this sample demonstrates

| Concept | How it's used here |
|---|---|
| **Agent-as-tool** | The analysis sub-agent is wrapped as an [`AIFunction`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.aifunction) via [`AsAIFunction()`](https://github.com/microsoft/agent-framework), making it callable by the orchestrator just like any other tool |
| **Multi-agent orchestration** | The orchestrator's LLM decides *when* to delegate to the sub-agent — no hardcoded routing |
| **In-process PowerShell** | Tool implementations host the PowerShell runtime inside the .NET process via [`Microsoft.PowerShell.SDK`](https://www.nuget.org/packages/Microsoft.PowerShell.SDK), running full PowerShell pipelines (`Import-Csv`, `Group-Object`, `Measure-Object`, etc.) without shelling out to a separate process |
| **Mock data** | A CSV file of ~25 fake holdings replaces real brokerage APIs, letting you explore the pattern without external dependencies |
| **Human-in-the-loop (HITL)** | The orchestrator naturally surfaces observations and asks the user whether to dig deeper — a conversational approval pattern |
| **Separate CopilotClient instances** | Each agent is backed by its own [`CopilotClient`](https://github.com/github/copilot-sdk), demonstrating that multiple agents can share the same `gh` CLI authentication context |

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│  User (REPL — Console.ReadLine)                                     │
└────────────────────────┬────────────────────────────────────────────┘
                         │ input string
                         ▼
┌─────────────────────────────────────────────────────────────────────┐
│  Orchestrator Agent  ("Portfolio Advisor")                           │
│  AIAgent backed by CopilotClient #1                                 │
│                                                                     │
│  Instructions: Friendly portfolio advisor persona.                  │
│                Delegates analysis tasks to the sub-agent.           │
│                Surfaces observations and asks follow-up questions.  │
│                                                                     │
│  Tools:                                                             │
│  ┌────────────────────────────────────────────────────────────────┐ │
│  │  portfolio_analyst  (AIFunction wrapping the sub-agent)        │ │
│  │                                                                │ │
│  │  ┌──────────────────────────────────────────────────────────┐  │ │
│  │  │  Analysis Agent  ("Portfolio Analysis Agent")             │  │ │
│  │  │  AIAgent backed by CopilotClient #2                      │  │ │
│  │  │                                                          │  │ │
│  │  │  Instructions: Data analyst persona. Uses tools for      │  │ │
│  │  │                real numbers, never fabricates data.       │  │ │
│  │  │                                                          │  │ │
│  │  │  Tools:                                                  │  │ │
│  │  │  ┌──────────────────┐ ┌──────────────────────────────┐   │  │ │
│  │  │  │ get_portfolio_   │ │ get_sector_breakdown         │   │  │ │
│  │  │  │ summary          │ │                              │   │  │ │
│  │  │  │                  │ │ PowerShell: Import-Csv |     │   │  │ │
│  │  │  │ PowerShell:      │ │ Group-Object Sector |       │   │  │ │
│  │  │  │ Import-Csv |     │ │ Measure-Object -Sum         │   │  │ │
│  │  │  │ Measure-Object   │ │                              │   │  │ │
│  │  │  └──────────────────┘ └──────────────────────────────┘   │  │ │
│  │  │  ┌──────────────────┐                                    │  │ │
│  │  │  │ get_top_holdings │                                    │  │ │
│  │  │  │                  │                                    │  │ │
│  │  │  │ PowerShell:      │                                    │  │ │
│  │  │  │ Sort-Object |    │                                    │  │ │
│  │  │  │ Select -First N  │                                    │  │ │
│  │  │  └──────────────────┘                                    │  │ │
│  │  └──────────────────────────────────────────────────────────┘  │ │
│  └────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
                         │ streaming AgentResponseUpdates
                         ▼
┌─────────────────────────────────────────────────────────────────────┐
│  Console output (streamed token-by-token)                           │
└─────────────────────────────────────────────────────────────────────┘
```

### What happens at runtime

1. **User types a question** (e.g., "What's my sector breakdown?")
2. **Orchestrator agent** receives the input and decides to call the `portfolio_analyst` tool
3. **`AsAIFunction()`** invokes the analysis sub-agent, passing the user's question as a natural-language task
4. **Analysis agent** selects the appropriate PowerShell tool (e.g., `get_sector_breakdown`)
5. **PowerShell pipeline** runs in-process: `Import-Csv | Group-Object | Measure-Object | ConvertTo-Json`
6. **Results flow back** through the chain: PowerShell → analysis agent → orchestrator → streamed to console

This two-hop delegation (orchestrator → sub-agent → tool) is the foundation for more complex multi-agent architectures where you might have many specialists, each with their own tools and data sources.

## Key concepts

### Agent-as-tool pattern

The [`AsAIFunction()`](https://github.com/microsoft/agent-framework) extension method (from `Microsoft.Agents.AI`) converts any `AIAgent` into an `AIFunction`. This is the key integration point: it lets one agent call another agent as if it were a regular tool.

```csharp
// Wrap the sub-agent so the orchestrator can call it as a tool
AIFunction analysisFunction = analysisAgent.AsAIFunction(
    new AIFunctionFactoryOptions
    {
        Name = "portfolio_analyst",
        Description = "Delegates to a specialist portfolio analyst agent..."
    });

// Give the wrapped function to the orchestrator as one of its tools
AIAgent orchestrator = orchestratorClient.AsAIAgent(
    tools: [analysisFunction],
    ...);
```

The orchestrator's LLM sees `portfolio_analyst` in its tool list and can decide to call it. When it does, the framework runs the analysis agent with the provided input, collects its response, and returns it to the orchestrator as a tool result.

### In-process PowerShell

The [`Microsoft.PowerShell.SDK`](https://www.nuget.org/packages/Microsoft.PowerShell.SDK) NuGet package embeds the PowerShell Core runtime directly into your .NET application. This means:

- **No external process**: no `Process.Start("pwsh", ...)`, no child process management
- **Cross-platform**: PowerShell Core (pwsh) runs on Windows, macOS, and Linux
- **Full pipeline support**: `Import-Csv`, `Group-Object`, `Measure-Object`, `ConvertTo-Json`, and the entire PowerShell cmdlet ecosystem
- **Structured results**: PowerShell outputs `PSObject` instances, which serialize cleanly to JSON for agent consumption
- **Module ecosystem**: you can `Import-Module` to bring in modules like [ImportExcel](https://github.com/dfinke/ImportExcel), [PSWriteHTML](https://github.com/EvotecIT/PSWriteHTML), or any other PowerShell module

```csharp
using System.Management.Automation;

using var ps = PowerShell.Create();
ps.AddScript(@"
    Import-Csv './data/holdings.csv'
    | Group-Object Sector
    | ForEach-Object { [PSCustomObject]@{ Sector = $_.Name; Count = $_.Count } }
    | ConvertTo-Json
");
var results = ps.Invoke();
```

This is what makes each tool implementation in `PowerShellTools.cs` work: each method creates a short-lived `PowerShell` instance, runs a pipeline against the CSV data, and returns the JSON result string for the agent to interpret.

### CopilotClient as the LLM backend

Both agents use [`CopilotClient`](https://github.com/github/copilot-sdk) from the GitHub Copilot SDK as their LLM backend. The `CopilotClient` handles:

- **Authentication** via the GitHub CLI (`gh auth login`) — no API keys needed
- **Model selection** managed by Copilot
- **Token streaming** for real-time output

The `Microsoft.Agents.AI.GitHub.Copilot` bridge package provides the `AsAIAgent()` extension method that wraps a `CopilotClient` as a `GitHubCopilotAgent` — a first-party implementation of Microsoft Agent Framework's [`AIAgent`](https://github.com/microsoft/agent-framework) abstraction.

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
cd PortfolioAdvisor
dotnet run
```

## Example session

```
╔══════════════════════════════════════════════════════════╗
║  💼  Portfolio Advisor — Multi-Agent MVP                  ║
║      GitHub Copilot SDK + MAF + PowerShell               ║
╠══════════════════════════════════════════════════════════╣
║  Try: 'Show me my portfolio summary'                     ║
║       'What does my sector breakdown look like?'          ║
║       'What are my top 5 holdings?'                       ║
║  Press Ctrl+C to exit.                                   ║
╚══════════════════════════════════════════════════════════╝

You: Show me my portfolio summary

Advisor: Here's your portfolio overview:
         • Total Value: $96,242.25
         • Cost Basis: $85,130.00
         • Gain/Loss: +$11,112.25 (+13.05%)
         • Holdings: 25 positions across 5 sectors

         Would you like me to break that down by sector?

You: Yes please

Advisor: Here's your sector breakdown:
         • Technology:  $42,371.75 (44.0%) — 5 holdings
         • Financials:  $18,012.90 (18.7%) — 4 holdings
         • Healthcare:  $16,699.20 (17.3%) — 4 holdings
         • Consumer:    $15,624.80 (16.2%) — 4 holdings
         • Energy:      $12,156.50 (12.6%) — 3 holdings
         • Bonds:       $28,377.00 (29.5%) — 5 holdings

         ⚠️ Technology is at 44% — that's significant concentration.
         Would you like to see your top holdings in that sector?

You: What are my top 3 holdings?

Advisor: Your three largest positions:
         1. NVDA (NVIDIA) — $17,506.00 (+94.5%)
         2. MSFT (Microsoft) — $14,532.00 (+48.3%)
         3. BND (Vanguard Total Bond) — $10,875.00 (-7.1%)

         Interesting mix — your two biggest winners are both tech,
         while your third largest is a bond fund that's underwater.
```

> **Note:** Exact responses will vary since the LLM generates natural language. The data values come from the mock CSV and the PowerShell tools — the agent does not fabricate numbers.

## Project structure

```
PortfolioAdvisor/
├── PortfolioAdvisor.csproj     — .NET 8 console app, NuGet references
├── Program.cs                  — Orchestrator agent wiring + interactive REPL
├── AnalysisAgentFactory.cs     — Sub-agent factory (registers PowerShell tools)
├── PowerShellTools.cs          — Tool implementations using hosted PowerShell
└── data/
    └── holdings.csv            — Mock portfolio data (25 holdings)
```

### File-by-file walkthrough

#### `Program.cs` — Orchestrator + REPL

Sets up the two-agent architecture:

1. Creates a `CopilotClient` for the analysis sub-agent and builds it via `AnalysisAgentFactory.Create()`
2. Wraps the sub-agent as an `AIFunction` using `AsAIFunction()` with a name and description
3. Creates a second `CopilotClient` for the orchestrator and builds it with the analysis function as a tool
4. Runs the interactive REPL loop, streaming responses token-by-token

#### `AnalysisAgentFactory.cs` — Sub-agent factory

A static factory that takes a `CopilotClient` and returns a fully configured `AIAgent`:

- Registers three `AIFunction` tools (portfolio summary, sector breakdown, top holdings)
- Sets the agent's persona to a data-focused analyst that never fabricates numbers
- Each tool is created via [`AIFunctionFactory.Create()`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.aifunctionfactory.create), which uses reflection to extract `[Description]` attributes for tool metadata

#### `PowerShellTools.cs` — In-process PowerShell tools

Three static methods, each running a PowerShell pipeline:

- **`GetPortfolioSummary()`** — loads the CSV, computes total value and cost basis using `Measure-Object -Sum`, calculates gain/loss and percentage, returns JSON
- **`GetSectorBreakdown()`** — groups by sector using `Group-Object`, computes weight percentage per sector, sorts by value descending, returns JSON array
- **`GetTopHoldings(int count)`** — computes per-holding value and gain/loss, sorts descending, takes top N via `Select-Object -First`, returns JSON array

Each method follows the same pattern:
1. `PowerShell.Create()` — creates an in-process PowerShell instance
2. `ps.AddScript(...)` — adds the pipeline script
3. `ps.Invoke()` — executes and returns `PSObject` results
4. Error handling via `ps.HadErrors` and `ps.Streams.Error`

#### `data/holdings.csv` — Mock portfolio

25 holdings across 5 sectors (Technology, Healthcare, Financials, Energy, Consumer) plus Bonds. Each row includes:

| Column | Description |
|---|---|
| `Symbol` | Ticker symbol (e.g., AAPL, MSFT) |
| `Name` | Company or fund name |
| `Sector` | Sector classification |
| `Shares` | Number of shares held |
| `PurchasePrice` | Price per share at purchase |
| `CurrentPrice` | Current market price per share |
| `AccountType` | Taxable, Roth, or Traditional IRA |

The data includes a mix of gains and losses across sectors and account types, making it interesting for analysis questions.

## Key NuGet packages

| Package | Version | Role |
|---|---|---|
| [`GitHub.Copilot.SDK`](https://github.com/github/copilot-sdk) | 0.1.18 | `CopilotClient` — LLM backend, auth via `gh` CLI |
| [`Microsoft.Agents.AI.GitHub.Copilot`](https://github.com/microsoft/agent-framework) | 1.0.0-preview | Bridge: `AsAIAgent()` extension method |
| [`Microsoft.Agents.AI`](https://github.com/microsoft/agent-framework) | 1.0.0-preview | `AsAIFunction()` for agent-as-tool pattern |
| [`Microsoft.PowerShell.SDK`](https://www.nuget.org/packages/Microsoft.PowerShell.SDK) | 7.4.7 | In-process PowerShell Core runtime |
| *(transitive)* `Microsoft.Agents.AI.Abstractions` | 1.0.0-preview | `AIAgent`, `AgentSession`, `AgentResponseUpdate` |
| *(transitive)* `Microsoft.Extensions.AI.Abstractions` | 10.3.0 | `AIFunctionFactory`, `AIFunction` |

> **Note:** The GitHub Copilot SDK and Microsoft Agent Framework packages are in preview and may have breaking changes.

## Extending this sample

### Add a new tool to the analysis agent

1. Add a method to `PowerShellTools.cs`:

```csharp
[Description("Returns holdings filtered by account type")]
public static string GetHoldingsByAccount(
    [Description("Account type: Taxable, Roth, or Traditional")] string accountType)
{
    using var ps = PowerShell.Create();
    ps.AddScript($@"
        Import-Csv '{CsvPath}'
        | Where-Object AccountType -eq '{accountType}'
        | ForEach-Object {{
            [PSCustomObject]@{{
                Symbol = $_.Symbol; Name = $_.Name
                Value  = [math]::Round([double]$_.Shares * [double]$_.CurrentPrice, 2)
            }}
        }} | ConvertTo-Json
    ");
    return InvokeAndReturn(ps);
}
```

2. Register it in `AnalysisAgentFactory.cs`:

```csharp
AIFunction accountTool = AIFunctionFactory.Create(
    (Func<string, string>)PowerShellTools.GetHoldingsByAccount,
    name: "get_holdings_by_account",
    description: "Returns holdings filtered by account type (Taxable, Roth, or Traditional)");
```

3. Add it to the agent's tools list: `tools: [summaryTool, sectorTool, topHoldingsTool, accountTool]`

### Add a second sub-agent

Create a new specialist (e.g., a risk agent) and register it alongside the analysis agent:

```csharp
// Build both sub-agents
AIAgent analysisAgent = AnalysisAgentFactory.Create(analysisClient);
AIAgent riskAgent     = RiskAgentFactory.Create(riskClient);

// Wrap both as tools for the orchestrator
AIFunction analysisTool = analysisAgent.AsAIFunction(...);
AIFunction riskTool     = riskAgent.AsAIFunction(...);

// Orchestrator sees both specialists
AIAgent orchestrator = orchestratorClient.AsAIAgent(
    tools: [analysisTool, riskTool], ...);
```

### Replace mock data with real data

The PowerShell tools currently read from a static CSV file. To connect to real systems, you could:

- Replace `Import-Csv` with `Invoke-RestMethod` to call a brokerage API
- Use `Import-Module` to load a PowerShell module for your data source
- Swap the PowerShell implementation entirely for a C# service call — the agent doesn't care how the tool gets its data, only that it returns JSON

## Further reading

- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) — the multi-agent framework used in this sample
- [GitHub Copilot SDK](https://github.com/github/copilot-sdk) — the LLM backend and CLI runtime
- [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) — the shared AI abstractions for .NET
- [Hosting PowerShell in a .NET application](https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-standard-library-binary-module) — background on in-process PowerShell hosting
