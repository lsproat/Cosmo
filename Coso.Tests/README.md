# Automated tests

Run the fast unit, provider-contract, and in-process API tests:

```powershell
dotnet test Coso.Tests/Cosmo.Tests.csproj
```

Run all local tests, including EF model and repository tests:

```powershell
dotnet test Cosmo.slnx
```

Neither command needs application credentials or a running database/model server.
Live PostgreSQL and Ollama tests are skipped unless their opt-in environment
variables are set. See [integration test instructions](../Cosmo.IntegrationTests/README.md).

## Coverage

- Domain: empty and initial-message conversations, identities, roles, append order,
  and preserved timestamps.
- Context selection: fixed clock/timezone, refreshed system instructions, pending
  input, immutable stored history, exact input limit, one-token overflow,
  oversized current input/system prompt, removal of multiple old turns, consecutive
  user messages, and cancellation.
- Budget diagnostics: model, token counts before/after selection, removed turns
  and messages, configured context/output/input budgets, and rejection diagnostics.
  Message contents are not logged.
- Handlers: successful exchange saved only after generation, cancellation forwarding,
  provider/context failures without appending or saving, retry without duplicate
  input, missing conversations, and propagation of database save failures.
- API: request/response contracts, create/continue across requests, failed request
  retries, trimmed prompts with full persisted history, input-budget boundaries,
  validation problems, malformed bodies, trace IDs, and provider status mapping.
- Ollama contract: request shape, role mapping, Unicode, prompt token count mapping,
  malformed/empty responses, HTTP errors, and cancellation, using a fake transport.
- Tokenizer: actual bundled tokenizer loading, special-token overhead, Gemma role
  and thinking formatting, whitespace, Unicode, and unsupported roles.
- Configuration/DI: startup validation for URLs, model, prompts, connection strings,
  positive token budgets and output reservation smaller than the context window,
  plus scoped EF repository/DbContext registration.

API tests replace model calls, token counting, and the database repository.
Deterministic budget tests use a synthetic counter with known costs so boundary
assertions are independent of tokenizer versions. Separate tests exercise the
real tokenizer and optional live-server agreement.

## Current behavior and limits

Send/create accept empty and whitespace messages, reject null, and limit input to
64,000 characters. Continuation has no application length validator. Missing
conversations and oversized token-budget requests currently produce
`ArgumentException` and HTTP 500; the API tests explicitly document this behavior.

Failed model/context requests leave history unchanged. Save failures propagate;
a database-backed request must discard its scoped DbContext after failure.
The in-memory repository is a shared-object test double and does not promise
transactional rollback after a save failure.

The tests do not claim concurrent-request serialization, idempotency, streaming,
or concurrent sequence-number allocation. History is ordered by SequenceNumber,
and a unique index prevents duplicate numbers within a conversation. Kestrel's 512 KiB
request limit, TLS, and deployment-specific timeouts need server-level tests.
