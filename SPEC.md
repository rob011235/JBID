# JBIDS — Living Specification

## Product vision

JBIDS (Job Based Instructional Design System) helps educators build curricula grounded in employer needs. It connects evidence from job announcements and, eventually, subject matter experts to training objectives, courses, and degree plans.

Educators remain responsible for reviewing and approving objectives. JBIDS preserves traceability so users can explain why an objective belongs in a curriculum and identify the evidence supporting it.

The application is an open source, MIT-licensed project that other academics can clone and use.

## How we use this specification

This is a living specification. Keep the product vision broad and add implementation detail one iteration at a time as we learn from using the software.

- Define the next iteration's workflow, scope, and completion criteria before implementing it.
- Keep future capabilities as a flexible roadmap rather than detailed commitments.
- Update the active iteration when requirements change, recording significant scope decisions briefly.
- Record what was delivered when an iteration is complete. Describe subsequent changes in a later iteration rather than silently rewriting completed work.
- Treat completion criteria as the basis for deciding whether an iteration is finished.

## Guiding requirements

- Maintain traceability from training objectives to their supporting evidence.
- Require human review and approval of objectives.
- Use the revised Bloom's taxonomy to classify objectives and guide measurable wording.
- Begin with manual entry and avoid required paid API usage. Add LLM assistance incrementally, prioritizing models running on the user's machine. A paid API may become an optional integration later.
- Preserve useful work between sessions.

## Iteration 1 — Job announcements to reviewed objectives

**Status:** Implemented; real-data acceptance review pending.

### Goal

Enable an educator to create a curriculum project, collect job announcement evidence, and develop approved training objectives linked to that evidence.

### User workflow and scope

1. **Create a curriculum project.** Enter a name and description, such as Embedded Systems Developer. List and reopen existing projects.
2. **Add job announcements to the project.** Manually enter a source URL, job title, employer, date collected, and pasted announcement text. View and edit saved announcements. Retain the pasted text so evidence remains available if the original page changes or disappears.
3. **Create training objectives.** Manually enter and edit objective text and select a revised Bloom's taxonomy level: Remember, Understand, Apply, Analyze, Evaluate, or Create. Objectives start in Draft status.
4. **Link objectives to evidence.** Link an objective to one or more announcements in the same project and save a supporting excerpt for each link. One announcement may support multiple objectives, and one objective may have multiple supporting links.
5. **Review objectives.** Mark an objective Approved or Rejected, or return it to Draft. Approval requires objective text, a Bloom's level, and at least one evidence link with a supporting excerpt. Changing an approved objective's text, Bloom's level, or supporting evidence returns it to Draft for review. Changing announcement text used as evidence also returns affected approved objectives to Draft.
6. **Resume work.** Persist projects, announcements, objectives, review status, and evidence links in the database. Retrieve and continue editing them after restarting the application.

### Included behavior

- Keep announcements and objectives organized by curriculum project.
- Show objectives and their review status within a project.
- Navigate from an objective to its supporting excerpts and announcements, including the original URLs.
- Navigate from an announcement to the objectives it supports.
- Validate required fields and show understandable validation messages.
- Preserve evidence relationships when records are edited. Source pruning was added in Iteration 2.

### Outside this iteration

- Automated job searches, web scraping, and automatic URL content retrieval.
- Direct LLM integration or automatic objective extraction.
- Course mapping, semester scheduling, and degree plan construction.
- Subject matter expert forms.
- Multi-user collaboration, sharing, and advanced approval roles.
- Full revision history, import/export, and reporting.

Users may use an external assistant to draft objective text and manually enter the results. This iteration does not automate that exchange.

### Completion criteria

Demonstrate the following using real job announcements:

1. Create one curriculum project and enter five job announcements with the required source details and pasted text.
2. Create at least ten training objectives, each with a Bloom's level and supporting evidence.
3. Demonstrate an objective supported by multiple announcements and an announcement supporting multiple objectives.
4. Approve ten objectives and navigate from each to its supporting excerpts, announcements, and original URLs.
5. Verify that incomplete objectives cannot be approved and that changing approved content or evidence returns affected objectives to Draft.
6. Demonstrate rejection and returning an objective to Draft.
7. Restart the application and verify that saved content, statuses, and relationships remain available and editable.

## Future direction — Flexible roadmap

The following capabilities describe the intended direction. Their order and scope may change as we learn; they are not committed iteration specifications.

- **LLM-assisted extraction:** Generate draft objectives from stored announcement text and feed them into the existing evidence and human review workflow. Preserve source references during extraction.
- **Objective refinement:** Split, combine, and improve objectives while maintaining traceability.
- **Course mapping:** Relate approved objectives to courses and inspect curriculum coverage.
- **Degree planning:** Organize courses into semesters and term-by-term degree plans, using labels such as Year 1 Fall.
- **Subject matter expert input:** Collect skills evidence through forms, assist with its analysis, and provide SME review.
- **Source discovery:** Help users find relevant announcements and collect their links.
- **Optional integrations:** Consider additional LLM providers, paid API access, and exchange formats as needs become clear.

