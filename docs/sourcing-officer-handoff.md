# Sourcing Officer handoff

The officer cycle covers department request review through delivery of compliant
offers to the technical and financial evaluation committee. This implementation
uses the existing shared database and roles; no migration is required.

## Demonstration sequence

1. A department employee with an active DepartmentId creates a request in
   /Department/Create and submits it. The request appears in
   /SourcingOfficer/Requests and available officers are notified.
2. Open the request and start review. Choose one of three outcomes:
   accept and complete the tender form; return for changes with a reason/message;
   or reject permanently with a reason/message. Returned requests can be edited
   and resubmitted by the department. Rejected requests cannot.
3. Acceptance creates one Draft tender using the latest request items.
   Complete its category, terms, future deadline and optional opening date.
4. Add technical and financial scoring criteria, each group totalling 100%.
   Configure required documents and select the four committee members.
5. Select approved, active suppliers registered in the tender category.
   Prepared invitations are not sent until publication.
6. Click **Lock preparation and request weight approval**. The tender remains
   Draft. The assigned technical and financial users receive links to review
   the locked specifications/criteria and submit their weight proposals.
   They see no supplier offers. Both proposals are required, and the existing
   average determines the overall split.
7. After approval, **Publish and send invitations** becomes available.
   The server rechecks approval, membership, supplier categories and dates.
   If preparation needs changes before publication, reopen it, edit and obtain
   both approvals again. Published preparation cannot be reopened this way.
8. Suppliers accept invitations and submit their offers through their module.
   The officer cannot read sealed offers before the deadline, closure and
   opening date. After the deadline, click **Close bidding**.
9. Compare submitted offers, request clarifications when needed, and record
   compliance outcomes with reasons. At least one offer must be compliant and
   every submitted offer must have a resolved compliance decision.
10. Click **Send tender and compliant offers to evaluation committee**.
    The tender becomes UnderEvaluation. Technical and financial accounts receive
    direct links and tasks; their pages now show the compliant offers.
    Later evaluation/report/recommendation and final decisions belong to the
    committee, manager and top-committee modules.

## What teammates must provide

| Integration | Required shared data |
| --- | --- |
| Employee accounts | Active Identity users with the relevant existing roles; department employees also need an active DepartmentId |
| Supplier registration | Approved, active Supplier; account SupplierId; SupplierCategory links matching the tender category |
| Supplier participation | TenderInvitation acceptance, submitted Bid/quoted BidItems, mandatory BidDocuments with the correct requirement IDs |
| Committee | Four distinct active members: sourcing manager, department, technical and financial representatives; matching Identity roles |
| Evaluation result | EvaluationReport linked to TenderId, report round, summary/recommendation and submitted state for manager review |

Supplier category eligibility is checked again during compliance and handoff.
Matching a user's display name or department name is not a substitute for the
actual foreign keys, role assignments and supplier-category relationships.

## Important compatibility change

Weight submission moved from UnderEvaluation to locked Draft preparation to
match brief section 2.2. The committee teammate must merge the controller,
service and view changes together with the officer module. Existing field and
route names are preserved, including SubmitWeights.

Previously published records without approval evidence from before publication
are flagged and blocked at handoff. They are not backfilled. Resolve legacy
tenders through the team's documented procedure; do not change weights after
seeing offers or invent old approval timestamps.

## Verification

Run from the repository root, using separate artifact directories if Visual
Studio is running the app:

```powershell
dotnet run --project tests/DepartmentSourcing.Checks --artifacts-path "$env:TEMP/stms-department-checks"
dotnet run --project tests/SourcingOfficer.Checks --artifacts-path "$env:TEMP/stms-sourcing-checks"
dotnet run --project tests/EvaluationCommittee.Checks --artifacts-path "$env:TEMP/stms-evaluation-checks"
```

The migrated SQL suites passed 38 department, 107 officer and 34 committee checks.
They use newly generated isolated LocalDB databases and delete them on normal
runs. The officer preview flags `-- --preview --handoff-preview` retain a
synthetic database and print its name.

For browser verification, publish the app and run it in Staging against that
isolated database at port 5189. Create
`$env:TEMP/stms-sourcing-review/preparation` for captures and run the
`tests/SourcingOfficer.Checks/preparation-browser-checks.js` callback through
Playwright CLI. The fresh journey checks 25 points; the verified rerun checked
26 including its synthetic-draft guard. Preview accounts use
`Preview-Only!4729` with officer@example.test, technical@example.test and
financial@example.test. These are test-fixture identities only.

The tests establish these producer/consumer contracts and actual authenticated
committee receipt. Supplier input is synthetic because a complete supplier
workflow controller is not present in this checkout. The team must run its
supplier registration/invitation/submission pages against the same contracts
when integrating them.

See [the cycle audit](sourcing-officer-audit.md) for the corrected cross-inbox
issues and the remaining supplier, committee-report and wider brief gaps.
