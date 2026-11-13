# Sourcing Officer delivery and team integration dependencies

Current correction changes only Sourcing Officer production files, supporting
officer tests and documentation. Admin, Supplier, Department, committee and
shared policy production files are not modified by this correction.

## Officer delivery

- Review department requests; accept into one linked tender, return with a
  required reason/message, or permanently reject with a reason/message.
- Prepare tender items, terms, documents and dates; select eligible suppliers
  matching the tender category. Server checks prevent cross-category invitations.
- Respect offer sealing, inspect submitted offers and attachments, record
  compliance decisions and forward compliant offers for committee evaluation.
- No criterion or maximum-score authoring; AddCriterion/RemoveCriterion deny access.
- No committee appointment controls or member list on tender details.
- No officer-facing preparation approval or reopening action. The retained
  legacy preparation methods are NonAction and cannot be invoked as HTTP actions.
- Committee messages target the authorized committee collectively, not a selected
  member. The officer view shows “Evaluation committee” instead of member names.
  Each recipient receives a participant-scoped thread through the existing inbox.

## Blocking dependencies for the team

### Evaluation committee owner

1. Add authorized criterion authoring, including type and maximum score.
   The current weight form only updates existing scoring criteria.
2. Supply the preparation lifecycle outside officer HTTP actions: readiness signal,
   lock, approval evidence and reset/version policy. Current committee pages omit
   unlocked Draft tenders and SubmitWeights expects a lock and the existing
   RequestPrePublicationWeights workflow marker.
3. Persist the evidence consumed by EvaluationPreparation.Approved, or agree a
   replacement shared contract with the team. Officer publication and evaluation
   handoff currently remain blocked until this evidence exists.

### Admin owner

1. Assignment exists already in AdminCommittee; do not rebuild it in the officer cycle.
2. Update its notification telling the officer to request approval.
3. Replace its instruction to ask the officer to reopen preparation with the agreed
   lifecycle owner. Define who notifies the committee when a draft is ready.

### Team acceptance

Run the real Department → Officer → Supplier → Officer → Committee journey using
actual role pages and SQL Server. Fixture-provided committee criteria and approval
records prove the consuming contract, not that the missing producer UI exists.

## Limits of this delivery

The officer cycle is not independently publishable end to end while the committee
producer is missing. Publication guards have not been bypassed and the brief's
preapproved evaluation rules have not been silently removed.

The current SQL officer suite passes 120 checks, including committee message fanout
and per-officer ownership. Some preparation checks invoke retained legacy methods
directly as fixture setup; these methods are not public HTTP actions. This is not
proof of an implemented committee preparation lifecycle.

Ten authenticated browser checks also passed for the removed controls, absent
member list/chooser, legacy-action HTTP 404 responses, collective message submit
and desktop/mobile viewport containment. Real microphone capture was not tested.
