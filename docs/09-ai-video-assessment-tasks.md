# AI assessment for project video recommendations

This is a follow-up to the existing project recommendation flow. It replaces the final keyword-only assessment with an Azure Foundry-backed assessment while preserving the current project, catalog, and recommendation pages.

The app currently runs on .NET 10, uses Razor Pages and EF Core, and has focused xUnit tests in `ReformApp.Tests/`. The current matcher is `VideoRecommendationService`. The reference sample is [04-dotnet-agent-framework.cs](E:/Backup/zeus/ai/ai-agents-for-beginners/04-tool-use/code_samples/04-dotnet-agent-framework.cs).

## Implementation approach

- Adapt the sample's Azure OpenAI client, `AsAIAgent` setup, and agent instructions for video relevance assessment. Do not copy its travel functions, console session, or streaming UI.
- Use the existing matcher to prepare a bounded candidate list, then ask the agent to assess and rank those candidates. This first version improves assessment and explanations, but cannot find videos excluded by the existing candidate search.
- Ask for structured results containing candidate video IDs, a relevance score from 0 to 100, and a concise Spanish explanation supported by the supplied catalog data. Validate the response before using it.
- Keep a configured minimum relevance score and the existing maximum of three recommendations. Set the threshold using the test cases in Task 1 before enabling AI recommendations.
- Keep the current matcher as a logged fallback if the Foundry request fails or returns invalid output. Never hide cancellation or silently treat a failed request as a successful AI result.
- Do not send project owner identifiers or unrelated user/account data to Foundry. Do not commit endpoints containing secrets, API keys, or credentials.

## Tasks and test checklist

Complete tasks in order. Each task has a bounded change and a completion check. Keep automated tests independent of Azure credentials and live model calls; reserve those for the final configured smoke test.

### 1. Define and test the assessment contract

**Change**

- Add typed request and result models for project description/category, candidate video data, and each assessed match (`VideoId`, score, explanation).
- Add a small validation step that rejects malformed scores, duplicate IDs, IDs outside the supplied candidate list, empty explanations, and invalid/empty model output.
- Prepare a small, deterministic set of project/video examples covering strong matches, unrelated videos, and ambiguous descriptions. Use it to choose and document the minimum score before enabling AI results.
- Use a minimum relevance score of **70/100** for the project “Quiero renovar los muebles de mi cocina y mejorar el almacenamiento” (category: Cocinas): the kitchen-cabinet example scores 91 (strong match), the related-but-not-equivalent kitchen-lighting example scores 58 (ambiguous/weak), and the unrelated patio-floor example scores 12 (no match). These are deterministic contract fixtures, not live model outputs.

**Tests**

- [x] Valid structured output is accepted and preserves score and explanation.
- [x] Invalid JSON, missing fields, out-of-range scores, duplicate/unknown IDs, and empty explanations are rejected.
- [x] The curated examples demonstrate expected strong, weak, and no-match outcomes; the chosen minimum-score boundary has tests immediately below and above it.

**Done when:** the contract and response validation can be tested without EF Core or Azure.

### 2. Add Foundry configuration and agent adapter

**Change**

- Add typed options for the Azure endpoint, deployment, and assessment settings; validate required values when the AI assessment is enabled.
- Register an assessment service through dependency injection in `Program.cs`.
- Adapt the sample's `AzureOpenAIClient` and `AsAIAgent` construction to the web app's service lifetime. Use the sample's Azure CLI authentication for local development and a managed identity/default Azure credential chain for deployed environments; keep credentials outside committed configuration.
- Add focused agent instructions: assess only supplied candidates, use only supplied evidence, return the required structure, and write explanations in Spanish. Bound transcript/context size.

**Tests**

- [x] Missing or malformed required configuration produces a clear startup/activation error when AI assessment is enabled.
- [x] With AI assessment disabled, the application can still start and use the existing matcher.
- [x] Agent construction and request formatting are verifiable without making a live Azure request.

**Done when:** application wiring is isolated from recommendation orchestration and has no secret values in source control.

### 3. Prepare a bounded candidate list

**Change**

- Separate candidate selection from final recommendation assessment so the current matcher can supply more than its final three results to the AI step.
- Set and document a bounded candidate limit of 12. Include only fields needed for assessment (video ID, title, catalog metadata, transcript excerpt, and detected materials/equipment).
- Preserve stable ordering for ties and avoid sending the complete catalog or oversized transcripts.

**Tests**

- [x] Candidate count never exceeds the configured limit.
- [x] Relevant candidates are retained in the expected order for the existing recommendation fixtures.
- [x] Empty project intent and an empty catalog return an empty candidate list without an Azure call.
- [x] Transcript excerpts respect the configured size limit.

**Done when:** candidate preparation is deterministic and has no model dependency.

### 4. Implement assessment, ranking, and fallback

**Change**

- Have the assessment service pass the project and bounded candidates to the agent, validate its response using Task 1, apply the minimum score, rank the results, and return at most three.
- On a handled Foundry/response-validation failure, log the failure and return recommendations from the existing matcher. Propagate cancellation and do not mask unrelated programming errors.
- Keep matching explanations grounded in the supplied title, metadata, or transcript; treat returned text as untrusted display content.

**Tests**

- [ ] A fake assessor returns validated results in descending score order, capped at three.
- [ ] Below-threshold results are omitted and a valid no-match response returns no recommendations.
- [ ] A simulated provider failure or invalid response logs the fallback and returns the existing matcher results.
- [ ] Cancellation is propagated and does not invoke fallback.
- [ ] Tests use a fake assessor and make no network calls.

**Done when:** the application can switch between AI assessment and the existing matcher without coupling either implementation to the Razor Page.

### 5. Persist and display the assessed recommendations

**Change**

- Keep saving the project before recommendation assessment, as the current creation flow does.
- Save the selected video IDs, order, and score as today. Add a migration and persisted explanation if needed so the AI explanation shown after creation is still available on the project detail page after reload.
- Update the create and detail pages to use the assessed explanation and retain the existing empty state, YouTube link behavior, and transcript-derived materials/equipment notices.
- Preserve project ownership checks and existing authorization behavior.

**Tests**

- [ ] A valid project is saved even when there are no recommendations.
- [ ] The saved recommendation IDs and order match the validated assessment.
- [ ] The AI explanation remains visible after loading the saved project in a new request.
- [ ] A user cannot view or alter another user's project through this flow.
- [ ] Existing tests for materials/equipment and empty recommendations continue to pass.

**Done when:** the generated explanation and ranking survive persistence and the existing project experience remains intact.

### 6. Verify configured Azure Foundry behavior

**Change**

- Document the non-secret configuration names and the required Foundry endpoint, deployment, and identity permissions for local and deployed environments.
- Add a manual smoke-test procedure that can be run only when a developer has configured Azure access. Do not run live model calls in the regular unit-test suite.

**Tests**

- [ ] With valid local Azure authentication and a configured deployment, creating a project produces a structured assessment or an explicit, logged fallback.
- [ ] With AI disabled or unavailable, project creation still completes using the existing matcher.
- [ ] `dotnet test .\ReformApp.Tests\ReformApp.Tests.csproj` passes without Azure credentials.
- [ ] `dotnet build .\reformapp\ReformApp.csproj` succeeds.

**Done when:** the normal automated checks are repeatable offline, and the live Foundry path has a documented manual check.
