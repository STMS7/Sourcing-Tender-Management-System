# Sourcing Officer cycle audit

The implemented officer flow is tested from department request submission
through receipt of compliant offers by the assigned technical and financial
committee. This is not a claim that the entire shared project implements every
requirement in brief V5.

## Defects corrected in this audit

| Finding | Impact before correction | Correction |
| --- | --- | --- |
| Supplier chat contexts differed between inboxes | Officer messages stored TenderId and BidId; CycleChat accepted only BidId. Some legitimate cross-inbox replies failed. | Both shapes now derive the tender from the bid, reject conflicting IDs, and share the same officer/supplier relationship and opening-date checks. |
| Committee communication used different role/stage rules | DepartmentRequester/RequestingDepartment aliases, the assigned manager, or officer messages during manager review could fail in one inbox. | Both inboxes use the shared officer-to-committee recipient policy, preserving assigned participants and the permitted stage. |
| Direct edit links displayed locked drafts | A user could open an edit form whose submission was guaranteed to be rejected. | GET and POST now both reject locked draft editing. Reopen preparation remains the supported recovery path before publication. |

These corrections introduce no migration or database backfill.

## Coverage against the officer cycle

| Area | Result |
| --- | --- |
| Department sends a request and officer sees its details | Tested through the actual Department and officer controllers |
| Start review and assignment isolation | Tested; another officer cannot review an already claimed request |
| Accept, return with reason/message, permanently reject | Tested, including department edit/resubmission and duplicate-conversion prevention |
| Draft configuration and category-matched supplier selection | Tested, including server rejection of forged or no-longer-eligible invitations |
| Criteria/weight approval before publication | Tested through both real evaluator controllers; one approval is insufficient |
| Locking and approval invalidation | Tested; reopening a draft removes proposals and requires fresh approval; publication closes this recovery action |
| Deadline, closure and sealed offers | Tested, including communication through both inboxes before the opening date |
| Comparison, mandatory documents and compliance | Tested with synthetic supplier records/quotes/documents against migrated SQL |
| Handoff to technical/financial committee | Tested against the actual receiving controllers; prior browser verification also covered both authenticated receiving pages |
| Department, supplier and committee messages | Tested through officer, generic recipient and CycleChat controllers, including both supplier context shapes and department-role aliases |
| Report tracking by officer | Reads real EvaluationReport rows, but the tests supply those rows because the committee report-authoring workflow is absent |

## Remaining shared-project gaps

1. **Supplier offer workflow is not implemented in this checkout.** Supplier
   registration and admin supplier management exist, but no supplier workflow
   controller/pages were found for invitation acceptance and offer submission.
   Synthetic supplier input verifies the officer's consuming contract, not a
   complete user journey through those missing pages.
2. **Committee scoring and unified report submission are incomplete.**
   EvaluationCommitteeController supports pre-publication weights and agreement
   on a recommended bid. It does not write criterion-level BidEvaluationScore
   rows or create/submit EvaluationReport to the manager. Reports are currently
   produced only by development/test seeding, while the manager reads them.
   Brief sections 2 and 4.4 require scoring and a consolidated report with
   observations and a substantiated recommendation. This is a blocker for the
   full project journey after the officer handoff.
3. **The wider brief still requires shared features.** No Ollama bid-analysis
   integration was found in the application source. Officer pages also have no
   PDF/Excel export action. The top committee's statistics CSV is a separate
   feature and does not provide officer report exports. These belong in the
   team's brief checklist; they are not established by the officer-cycle tests.

Existing published tenders without genuine pre-publication approval evidence
remain blocked at evaluation handoff. No approval history is fabricated or
backdated. The team needs a documented resolution for those legacy records.

## Verification and limits

The current migrated SQL runs passed 107 sourcing, 34 evaluation and 38
department checks: 179 in total. The sourcing suite includes 17 additional
regressions for locked direct-edit access and cross-inbox compatibility.
The earlier 26-check preparation browser run was not rerun in this audit;
the new regressions exercise the changed controllers and database policies.

The tests use fresh isolated LocalDB databases and do not modify the configured
application database. They establish the tested contracts and paths, not an
absolute absence of defects or completion of missing teammate modules.

For delivery steps and exact integration fields, see
[the handoff guide](sourcing-officer-handoff.md) and
[the integration contract](sourcing-integration-contract.md).
