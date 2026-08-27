# 📝 AskEIVA Project Roadmap & TODO List

This document tracks upcoming feature implementations, system enhancements, technical debt remediations, and architectural refinements for the AskEIVA platform.

---

## 🟩 Completed Milestones
* **Architectural Migration:** Replaced the legacy Python/Streamlit prototype with a decoupled C# / .NET 10 Clean Architecture layout across Domain, Application, Infrastructure, WebUI, and Worker projects.
* **Asynchronous Web Ingestion:** Built an HTML parsing stream using `HtmlAgilityPack` to extract documentation text from public directories.
* **Basic CQRS Orchestration:** Implemented core MediatR commands and queries for ingesting data sources and executing hybrid search queries.
* **Idempotency Protections:** Safeguarded the administrative UI by dynamically disabling synchronization buttons when Weaviate collections are populated.
* **Asset Synchronization:** Fixed cache-busting behavior for UI static assets to ensure reliable hot-reload delivery.

---

## 🔴 Critical Priority: Security & Code Quality Remediations
- [ ] **Remove TLS/SSL Bypass:** Delete the `RemoteCertificateValidationCallback => true` workaround in `Program.cs` and configure valid development/production certificates for endpoints reaching Weaviate.
- [ ] **Data Sanitization & Injection Defense:** Add input sanitization layers for ingested Freshdesk tickets, Jira ADF payloads, and scraped documentation before passing content into LLM prompts.
- [ ] **Remove Inline Fallback Secrets:** Eliminate hard-coded fallback strings (e.g., `'YOUR_KEY'`) across configuration classes; enforce fail-fast startup behavior if required secrets are absent.
- [ ] **Sensitive Data Scrubbing:** Audit the repository root and build artifacts to ensure no unredacted customer support tickets or real internal data files are tracked in version control.

---

## 🟨 High Priority: Core App Improvements & Testing
- [ ] **Automated Test Project Setup:** Add an `xUnit` test project (`AskEiva.Tests`) to the `.slnx` solution for unit and integration testing.
- [ ] **TextSplitter Unit Tests:** Validate sliding-window character/word splitting against edge cases, dense parameter logs, and specialized marine survey formats.
- [ ] **Worker Ingestion Loop:** Refactor `AskEiva.Worker` from hard-coded page limits (1–50) into an incremental, scheduled background pipeline for continuous Freshdesk, Jira, and documentation updates.
- [ ] **Fix Jira Ingestion:** Debug and stabilize `IJiraService` and `AtlassianDocumentParser.cs` to handle nested ADF tables and complex ticket structures.
- [ ] **Weaviate Retry Policies:** Implement transient fault handling and backoff using `Polly` inside Weaviate HTTP clients and batch endpoints.
- [ ] **Startup Environment Validation:** Ensure `Program.cs` and `WeaviateSchemaProvisioner.cs` fail with clear diagnostic logs if API keys or database connections are invalid.

---

## 🟦 Medium Priority: Feature Enhancements
- [ ] **Hybrid Search Tuning:** Add a runtime UI control in the WebUI to adjust the vector-to-keyword balance (`alpha` parameter) dynamically in `SearchKnowledgeQuery.cs`.
- [ ] **Context Graph RAG Optimization:** Refactor `BuildGlobalContextGraphCommand` to improve relationship extraction and pass balanced semantic context weights to Mistral.
- [ ] **Evaluation Trace Metrics:** Expand `EvaluationDashboard.razor` to visually chart historical validation benchmarks and user feedback (`SubmitSwipeFeedbackCommand`) over time.
- [ ] **Custom Workflow Builder:** Design a workflow portal enabling internal staff to build custom multi-step AI tasks.
- [ ] **Domain-Specific Fine-Tuning:** Evaluate fine-tuning or domain-adapting a specialized open model using curated EIVA interaction logs.

---

## 🟧 Low Priority: DevOps, Documentation & Distribution
- [ ] **CI/CD Build Pipeline:** Add GitHub Actions workflows to automate solution builds, code linting, and automated test runs on pull requests.
- [ ] **Setup & Onboarding Documentation:** Update `README.md` with explicit local setup instructions, required `.slnx` SDK versions, background worker execution steps, and environment variable requirements.
- [ ] **Project Governance:** Add standard repository files including `LICENSE`, `CONTRIBUTING.md`, and `CHANGELOG.md`.
- [ ] **Structured Logging:** Integrate `Serilog` into the Infrastructure and Worker layers to capture diagnostic pipeline events cleanly.