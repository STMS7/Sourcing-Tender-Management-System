# Sourcing integration contract

This document describes the existing integration points that teammates' pages
must use. It does not assign new team responsibilities or approve new business
policies. Domain entities, foreign keys and enums remain those in the shared
ApplicationDbContext. No database migration is introduced by this review.

## Verified repository boundary

The reviewed origin/main commit a624828 includes the officer module, authentication,
top committee, sourcing manager and the technical/financial EvaluationCommitteeController.
Handoff is now tested against the real evaluation controller and its authenticated
technical and financial pages. Requesting department and supplier workflow
controllers are still not present in that reviewed main tree.

Tests verify migrated SQL schema compatibility, producer/consumer data contracts
and actual evaluation-page receipt. They do not establish compatibility with
teammates' unpushed request/supplier pages. Repeat the complete purchasing journey
when those pages are available.

## Shared names and routes

| Integration point | Exact existing name |
| --- | --- |
| Database context | ApplicationDbContext |
| Staff user | ApplicationUser, with DepartmentId and IsActive |
| Supplier user | ApplicationUser, with SupplierId and IsActive |
| Officer roles | SourcingOfficer or legacy ProcurementOfficer |
| Requesting department role | DepartmentRepresentative; RequestingDepartment is also recognized if the team creates that role |
| Supplier role | Supplier |
| Evaluation roles | EvaluationCommittee, SourcingManager, DepartmentRepresentative, FinancialRepresentative, TechnicalRepresentative |
| Officer entry | /SourcingOfficer |
| Approved request detail | /SourcingOfficer/RequestDetails/{requestId} |
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

An authorized upstream review must set Status to
ProcurementRequestStatus.Approved. The officer cannot approve a request.
Submitted, UnderReview, Rejected and Returned requests do not enter the sourcing
queue.

An active request WorkAssignment must use:

- Kind = AssignmentKind.ProcurementRequest.
- ProcurementRequestId = the actual request ID.
- AssignedToUserId = an officer user.
- CompletedAt = null.

Unassigned approved requests are currently available to all officers. A request
assigned only to another officer is excluded. Creating a draft copies source
items into TenderItems and changes the request to ConvertedToTender atomically.
The requesting page must read this state back; it must not reset it to Approved
or modify an already-published tender by editing the source request.

## Sourcing to supplier

The officer reads approved, active Supplier records. Supplier accounts must
reference Supplier.Id through ApplicationUser.SupplierId. A user ID is not a
supplier company ID.

TenderInvitation uses TenderId + SupplierId as a unique pair. Invitations are
prepared while the tender is Draft. Do not show prepared draft invitations to
suppliers as live invitations.

For actionable participation, supplier pages must verify:

- The signed-in account belongs to the invitation's SupplierId.
- The supplier is approved and active.
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
recognizes RequestingDepartment where the team uses that role alias.

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

Officer edits to scoring criteria lock at publication through
EvaluationCriteriaLockedAt. Supplier pages must not change those criteria.
The existing weighting convention is 100% within each criterion group.
The overall TechnicalWeight/FinancialWeight split belongs to the technical and
financial committee, and is not part of the officer's Create/Edit input contract.
Officer pages display pending until both evaluators submit complete proposals,
then show the averaged agreed split read-only. Existing numeric defaults in the entity are not
evidence of an agreed evaluation decision.

The current committee implementation independently stores proposal metadata in
__weight: EvaluationCriterion rows and writes agreed group weights during
evaluation. That existing team behavior is not changed by the officer-cycle
correction. Officer criterion input now rejects that reserved prefix. The team
still needs to reconcile the committee proposal timing with the brief's
pre-approved-weight requirement; it is not a policy the officer module can
approve on its own.

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

Department messaging is available for approved requests or converted requests
linked to tenders in Draft, Published, Closed or UnderEvaluation. Supplier
messaging is available after bidding closes/opens, while Closed or
UnderEvaluation. Evaluation-member messaging is available while Closed,
UnderEvaluation or AwaitingManagerReview. Historical threads stay readable to
their participants after the active window; new replies are blocked.

Send recipient users to /Messages. Officer accounts are redirected to their
workspace automatically. Read notifications using UserId = the signed-in user;
new message notifications already include the appropriate conversation link.

## Integration review result

The SQL checks use the actual migration chain and migration-seeded role IDs.
The fixture mirrors the application's Identity store options, including the
128-character key setting supplied by AddDefaultIdentity.

Verified contracts include approved request conversion, supplier invitation
responses, bid item joins, mandatory documents, compliance/eligibility gates,
committee membership, report arrival, stage restrictions and two-party messages.

Before final team acceptance, run the real pages through:

department request → authorized approval → officer tender → supplier acceptance
and bid → compliance → committee report → manager review.

The remaining prerequisite is implementation of the requesting-department and
supplier workflow pages against these contracts and agreement on the policy items already listed in
sourcing-officer.md. Current test coverage is not proof that unimplemented
pages are conflict-free.
