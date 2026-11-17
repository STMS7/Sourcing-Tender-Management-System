# Generates Zaytouna STMS mockup screens (1440px frames) -> import into Figma (html.to.design / Figma MCP).
import os
OUT = os.path.dirname(os.path.abspath(__file__))
CSS = """
:root{--olive:#17402A;--deep:#0B2416;--cream:#FBF3DF;--lime:#DCEB4B;--yellow:#FFE38A;--sage:#C9D3BC;--mist:#EEF0DC;--muted:#4A5A4E;--danger:#9A2E1C;--ok:#2F6B2F;--warn:#A66A00}
*{box-sizing:border-box;margin:0}
body{font:400 14px/1.5 'DM Sans',Arial,sans-serif;color:var(--olive);background:var(--cream);width:1440px;min-height:900px}
.shell{display:flex;min-height:900px}
.side{width:248px;background:var(--olive);color:var(--cream);padding:24px 16px;flex:none}
.logo{display:flex;align-items:center;gap:10px;font-weight:700;font-size:20px;margin:0 8px 28px}
.logo i{width:34px;height:34px;border-radius:50%;background:var(--lime);display:block}
.logo small{display:block;font-weight:400;font-size:11px;opacity:.7}
.side a{display:block;padding:10px 12px;border-radius:12px;color:var(--cream);margin-bottom:4px;opacity:.85}
.side a.on{background:var(--lime);color:var(--olive);font-weight:700;opacity:1}
.main{flex:1;padding:28px 36px}
.top{display:flex;justify-content:space-between;align-items:center;margin-bottom:24px}
.top h1{font-size:28px;font-weight:700}.top p{color:var(--muted)}
.who{display:flex;gap:12px;align-items:center}.av{width:38px;height:38px;border-radius:50%;background:var(--sage);display:grid;place-items:center;font-weight:700}
.search{background:#fff;border-radius:20px;padding:9px 18px;color:var(--muted);width:260px}
.grid{display:grid;gap:18px}.c4{grid-template-columns:repeat(4,1fr)}.c3{grid-template-columns:repeat(3,1fr)}.c2{grid-template-columns:2fr 1fr}.e2{grid-template-columns:1fr 1fr}
.card{background:#fff;border-radius:26px;padding:22px 24px;margin-bottom:18px}
.card h3{font-size:16px;margin-bottom:12px}
.kpi b{display:block;font-size:34px;line-height:1.1}.kpi span{color:var(--muted)}
.kpi.hi{background:var(--lime)}.kpi.dk{background:var(--olive);color:var(--cream)}.kpi.dk span{color:var(--sage)}
table{width:100%;border-collapse:collapse}th{text-align:left;font-size:12px;text-transform:uppercase;letter-spacing:.05em;color:var(--muted);padding:8px 10px;border-bottom:1px solid var(--sage)}
td{padding:12px 10px;border-bottom:1px solid var(--mist)}
.pill{display:inline-block;padding:3px 12px;border-radius:20px;font-size:12px;font-weight:700;background:var(--mist)}
.p-ok{background:#EEF4C2;color:var(--ok)}.p-warn{background:var(--yellow);color:#4A3203}.p-bad{background:#F7DCD3;color:var(--danger)}.p-info{background:var(--sage)}
.btn{display:inline-block;padding:10px 22px;border-radius:24px;background:var(--olive);color:var(--cream);font-weight:700;margin-right:8px}
.btn.alt{background:var(--lime);color:var(--olive)}.btn.ghost{background:transparent;border:1.5px solid var(--olive);color:var(--olive)}.btn.bad{background:var(--danger)}
.field{margin-bottom:14px}.field label{display:block;font-weight:700;font-size:12px;margin-bottom:4px}
.field div{background:var(--cream);border:1px solid var(--sage);border-radius:14px;padding:10px 14px;color:var(--muted)}
.steps{display:flex;gap:8px;margin-bottom:18px}.steps span{flex:1;background:var(--mist);border-radius:14px;padding:10px;text-align:center;font-size:12px}.steps .d{background:var(--olive);color:var(--cream)}.steps .n{background:var(--lime);font-weight:700}
.chart{display:flex;align-items:flex-end;gap:14px;height:160px}.chart i{flex:1;background:var(--olive);border-radius:8px 8px 0 0}.chart i:nth-child(even){background:var(--lime)}
.pub{background:var(--cream)}.nav{display:flex;justify-content:space-between;align-items:center;padding:20px 64px}.nav .logo{margin:0}.nav nav a{margin-left:28px;font-weight:600}
.hero{padding:70px 64px;display:grid;grid-template-columns:1.2fr 1fr;gap:48px;align-items:center}
.hero h1{font-size:64px;line-height:1.05;margin-bottom:18px}.hero .img{height:420px;border-radius:40px;background:linear-gradient(135deg,var(--olive),#4E6B2F)}
.sec{padding:40px 64px}.sec h2{font-size:34px;margin-bottom:20px}
.foot{background:var(--olive);color:var(--cream);padding:36px 64px;margin-top:40px}
.center{display:grid;place-items:center;min-height:900px;background:var(--olive)}
.auth{background:var(--cream);border-radius:32px;width:480px;padding:40px}.auth h1{font-size:28px;margin-bottom:4px}
.tl{border-left:3px solid var(--sage);padding-left:18px}.tl div{margin-bottom:14px;position:relative}.tl div:before{content:'';position:absolute;left:-26px;top:5px;width:11px;height:11px;border-radius:50%;background:var(--olive)}
.msg{background:var(--mist);border-radius:18px;padding:12px 16px;margin-bottom:10px;max-width:70%}.msg.me{background:var(--lime);margin-left:auto}
.avatar-cap{font-size:12px;color:var(--muted)}
.sw{display:flex;gap:12px}.sw div{width:120px;height:90px;border-radius:18px;padding:10px;font-size:12px}
"""
def pill(t, k=""): return f'<span class="pill p-{k}">{t}</span>'
def table(h, rows):
    return "<table><tr>" + "".join(f"<th>{x}</th>" for x in h) + "</tr>" + "".join("<tr>" + "".join(f"<td>{c}</td>" for c in r) + "</tr>" for r in rows) + "</table>"
