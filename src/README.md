# ConvoContentBuddy

## Overview

ConvoContentBuddy is an autonomous, AI-powered background listening tool designed to provide real-time programming interview assistance. It processes live speech audio, isolates semantic intent, retrieves exact algorithmic problems from a local vector/graph database, and leverages Gemini 2.5 Flash with Search Grounding to present optimal code solutions in a zero-interaction, ambient UI.

## Architecture

The application follows a microservices architecture orchestrated by .NET 10 Aspire:

- **UI Layer (Blazor WASM)**: Real-time transcription display and solution cards
- **Brain API (ASP.NET Core 10)**: Semantic Kernel orchestration with hybrid retrieval
- **Knowledge Base**: Qdrant (vector store) + PostgreSQL (graph database)
- **Real-Time Sync**: SignalR with Redis backplane for TMR (Triple Modular Redundancy)

## Prerequisites

- .NET 10.0 SDK
- Docker & Docker Compose
- Gemini API Key

## Quick Start

1. **Clone the repository**:
   ```bash
   git clone https://github.com/nam20485/ConvoContentBuddy.git
   cd ConvoContentBuddy
   ```

2. **Configure secrets**:
   ```bash
   cd src/ConvoContentBuddy.AppHost
   dotnet user-secrets set "Gemini:ApiKey" "your-gemini-api-key"
   ```

3. **Run with Aspire**:
   ```bash
   cd src
   dotnet run --project ConvoContentBuddy.AppHost
   ```

4. **Access the dashboard**:
   - Aspire Dashboard: http://localhost:15000
   - Web UI: http://localhost:5001
   - API: http://localhost:5002

## Project Structure

```
src/
├── ConvoContentBuddy.AppHost/          # Aspire orchestrator
├── ConvoContentBuddy.ServiceDefaults/  # Shared service configuration
├── ConvoContentBuddy.API.Brain/        # Main API (Semantic Kernel, SignalR)
├── ConvoContentBuddy.UI.Web/           # Blazor WASM frontend
├── ConvoContentBuddy.Data.Seeder/      # LeetCode data ingestion worker
├── ConvoContentBuddy.Tests.Unit/       # Unit tests
└── ConvoContentBuddy.Tests.Integration/# Integration tests
```

## Features

- **Zero-Interaction UI**: Solutions appear organically as you speak
- **Live Transcription**: Real-time speech-to-text with Web Speech API
- **Hybrid Retrieval**: Vector + Graph + LLM verification pipeline
- **Aerospace-Grade Resilience**: N+2 failover with Safe Mode
- **Triple Modular Redundancy**: 3 API replicas with Redis backplane

## Development

### Running Tests

```bash
cd src
dotnet test
```

### Building Docker Images

```bash
cd src
Dockerfile build -t convocontentbuddy-api -f ConvoContentBuddy.API.Brain/Dockerfile .
Dockerfile build -t convocontentbuddy-ui -f ConvoContentBuddy.UI.Web/Dockerfile .
```

### Seeding Data

The data seeder runs automatically on startup. To run manually:

```bash
cd src/ConvoContentBuddy.Data.Seeder
dotnet run
```

## License

MIT License
