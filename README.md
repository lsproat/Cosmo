## Roadmap / Design Goals

This project is intended to grow beyond a simple wrapper around an LLM API. The long-term goal is to build a model-agnostic AI orchestration layer that owns conversation state, context management, memory, routing, and tool execution independently of any individual model provider.

### Conversation Management

- Persist conversations and messages independently of the underlying LLM provider.
- Create conversations lazily when the first message is sent rather than when a user opens a new chat.
- Allow a single request to create a conversation and send its first message.
- Maintain stable conversation IDs at the orchestration layer.
- Support retrieving and continuing existing conversations.
- Keep the conversation model independent of provider-specific message formats.
- Allow a conversation to move between different model providers without losing its history.

### Context Management

- Introduce a dedicated context builder responsible for determining what information should be sent to a model for each request.
- Avoid indefinitely resending the entire raw conversation as conversations grow.
- Support configurable context-window limits based on the selected model.
- Truncate or summarize older conversation history when necessary.
- Preserve the most relevant recent messages while intelligently compressing older context.
- Keep context-building logic separate from individual model-provider implementations.
- Eventually support relevance-based retrieval rather than relying exclusively on chronological history.

### Long-Term Memory

- Build a persistent memory layer that exists independently of conversation history.
- Extract durable information from conversations that may be useful in future interactions.
- Distinguish short-term conversational context from long-term user or system memory.
- Retrieve relevant memories dynamically when constructing model context.
- Explore embedding/vector-based memory retrieval.
- Experiment with periodic memory consolidation, including a possible daily process that reviews recent conversations and decides what information should be retained, consolidated, updated, or discarded.
- Allow memory to remain useful when switching between local and cloud models.

### Model-Agnostic Provider Architecture

- Support multiple LLM providers behind a common internal abstraction.
- Keep application and domain logic independent of Ollama-specific or cloud-provider-specific APIs.
- Add additional local and cloud providers without requiring changes to conversation or memory logic.
- Normalize provider-specific request and response formats into shared application models.
- Support different model capabilities, token limits, and configuration requirements.
- Allow the active model or provider to be changed without migrating conversation data.

### Intelligent Model Routing

- Add a routing layer capable of selecting an appropriate provider/model for a request.
- Route inexpensive or privacy-sensitive requests to local models when appropriate.
- Allow more demanding tasks to be routed to more capable cloud models.
- Consider model capability, latency, cost, privacy, availability, and context-window requirements when routing.
- Support explicit user/model selection in addition to automatic routing.
- Eventually support fallback behavior when a preferred provider is unavailable.

### Local Model Support

- Continue expanding first-class Ollama support.
- Handle models that have unloaded from memory after inactivity.
- Account for longer initial response times when a model must be loaded.
- Explore model warm-up/preloading behavior.
- Expose provider health and availability information.
- Keep local inference usable without requiring internet connectivity where possible.

### Tools and MCP

- Add tool/function calling without coupling tools to a particular LLM provider.
- Explore Model Context Protocol (MCP) integration.
- Allow models to discover and invoke approved external capabilities through the orchestration layer.
- Keep authorization and execution of tools controlled by the gateway rather than directly by the model.
- Eventually integrate services such as Home Assistant and other personal infrastructure.

### API and Application Architecture

- Continue using CQRS/MediatR to keep API transport concerns separate from application behavior.
- Maintain clear boundaries between API, Application, Domain, and Infrastructure layers.
- Add request validation through FluentValidation and MediatR pipeline behaviors.
- Standardize application errors and API error responses.
- Keep HTTP request DTOs separate from application commands where appropriate.
- Add query paths for retrieving conversations, messages, models, and provider information.

### Persistence

- Add durable storage for conversations and messages.
- Define persistence models without leaking database concerns into the domain/application layers.
- Add storage for long-term memories and generated conversation summaries.
- Design persistence so additional metadata can be added later without tightly coupling it to a specific model.
- Consider caching where it meaningfully improves context retrieval or provider performance.

### Performance

- Treat performance as a first-class concern rather than an afterthought.
- Minimize unnecessary allocations and transformations in frequently executed message/context paths.
- Avoid repeatedly converting or normalizing values that can be represented more efficiently internally.
- Use appropriate immutable/read-only structures for message data where practical.
- Measure context-building and provider latency.
- Add caching only where measurements justify it.
- Track local model load time separately from inference time.

### Reliability and Resilience

- Add provider-specific timeout policies.
- Distinguish model-loading delays from failed requests.
- Add cancellation support throughout the request pipeline.
- Introduce retry behavior only for operations that are safe to retry.
- Add provider health checks.
- Support graceful degradation and eventual provider fallback.
- Ensure failures from one provider do not leak provider-specific details throughout the application.

### Observability

- Add structured logging throughout the orchestration pipeline.
- Capture model/provider selection, request duration, context size, and response timing.
- Track token usage where providers expose it.
- Add metrics around context construction, model loading, inference, failures, and routing.
- Add distributed tracing as the system begins interacting with more external services.
- Make routing and context decisions inspectable enough to understand why the system behaved a certain way.

### Security and Privacy

- Add authentication/authorization before exposing the gateway beyond trusted development environments.
- Keep provider credentials and API keys outside source control.
- Validate configuration at application startup.
- Restrict access to locally hosted Ollama endpoints.
- Treat stored conversations and memories as sensitive application data.
- Ensure tool execution is explicitly authorized rather than allowing unrestricted model-driven actions.
- Eventually support different privacy policies for local versus cloud inference.

### Testing

- Add unit tests for application handlers, validation, routing, and context-building logic.
- Add integration tests for persistence.
- Add provider contract tests so different LLM implementations behave consistently from the application's perspective.
- Mock model providers so orchestration behavior can be tested without running an LLM.
- Add end-to-end tests for conversation creation and continuation flows.

### Deployment and Operations

- Containerize the application for deployment to the local Linux server.
- Support environment-specific configuration for development and production.
- Add CI/CD for build, test, and deployment.
- Add startup configuration validation so missing required settings fail clearly.
- Provide health endpoints for the gateway and configured providers.
- Eventually make deployment reproducible enough to run the gateway on different machines without substantial manual setup.

## Microsoft.Extensions.AI

As the project evolves, I want to expand this README with a dedicated comparison between this architecture and `Microsoft.Extensions.AI`, including where the approaches overlap, which abstractions Microsoft already provides, and why this project intentionally owns additional concerns such as conversation persistence, context construction, memory, provider routing, and higher-level orchestration.

The goal is not to recreate framework functionality simply for the sake of doing so, but to understand the underlying architectural problems and explore where a purpose-built personal AI orchestration layer differs from a general-purpose model abstraction.
