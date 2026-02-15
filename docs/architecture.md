# ClawSharp Architecture

## High-Level Overview

```
┌─────────────────────────────────────────────────────┐
│                    Channels                          │
│  ┌──────────┐  ┌─────────┐  ┌───────┐  ┌────────┐  │
│  │ Telegram │  │ Discord │  │ Slack │  │  CLI   │  │
│  └────┬─────┘  └────┬────┘  └───┬───┘  └───┬────┘  │
│       └──────────────┴──────────┴───────────┘       │
│                      │ IChannel / IMessageBus       │
├──────────────────────┼──────────────────────────────┤
│                  Agent Loop                          │
│  ┌───────────────────┴───────────────────────┐      │
│  │  Receive → Think → Act → Respond → Store  │      │
│  └───┬──────────┬────────────────┬───────────┘      │
│      │          │                │                   │
│  ┌───┴───┐  ┌──┴──────┐  ┌─────┴────┐              │
│  │ Tools │  │ Provider│  │  Memory  │              │
│  │(ITool)│  │(ILlm-   │  │(ISession │              │
│  │       │  │Provider) │  │ Manager) │              │
│  └───────┘  └─────────┘  └──────────┘              │
├─────────────────────────────────────────────────────┤
│                   Gateway (HTTP API + UI)            │
└─────────────────────────────────────────────────────┘
```

## Key Interfaces

| Interface | Location | Purpose |
|-----------|----------|---------|
| `ILlmProvider` | `Core/Providers/` | Abstraction over LLM APIs (OpenAI, Anthropic, etc.) |
| `ITool` | `Core/Tools/` | A capability the agent can invoke |
| `IToolRegistry` | `Core/Tools/` | Registry for discovering and invoking tools |
| `IChannel` | `Core/Channels/` | Inbound/outbound messaging for a platform |
| `IMessageBus` | `Core/Channels/` | Routes messages between channels and the agent |
| `ISessionManager` | `Core/Sessions/` | Manages conversation sessions and context |

## Project Responsibilities

- **ClawSharp.Core** — Interfaces, models, configuration. Zero external dependencies. Everything depends on Core.
- **ClawSharp.Agent** — The agent loop: receives messages, calls the LLM, executes tools, returns responses.
- **ClawSharp.Providers** — Concrete LLM provider implementations (OpenAI, Anthropic, Ollama, OpenRouter).
- **ClawSharp.Tools** — Built-in tools (file I/O, shell exec, web search, etc.).
- **ClawSharp.Memory** — Persistent storage, embeddings, and semantic search.
- **ClawSharp.Infrastructure** — Dependency injection, logging, service registration.
- **ClawSharp.Gateway** — ASP.NET Core HTTP API for external access and the web UI.
- **ClawSharp.Cli** — Command-line interface and entry point.
- **ClawSharp.UI** — Blazor-based web frontend.

## Data Flow

1. **Message arrives** via a channel (Telegram, CLI, etc.)
2. **IMessageBus** routes it to the agent
3. **Agent loop** builds an `LlmRequest` with conversation history + available tools
4. **ILlmProvider** sends the request to the LLM and returns an `LlmResponse`
5. If the response contains **tool calls**, the agent executes them via `IToolRegistry` and loops back to step 3
6. Final response is sent back through the **channel**
7. Conversation is persisted via **ISessionManager**

## Configuration

All configuration flows through `ClawSharpConfig` (loaded from TOML). Sub-configs:

- `ProvidersConfig` — API keys, endpoints, default models
- `GatewayConfig` — HTTP bind address, port, API key
- `ChannelsConfig` — Per-channel settings (tokens, allowed users)
- `MemoryConfig` — Database path, embedding settings
- `SecurityConfig` — Sandbox rules, allowed commands
- `HeartbeatConfig` — Periodic polling settings
- `TunnelConfig` — External tunnel (cloudflared, etc.)
