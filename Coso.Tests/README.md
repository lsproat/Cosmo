# Automated tests

Run the complete solution suite from the repository root:

```sh
dotnet test Cosmo.slnx
```

The suite uses the existing xUnit framework and assertions. HTTP integration tests use
`Microsoft.AspNetCore.Mvc.Testing` to run the actual API entry point in an in-memory
TestServer. No Ollama server, network listener, credentials, or local settings are
required. Test configuration replaces application configuration sources, model calls
use a stub, and runtime context uses a fixed clock and custom timezone. Machine logging
providers are disabled in the test host. Provider contract tests use a fake HTTP handler.

## Coverage

- **Domain:** first user message, independent conversation IDs, distinct message IDs,
  append order, roles/content, and preservation of creation timestamps after mutation.
- **Context:** configured system prompt first, local date/time across a UTC date boundary,
  ordered history including stored system messages, empty history, non-mutation, history
  snapshots, and refreshed runtime context.
- **Handlers:** stateless send, creation and persistence after a successful first response,
  continuation over multiple turns, context and cancellation forwarding, missing IDs,
  and storage behavior on model failure/cancellation.
- **Validation:** actual null/length rules and boundaries, validator aggregation,
  short-circuiting, no-validator behavior, and cancellation of async validators.
- **Repository:** missing IDs, independent conversations, live updates, duplicate rejection.
- **API:** all three POST routes, JSON response contracts, create/continue across requests,
  request binding, invalid IDs/bodies, validation problems and trace IDs, provider failures,
  timeouts, and validation error grouping/deduplication.
- **Ollama:** endpoint/method/content type, model and non-streaming payload, ordered role
  mapping and Unicode content, successful response preservation, malformed/empty responses,
  unsupported roles, HTTP/transport errors, and caller cancellation.
- **Configuration:** binding plus the application's registered startup validation for
  HTTP(S) URLs, required model, and required base prompt.

## Current behavior and limitations

`SendMessage` is stateless and accepts no conversation ID. Creation and continuation are
separate endpoints/handlers. Send/create permit empty and whitespace messages but reject
null and strings longer than 64,000 characters. Continuation has no application validator.
Missing conversations currently produce `ArgumentException` and HTTP 500. Failed
continuations retain the pending user message; failed creations are not persisted. These
tests document current behavior rather than implementing planned features.

The multi-validator regression test exposed duplicated failures caused by sharing a
FluentValidation context. The only production change gives each validator its own context.

Intentionally excluded: property-only DTO tests, exact generated timestamps (the domain
uses the real clock without an abstraction), arbitrary GUID values, unimplemented domain
guards, optional-ID send behavior, persistence/streaming features, real Ollama inference,
and deployment-specific behavior. In-memory TestServer does not enforce Kestrel's 512 KiB
request limit; that needs a separate server-level test. No tests claim concurrency guarantees
for the mutable message list. OpenAPI, TLS redirection, and the configured five-minute
HttpClient timeout are not exercised as deployment contracts.
