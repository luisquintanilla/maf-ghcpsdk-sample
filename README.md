# Microsoft Agent Framework + GitHub Copilot SDK — Samples

A collection of C# samples that progressively demonstrate how to build AI agents using the [GitHub Copilot SDK](https://github.com/github/copilot-sdk) and [Microsoft Agent Framework (MAF)](https://github.com/microsoft/agent-framework). The samples start with a minimal single-agent "hello world" and build toward real-world multi-agent architectures for line-of-business scenarios.

## Why these technologies together?

Building useful AI agents requires three things:

1. **An LLM backend** — a model that understands natural language and can decide when to call tools
2. **An agent framework** — abstractions for wiring tools, instructions, sessions, and multi-agent coordination
3. **Tool implementations** — the actual business logic your agents can invoke

These samples wire those pieces together using:

| Layer | Technology | What it provides |
|---|---|---|
| **LLM backend** | [GitHub Copilot SDK](https://github.com/github/copilot-sdk) | `CopilotClient` — model access, auth via `gh` CLI, streaming, no API keys needed |
| **Agent framework** | [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) | `AIAgent` — tool dispatch, sessions, multi-agent patterns (agent-as-tool, workflows) |
| **Tool primitives** | [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) | `AIFunction`, `AIFunctionFactory` — wrap any C# method as a tool the LLM can call |
| **Bridge** | `Microsoft.Agents.AI.GitHub.Copilot` | `AsAIAgent()` — connects CopilotClient to the MAF `AIAgent` abstraction |

The key architectural insight is that `CopilotClient.AsAIAgent()` produces a standard `AIAgent` instance. This means everything in the Microsoft Agent Framework ecosystem — multi-agent orchestration, workflows, agent-as-tool — works out of the box with GitHub Copilot as the LLM backend.

## Samples

The samples are ordered as a learning path. Start with HelloWorldAgent to understand the fundamentals, then move to PortfolioAdvisor for multi-agent patterns.

### 1. [HelloWorldAgent](HelloWorldAgent/) — Single agent with tools

**Pattern:** One agent, two tools, interactive REPL

The minimal starting point. A single `AIAgent` backed by `CopilotClient` with two simple tools (`get_greeting` and `get_current_time`). Teaches the fundamentals: tool registration via `AIFunctionFactory`, agent construction via `AsAIAgent()`, session management, and streaming responses.

```
User → GitHubCopilotAgent → [get_greeting, get_current_time] → response
```

**You'll learn:** How CopilotClient, AIAgent, AIFunction, and AgentSession fit together.

### 2. [PortfolioAdvisor](PortfolioAdvisor/) — Multi-agent with PowerShell tools

**Pattern:** Orchestrator agent delegates to a specialist sub-agent via `AsAIFunction()`

A portfolio advisor where the user-facing orchestrator delegates analysis tasks to a specialist sub-agent. The sub-agent's tools run **in-process PowerShell** pipelines (`Import-Csv`, `Group-Object`, `Measure-Object`) against mock portfolio data. Demonstrates the agent-as-tool pattern, multi-agent orchestration, and hosting the PowerShell runtime inside a .NET application via [`Microsoft.PowerShell.SDK`](https://www.nuget.org/packages/Microsoft.PowerShell.SDK).

```
User → OrchestratorAgent → AnalysisAgent.AsAIFunction()
                                │
                                └─ PowerShell tools (portfolio summary,
                                   sector breakdown, top holdings)
```

**You'll learn:** How to build multiple agents with separate responsibilities, wire them together with `AsAIFunction()`, and leverage the PowerShell ecosystem as agent tooling.

## Concepts

### What is an AI agent?

An AI agent is a program that uses a large language model (LLM) to interpret natural-language input, decide what actions to take, and call tools (functions) to accomplish tasks. Unlike a simple chatbot that only generates text, an agent can *do things* — query databases, run calculations, call APIs, process files.

In Microsoft Agent Framework, an agent is represented by the [`AIAgent`](https://github.com/microsoft/agent-framework) abstraction, which combines:

- **Instructions** (system prompt) — defines the agent's persona and behavior
- **Tools** ([`AIFunction`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.aifunction)) — functions the LLM can call
- **Session** ([`AgentSession`](https://github.com/microsoft/agent-framework)) — conversation history for multi-turn interactions

### What is the agent-as-tool pattern?

The agent-as-tool pattern lets one agent call another agent as if it were a regular tool. This is the foundation of multi-agent architectures:

1. You build a **specialist agent** with its own tools and instructions
2. You wrap it with [`AsAIFunction()`](https://github.com/microsoft/agent-framework) to produce an `AIFunction`
3. You give that function to an **orchestrator agent** as one of its tools
4. The orchestrator's LLM decides when to delegate to the specialist

This creates a hierarchical architecture where each agent has a focused responsibility. The orchestrator doesn't need to know *how* the specialist does its work — it only knows *what* it can do (from the function description).

### Why multi-agent instead of one agent with many tools?

A single agent with 20+ tools creates problems:

- **Tool selection degrades** — the LLM has too many choices and picks the wrong one more often
- **Instructions bloat** — one system prompt tries to cover too many responsibilities
- **Context overwhelm** — the conversation fills with irrelevant tool results

Multi-agent solves this by giving each specialist a **small, focused toolset** and **domain-specific instructions**. The orchestrator only sees high-level specialist descriptions, not every individual tool.

### What is in-process PowerShell hosting?

The [`Microsoft.PowerShell.SDK`](https://www.nuget.org/packages/Microsoft.PowerShell.SDK) NuGet package lets you run PowerShell Core inside your .NET process. Instead of shelling out with `Process.Start("pwsh", ...)`, you create a `PowerShell` instance directly in C#:

```csharp
using System.Management.Automation;

using var ps = PowerShell.Create();
ps.AddScript("Get-Process | Sort-Object CPU -Descending | Select-Object -First 5 | ConvertTo-Json");
var results = ps.Invoke();
```

This gives agent tools access to the entire PowerShell cmdlet and module ecosystem — CSV processing, Excel files, API calls, data transforms — without external dependencies or platform-specific process management. PowerShell Core is cross-platform (Windows, macOS, Linux).

## Prerequisites

All samples share the same prerequisites:

| Requirement | Details |
|---|---|
| [.NET 8 SDK](https://dot.net) or later | `dotnet --version` |
| [GitHub CLI](https://cli.github.com) | `gh --version` |
| GitHub Copilot subscription | Required for model access |

Authenticate the CLI before running any sample:

```bash
gh auth login
```

## Getting started

```bash
# Clone the repo
git clone <repo-url>
cd maf-ghcpsdk-sample

# Start with the hello-world sample
cd HelloWorldAgent
dotnet run

# Then try the multi-agent sample
cd ../PortfolioAdvisor
dotnet run
```

## Solution structure

```
maf-ghcpsdk-sample/
├── maf-ghcpsdk-sample.sln          — Solution file (all samples)
├── README.md                        — This file
│
├── HelloWorldAgent/                 — Sample 1: Single agent fundamentals
│   ├── HelloWorldAgent.csproj
│   ├── Program.cs                   — Agent wiring + REPL
│   ├── GreetingTools.cs             — Pure C# tool implementations
│   └── README.md                    — Sample-specific documentation
│
└── PortfolioAdvisor/                — Sample 2: Multi-agent + PowerShell
    ├── PortfolioAdvisor.csproj
    ├── Program.cs                   — Orchestrator agent + REPL
    ├── AnalysisAgentFactory.cs      — Sub-agent factory
    ├── PowerShellTools.cs           — Tools using in-process PowerShell
    ├── data/holdings.csv            — Mock portfolio data
    └── README.md                    — Sample-specific documentation
```

## Roadmap

Future samples may explore:

- **Constraint solving** — using [Z3](https://github.com/Z3Prover/z3) (via [`Microsoft.Z3`](https://www.nuget.org/packages/Microsoft.Z3)) for portfolio optimisation, asset allocation, and tax-lot selection
- **Sequential workflows** — chaining agents in a pipeline using [`AgentWorkflowBuilder.BuildSequential()`](https://github.com/microsoft/agent-framework)
- **Concurrent workflows** — fan-out to multiple specialist agents using [`AgentWorkflowBuilder.BuildConcurrent()`](https://github.com/microsoft/agent-framework)
- **Charting and visualisation** — generating charts with [ScottPlot](https://scottplot.net/) or [MathNet.Numerics](https://numerics.mathdotnet.com/)
- **A2A protocol** — hosting agents over HTTP with `MapA2A()` for remote agent-to-agent communication

## Further reading

- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) — multi-agent framework, `AIAgent` abstraction, workflows
- [GitHub Copilot SDK](https://github.com/github/copilot-sdk) — LLM backend, `CopilotClient`, auth and streaming
- [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) — shared AI abstractions for .NET
- [Hosting PowerShell in .NET](https://learn.microsoft.com/powershell/scripting/dev-cross-plat/create-standard-library-binary-module) — background on in-process PowerShell

> **Note:** The GitHub Copilot SDK and Microsoft Agent Framework packages are in preview and may have breaking changes.