def card(t, body, cls=""): return f'<div class="card {cls}"><h3>{t}</h3>{body}</div>'
def kpis(items):
    cls = ["hi", "dk", "", ""]
    return '<div class="grid c4">' + "".join(f'<div class="card kpi {cls[i % 4]}"><b>{v}</b><span>{l}</span></div>' for i, (v, l) in enumerate(items)) + '</div>'
def form(fields): return "".join(f'<div class="field"><label>{l}</label><div>{v}</div></div>' for l, v in fields)
def steps(items, cur): return '<div class="steps">' + "".join(f'<span class="{"d" if i < cur else "n" if i == cur else ""}">{s}</span>' for i, s in enumerate(items)) + '</div>'
def btns(*b): return "".join(f'<span class="btn {c}">{t}</span>' for t, c in b)
def chart(): return '<div class="chart">' + "".join(f'<i style="height:{h}%"></i>' for h in (40, 65, 50, 85, 70, 95, 60)) + '</div>'

NAV = {
 "Department": ["Dashboard", "Purchase requests", "New request", "Request status", "Messages", "Notifications"],
 "Sourcing Officer": ["Overview", "Purchase requests", "My tenders", "Messages", "Reports"],
 "Sourcing Manager": ["Overview", "Pending approvals", "Tenders", "Reports", "Messages"],
 "Evaluation Committee": ["Dashboard", "Tenders to evaluate", "My evaluations", "Messages"],
 "Top Committee": ["Dashboard", "Meeting", "History", "Statistics"],
 "Supplier": ["Dashboard", "Invitations", "My offers", "Questions", "Notifications", "Report"],
 "Admin": ["Overview", "Users", "Departments", "Roles", "Categories", "Suppliers", "Committees", "Audit logs", "Contact messages"],
}
SCREENS = []
def save(file, title, page):
    p = os.path.join(OUT, file)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    open(p, "w", encoding="utf-8").write(f'<!doctype html><html><head><meta charset="utf-8"><title>{title}</title><style>{CSS}</style></head><body>{page}</body></html>')
    SCREENS.append((file, title))
def app(file, role, active, title, sub, body):
    nav = "".join(f'<a class="{"on" if n == active else ""}">{n}</a>' for n in NAV[role])
    save(file, title, f'<div class="shell"><aside class="side"><div class="logo"><i></i><div>Zaytouna<small>{role}</small></div></div>{nav}</aside><main class="main"><div class="top"><div><h1>{title}</h1><p>{sub}</p></div><div class="who"><span class="search">Search…</span><span class="av">AB</span></div></div>{body}</main></div>')
