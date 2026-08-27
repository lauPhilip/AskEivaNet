# AskEIVA

AskEIVA is an internal support assistant for EIVA. It pulls documentation, Freshdesk tickets, Jira issues, and release notes into a Weaviate vector database, then lets staff query that data through a Blazor web interface powered by Mistral AI.

> **Status:** Active development. Not ready for production.

---

## What It Does

EIVA's technical support data is scattered across several tools:

* **Freshdesk:** Past customer support tickets
* **Jira:** Development issues and bug tracking
* **Web:** Documentation and product manuals
* **Release notes:** Product update logs

AskEIVA ingests these sources into Weaviate, extracts cross-links between them, and answers questions through a chat UI using grounded company data.

---

## Technology Stack

| Layer / Area | Details |
| :--- | :--- |
| **Languages** | C#, HTML, CSS, JavaScript |
| **Runtime** | .NET 10 |
| **Frameworks** | Blazor Server (Interactive Server Components) with MudBlazor UI, MediatR (CQRS), ASP.NET Core Identity |
| **Database** | Weaviate (Cloud-hosted vector database) |
| **Package Manager** | NuGet |
| **Build Tool** | MSBuild (`.slnx` solution format) |
| **Key Dependencies** | MediatR, MudBlazor, ASP.NET Core Identity, HtmlAgilityPack, Mistral AI API client (custom), Weaviate HTTP client (custom), Freshdesk/Jira REST integrations |
| **Test Framework** | None (Planned) |

---

## Project Structure

The project uses Clean Architecture with CQRS via MediatR:

```plaintext
src/
├── AskEiva.Domain/          # Core models, value objects, and service interfaces
├── AskEiva.Application/     # Commands, queries, and business logic
├── AskEiva.Infrastructure/  # Weaviate client, web scrapers, and external APIs
└── AskEiva.WebUI/           # Blazor Server UI for search, chat, and ingestion