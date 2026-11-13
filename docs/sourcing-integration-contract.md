# Sourcing integration contract

This document describes the existing integration points that teammates' pages
must use. It does not assign new team responsibilities or approve new business
policies. Domain entities, foreign keys and enums remain those in the shared
ApplicationDbContext. No database migration is introduced by this review.

## Verified repository boundary

This checkout integrates Raghad's DepartmentController and CycleChatController
with the officer module. Department submission, sourcing review, formal return,
chat reply, department edit/resubmission and acceptance into a tender are tested
through the real controllers and authenticated browser pages. Existing evaluation
handoff tests remain applicable. This does not establish compatibility with
supplier pages that are absent from this checkout.

## Shared names and routes

| Integration point | Exact existing name |
| --- | --- |
| Database context | ApplicationDbContext |
| Staff user | ApplicationUser, with DepartmentId and IsActive |
| Supplier user | ApplicationUser, with SupplierId and IsActive |
| Officer roles | SourcingOfficer or legacy ProcurementOfficer |
| Requesting department role | DepartmentRepresentative, DepartmentRequester or RequestingDepartment |
| Supplier role | Supplier |
| Evaluation roles | EvaluationCommittee, SourcingManager, DepartmentRepresentative, FinancialRepresentative, TechnicalRepresentative |
| Officer entry | /SourcingOfficer |
| Purchase request detail | /SourcingOfficer/RequestDetails/{requestId} |
| Accept and create draft | GET/POST /SourcingOfficer/AcceptRequest |
| Return for changes / reject | POST /SourcingOfficer/ReturnRequest or /SourcingOfficer/RejectRequest |
| Tender entry | /SourcingOfficer/Details/{tenderId} |
| Officer messages | /SourcingOfficer/Messages |
| Recipient inbox | /Messages?conversationId={clarificationId} |
| Recipient response | POST /Messages/Reply |

These are MVC HTML endpoints, not JSON APIs. State-changing requests require an
antiforgery token. Reuse the existing MessagesController rather than introducing
another controller with the same name/routes. Add links using MVC tag helpers.

## Requesting department to sourcing

Create a ProcurementRequest with valid RequestNumber, Title, DepartmentId,
RequestedByUserId and ProcurementRequestItems. The requesting user must have an
active account and the department role for messaging.

Each item needs ItemName, positive Quantity, Unit and Specifications as
appropriate. CategoryId is optional on request items, but creating a tender
requires choosing an existing Category.

DepartmentController creates Draft requests and submits them as Submitted,
notifying available officers. The default officer queue includes Submitted,
UnderReview, Returned and legacy Approved requests within its assignment scope.
Rejected and ConvertedToTender history can be selected by status.
StartReview claims the request and sets UnderReview. ReturnRequest requires a
reason, sets Returned, records a ProcurementRequestReview and creates a linked
Clarification addressed to the request owner, plus a notification. The department
can edit Returned requests and resubmit them to the same assigned officer.
RejectRequest records a permanent Rejected decision and message; it cannot be
edited or resubmitted. Sending ordinary chat alone does not return a request.

An active request WorkAssignment must use:

- Kind = AssignmentKind.ProcurementRequest.
- ProcurementRequestId = the actual request ID.
- AssignedToUserId = an officer user.
- CompletedAt = null.

Unassigned Submitted/UnderReview/Approved requests are available to officers.
A request assigned only to another officer is excluded. AcceptRequest saves the
approval review, copies the latest request items into a Draft tender, completes
the request assignment and sets ConvertedToTender in one serializable transaction.
The acceptance GET and invalid POST do not approve or convert anything. The
legacy Create route still consumes already-Approved requests. A second acceptance
cannot create another tender. Department edit/submit rejects converted requests.
Department accounts need an active DepartmentId for DepartmentController.
Request conversations in both CycleChat and the officer/shared inbox use the
same request communication policy and the named request owner.

## Sourcing to supplier

The officer reads approved, active Supplier records with a SupplierCategory
link matching Tender.CategoryId. Missing category links never imply eligibility.
The picker and Invite/Publish actions enforce this; compliance and handoff
recheck it. The supplier registration/profile module must persist those links. Supplier accounts must
reference Supplier.Id through ApplicationUser.SupplierId. A user ID is not a
supplier company ID.

