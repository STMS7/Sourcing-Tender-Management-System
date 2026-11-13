# Sourcing officer implementation

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
| Approved requests | /SourcingOfficer/Requests | Search the sourcing queue |
| Request details | /SourcingOfficer/RequestDetails/{id} | Read the approved request, source items and start a clarification or tender |
| Create draft | /SourcingOfficer/Create?requestId={id} | Copy approved request items into a tender |
| My tenders | /SourcingOfficer/Tenders | Search and filter by current status |
| Edit draft | /SourcingOfficer/Edit/{id} | Change draft configuration only |
| Tender details | /SourcingOfficer/Details/{id} | Items, criteria, document requirements, invitations, committee/report tracking and history |
| Offer comparison | /SourcingOfficer/Offers/{id} | Prices, currencies, delivery, technical responses, document records and compliance history |
| Messages | /SourcingOfficer/Messages | Three contact categories, transaction selection, participant-only threads and responses |
| Recipient inbox | /Messages | Shared inbox and response forms for department, supplier and committee accounts |

State-changing actions are POST requests protected by antiforgery validation:
Create, Edit, AddCriterion, RemoveCriterion, AddRequirement, RemoveRequirement,
Invite, RemoveInvitation, Publish, Close, Compliance, ForwardToEvaluation,
AskClarification, Reply and Resolve.

## Workflow implemented

1. An approved request is available when it has no active request assignment or
   has an active assignment to the current officer. Requests assigned only to
   another officer are hidden.
2. Creating a draft copies the request items, sets ownership from the signed-in
   user, and marks the source request ConvertedToTender in one transaction.
3. The draft contains category, terms and UTC dates. Overall technical/financial
   weights belong to the evaluation committee, not the officer form.
   The officer configures criteria, document requirements and approved suppliers.
4. Publication checks the future deadline, opening date, items, terms, criterion weights
   and supplier eligibility. It locks criteria and configuration, records history,
   and creates in-system notifications for active supplier accounts.
5. Closing is explicit and permitted only after the deadline. The current shared
   enum uses Published during bidding; this module does not add an Open status.
6. Offer contents stay sealed until closure and any opening date. Draft and
   withdrawn bids are excluded. Comparison does not rank across currencies or
   guess whether cost components are already included in TotalAmount.
7. Compliance outcomes require a reason. A compliant outcome requires an accepted
   invitation, an active approved supplier, valid quoted item links/nonnegative
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
TechnicalRepresentative. The module reads existing memberships; it does not
take over committee assignment.

Clarification recipients are derived on the server:

- Approved request, or converted request linked to an actively sourced tender:
  its requesting user with DepartmentRepresentative or RequestingDepartment role.
- Closed or UnderEvaluation tender's submitted bid, after the opening date:
  active Supplier-role accounts belonging to that bid's supplier.
- Closed, UnderEvaluation or AwaitingManagerReview tender: active assigned
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

- Whether unassigned approved requests belong in a shared sourcing queue.
- Whether conversion should reserve a request at draft creation, as implemented,
  or at publication. This implementation prevents duplicate conversion by
  transitioning the request in a serializable transaction.
- Whether criteria weights total 100 within each technical/financial group.
  Group results then use Tender.TechnicalWeight and FinancialWeight. No committee
  scoring formula is implemented here.
- Whether the officer is allowed to configure criteria directly or needs an
  additional approval workflow. There is no criterion-approval record in the
  current model, so this module does not claim to enforce such approval.
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

RequestDetails includes Chat with requesting department and Request changes in
chat. The revision shortcut preselects the request owner and supplies a subject
containing the source request number. Sending creates a normal clarification
thread; it does not return or reject the request or modify its approval status.
The department edits request data through its own workflow. The officer can read
the latest conversation statuses from the source request and reopen the thread.
Converted source requests remain readable to their sourcing officer while the
communication stage is active; Create tender is hidden after conversion.

CommitteeWeighting reserves the __weight: criterion-name prefix. Officer-created
criteria cannot use it or impersonate evaluator proposals. The existing
committee module's weight-proposal process is otherwise unchanged by this work.

The new sourcing-cycle-browser-checks.js uses the current /Auth/Login route and
an isolated --preview --handoff-preview fixture. That fixture adds one closed,
compliant tender ready for the UI handoff. The script checks department messages,
replies and request history, sends the ready tender, then signs in separately as
the technical and financial evaluators to verify receipt.

Verified in this update: 70 sourcing SQL checks, 28 existing evaluation committee
checks, and 19 browser assertions. No shared model or migration change is needed.

## Overall evaluation weights

TechnicalWeight and FinancialWeight are no longer officer-editable or included
in SourcingTenderForm. Create and Edit do not bind or assign them, so fabricated
form fields cannot change them. Officer edits also preserve legacy stored values.
The entity's existing numeric defaults are retained for schema compatibility;
they are not displayed as an agreed committee decision.

Tender details show Awaiting the technical & financial evaluation committee
until both actual evaluator proposals are present. Then the agreed percentages
are shown read-only. CommitteeWeighting and EvaluationCommitteeController remain
responsible for the existing proposal/agreement process.

This correction concerns the overall technical/financial split. Within-group
criterion weights and the officer's criterion-definition screen are unchanged.
The existing policy question about pre-publication approval versus the team's
current post-bid committee weighting process remains for the team to settle.

Verified in this correction: 72 migrated SQL sourcing checks and seven browser
assertions. An authenticated Edit POST with fabricated 99/1 weight fields left
the draft's stored 60/40 values unchanged. Separate technical and financial
accounts then submitted 80/20 and 60/40 proposals; the officer page displayed the
resulting agreed 70/30 split read-only. The browser callback is
weight-ownership-browser-checks.js and uses the handoff preview fixture.

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
