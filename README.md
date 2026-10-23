# JBIDS

JBIDS (Job Based Instructional Design System) helps educators turn job announcement evidence into reviewed training objectives. See [SPEC.md](SPEC.md) for the product vision and iterative scope.

## Run locally

Requires the .NET 10 SDK and a working SQL Server instance. The default development connection uses SQL Server LocalDB. Configure `ConnectionStrings:DefaultConnection` through user secrets or environment variables to use another SQL Server instance.

From the repository root:

```powershell
dotnet restore JBIDS.slnx
dotnet ef database update --project JBIDS.Server
dotnet run --project JBIDS.Server --launch-profile https
```

Open the address printed by the server. Apply migrations before using the project screens. Migrations are explicit and are not applied automatically at startup.

## Iteration 1 workflow

1. Open Projects from the home page and create a curriculum project.
2. Open the project and add announcements, including their source URLs and pasted text.
3. Add measurable training objectives and select their Bloom's levels.
4. Add supporting announcement excerpts to each objective.
5. Review objectives using Approve, Reject, or Return to Draft.

Changing approved objective text, its Bloom's level, or its evidence returns it to Draft. Changing preserved announcement text returns all affected approved objectives to Draft. Both directions of the evidence relationship are available on the project page. Deletion is deferred to preserve evidence relationships.

This iteration is intended for a locally operated shared workspace. Project data is not partitioned by signed-in user. Existing account screens remain available, but sign-in does not restrict the curriculum workspace.

## Automated checks

```powershell
dotnet run --project JBIDS.Checks
```

The default checks create and remove an isolated temporary SQLite database. They exercise five synthetic announcements, ten approvals, evidence relationships, validation, project isolation, rejection, approval invalidation, and persistence across independently constructed contexts/services. They do not change the application's configured database.

To additionally exercise the SQL Server migrations against an isolated, automatically named LocalDB database:

```powershell
dotnet run --project JBIDS.Checks -- --sqlserver
```

SQL Server LocalDB must be available to the process. SQLite checks create the schema from the EF model; only the SQL Server mode applies the SQL Server migrations.

## Acceptance review

The automated checks use synthetic sources. Before considering Iteration 1 accepted, perform the completion criteria in `SPEC.md` with five real announcements and ten real objectives, restart the application, and confirm that saved content and review decisions remain available. Also verify validation messages, edit/cancel behavior, source links, and navigation between announcements and objectives.

## License

MIT. Copyright (c) 2026 Robert Garner.

## Site identity

JBIDS stands for **Job Based Instructional Design System**. The home page explains the current evidence-to-objective workflow; the workspace is at `/projects`. The responsive navy and teal design includes an original SVG logo (`wwwroot/images/jbids-logo.svg`), standalone mark (`wwwroot/images/jbids-mark.svg`), and SVG favicon (`wwwroot/favicon.svg`), with PNG, ICO, and Apple touch icon variants. The mark connects source lines to a learning path and review check.

## Tavily configuration

Open **Configuration** (`/configuration`) and sign in to save your own Tavily API key. You can save, replace, or remove it. The page reports whether a key is saved and never redisplays the stored key. Saving checks formatting only; it makes no Tavily requests and does not verify the key or spend credits. Use **Find job announcements** inside a project to search with this key.

Credentials belong to the authenticated account, even though curriculum projects currently use a shared workspace. They are protected with ASP.NET Core Data Protection using a per-account purpose and stored in the existing Identity user-token table. Credential storage needs no additional schema changes; discovery requires the migration described below. Existing Identity migrations must already be applied. Deleting an account deletes its stored credential.

Use HTTPS when entering credentials. Preserve the server's Data Protection key ring across restarts and deployments, alongside database backups; losing it makes saved credentials unreadable and users must replace them. The server identity needs permission to write its key ring. Server administrators who control the application and key ring can decrypt credentials. Do not enable sensitive-data logging or request-body logging for credential operations.

The integration checks use synthetic credentials to verify encryption at rest, account isolation, replacement, removal, validation, and rejection of anonymous access. They never contact Tavily.

## Find job announcements

Open **Projects**, open a project, and click **Find job announcements**. Enter a job description or query and click **Find and save announcements**. All unique results are saved directly to that project's Job announcements list. There is no selection, employer-entry, or per-item import step.

Full source text is retrieved automatically where available. If retrieval fails or the extraction allowance is exhausted, the saved item retains a clearly marked search preview and its original URL. Missing employers are recorded as **Not identified**, editable later. Repeating a query skips existing URLs.

Return to the project to view, edit, or prune saved announcements. Each has a **Delete announcement** button; check multiple announcements and use **Delete selected** to prune in bulk. Deletion removes the sources and their evidence links, keeps the objectives, and returns affected approved objectives to Draft. Filtering for relevance belongs to the later announcement-to-objective conversion workflow.