def public(file, title, body):
    save(file, title, f'<div class="pub"><div class="nav"><div class="logo"><i></i>Zaytouna</div><nav><a>Home</a><a>About</a><a>Contact</a><a>Powered by</a><span class="btn" style="margin-left:28px">Sign in</span></nav></div>{body}<div class="foot">© Zaytouna — Sourcing &amp; Tender Management System · Privacy · Contact · Map</div></div>')

# ---------- 00 design system
save("00-design-system.html", "Design system", f'''<div class="main" style="width:1440px"><div class="top"><div><h1>Zaytouna design system</h1><p>Olive / cream / lime palette · DM Sans · 26px card radius · pill buttons</p></div></div>
<div class="card"><h3>Colors</h3><div class="sw"><div style="background:#17402A;color:#FBF3DF">Olive<br>#17402A</div><div style="background:#0B2416;color:#FBF3DF">Deep<br>#0B2416</div><div style="background:#FBF3DF;border:1px solid #C9D3BC">Cream<br>#FBF3DF</div><div style="background:#DCEB4B">Lime<br>#DCEB4B</div><div style="background:#FFE38A">Yellow<br>#FFE38A</div><div style="background:#C9D3BC">Sage<br>#C9D3BC</div><div style="background:#EEF0DC">Mist<br>#EEF0DC</div><div style="background:#9A2E1C;color:#fff">Danger<br>#9A2E1C</div></div></div>
<div class="card"><h3>Buttons &amp; status</h3>{btns(("Primary", ""), ("Accent", "alt"), ("Ghost", "ghost"), ("Danger", "bad"))}<br><br>{pill("Approved", "ok")} {pill("Pending", "warn")} {pill("Rejected", "bad")} {pill("Draft", "info")}</div>
<div class="card"><h3>Form field</h3>{form([("Label", "Placeholder value")])}</div>
{kpis([("42", "KPI highlight"), ("18", "KPI dark"), ("7", "KPI plain"), ("96%", "KPI plain")])}</div>''')

# ---------- Public
public("01-public/home.html", "Home", '<div class="hero"><div><h1>Sourcing so clear, every award is obvious.</h1><p style="font-size:18px;color:var(--muted);margin-bottom:24px">One request. One tender. One transparent award — across every role.</p>' + btns(("Get started", ""), ("Register as supplier", "ghost")) + '</div><div class="img"></div></div><div class="sec"><h2>Every role. One cycle.</h2><div class="grid c4">' + "".join(f'<div class="card kpi"><b style="font-size:20px">{t}</b><span>{d}</span></div>' for t, d in [("Department", "Raise purchase requests"), ("Sourcing", "Run tenders & invite suppliers"), ("Committee", "Score bids, vote award"), ("Supplier", "Submit offers online")]) + '</div></div>')
public("01-public/about.html", "About", '<div class="sec"><h2>About Zaytouna</h2><div class="grid e2"><div class="card"><h3>Mission</h3><p>From paper chaos to a single auditable tender journey.</p></div><div class="card"><h3>Team</h3><p>Team presentation, map and values.</p></div></div></div>')
public("01-public/contact.html", "Contact", '<div class="sec"><h2>Contact us</h2><div class="grid e2"><div class="card">' + form([("Name", "Full name"), ("Email", "you@company.com"), ("Message", "How can we help?")]) + btns(("Send message", "")) + '</div><div class="card"><h3>Find us</h3><div class="hero" style="padding:0"><div class="img" style="height:260px;grid-column:1/3"></div></div></div></div></div>')

