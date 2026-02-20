# PortfolioWorkflows — Sequential & Concurrent Agent Pipelines

The fifth sample in the progressive multi-agent learning path. This project
demonstrates how to compose multiple specialist agents into **sequential** and
**concurrent** workflows using MAF's `AgentWorkflowBuilder` API.

## What This Sample Teaches

| Concept | Description |
|---------|-------------|
| **AgentWorkflowBuilder** | MAF's built-in API for composing agents into sequential and concurrent workflows |
| **Sequential pipeline** | `BuildSequential` — agents run one after another, output feeds the next |
| **Concurrent pipeline** | `BuildConcurrent` — agents run in parallel, outputs are aggregated |
| **InProcessExecution** | `RunStreamingAsync` executes workflows with streaming event output |
| **WorkflowEvent streaming** | `AgentResponseUpdateEvent`, `WorkflowOutputEvent`, `WorkflowErrorEvent` |
| **Five specialist agents** | Analysis, Optimization, Tax, Retirement, and Summary |

## Architecture

### Sequential Rebalancing Pipeline

```
User
  │
  ▼
┌──────────────────────────────────────────────┐
│     AgentWorkflowBuilder.BuildSequential     │
│                                              │
│  Analysis → Optimization → Tax → Summary     │
│                                              │
│  Each agent's output becomes the next        │
│  agent's input automatically                 │
└──────────────────────────────────────────────┘
```

The workflow engine chains agents in order:
1. **Analysis Agent** — retrieves portfolio summary, sector breakdown, top holdings
2. **Optimization Agent** — runs Z3 solver for optimal allocation weights
3. **Tax Agent** — analyses asset location and tax-loss harvesting
4. **Summary Agent** — synthesizes all results into a unified rebalancing plan

### Concurrent Annual Review

```
User
  │
  ▼
┌──────────────────────────────────────────────┐
│     AgentWorkflowBuilder.BuildConcurrent     │
│                                              │
│  ┌─ Analysis Agent ──────┐                   │
│  ├─ Tax Agent ───────────┤  (parallel)       │
│  └─ Retirement Agent ────┘                   │
│                                              │
│  Outputs aggregated automatically            │
└──────────────────────────────────────────────┘
```

All three agents receive the same input and run in parallel. The workflow
engine aggregates their outputs automatically.

## Key Concepts

### AgentWorkflowBuilder vs Agent-as-Tool

In prior samples (PortfolioAdvisor), a single orchestrator agent wraps
sub-agents as `AIFunction` tools and decides *when* and *whether* to call
them based on the user's question.

With **AgentWorkflowBuilder**, the execution topology is defined
**declaratively** in code — no orchestrator LLM is needed:

```csharp
// Sequential: output of each agent feeds the next
Workflow pipeline = AgentWorkflowBuilder.BuildSequential(
    "Rebalancing Pipeline",
    new[] { analysisAgent, optimizationAgent, taxAgent, summaryAgent });

// Concurrent: all agents get the same input, run in parallel
Workflow review = AgentWorkflowBuilder.BuildConcurrent(
    "Annual Portfolio Review",
    new[] { analysisAgent, taxAgent, retirementAgent });
```

### Streaming Workflow Execution

Workflows are executed with `InProcessExecution.RunStreamingAsync` and produce
a stream of `WorkflowEvent` objects:

```csharp
await using StreamingRun run = await InProcessExecution.RunStreamingAsync(
    workflow, new List<ChatMessage> { new(ChatRole.User, input) });

await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is AgentResponseUpdateEvent e)
        Console.Write(e.Update.Text);      // streaming text from each agent
    else if (evt is WorkflowOutputEvent)
        break;                              // workflow complete
    else if (evt is WorkflowErrorEvent err)
        Console.WriteLine(err.Exception);   // error handling
}
```

This gives you:
- **Predictable execution** — the topology is fixed at build time
- **Streaming per-agent output** — see each agent's response as it streams
- **Built-in error handling** — `WorkflowErrorEvent` surfaces failures

## Specialist Agents

| Agent | Tools | Purpose |
|-------|-------|---------|
| **Analysis** | `get_portfolio_summary`, `get_sector_breakdown`, `get_top_holdings` | PowerShell-based portfolio analytics |
| **Optimization** | `optimize_allocation`, `compute_portfolio_stats`, `render_frontier_chart` | Z3 constraint solving + TensorPrimitives |
| **Tax** | `optimize_asset_location`, `find_harvest_candidates`, `compute_tax_savings`, `render_tax_chart` | Z3-based tax optimization |
| **Retirement** | `project_retirement` | Compound growth projection |
| **Summary** | *(none)* | Synthesizes outputs into a plan |

## Prerequisites

- .NET 8 SDK
- GitHub Copilot subscription with SDK access
- `GITHUB_TOKEN` environment variable set

## How to Run

```bash
cd PortfolioWorkflows
dotnet run
```

Then type:
- **`rebalance`** — runs the sequential rebalancing pipeline
- **`review`** — runs the concurrent annual portfolio review
- **Ctrl+C** — exits

## Project Structure

```
PortfolioWorkflows/
├── PortfolioWorkflows.csproj
├── Program.cs                         — Orchestrators + REPL
├── Agents/
│   ├── AnalysisAgentFactory.cs        — Portfolio analysis (PowerShell)
│   ├── OptimizationAgentFactory.cs    — Z3 + TensorPrimitives optimization
│   ├── TaxAgentFactory.cs             — Z3 tax optimization
│   ├── RetirementAgentFactory.cs      — Compound growth projection
│   └── SummaryAgentFactory.cs         — Pipeline output synthesizer
├── Tools/
│   ├── PowerShellTools.cs             — Portfolio CSV analysis
│   ├── OptimizationTools.cs           — Z3 solver + Plotly charting
│   ├── TaxTools.cs                    — Tax optimization + charting
│   └── RetirementTools.cs             — Retirement projection
├── data/
│   ├── holdings.csv                   — 25 mock holdings
│   ├── market_data.json               — Expected returns, volatility
│   ├── investor_profile.json          — Investor constraints & goals
│   └── tax_lots.csv                   — 30 tax lots with purchase dates
└── README.md
```
