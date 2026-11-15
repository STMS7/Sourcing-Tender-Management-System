# Sourcing Officer action map

The existing Lujain dashboard, tabs, palette and page structures are retained.
Actions appear within their authorized request/tender stage. Disabled publication
and handoff controls have readiness explanations; server-side checks remain final.

| Context | Existing backend actions available in the UI |
| --- | --- |
| Dashboard / tender list | Review requests, approved requests to create a tender, draft/published/closed filters, messages |
| Request details | StartReview, AcceptRequest, ReturnRequest with reason, RejectRequest with reason, department chat |
| Approved request | Create draft through Create; converted requests link to the actual authorized tender |
| Unlocked draft | Edit, AddRequirement / RemoveRequirement in Documents, Invite / RemoveInvitation in Suppliers |
| Preparation | RequestWeightApproval (labelled Request pre-publication review), ReopenPreparation on a locked draft |
| Publication | Publish and send invitations, enabled only when PublicationReadiness.Ready is true |
| Published | Close after the deadline; sealed offers remain inaccessible |
| Offers after opening | Compare offers, DownloadBidDocument, Compliance before handoff, supplier clarification, AnalyzeOffers AI assistant |
| Closed tender | ForwardToEvaluation in Evaluation tab, with officer issues and external dependencies displayed separately |
| Messages | AskClarification, SendCommitteeMessage, reply through the shared Messages/Reply route, Resolve when answered |
| Result cycle | ForwardReportToManager, AnnounceResult or SendBackToCommittee according to TenderResultFlow.Step |

Legacy AddCriterion, RemoveCriterion and AssignEvaluationCommittee return Forbid
and deliberately have no controls. The officer does not author evaluation rules,
weights or committee assignments. RequestWeightApproval submits preparation for
external review; it does not submit supplier offers early.

## Navigation fixes

- Closed-offer dashboard count is now a link; approved-request shortcuts are filtered.
- Request return/rejection shortcuts lead to the existing reason forms; rejection
  expands its disclosure without posting a decision.
- Draft supplier/document management and offer comparison are visible in the
  tender action bar, rather than only discoverable inside tab content.
- Pending result actions have a top-level shortcut to the existing result form.
- Offer handoff links target `#evaluation`. Legacy `#evaluation-handoff`, direct
  `#tab-evaluation` links and hash changes also select the correct tab.
- Tender details read two additional authorized values: linked tender ID on a
  request and whether a draft already contains offers. No workflow or role rules
  were changed.

## Verification

- 124 existing sourcing workflow checks passed on a freshly generated isolated
  SQL database, including permission, sealed-offer, supplier category, preparation,
  handoff and messaging guards. One stale assertion was updated to read committee
  approval from PublicationReadiness instead of expecting it in the Admin-only
  PreparationWaitingReasons list; production rules were not changed to fit it.
- 20 targeted browser assertions passed against the isolated localhost:5197
  fixture: actual request conversion, invitation/document POSTs, reason-required
  rejection shortcut, disabled incomplete publication, linked tender navigation,
  AI button discovery, post-handoff read-only compliance, tab links and mobile fit.
- The browser test consumes synthetic submitted request 2; use a fresh fixture
  when rerunning it. It must not be pointed at the user's normal database.
- Build and JavaScript syntax checks passed. The existing BiometricController
  duplicate-using warning remains unrelated to this change.
