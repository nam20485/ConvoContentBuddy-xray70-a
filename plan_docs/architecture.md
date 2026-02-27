# ConvoContentBuddy - System Architecture

## Overview

ConvoContentBuddy is an autonomous background listener designed to provide real-time programming interview assistance. The system processes live speech audio, isolates semantic intent, retrieves exact algorithmic problems from a local vector/graph database, and leverages Gemini 2.5 Flash with Search Grounding to present optimal code solutions in a zero-interaction, ambient UI.

## Architectural Principles

### 1. Aerospace-Grade Resilience (TMR)
The system employs Triple Modular Redundancy and graceful failovers. If a primary AI or database layer fails, the system continues to operate in a degraded "Safe Mode."

### 2. Ambient User Experience
The UI is zero-interaction. Solutions and code snippets "appear" organically as the conversation evolves, requiring no clicks from the user.

### 3. High-Speed Semantic Matching
The system relies on vector embeddings and relational graphs to guarantee sub-500ms retrieval of coding problem contexts.

## System Layers

### Layer 1: Audio Input & Transcription
**Components:**
- Browser-based Web Speech API (webkitSpeechRecognition)
- JavaScript Interop layer (speechInterop.js)
- Blazor WASM client

**Flow:**
1. Captures continuous audio streams from user microphone
2. Converts speech into timestamped text segments
3. Filters out silence and manages "active" conversation buffers
4. Passes transcript chunks to the Brain API via SignalR or HTTP

### Layer 2: Context & Intent Analysis ("The Brain")
**Components:**
- ASP.NET Core 10 API (ConvoContentBuddy.API.Brain)
- Microsoft.SemanticKernel
- Gemini 2.5 Flash (gemini-2.5-flash-preview-09-2025)
- Microsoft.Extensions.AI (.NET 10)

**Process:**
1. Periodically (or upon detecting keywords like "array", "pointer", "sum") sends recent transcript buffer to Gemini
2. Identifies specific LeetCode problem title/number and programming language being discussed
3. Triggers the Resource Retrieval Layer

### Layer 3: Resource Retrieval (Hybrid Chain)
**Components:**
- VectorSearchProvider (Qdrant gRPC)
- GraphTraversalProvider (PostgreSQL with pgvector)
- HybridRetrieverService (Semantic Kernel)
- Google Search Grounding (via Gemini API)

**Flow:**
1. **Vector Search**: Query Qdrant with transcript embedding → Top-3 candidates with similarity scores
2. **Graph Expansion**: Query PostgreSQL for neighbors of Top-1 match → "Follow-up" context
3. **LLM Verification**: Use Gemini 2.5 Flash to confirm final problem ID
4. **Search Grounding**: Retrieve optimal time/space complexity solutions from web

### Layer 4: Ambient UI
**Components:**
- Blazor WebAssembly (ConvoContentBuddy.UI.Web)
- SignalR Client with Redis Backplane
- Tailwind CSS
- Monaco Editor or Prism.js (syntax highlighting)

**Components:**
- **Live Feed**: Real-time transcript visualization
- **Active Problem Card**: Highlighting the detected challenge
- **Solution Panel**: Code snippets and complexity analysis that update as conversation evolves

## Data Architecture

### Vector Store (Qdrant)
**Collection**: `leetcode_problems`
**Dimensions**: 1536
**Similarity**: Cosine
**Payload Schema**:
```json
{
  "id": "integer",
  "title": "string",
  "title_slug": "string",
  "difficulty": "enum:Easy|Medium|Hard",
  "description": "string",
  "topics": ["string"],
  "acceptance_rate": "float"
}
```

### Graph Store (PostgreSQL + pgvector)
**Tables**:
- `problems`: Problem metadata with vector embedding
- `problem_edges`: Adjacency list for relationships (source_id, target_id, relationship_type)
- `topics`: Normalized topic tags