TenderInvitation uses TenderId + SupplierId as a unique pair. Invitations are
prepared while the tender is Draft. Do not show prepared draft invitations to
suppliers as live invitations.

For actionable participation, supplier pages must verify:

- The signed-in account belongs to the invitation's SupplierId.
- The supplier is approved, active and linked to the tender category.
- Tender.Status is Published and the UTC deadline has not elapsed.
- The invitation is eligible and has not been declined/expired.

Use InvitationStatus.Invited, Accepted, Declined and Expired. There is no Pending
value in the current shared invitation enum. Accept/decline actions should update
Status, RespondedAt and ResponseReason. Historical invitations can remain visible
after the tender closes; they must not allow a new submission.

## Supplier to sourcing

A bid uses Bid.TenderId and Bid.SupplierId. There is one unique Bid per
tender/supplier in the shared schema. Keep unfinished offers Draft, set a
submitted offer to Submitted and set SubmittedAt on submission. Withdrawn and
Draft offers are excluded from the officer comparison.

BidItems must reference the bid and the corresponding TenderItem.Id, not the
ProcurementRequestItem.Id. There is one unique line per BidId/TenderItemId.
Use UnitPrice, TotalPrice and TechnicalResponse; preserve the shared decimal
precisions and Currency. Document whether TotalAmount includes each cost
component; the officer does not guess or convert currencies.

For a Compliant decision, the officer now independently checks:

- An Accepted invitation exists for the bid's supplier and tender.
- The supplier is still approved and active.
- At least one item is quoted, all quoted item IDs belong to this tender,
  there are no duplicates, and amounts are nonnegative.
- Every mandatory TenderDocumentRequirement has a matching BidDocument.

The same checks run again at evaluation handoff so later supplier/document edits
cannot silently invalidate an earlier compliant decision. Whether a partial
quote meets a tender's actual requirements remains a human compliance/policy
decision; this guard does not introduce a partial-award policy.

Bid.ComplianceStatus is separate from Bid.Status. Supplier and evaluation pages
must read ComplianceStatus for Pending, Compliant, NonCompliant and
PendingClarification. Do not expect the officer to overwrite the bid submission
status when recording compliance.

## Attachments

For each uploaded BidDocument, set BidId, FileName, FilePath, ContentType,
FileSize and TenderDocumentRequirementId when fulfilling a specific requirement.
The requirement must belong to the same tender. DocumentType alone does not
satisfy the foreign-key requirement check.

Private storage defaults to App_Data/Uploads beneath the application's content
root; PrivateFileStorage:Root can override it. Store a relative FilePath beneath
that root. For example, bids/42/quality.pdf resolves beneath that private root.
Do not store browser URLs as FilePath or publish protected bid files under
wwwroot. Missing files, traversal paths and incompatible roots return 404 from
the protected download action.

Use unique storage filenames. The supplier upload handler remains responsible
for file size/type validation, ownership and preserving locked submitted data.
The officer download handler enforces transaction access independently.

## Evaluation and manager handoff

POST AssignEvaluationCommittee can select existing users on an unlocked draft.
The team may also populate the same memberships through its shared module.
Committee assignment must create four active, distinct TenderCommitteeMembers
with CommitteeType.Evaluation and the following RoleType values:

- SourcingManager.
- DepartmentRepresentative.
- FinancialRepresentative.
- TechnicalRepresentative.

Also populate the required Role display string. A role membership alone does not
assign a user to a tender. For an assigned officer, use a separate WorkAssignment
with Kind.Tender and TenderId.

Each committee position also needs its matching Identity role. The handoff
checks this so TechnicalRepresentative and FinancialRepresentative accounts can
actually open the receiving evaluation pages. The department position also
recognizes DepartmentRequester and RequestingDepartment role aliases.

Successful officer handoff sets Tender.Status to UnderEvaluation and creates
committee notifications/tasks. The evaluation page must select the tender's
submitted offers with ComplianceStatus.Compliant; it must not score all Bids
without checking compliance.

This handoff occurs after bid reception and compliance. It does not send a new
draft directly to evaluation. Its readiness checklist and POST use the same
validation. Technical and financial task/notification links target
/EvaluationCommittee/Evaluate/{tenderId}; the officer sees the recorded sent time.

