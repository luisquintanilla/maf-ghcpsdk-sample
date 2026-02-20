# PortfolioWorkflows — Sequential & Concurrent Agent Pipelines

The fifth sample in the progressive multi-agent learning path. This project
demonstrates how to compose multiple specialist agents into **sequential** and
**concurrent** workflows using MAF's agent-as-tool pattern.

## What This Sample Teaches

| Concept | Description |
|---------|-------------|
| **Deterministic orchestration** | Execution order is enforced by the orchestrator's system prompt, not decided ad-hoc by the LLM |
| **Sequential pipeline** | Agents run one after another — each builds on the previous output |
| **Concurrent pipeline** | Multiple agents run in a single turn — the LLM calls all tools at once |
| **Agent-as-tool composition** | Each specialist agent is wrapped as an `AIFunction` and given to an orchestrator |
| **Five specialist agents** | Analysis, Optimization, Tax, Retirement, and Summary |

## Architecture

### Sequential Rebalancing Pipeline

```
User
  │
  ▼
┌──────────────────────────────────────────────┐
│         Sequential Orchestrator              │
│  (system prompt enforces strict order)       │
│                                              │
│  1. portfolio_analyst ────────────────┐      │
│  2. portfolio_optimizer ──────────────┤      │
│  3. tax_advisor ──────────────────────┤      │
│  4. plan_summarizer ──────────────────┘      │
│                                              │
│  Each step's output feeds the next           │
└──────────────────────────────────────────────┘
```

The orchestrator calls each agent in strict order:
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
│         Concurrent Orchestrator              │
│  (system prompt: call all tools at once)     │
│                                              │
│  ┌─ portfolio_analyst ───┐                   │
│  ├─ tax_advisor ─────────┤  (parallel)       │
│  └─ retirement_projector ┘                   │
│                                              │
│  Unified annual review report                │
└──────────────────────────────────────────────┘
```

The orchestrator invokes all three tools in a single turn, then combines
the results into an annual portfolio review report.

## Key Concepts

### Deterministic vs LLM-Driven Orchestration

In prior samples (PortfolioAdvisor), a single orchestrator agent decides
*when* and *whether* to call sub-agents based on the user's question. This
is flexible but non-deterministic.

In **workflow-style orchestration**, the system prompt enforces a fixed
execution order. The LLM still generates natural-language output between
steps, but the *sequence of tool calls* is predetermined. This gives you:

- **Predictable execution** — the same steps run every time
- **Auditability** — you know exactly which agents ran and in what order
- **Composability** — pipelines can be assembled from reusable agents

### Agent-as-Tool Pattern

Since the MAF `AgentWorkflowBuilder` API is not yet publicly available, this
sample uses the proven **agent-as-tool** pattern:

```csharp
// Wrap a specialist agent as a callable tool
AIFunction analysisFunction = analysisAgent.AsAIFunction(
    new AIFunctionFactoryOptions { Name = "portfolio_analyst", ... });

// Give it to an orchestrator whose instructions enforce call order
AIAgent orchestrator = client.AsAIAgent(
    tools: [analysisFunction, optimizationFunction, taxFunction, summaryFunction],
    instructions: "Call these tools in strict order: 1, 2, 3, 4...");
```

This achieves the same deterministic pipeline behavior while staying within
the current public API surface.

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
