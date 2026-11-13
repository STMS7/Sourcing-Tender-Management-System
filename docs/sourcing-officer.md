# Sourcing officer implementation

For the complete delivery walkthrough, see [the handoff guide](sourcing-officer-handoff.md).

For exact teammate-facing field names, state values, routes and attachment
conventions, see [the integration contract](sourcing-integration-contract.md).

This module extends the existing MVC and Bootstrap screens. Its display name is
Sourcing Officer; both existing Identity roles `SourcingOfficer` and the legacy
`ProcurementOfficer` are supported. No role rename, domain-model change, or migration is included.

The entry route is `/SourcingOfficer`. Admin can assign the existing
`SourcingOfficer` or `ProcurementOfficer` role through the existing user-management screen. This work is
on `codex/osamabanihani`; it must be reviewed against the team's workflow policies
before merging.

## Screens and actions

| Screen | Route | Purpose |
| --- | --- | --- |
| Overview | /SourcingOfficer | Live counts, recent tenders, personal task filtering and optional browser speech search |
| Purchase requests | /SourcingOfficer/Requests | Search incoming requests and filter review/history statuses |
| Request details | /SourcingOfficer/RequestDetails/{id} | Review items, return/reject with a message, chat or accept |
| Accept request | /SourcingOfficer/AcceptRequest/{id} | Accept a Submitted/UnderReview request and create its draft together |
| Create draft | /SourcingOfficer/Create?requestId={id} | Copy approved request items into a tender |
| My tenders | /SourcingOfficer/Tenders | Search and filter by current status |
| Edit draft | /SourcingOfficer/Edit/{id} | Change draft configuration only |
| Tender details | /SourcingOfficer/Details/{id} | Items, criteria, document requirements, invitations, committee/report tracking and history |
| Offer comparison | /SourcingOfficer/Offers/{id} | Prices, currencies, delivery, technical responses, document records and compliance history |
| Messages | /SourcingOfficer/Messages | Three contact categories, transaction selection, participant-only threads and responses |
| Recipient inbox | /Messages | Shared inbox and response forms for department, supplier and committee accounts |

State-changing actions are POST requests protected by antiforgery validation:
StartReview, ReturnRequest, RejectRequest, AcceptRequest, Create, Edit, AddCriterion, RemoveCriterion, AddRequirement, RemoveRequirement,
Invite, RemoveInvitation, Publish, Close, Compliance, ForwardToEvaluation,
AskClarification, Reply, Resolve, RequestWeightApproval and ReopenPreparation.
The legacy AssignEvaluationCommittee action always denies access. Assignment
is performed exclusively through Admin's /AdminCommittee module.

## Workflow implemented

The Department integration has its own migrated SQL checks:

```powershell
dotnet run --project tests/DepartmentSourcing.Checks --artifacts-path "$env:TEMP/stms-department-checks"
```

The 39-check preview run also covers creation of its pending preview request.
Use `-- --preview` to retain the isolated database and print its name for a
Staging preview. Synthetic users `requester@example.test` and
`officer@example.test` use `Preview-Only!4729`; these accounts exist only in
the newly generated test database. The browser callback in
`tests/DepartmentSourcing.Checks/browser-checks.js` targets that preview at
`http://127.0.0.1:5188` and checks 20 points across the real two-account journey,
including mobile overflow and the revised item snapshot. Screenshots go into
`$env:TEMP/stms-sourcing-review/request-cycle`; create that directory first.

1. Department submission makes a request Submitted and notifies available officers.
   Submitted/UnderReview and legacy Approved requests are available when unassigned
   or assigned to the current officer. Start review claims a pending request.
   A formal return saves Returned, a reason/review and a linked message; the
   department can edit and resubmit to that officer. Permanent rejection prevents
   further edit/resubmission. Returned items and rejection history remain visible
   to their reviewing officer.
2. Accept and create tender validates the tender form, saves an approval review,
   copies the latest request items, sets the signed-in officer as owner, completes
   the request assignment and marks ConvertedToTender in one transaction.
   Opening/cancelling the form or submitting invalid data leaves it pending.
   The legacy Create route still supports previously Approved requests.