Each search returns up to 20 results. JBIDS reserves one credit for the basic query and up to four more credits for automatic extraction in batches of five. Existing URLs do not need extraction. Failed or uncertain requests retain their reservations; the persistent counter is conservative rather than Tavily's billing total. The monthly allowance defaults to 900 reserved credits per account (`Tavily:MonthlyCreditLimit`, 0–1,000). Keep paid usage disabled at Tavily; the JBIDS counter cannot account for other applications sharing a key.

The earlier `AddAnnouncementDiscoveryProvenance` migration must be applied if not already installed. This simplification adds no new database migration.

Automated checks use simulated provider responses and cover automatic saving, preview fallback, duplicate URLs, individual/bulk pruning, and approval invalidation without live API credits.

## Manage job search websites (site administrator)

Apply the `AddJobSearchWebsites` migration:

```powershell
dotnet ef database update --project JBIDS.Server
```

It creates `JobSearchWebsites` and seeds six enabled domains: **Indeed** (`indeed.com`), **LinkedIn Jobs** (`linkedin.com`), **Glassdoor** (`glassdoor.com`), **ZipRecruiter** (`ziprecruiter.com`), **Dice** (`dice.com`), and **USAJOBS** (`usajobs.gov`). The seed is part of the migration; restarting does not recreate deleted sites or overwrite administrator edits.

Designate the site administrator explicitly using the existing account's sign-in email. For local development:

```powershell
dotnet user-secrets set "SiteAdmin:Email" "your-sign-in-email@example.com" --project JBIDS.Server
```

For deployment, set the server environment variable `SiteAdmin__Email`. The default is blank, which grants no account administrator access. There is no first-registrant administrator assignment. The policy requires an authenticated server-issued email claim matching this installation-controlled value. Restart the application after setup.

Sign in as that account and select **Job websites**, or open `/admin/job-websites`. Add, edit, enable/disable, or delete domains. Management is protected both on the page and in the service. Ordinary users can continue searching the enabled sites.

Enter domains such as `indeed.com`, without page paths or query parameters. Standard HTTP/HTTPS home URLs and leading `www.` are normalized to a lowercase domain. Duplicate domains are rejected.

Every search sends the enabled domains to Tavily as restrictions. Results are also checked locally for an exact domain or subdomain match; an off-list site or lookalike domain is discarded before saving or extraction. With no enabled websites, search stops before contacting Tavily or reserving credits. Changes apply to future searches. Previously saved announcements remain available for normal pruning.

The **Add website** and **Edit** actions open a modal editor. Save closes the modal and refreshes the table; Cancel, the close button, or Escape dismiss it without saving. Validation errors appear inside the modal.

Announcements are listed in a table showing title, employer, collection date, content availability, and linked-objective count. Click a title or **Details** to open the full announcement in a modal, including its original URL, search provenance, and related objectives. Selection, editing, and individual/bulk deletion remain available on the project page.

## Extract candidate objectives with your ChatGPT account

Apply the new schema and generate the local-helper download before starting the web server:

```powershell
dotnet ef database update --project JBIDS.Server
powershell -File scripts/Package-LocalHelper.ps1
```

Open a project and click **Extract candidate objectives** under Training objectives.

1. Download and unzip the helper. Install the .NET 10 runtime and [Codex CLI](https://developers.openai.com/codex/cli). Run **Sign-in.cmd** and sign in with ChatGPT.
2. Click **Download extraction file** for up to five announcements awaiting extraction.
3. Drag that file onto **Extract.cmd**. It writes a `.results.json` beside the downloaded input.
4. Upload that result file on the extraction page, using the same JBIDS account and project. Review saved candidates on the project page.

ChatGPT credentials remain on the user's computer. Announcement text and existing objective text are sent to OpenAI through Codex. The helper requires ChatGPT authentication, forces ChatGPT login mode and removes API-key variables from the child process; it does not fall back to paid API calls. Subscription usage limits still apply.

Objectives can have nested parents at any depth, editable in the objective editor. The model can reuse existing objectives and attach evidence from additional announcements. Imports also deduplicate normalized exact wording. All newly generated candidates are Draft and require human approval. Each objective preserves an exact supporting excerpt; invented excerpts, invalid hierarchies and stale source results are rejected.

Successful sources are marked **Complete**, including zero-objective results. Repeated uploads do not duplicate saved results. Failed sources stay pending. Re-running the same local input resumes checkpointed results; upload a partial result to save successful sources. Changing announcement text or selecting **Allow reprocessing** makes it pending again and invalidates outstanding results, while preserving existing objectives and evidence. Reprocessing adds evidence/candidates; it does not erase previous work.

The helper uses file exchange so it works with an eventually hosted JBIDS without exposing a local service or uploading ChatGPT credentials. The shared JSON contract allows a future API provider. The drag-and-position mind-map editor is planned for a later iteration.

Deployment: build the helper archive before starting/publishing JBIDS.Server so `/downloads/JBIDS.LocalHelper.zip` is included. The generated ZIP is ignored by git and reproducible from source. See `JBIDS.LocalHelper/README.md` for terminal usage and executable-location troubleshooting. Automated checks exercise a fake Codex process and an isolated database; they consume no ChatGPT or Tavily usage.