# ---------- Auth
for f, t, b in [
 ("02-auth/login.html", "Sign in", f'<div class="auth"><h1>Welcome back</h1><p class="avatar-cap" style="margin-bottom:20px">Sign in to your workspace</p>{form([("Email", "name@company.com"), ("Password", "••••••••")])}{btns(("Sign in", ""))}<br><br>{btns(("Use biometrics", "ghost"))}<p style="margin-top:16px">New supplier? <b>Register</b></p></div>'),
 ("02-auth/register-supplier.html", "Supplier registration", f'<div class="auth" style="width:640px"><h1>Supplier registration</h1><br>{steps(["Company", "Contact", "Documents", "Review"], 1)}{form([("Company name", "Al Noor Trading"), ("Commercial registration", "CR-0000000"), ("Contact email", "sales@alnoor.com"), ("Phone", "+966 5x xxx xxxx")])}{btns(("Continue", ""), ("Back", "ghost"))}</div>'),
 ("02-auth/registration-pending.html", "Registration pending", f'<div class="auth"><h1>Thank you</h1><p style="margin:10px 0 20px">Your registration is under admin review. You will be notified by email.</p>{pill("Pending review", "warn")}</div>'),
 ("02-auth/access-denied.html", "Access denied", f'<div class="auth"><h1>Access denied</h1><p style="margin:10px 0 20px">Your role cannot open this page.</p>{btns(("Back to workspace", ""))}</div>')]:
    save(f, t, f'<div class="center">{b}</div>')

# ---------- Department
app("03-department/dashboard.html", "Department", "Dashboard", "Department dashboard", "Your purchase requests at a glance",
    kpis([("12", "Open requests"), ("5", "In tender"), ("3", "Awaiting approval"), ("28", "Completed")]) + '<div class="grid c2"><div>' + card("Requests by month", chart()) + card("Recent requests", table(["Ref", "Item", "Status"], [["PR-104", "Laptops x20", pill("In tender", "info")], ["PR-103", "Office chairs", pill("Approved", "ok")], ["PR-102", "Printer toner", pill("Pending", "warn")]])) + '</div><div>' + card("Quick action", btns(("New request", "alt"))) + card("Activity", '<div class="tl"><div>PR-104 sent to sourcing</div><div>PR-103 approved</div><div>PR-102 drafted</div></div>') + '</div></div>')
app("03-department/request-form.html", "Department", "New request", "New purchase request", "Describe what the department needs",
    card("Request details", form([("Title", "Laptops for finance team"), ("Category", "IT equipment"), ("Quantity", "20"), ("Estimated budget", "SAR 80,000"), ("Needed by", "2026-12-30"), ("Justification", "Replace aging devices…")]) + btns(("Submit request", ""), ("Save draft", "ghost"))))
app("03-department/request-status.html", "Department", "Request status", "Request PR-104", "Track the full tender journey",
    steps(["Request", "Sourcing review", "Tender open", "Evaluation", "Committee", "Award"], 3) + '<div class="grid c2"><div>' + card("Details", form([("Item", "Laptops x20"), ("Budget", "SAR 80,000"), ("Officer", "Sara A.")])) + '</div><div>' + card("Timeline", '<div class="tl"><div>Tender published</div><div>3 bids received</div><div>Evaluation started</div></div>') + '</div></div>')
app("03-department/requests-list.html", "Department", "Purchase requests", "Purchase requests", "All requests for your department",
    card("Requests", table(["Ref", "Title", "Date", "Status", ""], [[f"PR-10{i}", t, f"2026-11-0{i}", pill(s, k), "View"] for i, (t, s, k) in enumerate([("Laptops", "In tender", "info"), ("Chairs", "Approved", "ok"), ("Toner", "Pending", "warn"), ("Servers", "Rejected", "bad")], 1)])))

# ---------- Sourcing officer
app("04-sourcing-officer/overview.html", "Sourcing Officer", "Overview", "Sourcing overview", "Requests, tenders and offers",
    kpis([("9", "New requests"), ("6", "Open tenders"), ("14", "Offers received"), ("4", "To award")]) + '<div class="grid c2"><div>' + card("Sourcing cycle", steps(["Request", "Tender", "Invite", "Offers", "Evaluate", "Award"], 2)) + card("Tenders", table(["Ref", "Title", "Closes", "Offers"], [["T-210", "Laptops", "Nov 28", "5"], ["T-209", "Chairs", "Nov 25", "3"]])) + '</div><div>' + card("Messages", '<div class="msg">Supplier asked about specs</div><div class="msg me">Clarification sent</div>') + '</div></div>')
app("04-sourcing-officer/requests.html", "Sourcing Officer", "Purchase requests", "Purchase requests", "Incoming from departments",
    card("Inbox", table(["Ref", "Department", "Title", "Budget", ""], [["PR-104", "Finance", "Laptops", "80,000", btns(("Accept", ""))], ["PR-105", "HR", "Training", "25,000", btns(("Review", "ghost"))]])))