3. The draft contains category, terms and UTC dates. All criterion weights and
   overall technical/financial weights belong exclusively to the committee.
   The officer configures criteria/documents, waits for Admin to assign the four
   committee members, and requests pre-publication approval. Preparation locks; both technical and
   financial members assign weights to their own criterion groups (100% each)
   and submit their proposed overall split against the locked criteria.
   The averaged split becomes approved before publication. Suppliers must be
   approved, active and registered in the tender category.
4. Publication checks the future deadline, opening date, items, terms, criterion weights
   and supplier/category eligibility, four valid committee members and current
   pre-publication approval evidence. It preserves the earlier preparation lock, records history,
   and creates in-system notifications for active supplier accounts.
5. Closing is explicit and permitted only after the deadline. The current shared
   enum uses Published during bidding; this module does not add an Open status.
6. Offer contents stay sealed until closure and any opening date. Draft and
   withdrawn bids are excluded. Comparison does not rank across currencies or
   guess whether cost components are already included in TotalAmount.
7. Compliance outcomes require a reason. A compliant outcome requires an accepted
   invitation, an active approved supplier in the tender category, valid quoted item links/nonnegative
   amounts and all mandatory document requirement IDs. These checks run again at
   evaluation handoff. Document presence is a guard;
   the officer still reviews authenticity and all substantive requirements.
8. Evaluation handoff requires every submitted offer to have a resolved
   compliance outcome, at least one compliant offer, and four active, distinct
   evaluation members in the required positions. It updates the tender stage,
   records history, and creates committee notifications and tasks atomically.
9. The officer tracks report status. Committee scoring, report writing, manager
   approval and awarding remain with their respective modules.

## Authorization and data contracts

Tender access is limited to its creator or an active tender WorkAssignment.
Request and tender assignment filters use AssignmentKind and CompletedAt.
All reads and mutations enforce record ownership on the server, including
document downloads and foreign record IDs supplied by forms.

Evaluation committee members must have CommitteeType.Evaluation and RoleType:
SourcingManager, DepartmentRepresentative, FinancialRepresentative and
TechnicalRepresentative. The draft committee form selects existing active users
with the matching account roles; it never creates or changes their roles.
Committee assignments become locked when preparation approval is requested.
Admin assigns members from Administration → Evaluation committees. The officer
has no assignment controls and its legacy assignment endpoint returns Forbid.
Admin's endpoint rechecks the active database role, validates four distinct
eligible employees and notifies the officer after saving. Reassignment requires
an unlocked, unpublished draft without bids; reopen preparation first if needed.

Clarification recipients are derived on the server:

- Submitted/UnderReview/Approved request, an officer's returned request, or a
  converted request linked to an actively sourced tender: its requesting user
  with DepartmentRepresentative, DepartmentRequester or RequestingDepartment role.
- Closed or UnderEvaluation tender's submitted bid, after the opening date:
  active Supplier-role accounts belonging to that bid's supplier.
- Locked preparation draft, Closed, UnderEvaluation or AwaitingManagerReview tender: active assigned
  evaluation members with an appropriate evaluation/representative/manager role.

Only sender and recipient can read and respond. Participant eligibility is
rechecked when replying. A sender follow-up returns the conversation to Pending;
a recipient response sets Answered. Only the sender can resolve an Answered
conversation. Historical conversations remain readable after stage changes.

The Messages screen uses the existing Clarification and ClarificationResponse
tables rather than creating a separate chat data store. A committee conversation
is with one selected, assigned member; it is not an unrestricted group broadcast.
Messages are grouped as Requesting department, Supplier, and Evaluation committee.
The screen lists the latest 100 threads in each category. Choosing a transaction
populates only eligible recipients, and the server repeats these checks on send.
Legacy /SourcingOfficer/Clarifications links redirect into this screen.

SourcingCommunicationPolicy is shared by the officer workspace and the generic
recipient inbox. /Messages lets teammates' users read and respond without
modifying their role-specific controllers. Reads require being the named sender
or recipient. Replies recheck current roles, active accounts, sourcing ownership,
the related party and the process stage. Notifications link to the correct inbox.
MessagesController handles recipient replies; the existing officer Reply action
remains compatible. Questions, responses, notifications and audit events are
saved transactionally. Teammates may link to /Messages from their dashboards.

