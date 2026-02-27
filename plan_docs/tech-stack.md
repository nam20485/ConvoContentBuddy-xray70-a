# ConvoContentBuddy - Technology Stack

## Overview
This document defines the complete technology stack for the ConvoContentBuddy application, an autonomous background listener designed to assist users in real-time during technical interviews or coding sessions.

## Core Technologies

### Language & Runtime
- **Language**: C# 14
- **Runtime**: .NET 10.0
- **SDK Version**: 10.0.0 (with rollForward: latestFeature)

### Web Frameworks
- **Backend**: ASP.NET Core 10
- **Frontend**: Blazor WebAssembly 10
- **Real-time Communication**: SignalR with Redis Backplane

## AI & Machine Learning

### AI Orchestration
- **Framework**: Microsoft.SemanticKernel
- **Abstractions**: Microsoft.Extensions.AI (.NET 10)

### AI Models
- **Primary LLM**: Gemini 2.5 Flash (gemini-2.5-flash-preview-09-2025)
- **Embeddings**: Gemini text-embedding-004
- **Search Grounding**: Google Search via Gemini API

## Data Storage

### Vector Database
- **Primary**: Qdrant (gRPC interface)
- **Collection**: leetcode_problems
- **Dimensions**: 1536
- **Similarity Metric**: Cosine

### Relational Database
- **Primary**: PostgreSQL with pgvector extension
- **Schema**: Problems table, ProblemEdges table (adjacency list)
- **ORM**: Npgsql.EntityFrameworkCore.PostgreSQL

### Caching & Backplane
- **Service**: Redis
- **Purpose**: SignalR backplane for multi-instance synchronization

## Infrastructure & Orchestration

### Container Orchestration
- **Platform**: .NET Aspire 10
- **Components**:
  - Aspire.Hosting.AppHost
  - Aspire.Hosting.Redis
  - Aspire.Hosting.PostgreSQL
  - Aspire.Hosting.Qdrant

### Resilience
- **Library**: Polly (via Microsoft.Extensions.Http.Resilience)
- **Policies**:
  - Exponential Backoff: 1s, 2s, 4s, 8s, 16s
  - Circuit Breaker: 30s break duration

### Observability
- **Tracing**: OpenTelemetry (OTLP)
- **Metrics**: System.Diagnostics.Metrics (.NET 10)
- **Health Checks**: Microsoft.Extensions.Diagnostics.HealthChecks

## Frontend Technologies

### UI Framework
- **Styling**: Tailwind CSS
- **Components**: Blazor WASM components

### Browser APIs
- **Speech Recognition**: Web Speech API (webkitSpeechRecognition)
- **Interoperability**: JavaScript interop (speechInterop.js)

## Development Tools

### Build & Package Management
- **Solution File**: ConvoContentBuddy.sln
- **Package Manager**: NuGet

### Testing
- **Unit Testing**: xUnit/NUnit (TBD)
- **Integration Testing**: ASP.NET Core TestHost
- **E2E Testing**: Playwright (TBD)

### CI/CD
- **Platform**: GitHub Actions
- **Workflows**:
  - Build and test
  - Secret scanning (TruffleHog)
  - Code validation

## Security

### Authentication & Authorization
- **Framework**: ASP.NET Core Identity (if applicable)
- **Metrics**: .NET 10 Authentication/Authorization metrics

### Secrets Management
- **Scanning**: TruffleHog
- **Storage**: Environment variables / Azure Key Vault (production)

## Deployment Targets

### Local Development
- **Orchestration**: .NET Aspire AppHost
- **Containers**: Docker/Podman compatible

### Production
- **Cloud**: Azure (via azd integration)
- **Deployment**: Containerized microservices
- **IaC**: Azure Developer CLI (azd) with Aspire manifests

## Summary

ConvoContentBuddy leverages a modern, cloud-native stack built on .NET 10 and .NET Aspire. The architecture emphasizes:

- **Resilience**: Triple Modular Redundancy (TMR) with Polly failover policies
- **Performance**: Sub-500ms vector search with Qdrant and sub-2s end-to-end latency
- **Real-time**: SignalR with Redis backplane for seamless multi-instance communication
- **AI-First**: Semantic Kernel with Gemini 2.5 Flash for intelligent problem identification
- **Developer Experience**: Single-command startup via Aspire AppHost

This stack supports the application's core mission: providing autonomous, real-time coding interview assistance through ambient AI.