app("04-sourcing-officer/tenders.html", "Sourcing Officer", "My tenders", "My tenders", "Tenders you manage",
    card("Tenders", table(["Ref", "Title", "Status", "Closing", "Offers"], [["T-210", "Laptops", pill("Open", "ok"), "Nov 28", "5"], ["T-209", "Chairs", pill("Evaluation", "info"), "Nov 25", "3"], ["T-208", "Toner", pill("Draft", "warn"), "—", "0"]])) + btns(("Create tender", "alt")))
app("04-sourcing-officer/tender-form.html", "Sourcing Officer", "My tenders", "Create tender", "Define scope, criteria and invitations",
    steps(["Basics", "Items", "Criteria", "Suppliers", "Publish"], 2) + card("Basics", form([("Title", "Laptops"), ("Closing date", "2026-11-28")])) + card("Evaluation criteria", table(["Criterion", "Weight"], [["Price", "40%"], ["Quality", "30%"], ["Delivery", "20%"], ["Support", "10%"]])) + btns(("Next", ""), ("Save draft", "ghost")))
app("04-sourcing-officer/offers-comparison.html", "Sourcing Officer", "My tenders", "Offer comparison", "Tender T-210 · AI assistant is advisory only",
    card("Bids", table(["Supplier", "Price", "Delivery", "Score", "Compliance"], [["Al Noor", "78,000", "14d", "86", pill("Compliant", "ok")], ["Gulf IT", "81,500", "10d", "82", pill("Compliant", "ok")], ["TechPlus", "74,000", "30d", "61", pill("Missing doc", "bad")]])) + card("AI bid analysis (advisory)", '<p>Al Noor offers best value. Final decision belongs to the committee.</p>'))
app("04-sourcing-officer/messages.html", "Sourcing Officer", "Messages", "Messages", "Clarifications and chats",
    '<div class="grid c2"><div>' + card("Conversation", '<div class="msg">Is warranty included?</div><div class="msg me">Yes, 3 years.</div><div class="msg">Thanks.</div>') + '</div><div>' + card("Threads", table(["Supplier", "Tender"], [["Al Noor", "T-210"], ["Gulf IT", "T-210"]])) + '</div></div>')

# ---------- Manager
app("05-sourcing-manager/overview.html", "Sourcing Manager", "Overview", "Manager overview", "Approvals and performance",
    kpis([("5", "Awaiting approval"), ("11", "Active tenders"), ("92%", "On time"), ("SAR 1.2M", "Awarded YTD")]) + card("Approvals", table(["Ref", "Title", "Officer", "Value", ""], [["T-210", "Laptops", "Sara", "78,000", btns(("Approve", ""), ("Return", "ghost"))]])) + card("Monthly spend", chart()))
app("05-sourcing-manager/details.html", "Sourcing Manager", "Tenders", "Tender details", "T-210 · Laptops",
    steps(["Request", "Tender", "Offers", "Evaluation", "Committee", "Award"], 3) + '<div class="grid c2"><div>' + card("Summary", form([("Officer", "Sara A."), ("Budget", "SAR 80,000"), ("Best offer", "SAR 78,000")])) + '</div><div>' + card("Decision", btns(("Approve", ""), ("Reject", "bad"))) + '</div></div>')

# ---------- Evaluation committee
app("06-evaluation-committee/dashboard.html", "Evaluation Committee", "Dashboard", "Evaluation dashboard", "Tenders assigned to you",
    kpis([("4", "Assigned"), ("2", "In progress"), ("1", "Submitted"), ("1", "Overdue")]) + card("Tenders", table(["Tender", "Bids", "Deadline", "Status", ""], [["T-210", "5", "Nov 30", pill("In progress", "warn"), btns(("Evaluate", ""))], ["T-209", "3", "Nov 26", pill("Submitted", "ok"), "View"]])))
app("06-evaluation-committee/evaluate.html", "Evaluation Committee", "Tenders to evaluate", "Evaluate bids", "T-210 · score each criterion",
    card("Scores — Al Noor", table(["Criterion", "Weight", "Score (0-10)", "Notes"], [["Price", "40%", "9", "Lowest compliant"], ["Quality", "30%", "8", "—"], ["Delivery", "20%", "7", "14 days"], ["Support", "10%", "9", "—"]])) + btns(("Submit evaluation", ""), ("Save draft", "ghost")))

