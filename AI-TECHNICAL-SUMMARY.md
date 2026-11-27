# AI features: technical summary

## In one sentence

A local language model (Ollama, `qwen2.5:7b`) reads the user's question and picks one of a fixed list of tools. Our C# code fetches the data with the user's permissions and does all the calculations.

**The model never queries the database, never decides anything and never changes any data.**

## 30-second pitch

> "The assistant runs locally and is free: no data leaves our server. The model only chooses which approved function to run. Permissions are checked in code, so a user cannot get through the chat what their role cannot see. Scores and comparisons are computed in C#, so they are repeatable. The AI is advisory only."

## There are two AI features

| | Chat assistant | Bid analysis |
|---|---|---|
| Where | AI Intelligence page and floating chat button | "AI analysis" button on the sourcing officer's offers page |
| Who | Staff roles. Suppliers are blocked | Sourcing officer |
| Model's job | Pick a tool, write a short comment | Explain a ranking that C# already computed |
| Code | `Services/AiIntelligence/` | `Services/AiAssistant/`, `Services/BidScoring.cs` |

## How a chat question flows

```
Browser  ->  POST /AiIntelligence/Ask   (login, anti-forgery token, max 500 characters)
         ->  1. PlanAsync      model returns JSON: { tool, tenderQuery, supplier, ... }
         ->  2. CorrectAsync   C# fixes the model's usual mistakes
         ->  3. RunAsync       C# checks the role, queries the database, builds the reply
         ->  4. SummarizeAsync model writes 1-2 sentences from that data only
         ->  JSON reply: cards, table, chart or text
```

1. **Routing.** The model must answer in a JSON schema where `tool` is one of an allowed list. Anything else becomes "none".
2. **Correction.** A small model makes mistakes. Example: "report about *supplier*" is forced to the supplier profile; comparing only one tender becomes "show its bids".
3. **Execution.** Each tool is a normal C# method using Entity Framework. No text-to-SQL.
4. **Summary.** The model sees only the returned data (at most 30 rows). If it fails, the data is still shown.

## The tools

| Area | Tools |
|---|---|
| Tenders | totals, by status, one tender's file, days from creation to winner notice |
| Bids | one tender's bids, price ranges, compare two tenders |
| Suppliers | profile, compare two, participation and wins |
| Other | general reports, contracts, deliveries, the user's own clarifications |
| Documents (RAG) | all staff: admin-uploaded PDFs; Admin only: supplier documents. Answers name the file and page, naming file and page (`nomic-embed-text`) |
| No data | general questions (labelled as general knowledge), help, none |

## Misspelled names

Done in C#, not by the model. It uses edit distance (missing, extra, wrong or swapped letters) and ignores case, spaces, hyphens and word order. A close but wrong name is never answered silently: the user gets "Did you mean ...?" with a button.

| Name length | Wrong letters allowed |
|---|---|
| under 4 | 0 |
| 4-5 | 1 |
| 6-8 | 2 |
| 9 or more | 3 |

## Bid analysis scoring

- **Financial score** = 100 x (lowest cost / this bid's cost).
- **Technical score** = average of committee scores. If there are none yet, the share of tender items the bid covers.
- **Total** = technical weight x technical + financial weight x financial (weights are set per tender).
- Ties: cheaper first, then faster delivery.

The same input always gives the same ranking. The model only adds the wording. The screen warns when currencies are mixed (no exchange rate is applied) or compliance is not finished. Results are cached while the bids stay unchanged.

## Security

| Risk | Protection |
|---|---|
| User asks for data outside their role | `ReportAccess` check in code before each data query |
| Prompt injection ("ignore your rules...") | The model can only pick a listed tool; permissions do not depend on the model |
| Script injection in answers | Browser inserts text with `textContent`, never as HTML |
| Prices seen before the opening date | Reports and AI answers read only unsealed offers: bidding over, deadline and opening date passed (`Services/SealedBids.cs`) |
| Cross-site request forgery | Anti-forgery token on the question form |
| Tampered chat context from the browser | Length-limited and only used as a routing hint |
| Chat history seen by the next user | Stored per user in the browser tab and cleared on logout |
| Data sent to a third party | Model runs on `localhost` |
| Large or malicious files | Admin only upload, PDF only, 5 MB limit, stored under App_Data (ignored by git) |

## Role access (summary)

| Data | Admin, Sourcing manager, Top committee | Officers | Evaluators | Departments |
|---|---|---|---|---|
| Tenders, bids | Yes | Yes | Yes | No |
| Suppliers, contracts, deliveries | Yes | Yes | No | No |
| Requests | Yes | Yes | No | Own department |
| Approvals | Yes | No | No | No |

## Configuration

`appsettings.json`, section `Ollama`: base URL, model name, timeout (120 s), context size (8192), temperature (0.1, so answers vary little). The Ollama service must be running, otherwise the chat returns "The AI service is not running" (503).

## Testing

`tests/AiIntelligence.Checks` uses a temporary database with made-up data. It covers name matching, answers, role limits and controller guards; with the `router` argument it also tests the real model. Last run: 95 checks; one real-model routing check can vary between runs.

```
dotnet build tests/AiIntelligence.Checks -o %TEMP%\stms-ai-checks
dotnet %TEMP%\stms-ai-checks\AiIntelligence.Checks.dll router
```

## Known limitations (say these before a senior asks)

1. **Evaluators are not limited to their own tenders** in the chat.
2. **No audit log** of AI questions and **no per-user rate limit**.
3. **Routing can be wrong** for new wording. The model is small; the tests cover a sample of sentences only.
4. **Currencies are not converted** in comparisons.
5. English only has been tested.

## Questions seniors may ask

| Question | Answer |
|---|---|
| Why not let the model write SQL? | It could read data the user may not see, or be tricked into it. Fixed tools keep control in code. |
| Why a local model? | Free, and procurement data stays on our server. |
| What if the model hallucinates? | Numbers and tables come from the database. Only the short comment is model text, and it can still be wrong. |
| Can the AI award a tender? | No. It has no tool that writes anything. |
| What happens if Ollama is down? | The chat shows an error. The rest of the system works normally. |
| How would you improve it? | Fix limitations 1 and 2, log questions, and test more phrasings or use a larger model. |
