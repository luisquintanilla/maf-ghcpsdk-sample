# Portfolio Optimizer — Multi-Agent + Z3 Constraint Solving

A line-of-business sample demonstrating **multi-agent orchestration** with **Z3 constraint solving**, **TensorPrimitives** vector math, and **Plotly.NET** interactive charting. This is the third sample in a progressive learning path, building on the [HelloWorldAgent](../HelloWorldAgent/) and [PortfolioAdvisor](../PortfolioAdvisor/) samples.

## What this sample teaches

| Concept | How it's used here |
|---|---|
| **Z3 constraint solving** | The optimization agent uses [Microsoft.Z3](https://github.com/Z3Prover/z3) to find optimal portfolio weights subject to sector caps, position limits, bond floors, and volatility constraints |
| **TensorPrimitives** | Portfolio statistics (expected return, volatility, Sharpe ratio) are computed using hardware-accelerated [`TensorPrimitives.Dot`](https://learn.microsoft.com/dotnet/api/system.numerics.tensors.tensorprimitives.dot) and [`TensorPrimitives.Multiply`](https://learn.microsoft.com/dotnet/api/system.numerics.tensors.tensorprimitives.multiply) |
| **Plotly.NET charting** | The efficient frontier is rendered as an interactive HTML chart using [Plotly.NET](https://plotly.net), sweeping volatility constraints through Z3 |
| **Three-agent orchestration** | An orchestrator delegates to two specialists: an analysis agent (PowerShell) and an optimization agent (Z3 + math + charts) |
| **Human-in-the-loop** | After optimization, the orchestrator presents results and asks for user confirmation |
| **Agent-as-tool** | Both sub-agents are wrapped via `AsAIFunction()` so the orchestrator can call them like regular tools |

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│  User (REPL — Console.ReadLine)                                         │
└──────────────────────────┬──────────────────────────────────────────────┘
                           │ input string
                           ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  Orchestrator Agent  ("Portfolio Optimizer")                             │
│  AIAgent backed by CopilotClient #1                                     │
│                                                                         │
│  Instructions: Portfolio optimization advisor. Delegates to              │
│                specialists. Asks for confirmation after optimization.   │
│                                                                         │
│  Tools:                                                                 │
│  ┌───────────────────────────────┐  ┌────────────────────────────────┐  │
│  │  portfolio_analyst            │  │  portfolio_optimizer           │  │
│  │  (Analysis Sub-Agent)         │  │  (Optimization Sub-Agent)      │  │
│  │  CopilotClient #2             │  │  CopilotClient #3              │  │
│  │                               │  │                                │  │
│  │  Tools:                       │  │  Tools:                        │  │
│  │  ├─ get_portfolio_summary     │  │  ├─ optimize_allocation  (Z3)  │  │
│  │  ├─ get_sector_breakdown      │  │  ├─ compute_portfolio_stats    │  │
│  │  └─ get_top_holdings          │  │  │  (TensorPrimitives)         │  │
│  │     (PowerShell pipelines)    │  │  └─ render_frontier_chart      │  │
│  │                               │  │     (Plotly.NET → HTML)        │  │
│  └───────────────────────────────┘  └────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘
                           │ streaming AgentResponseUpdates
                           ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  Console output (streamed token-by-token)                               │
└─────────────────────────────────────────────────────────────────────────┘
```

### What happens at runtime

1. **User asks a question** (e.g., "Optimize my portfolio for moderate risk")
2. **Orchestrator** decides which sub-agent to delegate to
3. **Optimization agent** selects the `optimize_allocation` tool
4. **Z3 solver** finds optimal weights subject to constraints (sector caps, position limits, bond floor, volatility target)
5. **Results flow back** through the chain: Z3 → optimization agent → orchestrator → streamed to console
6. **Orchestrator asks for confirmation** before considering the allocation accepted

## Key concepts

### Z3 constraint solving

The [Z3 theorem prover](https://github.com/Z3Prover/z3) is used as an optimizer to find portfolio weights that maximize expected return while satisfying:

- **Full investment**: all weights sum to 1.0
- **Position limits**: no single asset exceeds 8% of the portfolio
- **Sector caps**: no sector exceeds 35% of the portfolio
- **Bond floor**: at least 20% allocated to bonds
- **Volatility target**: weighted average volatility stays under the target

### TensorPrimitives

[`System.Numerics.Tensors`](https://learn.microsoft.com/dotnet/api/system.numerics.tensors.tensorprimitives) provides hardware-accelerated SIMD operations. The sample uses:

- `TensorPrimitives.Dot(weights, returns)` — expected portfolio return in one call
- `TensorPrimitives.Multiply(weights, vols)` — element-wise weight × volatility

### Plotly.NET

[Plotly.NET](https://plotly.net) generates interactive HTML charts. The efficient frontier tool:

1. Sweeps volatility constraints from 5% to 30% in ~50 steps
2. Runs Z3 for each point to find the optimal return at that volatility level
3. Plots the frontier curve with the current portfolio position marked

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
cd PortfolioOptimizer
dotnet run
```

## Example session

```
╔══════════════════════════════════════════════════════════╗
║  📊  Portfolio Optimizer — Multi-Agent + Z3              ║
║      GitHub Copilot SDK + MAF + Z3 + TensorPrimitives    ║
╠══════════════════════════════════════════════════════════╣
║  Try: 'Optimize my portfolio for moderate risk'          ║
║       'Show me the efficient frontier'                   ║
║       'What are my current holdings?'                    ║
║  Press Ctrl+C to exit.                                   ║
╚══════════════════════════════════════════════════════════╝

You: Optimize my portfolio for moderate risk

Advisor: I've run the Z3 optimizer with a moderate risk profile (max 15% volatility).
         Here's the recommended allocation:

         | Asset | Sector     | Weight |
         |-------|------------|--------|
         | MSFT  | Technology |  8.00% |
         | V     | Financials |  8.00% |
         | COST  | Consumer   |  8.00% |
         | ...   | ...        |  ...   |

         📊 Expected Return: 7.85%
         📉 Volatility: 14.92%
         📈 Sharpe Ratio: 0.258

         Would you like to accept this allocation, or should I adjust the risk level?
```

## Project structure

```
PortfolioOptimizer/
├── PortfolioOptimizer.csproj        — .NET 8 console app, NuGet references
├── Program.cs                       — Orchestrator agent wiring + interactive REPL
├── AnalysisAgentFactory.cs          — Analysis sub-agent factory (PowerShell tools)
├── PowerShellTools.cs               — Portfolio analysis via hosted PowerShell
├── OptimizationAgentFactory.cs      — Optimization sub-agent factory (Z3/math/chart tools)
├── OptimizationTools.cs             — Z3 solver, TensorPrimitives math, Plotly.NET charting
├── data/
│   ├── holdings.csv                 — Mock portfolio data (25 holdings)
│   ├── market_data.json             — Expected returns, volatilities, correlations
│   └── investor_profile.json        — Investor profile: age, risk tolerance, goals
└── README.md
```

## Key NuGet packages

| Package | Version | Role |
|---|---|---|
| [`GitHub.Copilot.SDK`](https://github.com/github/copilot-sdk) | 0.1.18 | `CopilotClient` — LLM backend, auth via `gh` CLI |
| [`Microsoft.Agents.AI.GitHub.Copilot`](https://github.com/microsoft/agent-framework) | 1.0.0-preview | Bridge: `AsAIAgent()` extension method |
| [`Microsoft.Agents.AI`](https://github.com/microsoft/agent-framework) | 1.0.0-preview | `AsAIFunction()` for agent-as-tool pattern |
| [`Microsoft.PowerShell.SDK`](https://www.nuget.org/packages/Microsoft.PowerShell.SDK) | 7.4.7 | In-process PowerShell Core runtime |
| [`Microsoft.Z3`](https://github.com/Z3Prover/z3) | 4.13.4 | Z3 theorem prover / optimizer |
| [`Plotly.NET.CSharp`](https://plotly.net) | 0.12.1 | Interactive HTML chart generation |
| [`System.Numerics.Tensors`](https://learn.microsoft.com/dotnet/api/system.numerics.tensors) | 9.0.2 | Hardware-accelerated TensorPrimitives |

## Further reading

- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) — the multi-agent framework used in this sample
- [GitHub Copilot SDK](https://github.com/github/copilot-sdk) — the LLM backend and CLI runtime
- [Z3 Theorem Prover](https://github.com/Z3Prover/z3) — the constraint solver used for portfolio optimization
- [Plotly.NET](https://plotly.net) — the charting library for efficient frontier visualization
- [TensorPrimitives](https://learn.microsoft.com/dotnet/api/system.numerics.tensors.tensorprimitives) — hardware-accelerated numerical operations
