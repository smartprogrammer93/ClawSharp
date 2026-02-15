# ClawSharp

A .NET implementation of the [OpenClaw](https://github.com/nicobailon/openclaw) AI agent framework. ClawSharp provides an extensible, multi-provider LLM agent with tool use, memory, and multi-channel messaging.

## Features

- **Multi-provider LLM support** — OpenAI, Anthropic, OpenRouter, Ollama, and any OpenAI-compatible API
- **Tool system** — Extensible tool registry for agent capabilities
- **Memory** — Persistent memory with optional vector search
- **Channels** — Telegram, Discord, Slack integration
- **Gateway** — HTTP API + optional web UI
- **Security** — Command sandboxing and access control

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Build

```bash
dotnet build
```

### Run

```bash
dotnet run --project src/ClawSharp.Cli
```

### Configure

```bash
cp config.example.toml ~/.clawsharp/config.toml
# Edit with your API keys and preferences
```

### Run Tests

```bash
dotnet test
```

## Project Structure

```
src/
  ClawSharp.Cli/            # CLI entry point
  ClawSharp.Core/           # Core interfaces, models, config
  ClawSharp.Agent/          # Agent loop and orchestration
  ClawSharp.Providers/      # LLM provider implementations
  ClawSharp.Tools/          # Built-in tools
  ClawSharp.Memory/         # Memory and embeddings
  ClawSharp.Gateway/        # HTTP API gateway
  ClawSharp.Infrastructure/ # DI, logging, cross-cutting
  ClawSharp.UI/             # Web UI (Blazor)
tests/
  ClawSharp.Core.Tests/
  ClawSharp.Cli.Tests/
  ClawSharp.Agent.Tests/
  ClawSharp.Infrastructure.Tests/
  ClawSharp.TestHelpers/    # Shared test utilities
docs/
  architecture.md           # Architecture overview
```

## Configuration

ClawSharp uses TOML configuration. See [`config.example.toml`](config.example.toml) for all available options.

Config file location: `~/.clawsharp/config.toml`

## Documentation

- [Architecture Overview](docs/architecture.md)

## License

MIT
