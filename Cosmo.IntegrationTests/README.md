# Ollama token count integration tests

These opt-in tests compare `Gemma4TokenCounter.CountTokens` with the actual
`prompt_eval_count` returned by `/api/chat`. They send fixed system/user messages,
conversation history, whitespace, and Unicode to a real server. Thinking is
explicitly enabled to match the current counter. Generation is limited to one
token because only the prompt count is needed.

Run from the repository root in PowerShell:

```powershell
$env:COSMO_OLLAMA_BASE_URL = 'http://xxx.xxx.x.xxx:xxxx' (replace with your server)
$env:COSMO_OLLAMA_MODEL = 'gemma4:26b'
dotnet test Cosmo.IntegrationTests/Cosmo.IntegrationTests.csproj --logger "console;verbosity=normal"
```

The model defaults to `gemma4:26b` and must already be installed on the server.
Only override it with a model that uses the same tokenizer and chat format.
Each request allows up to three minutes for model loading and prompt evaluation.
Failures report the scenario, estimated count, actual count, difference, and local
rendered prompt. Server or tokenizer changes may cause an exact-match assertion
to fail; investigate the difference rather than applying a fixed offset.

Without `COSMO_OLLAMA_BASE_URL`, these tests are skipped. Remove the setting to
restore normal local-only test runs:

```powershell
Remove-Item Env:COSMO_OLLAMA_BASE_URL
Remove-Item Env:COSMO_OLLAMA_MODEL
```

These tests exercise tokenizer agreement with real Ollama, independently of
`OllamaModelProvider` response-content validation. The existing provider tests
cover the application's HTTP request and response handling.