## Iteration record

| Iteration | Status | Outcome |
| --- | --- | --- |
| 1 — Job announcements to reviewed objectives | Implemented; acceptance pending | Manual project, announcement, objective, evidence, and review workflows delivered. Automated relational persistence checks pass; SQL Server and real-data UI acceptance remain pending. |

## Significant decisions

- **2026-10-22:** Adopt a broad product vision with detailed specifications added incrementally. Avoid specifying the entire build in advance.
- **2026-10-22:** Define Iteration 1 around manual evidence collection, objective authoring, human approval, and persistence. Defer direct LLM integration until the review workflow is usable.

## Iteration 1 implementation and validation notes

- Implemented with interactive Blazor Server components and EF Core using the existing SQL Server configuration. Each persistence operation uses a separate database context.
- Added the `AddCurriculumWorkflow` migration and runnable `JBIDS.Checks` integration checks. See `README.md` for setup and check commands.
- Automated checks pass against an isolated SQLite database: five synthetic announcements, ten approved objectives, many-to-many evidence, fresh-context persistence, validation, project isolation, rejection, and approval invalidation.
- The solution builds with zero warnings and zero errors.
- SQL Server LocalDB could not start in the implementation environment. The SQL migration script was generated; applying it and testing against SQL Server remain pending.
- Browser verification was blocked by a connection timeout. Local startup also reported restricted access to the default data-protection key directory. Acceptance with five real announcements and ten real objectives, including restarting the application, remains pending.
- This iteration provides a shared workspace for a locally operated installation. User-specific ownership, collaboration, and access rules remain outside its scope.

## Site identity and presentation

- **Name:** JBIDS — Job Based Instructional Design System.
- **Style:** Modern navy and teal, responsive layouts, accessible navigation, and visible keyboard focus.
- **Home page:** Describe the purpose and current workflow, distinguish future capabilities, and link to the Projects workspace at `/projects`.
- **Identity:** Original SVG logo and matching favicon connecting job evidence, learning objectives, and human review.

## Iteration 2 — Job discovery (incremental scope)

**Status:** Implemented; automated checks pass; live acceptance pending.

The intended workflow is to describe an occupation, search for related announcements using Tavily, save all discovered sources directly, and prune later into a curriculum project. Jev relevance evaluation and objective generation are deferred.

### Account configuration increment

- Provide a signed-in Configuration page for each user's own Tavily API key.
- Mask entry; show configured/unconfigured status without displaying a stored key.
- Allow saving, replacing, and removing the credential. Never log its plaintext value.
- Protect the key server-side using ASP.NET Core Data Protection with account-specific purposes, and persist it in the existing Identity user-token table.
- Reject anonymous credential operations and isolate credentials by authenticated account. Account deletion removes its credential.
- Saving validates formatting only. It makes no provider calls, consumes no Tavily credits, and does not claim to verify the key or enforce free-tier limits.
- Verify credential protection, isolation, replacement, removal, validation, and anonymous rejection with synthetic-key integration checks. Live account-page acceptance and restart verification remain pending where the environment cannot run SQL Server or persist the default encryption key ring.

### Automatic collection and later pruning

- Enter a job description or search query and run **Find and save announcements**. Save every unique result to the project automatically, without search-term review, result selection, employer entry, or per-item import.
- Request up to 20 results with one basic Tavily search. Retrieve full plain text automatically for new sources in batches of five.
- Persist results before extraction so failed or interrupted retrieval does not lose discovered items. If full text is unavailable, retain a clearly marked search preview with the source URL. Do not represent preview text as the full announcement.
- Record unknown employers as **Not identified** rather than requiring entry or guessing an employer. Allow normal editing later.
- Preserve source URLs, collection dates, and query provenance. Skip existing URLs in the project on repeated searches.
- Provide individual and bulk deletion on the project's announcement list. Delete related evidence links atomically, retain objectives, and return affected approved objectives to Draft. Reject cross-project deletion requests.
- Filtering for relevance belongs to the later announcement-to-objective conversion. Collection does not require manual filtering or LLM classification.
- Retain encrypted per-account keys, bounded requests, sanitized errors, and persistent credit reservations. A run reserves up to five credits: one search and up to four extraction batches. The monthly allowance defaults to 900 credits per account, configurable through `Tavily:MonthlyCreditLimit` (0–1,000). Keep paid usage disabled at Tavily.

### Completion criteria

1. One query action saves results directly to the project's announcement list.
2. Full text replaces saved previews when available; failed extraction retains marked previews.
3. Repeating a query skips existing source URLs.
4. Individual and bulk pruning remove announcements and associated evidence without deleting objectives or leaving stale approvals.
5. Existing key isolation, provider-failure handling, credit limits, and traceability continue to work.

### Scope decision

**2026-10-22:** Replace the review-and-import workflow with immediate collection and later pruning at the user's direction. Use objective conversion for relevance filtering.