**Relationships**:
- "Similar Questions" (derived from LeetCode metadata)
- "Prerequisite" (e.g., Two Sum → Three Sum)
- "Follow-up" (e.g., Merge Two Sorted Lists → Merge K Sorted Lists)

### Cache & Backplane (Redis)
**Purpose**: SignalR backplane for multi-instance API synchronization
**Data**: WebSocket connection state, active session metadata

## Resilience Architecture (N+2 Failover)

### Triple Modular Redundancy (TMR)
- API.Brain runs with `withReplicas(3)` in Aspire AppHost
- Redis backplane ensures SignalR state persistence across instance restarts
- Health checks (/health) monitor each instance

### Failover Tiers
**Tier 1 (Primary)**: Gemini 2.5 Flash + Search Grounding
- Full hybrid chain: Vector → Graph → LLM Verify → Search

**Tier 2 (Fallback)**: Alternative Model/Region
- Azure OpenAI or secondary Gemini key
- Reduced context window, basic retrieval

**Tier 3 (Safe Mode)**: Deterministic Local Only
- Bypass LLM verification
- Return #1 Vector match with `confidence: low` flag
- UI displays "Safe Mode Active" warning

### Circuit Breaker Policy
- **Threshold**: 5 consecutive failures
- **Break Duration**: 30 seconds
- **Half-Open Requests**: 1 test request
- **Monitoring**: OpenTelemetry traces for all state transitions

## Security Architecture

### Authentication & Authorization
- ASP.NET Core Identity (if user accounts required)
- JWT tokens for API authentication
- Role-based access control (RBAC) for admin functions

### Secrets Management
- Development: User secrets / .env files
- Production: Azure Key Vault integration
- Scanning: TruffleHog for secret detection in CI

### Data Protection
- Encryption at rest (database-level)
- TLS 1.3 for all communications
- Input sanitization for all transcript data

## Deployment Architecture

### Local Development
```
Developer Machine
├── Aspire AppHost (orchestrator)
│   ├── ConvoContentBuddy.UI.Web (Blazor WASM)
│   ├── ConvoContentBuddy.API.Brain (x3 replicas)
│   ├── Qdrant (vector store)
│   ├── PostgreSQL (graph store)
│   └── Redis (SignalR backplane)
```

### Production (Azure)
```
Azure Container Apps / AKS
├── Frontend: Static Web Apps (Blazor WASM)
├── API: Container Apps (3 replicas, auto-scaling)
├── Vector: Qdrant Cloud / Azure Container Instance
├── Graph: Azure Database for PostgreSQL (with pgvector)
└── Cache: Azure Cache for Redis
```

### CI/CD Pipeline
1. **Build**: .NET 10 build, test, package
2. **Security**: TruffleHog secret scan, dependency check
3. **Deploy**: Aspire manifest generation, container push
4. **Verify**: Health checks, smoke tests

## Performance Targets

### Latency Requirements
- **Vector Search**: < 500ms (Qdrant gRPC)
- **Graph Query**: < 100ms (PostgreSQL)
- **LLM Verification**: < 800ms (Gemini 2.5 Flash)
- **End-to-End**: < 2.0s (Transcript → UI Card)

### Throughput Targets
- **Concurrent Users**: 100+ (with TMR)
- **Transcript Processing**: 10 chunks/second per user
- **Vector Queries**: 1000/second (Qdrant)

### Availability Targets
- **Uptime**: 99.9% (with N+2 failover)
- **Recovery Time**: < 5 seconds (auto-restart)
- **Failover Time**: < 1 second (circuit breaker)

## Summary

The ConvoContentBuddy architecture is designed for aerospace-grade resilience and ambient user experience. By leveraging .NET 10 Aspire for orchestration, Qdrant and PostgreSQL for hybrid semantic storage, and Gemini 2.5 Flash for intelligent analysis, the system achieves sub-2-second end-to-end latency while maintaining 99.9% uptime through Triple Modular Redundancy and N+2 failover strategies.
