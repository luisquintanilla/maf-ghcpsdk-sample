# PortfolioTaxAdvisor

**Phase 4** of the progressive multi-agent learning path — demonstrates **Z3 constraint solving for tax optimization**: asset location (which account type for each holding) and tax-loss harvesting (which lots to sell while avoiding wash sale violations).

## Architecture

```
User (REPL)
  │
  ▼
OrchestratorAgent (CopilotClient #1)
  │  tools: [analysisAgentTool, taxAgentTool]
  │
  ├── analysisAgentTool = AnalysisAgent.AsAIFunction()
  │     tools: [get_portfolio_summary, get_sector_breakdown, get_top_holdings]
  │
  └── taxAgentTool = TaxAgent.AsAIFunction()
        TaxAgent (CopilotClient #2)
          tools:
            ├── optimize_asset_location  → Z3 assignment CSP
            ├── find_harvest_candidates  → Z3 selection w/ wash sale
            ├── compute_tax_savings      → TensorPrimitives
            └── render_tax_chart         → Plotly.NET waterfall → HTML
```

## Key Concepts

- **Z3 Constraint Solving** — Uses Microsoft Z3 SMT solver for:
  - **Asset location optimization**: assigns each holding to the best account type (Taxable, Roth, Traditional) to minimize tax drag
  - **Tax-loss harvesting**: selects lots to sell while respecting wash sale rules (30-day window)
- **TensorPrimitives** — Vectorized computation of tax savings across all holdings
- **Plotly.NET** — Waterfall chart visualization of tax optimization savings
- **Human-in-the-Loop** — Harvesting candidates require user approval before acceptance

## Prerequisites

- .NET 8 SDK
- A GitHub Copilot subscription with SDK access
- `GITHUB_TOKEN` environment variable set

## Running

```bash
cd PortfolioTaxAdvisor
dotnet run
```

## Sample Prompts

- "Show me my portfolio summary"
- "Optimize my asset location for taxes"
- "Find tax-loss harvesting opportunities"
- "Compute my potential tax savings"
- "Generate a tax savings chart"