The existing `AddAnnouncementDiscoveryProvenance` migration remains necessary if not already applied. This change requires no additional migration.

Validation: build passes with zero warnings/errors; simulated-provider and relational checks pass for automatic saving, full-text enrichment, preview fallback, duplicates, and individual/bulk source deletion with approval invalidation. Live provider/browser acceptance remains pending.

## Managed job search websites

**2026-10-22 scope addition:** Restrict discovery to administrator-managed job websites while preserving the single-action search-and-save workflow.

- Persist website name, normalized unique domain, and enabled status in a `JobSearchWebsites` table.
- Seed Indeed, LinkedIn Jobs, Glassdoor, ZipRecruiter, Dice, and USAJOBS as enabled via the `AddJobSearchWebsites` migration.
- Provide `/admin/job-websites` for adding, editing, enabling/disabling, and deleting entries. Enforce the SiteAdmin policy in both the page and management service.
- Designate the administrator explicitly through server configuration `SiteAdmin:Email` matching the authenticated account email. A blank value grants no administrative access. No account becomes administrator merely by registering.
- Pass enabled domains as restrictive Tavily parameters and reject off-list or lookalike-domain results locally. Allow legitimate subdomains. If no sites are enabled, stop without making provider calls or spending credits.
- Apply website settings to future discovery; do not alter previously collected announcements. Administrative edits and deletions persist across restarts.
- Validate six seed rows, administrator authorization, normalization and duplicate rejection, CRUD persistence, enabled-domain request parameters, local response filtering, and the empty-list credit guard using isolated relational and simulated-provider checks.

Managed website validation: build and automated checks pass with zero warnings/errors. EF reports no pending model changes. Applying the migration was attempted, but LocalDB could not start in the implementation environment; apply it from the normal development session. Live browser/provider acceptance remains pending.

**2026-10-22 presentation update:** The job website add/edit form opens in a modal dialog. Save refreshes the website table; Cancel, Close, or Escape dismisses unsaved changes. Validation stays inside the editor.

**2026-10-22 presentation update:** Show announcements in a table with clickable titles and Details buttons opening a read-only modal for text, source metadata, search provenance, and linked objectives. Keep pruning and editing accessible from the table; source references on objectives also open the detail modal.

## Iteration 3 - Candidate objective extraction

**Scope:** Generate evidence-backed candidate objectives from saved announcements using the user's local Codex CLI signed in with ChatGPT. A future API option can use the same versioned request/response contract. The mind-map editor is a later iteration.

- The hosted or local website exports an account-owned immutable JSON request with up to five pending announcements and existing project objectives. Downloading alone does not mark work complete.
- A downloadable .NET 10 helper uses the local ChatGPT sign-in, requires ChatGPT authentication, clears API-key environment variables, forces ChatGPT login mode and offers no paid API fallback. Credentials never go to JBIDS. Announcement and objective text goes to OpenAI through Codex; subscription limits apply.
- Process one source per Codex request with schema-constrained output, read-only sandboxing, a ten-minute timeout and no automatic provider retries. Checkpoint each successful result, including empty output. Rerunning a package resumes saved results; failed sources remain retryable.
- Upload results to the same project and JBIDS account. Validate the immutable source revision/text, referenced existing objectives, measurable candidate content, revised Bloom levels, exact supporting excerpts, unique keys and acyclic parent relationships. Save candidates, evidence and completion atomically. Reject stale/cross-project/cross-account results.
- Support any number of nested objective levels through a nullable parent reference. Allow human parent editing and reject self-parenting, cycles or a parent from another project. Visual node positions will be separate from parent relationships in the future mind map.
- Reuse semantically equivalent existing objectives through their identifiers; send prior proposals to the helper to encourage consistent wording within a batch. Deduplicate normalized exact text during import. Semantic equivalence relies on the model and remains subject to human review.
- Every new candidate starts Draft. New evidence returns an approved reused objective to Draft. Never auto-approve or overwrite reviewed objective text/hierarchy during extraction.
- Persist completion even when no objectives are found. Repeated uploads are idempotent. Announcement text changes or explicit Allow reprocessing invalidate old results and make the source pending again, preserving prior objectives/evidence.
- Projects remain a shared workspace as in iteration 1; extraction request ownership protects the handoff, not project-level collaboration permissions.

**Setup:** Apply `AddCandidateObjectiveExtraction` and package the local helper with `scripts/Package-LocalHelper.ps1` before starting/publishing the site. Use the project's **Extract candidate objectives** link. Live ChatGPT extraction and browser acceptance require the user's normal session; automated verification uses simulated Codex and an isolated relational database.

Validation: solution builds with zero warnings/errors. Isolated relational checks and a real helper process driven by a fake Codex executable pass, covering account isolation, nested hierarchy, shared evidence, empty completion, idempotency, stale results, transaction rollback, ChatGPT-only authentication, partial failure and checkpoint resume. The packaged helper download returns HTTP 200. No live ChatGPT calls were made. Apply the SQL Server migration in the normal development session; LocalDB is unavailable in the implementation environment.
