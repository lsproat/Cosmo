# Integration tests

The default run executes EF model and repository tests without external services:

```powershell
dotnet test Cosmo.IntegrationTests/Cosmo.IntegrationTests.csproj
```

These validate mappings, graph tracking, explicit saves, reloads, cancelled saves,
discarded changes, and sequence-number sorting. Repository tests use EF's
InMemory provider. They do not verify PostgreSQL SQL, constraints, or transactions.

## Live PostgreSQL tests

Use a dedicated test database and a role permitted to create schemas:

```powershell
$env:COSMO_POSTGRES_TEST_CONNECTION_STRING = 'Host=localhost;Database=cosmo_test;Username=cosmo_test;Password=replace_me'
dotnet test Cosmo.IntegrationTests/Cosmo.IntegrationTests.csproj --filter Category=Postgres
Remove-Item Env:COSMO_POSTGRES_TEST_CONNECTION_STRING
```

Each test creates tables from the EF model in its own randomly named
`cosmo_test_<guid>` schema and removes that schema in cleanup. The database itself
must already exist. Tests do not read application connection settings or modify
the application's tables. An interrupted test process may leave its temporary
schema behind.

Coverage includes read-only property materialization, ordered message loading,
saving a continuation with successive sequence numbers, discarding unsaved changes, database cascading deletion,
foreign-key enforcement, and atomic rollback of a failed save. Fixed distinct
timestamps make ordering assertions independent of clock precision.

Without `COSMO_POSTGRES_TEST_CONNECTION_STRING`, both PostgreSQL tests are skipped.

## Live Ollama token counts

These tests compare `Gemma4TokenCounter.CountTokens` with the actual
`prompt_eval_count` returned by `/api/chat`. Cases include fixed messages,
conversation history, whitespace, and Unicode. Thinking is explicitly enabled to
match the current counter; generation is limited to one token.

```powershell
$env:COSMO_OLLAMA_BASE_URL = 'http://localhost:11434'
$env:COSMO_OLLAMA_MODEL = 'gemma4:26b'
dotnet test Cosmo.IntegrationTests/Cosmo.IntegrationTests.csproj --filter FullyQualifiedName~OllamaTokenCountIntegrationTests --logger "console;verbosity=normal"
Remove-Item Env:COSMO_OLLAMA_BASE_URL
Remove-Item Env:COSMO_OLLAMA_MODEL
```

The model defaults to `gemma4:26b` and must already be installed on the server.
Only override it with a model using the same tokenizer and chat format.
Each request allows three minutes for loading/evaluation. Failures report the
model, scenario, counts, delta, and fixed test prompt. Investigate differences
rather than applying a constant offset.

Without `COSMO_OLLAMA_BASE_URL`, live inference is skipped. These tests exercise
tokenizer agreement independently of provider answer-content validation.
