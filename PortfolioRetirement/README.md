# PortfolioRetirement — Monte Carlo Retirement Planning

The sixth sample in the progressive multi-agent learning path. This project demonstrates
**SIMD-accelerated Monte Carlo simulation** using `System.Numerics.Tensors.TensorPrimitives`
and interactive **Plotly.NET** visualizations for retirement planning.

## Architecture

```
User (REPL)
  │
  ▼
OrchestratorAgent (CopilotClient #1)
  │  tools: [analysisAgentTool, retirementAgentTool]
  │
  ├── analysisAgentTool = AnalysisAgent.AsAIFunction()
  │     PowerShell-based portfolio analysis
  │     ├── get_portfolio_summary
  │     ├── get_sector_breakdown
  │     └── get_top_holdings
  │
  └── retirementAgentTool = RetirementAgent.AsAIFunction()
        Retirement planning with Monte Carlo + charts
        ├── run_monte_carlo
        ├── compare_withdrawal_strategies
        ├── optimize_social_security
        ├── render_probability_cone
        └── render_strategy_comparison
```

## What This Sample Teaches

| Concept | Details |
|---|---|
| **TensorPrimitives / SIMD** | Vectorized batch math via `System.Numerics.Tensors` — multiply, add, and sum entire arrays in one call instead of element-by-element loops |
| **Box-Muller Transform** | Generate normally distributed random returns without external libraries |
| **Monte Carlo Simulation** | 10,000-path stochastic portfolio projection with percentile extraction |
| **Withdrawal Strategies** | 4% Rule, Dynamic Percentage, and Guardrails — compared via simulation |
| **Social Security Optimization** | Exhaustive search over 81 claiming-age combinations with COLA |
| **Plotly.NET** | Interactive HTML charts (probability cones, grouped bar comparisons) |
| **Human-in-the-Loop** | Orchestrator presents all withdrawal strategies and asks user preference before proceeding |
| **Multi-Agent Orchestration** | Two sub-agents (analysis + retirement) wrapped as tools for a user-facing orchestrator |

## Prerequisites

- .NET 8 SDK
- A GitHub Copilot subscription with SDK access
- `GITHUB_TOKEN` environment variable set

## How to Run

```bash
cd PortfolioRetirement
dotnet run
```

### Example prompts

- `Run a Monte Carlo simulation for my retirement`
- `Compare withdrawal strategies for a $1.5M portfolio`
- `Optimize my Social Security claiming age`
- `Show me my portfolio summary`
- `Render a probability cone chart for 18 years`

## Key Files

| File | Purpose |
|---|---|
| `MonteCarloEngine.cs` | TensorPrimitives-based simulation engine with Box-Muller RNG |
| `RetirementTools.cs` | Tool implementations: Monte Carlo, withdrawals, SS optimization, charts |
| `RetirementAgentFactory.cs` | Factory creating the retirement sub-agent with all tools |
| `AnalysisAgentFactory.cs` | Factory creating the portfolio analysis sub-agent (reused from PortfolioAdvisor) |
| `PowerShellTools.cs` | PowerShell-based portfolio data tools (reused from PortfolioAdvisor) |
| `Program.cs` | Orchestrator + REPL with 3 CopilotClients |

## Data Files

- `data/holdings.csv` — 25 portfolio holdings with shares, prices, sectors, account types
- `data/market_data.json` — Expected returns, volatility, dividend yields, and sector correlations for all 25 assets
- `data/investor_profile.json` — Investor demographics, accounts, Social Security estimates, retirement goals
- `data/social_security.json` — SS claiming ages, reduction/increase factors, COLA assumption
