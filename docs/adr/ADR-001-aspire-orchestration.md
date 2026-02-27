# ADR-001: .NET 10 Aspire for Microservices Orchestration

## Status

Accepted

## Context

ConvoContentBuddy requires a microservices architecture with multiple components (API, UI, databases, cache). We need a solution that:
- Simplifies local development environment setup
- Provides consistent orchestration between dev and production
- Supports service discovery and connection string management
- Integrates with observability tools (OpenTelemetry)

## Decision

We will use **.NET 10 Aspire** as the orchestration platform for ConvoContentBuddy.

## Consequences

### Positive
- Single-command startup for all services (`dotnet run --project AppHost`)
- Automatic service discovery and connection string injection
- Built-in dashboard for monitoring services
- Native integration with OpenTelemetry
- Docker Compose generation for production

### Negative
- New technology (Aspire 10.0 is in preview)
- Requires .NET 10 SDK
- Learning curve for team members unfamiliar with Aspire

## Alternatives Considered

- **Docker Compose only**: More manual configuration, no service discovery
- **Kubernetes (k8s)**: Too complex for local development
- **Tye**: Deprecated in favor of Aspire

## Related Decisions

- ADR-002: Semantic Kernel for AI Orchestration
- ADR-003: SignalR with Redis Backplane for TMR
