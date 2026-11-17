# Zaytouna Sourcing & Tender Management System

LiveServer: [here](
https://abdallahmdalalg-001-site1.itempurl.com/)

Trello: [here](https://trello.com/b/02vDIF0Q/stms) 

A web platform that runs the whole procurement cycle for an organisation: from a department's purchase request, through tender preparation, supplier bidding, compliance and committee evaluation, to the final Top Committee decision and the result sent to the winning supplier.

Built with **ASP.NET Core MVC (.NET 10)**, **Entity Framework Core** and **SQL Server**.

---

## Features

- **Role-based workspaces**: every role has its own dashboard and sidebar, with gold "action needed" counts.
- **Full tender cycle** with a progress bar on every tender page that shows the current stage and who must act next.
- **Supplier portal**: self-registration (PDF or photos of the official document), invitations, offer submission, offer reports (print / save as PDF / CSV), messages and notifications.
- **Evaluation committee**: technical/financial weight approval, bid selection, and manager/department acceptance.
- **Top Committee**: voting (majority wins, any "return" sends the tender back), scheduled video meetings, decision history and site statistics with CSV export.
- **Clarifications and messages** between departments, officers, committees and suppliers.
- **Shared notifications page** for all users, with search and filters.
- **AI bid analysis (optional)**: a local LLM via [Ollama](https://ollama.com) ranks and explains offers. It is advisory only and never decides.
- **Security**: ASP.NET Core Identity, two-factor authentication, biometric sign-in (fingerprint / face), role checks on every action, private file storage and an audit log.
- **Reports**: QR codes linking to live tender PDFs (QuestPDF) and Excel exports (ClosedXML).

---

## Roles

| Role | What they do |
|---|---|
| **Admin** | Creates users, departments and categories; verifies and approves suppliers. |
| **Department Requester** | Creates and submits purchase requests. |
| **Department Representative** | Sits on the evaluation committee for their department. |
| **Sourcing Officer** | Reviews requests, prepares and publishes tenders, checks offer compliance, forwards reports and announces results. |
| **Technical / Financial Representative** | Approves the evaluation weights and chooses the best bid. |
| **Sourcing Manager** | Accepts the agreed bid, approves reports and forwards Top Committee decisions. |
| **Top Committee** (4 members) | Votes on the final award. |
| **Supplier** | Registers, answers invitations and submits offers. |

---

## The tender cycle

```
Request → Preparation → Weights → Publish → Bidding → Compliance → Evaluation → Approvals → Result
```

1. **Admin** sets up departments, categories and users, then approves suppliers (business license verified, category assigned).
2. **Requester** creates a purchase request and submits it.
3. **Sourcing Officer** accepts it (this creates the tender draft), sets dates, terms and documents, invites suppliers, and clicks **Request pre-publication review**.
4. **Technical + Financial** members submit the weight split.
5. **Officer** publishes the tender.
6. **Supplier** accepts the invitation and submits an offer before the deadline.
7. **Officer** closes bidding, marks each offer compliant or not, and forwards the tender to evaluation.
8. **Technical + Financial** choose the same bid, then the **Manager + Department Representative** accept it.
9. **Officer** forwards the report → **Manager** approves → **Top Committee** votes → **Manager** forwards the decision.
10. **Officer** announces the result, and the winning supplier is notified.

---

## Getting started

### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (LocalDB, Express or full)
- *(Optional)* [Ollama](https://ollama.com) with `qwen2.5:7b` for AI bid analysis

### 1. Clone

```bash
git clone https://github.com/STMS7/Sourcing-Tender-Management-System.git
cd Sourcing-Tender-Management-System/Sourcing-Tender-Management-System
```

### 2. Configure the database

Edit `ConnectionStrings:DefaultConnection` in `appsettings.json` if your SQL Server is not `localhost`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=aspnet-Sourcing_Tender_Management_System;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```

### 3. Create the database

```bash
dotnet ef database update
```

### 4. Create the first admin

The first admin account is created at startup from configuration. Keep the password out of source control by using user secrets:

```bash
dotnet user-secrets set "AdminBootstrap:Email" "admin@example.com"
dotnet user-secrets set "AdminBootstrap:Password" "<a-strong-password>"
dotnet user-secrets set "AdminBootstrap:FullName" "System Admin"
```

### 5. Run

```bash
dotnet run --launch-profile https
```

Open **https://localhost:7288** and sign in as the admin.

### 6. (Optional) AI bid analysis

```bash
ollama pull qwen2.5:7b
```

To turn it off, set `"Ollama": { "Enabled": false }` in `appsettings.json`.

---

## Configuration notes

| Setting | Purpose |
|---|---|
| `AdminBootstrap:*` | Creates the first admin if none exists. |
| `Ollama:*` | Local AI assistant (model, URL, timeout). |
| `Meetings:Domain` | Video meeting server (default `meet.jit.si`). |
| `SupplierPortal:AllowPendingInDevelopment` | **Development only.** Lets Pending suppliers use the portal for testing. Set it to `false` to require admin approval. |

---

## Project structure

```
Controllers/        MVC controllers, one per workspace (Department, SourcingOfficer, EvaluationCommittee, ...)
Models/             Entities and view models
Data/               DbContext, migrations, admin bootstrap and dev seed
Services/           Business rules (tender journey, scoring, weighting, result flow, AI assistant, reports)
Views/              Razor views per workspace, plus shared layouts and components
Areas/Identity/     Account / profile pages (password, 2FA, biometrics)
wwwroot/css, js/    Styles and scripts
App_Data/           Private uploaded supplier files (not served publicly)
```

---

## Tech stack

ASP.NET Core MVC · Razor · Entity Framework Core · SQL Server · ASP.NET Core Identity · Bootstrap · QuestPDF · ClosedXML · QRCoder · Jitsi (meetings) · Ollama (optional AI)

---

## License

Academic project. All rights reserved by the project team.
