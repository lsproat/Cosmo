# Cosmo

**Cosmo is a model-agnostic .NET orchestration layer for local and cloud LLMs.**

The project is designed to keep application-level AI concerns—such as conversation state, context construction, token budgeting, memory, routing, and tool execution—independent of any individual model provider.

Ollama is currently the first implemented provider, allowing Cosmo to work with locally hosted models while the broader orchestration architecture continues to evolve.

> **Status:** Active development. Core chat orchestration, conversation management, provider abstraction, context construction and token budgeting, Ollama integration, automated testing, and CI are implemented.

---

## Why Cosmo?

Many LLM integrations begin as thin wrappers around a provider API. As an application grows, however, concerns such as conversation history, context-window management, memory, routing, tools, and provider-specific behavior quickly become application responsibilities.

Cosmo explores an architecture where those responsibilities belong to the orchestration layer rather than to Ollama or any future cloud provider.

The goal is to allow conversations and application behavior to remain portable across models and providers while keeping provider-specific integrations behind clearly defined boundaries.

---

## Current Capabilities

### Implemented

- Model-agnostic provider abstraction
- Ollama model provider
- Application-owned conversation state
- Lazy conversation creation when the first message is sent
- Stable conversation IDs independent of the model provider
- Provider-independent conversation message models
- Dedicated conversation context builder
- Configurable context-window and output-token budgeting
- Conversation-history trimming when requests exceed the available input budget
- Base system prompt and runtime context support
- Model-specific token counting
- Automated tests
- GitHub Actions build and test validation
- Centralized API error handling
- OpenAPI support

### Planned

- Durable conversation persistence
- Conversation retrieval/query endpoints
- Conversation summarization and long-term memory
- Additional local and cloud model providers
- Intelligent model routing and fallback
- Tool/function calling and MCP integration
- Structured observability and provider health checks
- Containerized deployment
- Authentication and authorization

---

## Architecture

Cosmo is organized as a layered .NET solution:

```text
Cosmo.Api
    HTTP endpoints, API configuration, OpenAPI, and error handling

Cosmo.Application
    Commands, handlers, orchestration, context construction, and abstractions

Cosmo.Domain
    Conversations, messages, and core domain behavior

Cosmo.Infrastructure
    Ollama integration, repositories, tokenization, and external services

Cosmo.Tests
    Automated application and infrastructure tests
```

The core architectural principle is:

> **Cosmo owns orchestration. Providers own model communication.**

Conversation state, context construction, token budgeting, and future memory/routing logic belong to Cosmo. Model providers are responsible for translating normalized application requests into the format expected by the underlying model runtime.

The application uses MediatR/CQRS to keep HTTP transport concerns separate from application behavior.

---

## Conversation and Context Management

A conversation is created lazily when its first message is sent, so creating a conversation and sending the initial message can happen in a single request.

Cosmo owns the conversation history rather than relying on the model provider to maintain state.

Before a model is invoked, the context builder determines what information can fit within the configured model context window.

Conceptually:

```text
Input Token Limit =
    Model Context Window
    - Reserved Output Tokens
```

The context builder currently supports:

- base system instructions
- runtime context such as the current date and time
- conversation history
- model-specific token counting
- configurable token budgets
- trimming older messages when the available input budget is exceeded

Future work will extend this system with conversation summarization, long-term memory, and relevance-based retrieval.

---

## Model Providers

Model providers implement a shared application abstraction so the rest of Cosmo does not depend directly on Ollama-specific request or response types.

Ollama is currently the only implemented provider.

The longer-term goal is to support additional local and cloud providers without requiring changes to conversation management, context construction, memory, or other orchestration logic.

---

## Running Locally

### Requirements

- .NET 10 SDK
- Ollama
- A model installed through Ollama

Clone the repository:

```bash
git clone https://github.com/lsproat/Cosmo.git
cd Cosmo
```

Restore dependencies:

```bash
dotnet restore
```

Run the test suite:

```bash
dotnet test
```

Start the API:

```bash
dotnet run --project Cosmo.Api
```

Ollama must be running and Cosmo must be configured to use an available local model.

---

## Current Limitations

Cosmo is still under active development.

Currently:

- Conversations are stored in memory and do not survive application restarts.
- Ollama is the only implemented model provider.
- Conversation summarization and long-term memory are not yet implemented.
- Tool execution and MCP integration are not yet available.
- Automatic model routing is not yet implemented.
- Authentication and authorization have not yet been added.
- Streaming responses are not currently exposed by the API.
- Production deployment and operational hardening are still planned.

---

## Roadmap

### Near Term

- Add durable conversation and message persistence
- Add conversation retrieval/query endpoints
- Expand automated test coverage
- Add structured logging and provider health checks
- Improve reliability and configuration validation

### Future Orchestration

- Add additional local and cloud model providers
- Summarize older conversation history
- Build provider-independent long-term memory
- Add relevance-based memory retrieval
- Introduce intelligent model routing and provider fallback
- Add provider-independent tool/function calling
- Explore Model Context Protocol (MCP) integration

### Operations

- Containerize Cosmo for deployment
- Add production environment configuration
- Add authentication and authorization
- Add metrics and tracing
- Make deployment reproducible across environments

---

## Microsoft.Extensions.AI

Cosmo currently uses its own provider abstraction while the orchestration architecture is being developed.

`Microsoft.Extensions.AI` provides related abstractions around AI clients and model-provider integrations. As Cosmo evolves, I plan to evaluate where those abstractions can replace or complement custom provider-level infrastructure.

Cosmo intentionally owns higher-level application concerns such as conversation persistence, context construction, token budgeting, memory, routing, tool authorization, and provider fallback.

The goal is not to recreate framework functionality unnecessarily, but to explore the architectural boundary between model-provider abstractions and a higher-level AI orchestration platform.

---

## Project Direction

Cosmo began as a clean interface around locally hosted models, but the broader goal is to build an application that owns its AI behavior independently of any single provider.

The project is evolving around three principles:

**Provider independence** — application behavior should not depend on a single model runtime or vendor.

**Application-owned orchestration** — conversation state, memory, routing, tools, and context policy belong to the application.

**Inspectable behavior** — as orchestration becomes more complex, it should remain possible to understand how and why Cosmo constructed context, selected a model, or invoked an external capability.