The supplier and evaluation modules must independently enforce deadline,
invitation, locked-criteria and compliance rules; hiding an officer button does
not protect another controller.

## Private attachments

DownloadBidDocument enforces tender ownership, bid visibility, private-root
containment and link checks before sending an attachment as application/octet-stream.
It never exposes the stored FilePath to the page.

The default root is App_Data/Uploads beneath the application content root.
It can be overridden through PrivateFileStorage:Root. BidDocument.FilePath must
resolve inside this root, preferably as a relative path. The supplier upload
implementation must agree on this storage contract. Missing or incompatible
files return 404. Existing shared storage conventions should take precedence
when the team finalizes the integration.

## Policies to confirm before merge

- Whether the team needs manager-controlled assignment instead of the current
  shared unassigned-request queue.
- The officer prepares criteria; both technical and financial members approve
  the locked criteria and weights before publication, with workflow evidence.
  Confirm only whether the team requires additional managerial approval beyond
  this implemented preparation approval.
- Explicit versus scheduled tender closure and the interpretation of OpeningDate.
- Supplier-side invitation links and private file storage.

## Officer cycle and department revision chat

The confirmed cycle sends the tender to the evaluation committee **after**
receiving supplier offers and finishing compliance, not immediately after draft
creation. The details page shows a readiness checklist and an explicit
Send tender and compliant offers to evaluation committee action.

Readiness and the POST action share the same checks. In addition to the existing
bid and committee-position checks, each assigned member must have the account
role needed for that position. Technical and financial members receive task and
notification links to /EvaluationCommittee/Evaluate/{id}. A successful handoff
records the sent timestamp and makes the tender available to the actual
EvaluationCommitteeController. The send action is unavailable before closure or
after the handoff.

RequestDetails includes Chat with requesting department and a distinct Return for
changes form. Return requires a reason, records the formal Returned review/state
and opens a linked message. Ordinary chat leaves the request state unchanged.
The department uses its own Edit and Submit / Resubmit controls, and can reply
through CycleChat or the shared inbox. Both use the same request communication
scope. The officer reads review reasons and the latest conversation statuses.
Converted source requests remain readable to their sourcing officer while the
communication stage is active; Create tender is hidden after conversion.

CommitteeWeighting reserves the __weight: criterion-name prefix. Officer-created
criteria cannot use it or impersonate evaluator proposals. The existing
committee module now receives preparation drafts separately from compliant offers.
Weight submission is blocked after publication or when any bid already exists.

The current preparation-browser-checks.js replaces the old post-bid weighting
browser scenario. With a fresh --preview --handoff-preview fixture on port 5189,
it verifies category filtering, draft committee assignment, both preparation
approvals, publication, server rejection of late weight changes and receipt of
compliant offers by both evaluator accounts. Older cycle/message callbacks use
port 5187 and should be run on their own fresh fixture.

## Committee-only evaluation weights

SourcingCriterionForm has no Weight property. AddCriterion always saves weight
zero, ignoring fabricated Weight fields. The officer still defines the name,
type, description and maximum score; maximum score is a scoring scale, not a
weight. Requesting approval requires both criterion types, not preassigned
weights. Technical members assign technical criterion weights and financial
members assign financial criterion weights. Their endpoint requires every own
group ID exactly once, weights of 0–100 with at most two decimals and a group
total of 100%. Other-group IDs and missing payloads are rejected without writes.
Officer details show Pending committee until the relevant member submits, then
show the assigned values read-only. Publication requires both valid groups.

TechnicalWeight and FinancialWeight are no longer officer-editable or included
in SourcingTenderForm. Create and Edit do not bind or assign them, so fabricated
form fields cannot change them. Officer edits also preserve legacy stored values.
The entity's existing numeric defaults are retained for schema compatibility;
they are not displayed as an agreed committee decision.

Tender details show approval as pending until both proposals and the matching
pre-publication workflow evidence exist for the currently assigned evaluators.
Existing numeric defaults or legacy post-bid proposals do not count as approval.

