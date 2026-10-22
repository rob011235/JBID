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
