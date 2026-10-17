# Verification (T057), 2026-10-17

- `dotnet build AgenticChangeOps.sln -c Release`: succeeded, no errors.
- `dotnet test AgenticChangeOps.sln -c Release`: 1263 passed, 3 skipped (the three opt-in live checks), 0 failed.
  Intake 118, Persistence 81, ModelAccess 216, Requirement 156 (+1 skipped), SampleCode 123, Workflow 184,
  Architect 155 (+2 skipped), Api 230.
- quickstart.md section 1 (tools off unchanged): passes.
- quickstart.md section 2 (confinement and limits, SampleCode tests): passes, including the 25 escaping spellings
  against the resolver and all three tools, and the directory-link escape.
- quickstart.md section 3 (design with a scripted model, filter SampleTools): passes.
- quickstart.md section 4 (live check): NOT RUN. It needs a real model endpoint and deployment and a signed-in
  identity; the opt-in test LiveSampleToolsCheckTests exists and is skipped without them.
