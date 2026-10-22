# JBID — Living Specification

## Product vision

JBID helps educators build curricula grounded in employer needs. It connects evidence from job announcements and, eventually, subject matter experts to training objectives, courses, and degree plans.

Educators remain responsible for reviewing and approving objectives. JBID preserves traceability so users can explain why an objective belongs in a curriculum and identify the evidence supporting it.

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

**Status:** Defined; not yet implemented.

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
- Preserve evidence relationships when records are edited. Deletion workflows are deferred for this iteration.

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
| 1 — Job announcements to reviewed objectives | Defined | Implementation and validation pending. |

## Significant decisions

- **2026-10-22:** Adopt a broad product vision with detailed specifications added incrementally. Avoid specifying the entire build in advance.
- **2026-10-22:** Define Iteration 1 around manual evidence collection, objective authoring, human approval, and persistence. Defer direct LLM integration until the review workflow is usable.
