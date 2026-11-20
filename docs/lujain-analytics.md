# Lujain analytics module

This delivery implements Lujain's Analytics assignment in Task_Split_v2: the administrator dashboard, supplier relationships and awards for the Top Committee, approvals and decisions, administrator audit reporting, filtered Excel/PDF exports, and an explained supplier risk card.

## Open in Visual Studio

1. Open `Sourcing-Tender-Management-System/Sourcing-Tender-Management-System.slnx` in Visual Studio with the .NET 10 SDK installed.
2. Set the web project as the startup project. Keep the team's existing SQL Server connection and database configuration.
3. Run with F5 or Ctrl+F5 using the existing HTTPS profile.
4. Sign in as an **Admin** and select **Analytics** in the sidebar, or the new link on the existing Admin dashboard. The URL is `/Analytics`.
5. Sign in as **TopCommittee** to open the supplier, approvals and committee decision reports. This role cannot open the administrator overview or audit trail, including by entering a download URL manually.

No new database tables, migrations, NuGet packages, passwords or seed accounts are required by this feature. The analytics font is embedded in the application, with its redistribution licence included, so Arabic supplier names in PDF do not depend on fonts installed on the hosting server.

## What is included

| Page | URL | Access |
| --- | --- | --- |
| Admin KPIs and clickable stage/month charts | `/Analytics?kind=Overview` | Admin |
| Supplier relationships, dealings and awards | `/Analytics?kind=Suppliers` | Admin, TopCommittee |
| Individual supplier risk and underlying evidence | `/Analytics?kind=Suppliers&SupplierId=123` | Admin, TopCommittee |
| Approval activity and status drill-through | `/Analytics?kind=Approvals` | Admin, TopCommittee |
| Committee report rounds and vote counts | `/Analytics?kind=Decisions` | Admin, TopCommittee |
| Searchable audit metadata | `/Analytics?kind=Audit` | Admin |

Use the date range, search and report-specific status selector. Clicking chart bars opens the relevant filtered records. **Show share** switches the visible chart labels between counts and percentages. For the supplier chart, shares describe the displayed top ten suppliers. The page works without JavaScript; the optional chart toggle and immediate date validation use the module's own small script.

PDF and Excel submit the current filter form and include **all filtered records**, regardless of the screen's 25-row table pages. Both formats are built from the same report document. Successful downloads create an `AnalyticsExported` audit event. Excel preserves numeric amounts and date cells and does not execute supplier-controlled names as formulas. Audit exports omit raw before/after values and IP addresses.

## Definitions that matter

- All report date boundaries are UTC calendar days; the selected end date is inclusive. Invalid dates, reversed ranges, invalid statuses, unknown report kinds and invalid pages are rejected on the server.
- The overview tender cohort uses **tender creation date**, with current statuses. Supplier registrations use registration date. Participating suppliers and approval-decision KPIs have their own submission and decision dates and are explicitly independent of the tender search/status filter.
- A supplier **dealing** is a distinct supplier–tender relationship with a non-draft bid or a current award. Withdrawn submissions remain participation; drafts do not. Invitations are shown separately and are not counted as completed dealings.
- Supplier dates apply separately to invitations, bid submissions, award workflow events and contract start dates. They describe activity cohorts, not historical snapshots of earlier statuses.
- Awards use the current `Tender.AwardedBidId` on an awarded tender. Multiple votes, approvals and workflow events never multiply the award count. Award dates come from `WorkflowTransition` with action/stage `Awarded`; tender creation is never substituted. Undated legacy awards appear only without a date filter.
- JOD, USD and other currencies are totalled separately. There is no implied exchange rate.
- Approval rows are individual approval steps. Pending records have no stored creation date, so they appear only without a date filter.
- Committee decisions show one row per evaluation report round, including historical rounds. A member vote is not labelled as a final decision. Completed rounds use their final vote date; pending rounds use report submission/creation date. The outcome comes from the saved recommendation status.

## Supplier risk card

This is a transparent descriptive indicator, **not** a calibrated probability, an AI decision or a supplier eligibility rule. It uses only data queried inside `Services/Analytics`; it does not depend on another team member's future supplier-document or guarantee feature.

| Evidence | Base weight | Adverse outcome |
| --- | --- | --- |
| Final bid compliance outcomes | 60% | Non-compliant / (compliant + non-compliant) |
| Concluded contract outcomes | 40% | Terminated / (completed + terminated) |

The weighted proportions are scaled to 100 and rounded away from zero. If a factor has no evidence, it is omitted and the remaining weight is normalized. With no evidence the card shows **Insufficient data**, not zero. Low is below 30, moderate is 30–59, and high is 60–100. The page and exports show the numerator, denominator, effective weights and contributions. Small samples can produce extreme scores and are explicitly identified as a limitation. These weights and thresholds are implementation defaults, not a policy approved by the team.

## Ownership and integration

Business queries, filters, presentation models, risk calculation, export orchestration and the PDF renderer live under `Services/Analytics`. MVC entry points are in `Controllers/AnalyticsController.cs`; the view, CSS and JavaScript are module-specific.

Shared changes are limited to registering `AddAnalytics()` in `Program.cs`, two resource entries in the project file for the embedded font/licence, a role-gated Analytics sidebar link, and an Admin dashboard link. The existing report service and other team members' feature services are not modified. Excel reuses the existing report renderer; PDF has its own olive report layout and embedded Arabic-capable font.

## Verification

The final check run passed **56 checks**, and the web project build succeeded. PDF samples from all five report families plus the individual supplier risk report were rendered and visually reviewed, including repeated headers and multi-page tables. The unchanged project has four existing compiler warnings on a fresh compilation; this module adds none. NuGet vulnerability metadata could not be refreshed in the restricted network during the test restore; no new package dependencies were introduced.

Run from the repository root:

```powershell
dotnet build Sourcing-Tender-Management-System/Sourcing-Tender-Management-System/Sourcing-Tender-Management-System.csproj --no-restore
dotnet run --project tests/Analytics.Checks/Analytics.Checks.csproj
```

The checks create a uniquely named `STMS_AnalyticsChecks_*` LocalDB database, apply the team's existing migrations, exercise real SQL queries and MVC/Razor/cookie authorization, and delete that database after completion. The harness never reads or writes the application's configured database. It covers risk arithmetic, insufficient evidence, inclusive dates, undated awards, duplicate award evidence, currencies, draft exclusion, unique dealings, pagination, export cell types, Arabic PDF, input validation, denied access and export audit records.

For an optional disposable browser preview, append `-- --preview`. Open the printed loopback `/fixture/sign-in/Admin` or `/fixture/sign-in/TopCommittee` URL. These sign-in endpoints exist only in the test harness, never in the production project. To stop the preview cleanly and remove its database, visit `http://localhost:5278/fixture/shutdown`. Generated sample PDF files are under the test project's ignored `bin/Debug/net10.0/qa` directory.

Browser verification includes desktop and 390px mobile layouts, chart share toggling, supplier drill-through and date filters, audit pagination and empty results. For large installations, screen pages currently paginate the fully filtered report in memory so the screen and exports retain identical rows; SQL-level paging/streaming is a future scaling step.
