# Arabic and English workspace search

All role cycles share the existing Sourcing Officer search approach. There are
35 unique searchable list/detail surfaces, exercised in 37 role/page
combinations including separate technical and financial accounts.

## Using search

Choose **English** or **العربية** under Voice language, press **Search by voice**
or **البحث بالصوت**, and allow microphone access when the browser asks.
Recognized words appear in the same search field used for typing, and results
update live. Press the microphone control again to stop. Clear search restores
the current list; existing server filter reset behavior is preserved.

The language choice is remembered on that browser. Search fields accept
right-to-left text without switching the whole English interface to Arabic.
Microphone labels, progress and failure messages follow the selected language.
This recognizes either language; it does not translate entity names or queries.

## Coverage

| Cycle | Search surfaces |
| --- | --- |
| Admin | Dashboard summaries/links and users |
| Admin catalog | Departments, categories, supplier registrations, roles, latest 200 audit actions |
| Requesting department | Dashboard recent requests/notifications, request list and request details |
| Sourcing Officer | Tasks, request queue/details, tenders/details, offer details and conversation list |
| Supplier | Dashboard, invitations/details, offers/details, clarification questions and notifications |
| Technical and financial committee | Assigned preparation/evaluation list and visible bid/item/criterion details |
| Sourcing Manager | Pending/recent reports and report comparison/vote details |
| Top Committee | Pending tenders, decision history, statistics and detailed report/vote/request records |
| Shared messaging | Recipient conversation list, CycleChat inbox and individual question/response history |

Standalone login, registration and create/edit forms do not receive unrelated
search bars. Printable/export report artifacts remain suitable for printing.

## Search boundaries

Five list screens retain their existing database search:
Admin Users, Department Index, Top Committee History, officer Requests and
officer Tenders. Voice and typing submit the same authorized GET query, keeping
selected status filters while changing the query. Clearing also restores records
excluded by a prefiltered initial page.

The officer endpoints keep their existing HTML-fragment contract. The other
three endpoints return a normal authorized HTML page; the shared client extracts
only the identified result container and leaves navigation, controls and language
state intact. Failed or redirected requests leave the last good results visible.
Superseded requests are aborted and cannot overwrite a newer query.

Other controls filter the authorized records already rendered on that page,
including any existing recent-record limit. They are not a global database
directory. Local Arabic matching ignores optional vowel marks/tatweel and matches
Arabic/Persian numerals to Latin numerals. Database screens retain their existing
SQL matching rules. Filtering editable catalog rows preserves unsaved values
and does not submit their save/delete forms.

No controller authorization, record access, database schema or purchasing-state
transition is changed. Search cannot reveal supplier offers that the underlying
authorized page did not load.

## Shared implementation

- `Views/Shared/_WorkspaceSearch.cshtml` renders the standard local-search bar.
- `WorkspaceSearchViewModel` defines its IDs, target container and visible label.
- `Views/Shared/_VoiceLanguage.cshtml` supplies the two supported language choices.
- `wwwroot/js/sourcing-search.js` retains the original asset URL and now handles
  every role workspace. `_Layout` loads it exactly once.
- Local result items use `data-search-item`; legacy officer tasks retain
  `data-task`. Bootstrap flex/grid rows respect the shared hidden-state rule.
- Existing server forms use `data-live-search`, `data-search-input`,
  `data-results` and `data-search-status`. Full-page responses additionally use
  `data-search-response="document"`.

To add another local list, render the shared partial once with unique IDs,
give its result container the matching ResultsId and mark each record with
`data-search-item`. Never mark table headers or nest the search form inside
an editable form. The component supports multiple independently scoped bars;
starting a new microphone session cancels the previous session.

## Runtime support and fallback

Voice uses the browser's SpeechRecognition or webkitSpeechRecognition API with
`en-US` and `ar-JO`. It requires a supporting browser, a secure context such as
HTTPS/localhost, microphone permission and an available speech service.
Recognition starts only after a user clicks the microphone control.

Unsupported recognition disables the microphone control while typing remains
available. Permission denial, missing microphone, no speech, network failure and
unsupported language have actionable messages in both languages. No transcript
or search query is saved in local storage; only the language preference is saved.
Late speech results cannot restore a cleared query. Page navigation aborts
microphone/fetch work, and restored pages reset stale loading status.

Without JavaScript, database search forms continue as ordinary GET forms.
Local-only filter controls stay hidden and their records remain visible.

## Verification

The build succeeded without warnings or errors. Browser verification passed
287 checks covering 35 unique pages across 37 role/page combinations, including
both languages, real authenticated live queries, status retention, local filtering,
Arabic marks/numerals, unsaved form preservation, supplier isolation, error
recovery, unsupported recognition and late speech events. A separate three-check
run verified the no-JavaScript fallback. All 179 existing migrated SQL workflow
checks also passed.

Desktop/mobile evidence covers all seven role workspaces at 1440px and 390px.
The independent source/screenshot finish review returned ship with no material
findings. Existing wide data tables retain horizontal scrolling on small screens.

Speech events in automation are simulated; authentication, HTML rendering and
search requests use the real application and isolated migrated SQL data.
Actual microphone capture, recognition accuracy and external browser speech
service behavior require a manual microphone test.

## Reproducing browser verification

Create the synthetic fixture from the repository root:

```powershell
dotnet run --project tests/WorkspaceSearch.BrowserFixture --artifacts-path "$env:TEMP/stms-voice-fixture"
```

The program prints its new STMS_WorkspaceSearch database name and retains it
for the browser run. It never targets the configured application database.
Publish the app and run that output in Staging on port 5191, with
`ConnectionStrings__DefaultConnection` pointing to the printed isolated LocalDB
database. Keep development/bootstrap seeding off.

The fixture includes admin, officer, requester, supplier, technical, financial,
manager and top accounts at `<account>@example.test`, all using the synthetic
password `Preview-Only!4729`. It also includes another supplier to test isolation.

Create `$env:TEMP/stms-sourcing-review/global-voice`, open a fresh Playwright CLI
session, then run the callbacks in
`tests/WorkspaceSearch.BrowserFixture/browser-checks.js` and
`no-javascript-checks.js`. Each browser run should start with a fresh session
because the recognition-unsupported checks install page initialization overrides.