# ---------- Top committee
app("07-top-committee/dashboard.html", "Top Committee", "Dashboard", "Top committee", "Tenders awaiting decision",
    kpis([("3", "Awaiting"), ("14", "Decided"), ("2", "Returned"), ("1", "Meeting today")]) + card("Awaiting decision", table(["Tender", "Recommended", "Value", ""], [["T-210", "Al Noor", "78,000", btns(("Open", "alt"))]])))
app("07-top-committee/meeting.html", "Top Committee", "Meeting", "Committee meeting", "T-210 · live voting",
    '<div class="grid c2"><div>' + card("Evaluation summary", table(["Supplier", "Avg score", "Price"], [["Al Noor", "86", "78,000"], ["Gulf IT", "82", "81,500"]])) + card("Votes", table(["Member", "Vote"], [["Member A", pill("Approve", "ok")], ["Member B", pill("Return", "warn")], ["Member C", pill("Waiting", "info")]])) + '</div><div>' + card("Cast your vote", btns(("Approve", ""), ("Return", "ghost"), ("Reject", "bad"))) + '</div></div>')
app("07-top-committee/history.html", "Top Committee", "History", "Decision history", "Past committee outcomes",
    card("History", table(["Tender", "Outcome", "Date"], [["T-205", pill("Approved", "ok"), "Oct 30"], ["T-201", pill("Rejected", "bad"), "Oct 12"], ["T-199", pill("Returned", "warn"), "Sep 28"]])))
app("07-top-committee/statistics.html", "Top Committee", "Statistics", "Statistics", "Decisions and spend",
    kpis([("34", "Decisions"), ("82%", "Approval rate"), ("SAR 3.4M", "Total awarded"), ("6d", "Avg. time")]) + card("Decisions per month", chart()))

# ---------- Supplier
app("08-supplier/dashboard.html", "Supplier", "Dashboard", "Supplier dashboard", "Invitations and offers",
    kpis([("3", "New invitations"), ("5", "Submitted offers"), ("2", "Awarded"), ("1", "Question open")]) + card("Open invitations", table(["Tender", "Closes", "Status", ""], [["T-210 Laptops", "Nov 28", pill("Open", "ok"), btns(("Submit offer", "alt"))]])))
app("08-supplier/invitation.html", "Supplier", "Invitations", "Invitation T-210", "Laptops x20",
    '<div class="grid c2"><div>' + card("Tender brief", form([("Scope", "20 laptops, 16GB RAM"), ("Closing", "Nov 28"), ("Delivery", "Riyadh")])) + '</div><div>' + card("Actions", btns(("Accept & bid", ""), ("Decline", "ghost")) + "<br><br>" + btns(("Ask a question", "ghost"))) + '</div></div>')
app("08-supplier/offer.html", "Supplier", "Invitations", "Submit offer", "T-210",
    steps(["Items", "Prices", "Documents", "Review"], 1) + card("Offer", form([("Unit price", "SAR 3,900"), ("Total", "SAR 78,000"), ("Delivery days", "14"), ("Validity", "60 days"), ("Attachments", "datasheet.pdf, CR.pdf")]) + btns(("Submit offer", ""), ("Save draft", "ghost"))))
app("08-supplier/offers.html", "Supplier", "My offers", "My offers", "History of your bids",
    card("Offers", table(["Tender", "Total", "Submitted", "Status"], [["T-210", "78,000", "Nov 15", pill("Submitted", "info")], ["T-205", "42,000", "Oct 02", pill("Awarded", "ok")], ["T-199", "15,000", "Sep 10", pill("Not selected", "bad")]])))
app("08-supplier/questions.html", "Supplier", "Questions", "Questions", "Clarifications with sourcing",
    card("Thread", '<div class="msg">Is installation required?</div><div class="msg me">Yes, onsite.</div>'))
app("08-supplier/notifications.html", "Supplier", "Notifications", "Notifications", "Latest updates",
    card("Inbox", table(["When", "Message"], [["Today", "New invitation T-211"], ["Yesterday", "Offer T-210 received"]])))

# ---------- Admin
app("09-admin/overview.html", "Admin", "Overview", "Admin overview", "System health and queues",
    kpis([("86", "Users"), ("4", "Pending suppliers"), ("12", "Departments"), ("9", "Contact messages")]) + card("Pending supplier registrations", table(["Company", "Date", ""], [["Al Noor", "Nov 14", btns(("Approve", ""), ("Reject", "ghost"))]])))
