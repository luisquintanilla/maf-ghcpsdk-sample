# GitHub Copilot SDK + Microsoft Agent Framework — Sample Collection

A progressive collection of C# samples showing how to build multi-agent AI applications using the **GitHub Copilot SDK** and **Microsoft Agent Framework (MAF)**. Each sample builds on the previous one, introducing new concepts.

## 🎓 Learning Path

| # | Sample | Concepts | Branch |
|---|--------|----------|--------|
| 1 | [HelloWorldAgent](HelloWorldAgent/) | Single agent, tools, streaming REPL | `main` |
| 2 | [PortfolioAdvisor](PortfolioAdvisor/) | Multi-agent orchestration, agent-as-tool | `main` |
| 3 | [PortfolioOptimizer](PortfolioOptimizer/) | Z3 solver, PowerShell tools, HITL approval, charts | `feature/portfolio-optimizer` |
| 4 | [PortfolioTaxAdvisor](PortfolioTaxAdvisor/) | Tax-lot optimization, constraint modeling, waterfall charts | `feature/portfolio-tax-advisor` |
| 5 | [PortfolioWorkflows](PortfolioWorkflows/) | `AgentWorkflowBuilder`, sequential & concurrent workflows, intent classification | `feature/portfolio-workflows` |
| 6 | [PortfolioRetirement](PortfolioRetirement/) | Monte Carlo simulation, SIMD vectorization, probability charts | `feature/portfolio-retirement` |

## Stack

- **[GitHub Copilot SDK](https://github.com/github/copilot-sdk)** — CLI runtime, auth (via `gh`), model access, and tool dispatch
- **[Microsoft Agent Framework](https://github.com/microsoft/agent-framework)** — `AIAgent` abstraction, sessions, streaming, workflows
- **[Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai)** — `AIFunctionFactory` for defining tools
- **[Microsoft Z3](https://github.com/Z3Prover/z3)** — constraint solver for portfolio optimization (Samples 3–5)
- **[Plotly.NET](https://plotly.net)** — interactive charts (Samples 3–6)

## Prerequisites

| Requirement | Details |
|---|---|
| [.NET 8 SDK](https://dot.net) or later | `dotnet --version` |
| [GitHub CLI](https://cli.github.com) | `gh --version` |
| GitHub Copilot subscription | Required for model access |
| [Pandoc](https://pandoc.org) + [Typst](https://typst.app) | *Optional* — for PDF report generation |

```bash
gh auth login
```

## Quick Start

```bash
# Sample 1 — Hello World
cd HelloWorldAgent && dotnet run

# Sample 3 — Portfolio Optimizer (on its feature branch)
git checkout feature/portfolio-optimizer
cd PortfolioOptimizer && dotnet run

# With verbose/debug modes (Samples 1–6)
dotnet run -- --verbose    # per-agent output + status indicators
dotnet run -- --debug      # raw function calls and arguments
```

## Architecture Progression

```
Sample 1: Single Agent
  User → Agent (2 tools) → Response

Sample 2: Multi-Agent Orchestration
  User → Orchestrator → Agent-as-tool (Analysis)
                       → Agent-as-tool (Advisor)

Samples 3–4: Specialist Agents + Solver Tools
  User → Orchestrator → Analysis Agent (PowerShell tools)
                       → Optimization Agent (Z3 + Plotly)
                       → HITL approval gate

Sample 5: Workflow Orchestration
  User → Intent Classifier (LLM triage)
       → Sequential Workflow: Analysis → Optimization → Tax → Summary
       → Concurrent Workflow: Analysis ∥ Tax ∥ Retirement

Sample 6: Simulation Engine
  User → Orchestrator → Analysis Agent
                       → Retirement Agent (Monte Carlo + SIMD)
                       → HITL approval gate
```

## Report Generation (Samples 3–6)

Each sample can generate HTML and PDF reports after any analysis:

```
💾 Save report? (y/n): y
  📄 HTML report: reports/report-20250219-153200.html
  📄 PDF report:  reports/report-20250219-153200.pdf
```

- **HTML** — styled report with embedded interactive Plotly charts
- **PDF** — via pandoc + typst (install with `winget install --id=Typst.Typst`)

## Key NuGet Packages

| Package | Role |
|---|---|
| `GitHub.Copilot.SDK` | `CopilotClient`, tool dispatch, CLI process management |
| `Microsoft.Agents.AI.GitHub.Copilot` | Bridge: `AsAIAgent()` extension method |
| `Microsoft.Agents.AI.Workflows` | `AgentWorkflowBuilder` for sequential/concurrent orchestration |
| `Microsoft.PowerShell.SDK` | Host PowerShell tools in-process |
| `Microsoft.Z3` | Constraint solver for optimization problems |
| `Plotly.NET.CSharp` | Interactive chart generation |

> **Note:** Both SDKs are in preview and may have breaking changes.

## Related

- [GitHub Copilot SDK docs](https://github.com/github/copilot-sdk)
- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework)
- [Epic issue tracking this collection](https://github.com/luisquintanilla/maf-ghcpsdk-sample/issues/6)