The committee writes an EvaluationReport linked to TenderId, with its Round,
PreparedByUserId, Summary and recommendation. On submission to the manager, use
Status.PendingManagerReview, SubmittedAt in UTC, and
TenderStatus.AwaitingManagerReview. The officer tracking screen reads those
fields directly. A manager's subsequent decisions belong to the manager module.

## Pre-publication approval contract

1. Populate items, terms, future dates, technical and financial criteria totalling
   100% within each group, document requirements and the four committee members.
2. POST /SourcingOfficer/RequestWeightApproval locks the Draft using
   EvaluationCriteriaLockedAt and notifies the technical/financial members.
   Tender.Status stays Draft. This is preparation approval, not bid handoff.
3. /EvaluationCommittee/Evaluate/{id} shows locked preparation to its assigned
   active technical/financial user, with no supplier offers. Each submits
   POST /EvaluationCommittee/SubmitWeights with tenderId, technicalWeight and
   financialWeight. Each pair must total 100%; the existing averaging convention
   is preserved. Submitting also approves the displayed locked criteria.
4. The existing __weight: EvaluationCriterion rows hold proposals. Workflow
   records bind each approval to the current member and preparation lock time.
   A combined approval event is recorded when both have submitted. No migration
   or new persisted field is required. These rows are excluded from criterion
   totals and cannot be created/deleted through the officer's criterion forms.
5. Publish verifies the agreed pair matches stored tender weights, both current
   members have approval evidence since the lock and before publication, all
   four members retain their roles, and selected suppliers match the category.
6. ReopenPreparation works only on unpublished drafts without bids. It removes
   proposals, clears the lock and notifies members. Old history remains for audit
   but cannot approve the newer preparation. Committee assignment, criteria,
   terms and dates must remain unchanged during approval; reopen first to edit.
7. SubmitWeights refuses published tenders and any tender already containing
   bids. Published/Closed committee pages show no offer contents. Only the later
   compliance handoff exposes compliant offers for evaluation.

Legacy numeric defaults and post-bid proposals are not evidence of approval
before publication. Existing published tenders without valid evidence are
flagged, not silently rewritten or backdated; follow the team's documented
legacy-tender procedure. The officer cannot choose a winner by changing weights.

## Messages

Use Clarification and ClarificationResponse for the existing subject-based
message threads. Do not write to ProcessMessage expecting those rows to appear
in this inbox; it is a different shared model.

Valid context shapes are:

| Party | ProcurementRequestId | TenderId | BidId |
| --- | --- | --- | --- |
| Request owner | request ID | null | null |
| Supplier | null | tender ID | bid ID belonging to that tender |
| Evaluation member | null | tender ID | null |

The officer composer derives recipients through SourcingCommunicationPolicy.
Read access is limited to the named sender/recipient; replies recheck active
roles, relationships and stage. No role can use the inbox as a user directory.

Department messaging is available for Submitted/UnderReview/Approved requests,
the reviewing officer's Returned requests, or converted requests linked to
tenders in Draft, Published, Closed or UnderEvaluation. Supplier
messaging is available after bidding closes/opens, while Closed or
UnderEvaluation. Evaluation-member messaging is available while Closed,
UnderEvaluation or AwaitingManagerReview, plus locked Draft preparation for
committee clarification. Historical threads stay readable to
their participants after the active window; new replies are blocked.

Send recipient users to /Messages. Officer accounts are redirected to their
workspace automatically. Read notifications using UserId = the signed-in user;
new message notifications already include the appropriate conversation link.

## Integration review result

The SQL checks use the actual migration chain and migration-seeded role IDs.
The fixture mirrors the application's Identity store options, including the
128-character key setting supplied by AddDefaultIdentity.

Verified contracts include department submission, formal return/message,
department edit/resubmission, atomic acceptance and legacy approved conversion, supplier invitation
responses, bid item joins, mandatory documents, compliance/eligibility gates,
committee membership, report arrival, stage restrictions and two-party messages.

Before final team acceptance, run the real pages through:

department request → officer review/return and resubmission → acceptance/tender → supplier acceptance
and bid → compliance → committee report → manager review.

Department pages are integrated and tested. Supplier workflow pages must still
be integrated against these contracts, and the team must agree on the policy
items listed in sourcing-officer.md. Current test coverage is not proof that
unimplemented pages are conflict-free.