app("09-admin/users.html", "Admin", "Users", "Users", "Manage accounts and roles",
    card("Users", table(["Name", "Email", "Role", "Department", "Status", ""], [["Sara A.", "sara@z.com", "Sourcing Officer", "Sourcing", pill("Active", "ok"), "Edit"], ["Omar K.", "omar@z.com", "Evaluator", "Finance", pill("Active", "ok"), "Edit"], ["Lina M.", "lina@z.com", "Department", "HR", pill("Locked", "bad"), "Reset"]])) + btns(("Create user", "alt")))
app("09-admin/user-form.html", "Admin", "Users", "Create user", "Role and department assignment",
    card("User", form([("Full name", "—"), ("Email", "—"), ("Role", "Department"), ("Department", "Finance"), ("Temporary password", "••••••••")]) + btns(("Create", ""), ("Cancel", "ghost"))))
app("09-admin/catalog.html", "Admin", "Categories", "Catalog", "Categories, departments and roles",
    '<div class="grid e2">' + card("Categories", table(["Name", "Items"], [["IT equipment", "42"], ["Furniture", "18"], ["Services", "27"]])) + card("Departments", table(["Name", "Head"], [["Finance", "Omar K."], ["HR", "Lina M."]])) + '</div>')
app("09-admin/suppliers.html", "Admin", "Suppliers", "Suppliers", "Registered companies",
    card("Suppliers", table(["Company", "CR", "Status"], [["Al Noor", "CR-123", pill("Approved", "ok")], ["Gulf IT", "CR-456", pill("Approved", "ok")], ["NewCo", "CR-789", pill("Pending", "warn")]])))
app("09-admin/committees.html", "Admin", "Committees", "Committees", "Evaluation and top committee members",
    card("Committee T-210", table(["Member", "Position", "Weight"], [["Omar K.", "Chair", "30%"], ["Dana S.", "Member", "20%"]])))
app("09-admin/audit-logs.html", "Admin", "Audit logs", "Audit logs", "Every action recorded",
    card("Logs", table(["Time", "User", "Action"], [["10:02", "Sara A.", "Published T-210"], ["09:40", "Admin", "Approved Al Noor"], ["09:12", "Omar K.", "Submitted evaluation"]])))
app("09-admin/contact-messages.html", "Admin", "Contact messages", "Contact messages", "From the public site",
    card("Inbox", table(["From", "Subject", "Date"], [["a@x.com", "Demo request", "Nov 15"], ["b@y.com", "Support", "Nov 14"]])))

# ---------- Shared
app("10-shared/reports.html", "Sourcing Manager", "Reports", "Reports", "Generate and export",
    '<div class="grid c3">' + "".join(card(t, "<p>" + d + "</p><br>" + btns(("Open", "alt"), ("Export PDF", "ghost"))) for t, d in [("Tender report", "Per tender summary"), ("Spend report", "By category"), ("Supplier report", "Performance")]) + '</div>' + card("Preview", chart()))
app("10-shared/notifications.html", "Department", "Notifications", "Notifications", "All your alerts",
    card("Inbox", table(["When", "Message", ""], [["Today", "PR-104 accepted by sourcing", "Mark read"], ["Yesterday", "PR-103 approved", "Mark read"]])))
app("10-shared/cycle-chat.html", "Sourcing Officer", "Messages", "Cycle chat", "Chat across the tender cycle",
    card("T-210 thread", '<div class="msg">Specs updated.</div><div class="msg me">Noted, thanks.</div><br><div class="field"><div>Type a message…</div></div>'))

# ---------- index
rows = "".join(f'<tr><td>{f.split("/")[0]}</td><td><a href="{f}">{t}</a></td></tr>' for f, t in SCREENS)
open(os.path.join(OUT, "index.html"), "w", encoding="utf-8").write(f'<!doctype html><meta charset="utf-8"><title>Zaytouna mockups</title><style>{CSS}body{{width:auto;padding:40px}}</style><h1>Zaytouna STMS — Figma mockups ({len(SCREENS)} screens)</h1><br><div class="card"><table>{rows}</table></div>')
print(len(SCREENS), "screens")