Criteria and document requirements lock when approval is requested. Before
publication, ReopenPreparation removes proposals, resets criterion weights to
zero and unlocks the draft;
all changes then require both members to approve again. Published tenders cannot
be reopened through this action. No automatic legacy-data backfill is performed.
Legacy published tenders without prior approval are flagged and blocked at
handoff; resolve those through the team's documented procedure, without
retroactively inventing approval evidence.

Verified: 112 sourcing SQL checks, 44 committee SQL checks and 38 department SQL
checks (194 total). The preparation browser journey passed 48 checks on its rerun,
including one synthetic-draft rerun guard, plus three separate checks for published
Admin read-only state, late reassignment rejection and antiforgery (51 browser
checks total). The maintained callback includes these checks (50 on a fresh fixture). It
exercises committee-only inputs, forged officer/cross-group/late submissions,
both approvals, actual publication and compliant-offer handoff. Existing
request/return/resubmission/acceptance browser checks remain separate.
No shared schema or migration is changed.

## Verification

Build:

    dotnet build Sourcing-Tender-Management-System/Sourcing-Tender-Management-System.slnx

SQL workflow checks:

    dotnet run --project tests/SourcingOfficer.Checks

The executable applies the actual team migrations and seeded roles in a uniquely
named STMS_SourcingChecks_* SQL LocalDB database and deletes it after normal runs.
It checks ownership, assignments,
source-item snapshots, duplicate conversion, criteria locking, deadlines,
mandatory documents, clarification recipients, committee handoff and audit data.
It does not touch the configured application database.

An optional --preview argument retains that isolated database and prints its
name, populating synthetic accounts/data for browser review. Its test-only login
is officer@example.test / Preview-Only!4729. It must never be used for production
seeding. Delete the isolated database after preview.

browser-checks.js is a Playwright CLI callback for a fresh --preview fixture on
http://127.0.0.1:5187, logged in with the fixture officer. It exercises all screen
routes, task filtering, invalid and valid form submissions, CSRF rejection,
anonymous redirects and desktop/mobile screenshots. Create the screenshot
directory declared in the callback before running it.

Browser speech availability depends on the browser and microphone permission.
The text-search fallback is always available; actual speech recognition requires
a real microphone test.

Integration verification includes the SQL workflow/authorization checks in the
combined executable and 26 browser assertions in messages-browser-checks.js.
The browser flow signs in as the officer and actual separate synthetic
department, supplier and committee accounts, sends questions, sends replies,
checks the officer's view, resolves threads, and checks unauthorized IDs and CSRF.
Desktop/mobile screenshots cover the officer workspace and recipient inbox.
The preview accounts use the same test-only password documented above:
requester@example.test, supplier-user@example.test and technical@example.test.
These accounts are generated only in an isolated --preview fixture.

## Live and voice search

Search is now shared across all role cycles. See
[Arabic and English workspace search](workspace-voice-search.md) for coverage,
runtime support, integration attributes and current browser verification.

Requests and Tenders use a shared sourcing-search.js component. Typing waits
250 ms before fetching a server-rendered result fragment. Status filters apply
immediately. Requests use the same authorized controller actions as ordinary
GET forms, with X-Sourcing-Live: 1; responses identify the fragment with
X-Sourcing-Fragment: 1. Clearing a query searches the database again rather than
only filtering already-loaded rows. Older requests are aborted and stale
responses cannot replace newer results. Errors retain the previous result set.
Without JavaScript, the Search/Filter buttons remain ordinary GET forms.

Dashboard tasks use the same input/voice component with immediate filtering of
the loaded personal tasks. Voice language is selectable as English (en-US) or
Arabic (ar-JO). Interim speech results fill the input and trigger the same search
path. Stop listening, permission failure, unavailable microphone/service, and
unsupported-browser fallbacks are handled. Voice recognition needs a supported
browser and secure context; local preview runs on localhost.

live-search-checks.js runs against the retained preview fixture. Its 14 browser
assertions cover live search/reset, status filtering, no navigation, voice
language/transcript wiring, stop/permission handling, failed search recovery,
mobile layout, request fragments, task search and unsupported speech fallback.
Speech events are simulated in this test; physical microphone recognition is
not claimed as verified.

Known pre-existing issue observed during browser review: the shared layout links
Sourcing_Tender_Management_System.styles.css, which returned 404. That unrelated
shared asset reference is left unchanged for the team to resolve.
