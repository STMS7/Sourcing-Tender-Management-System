using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Models;
using Sourcing_Tender_Management_System.Models.TopCommittee;

namespace Sourcing_Tender_Management_System.Data.Seed;

/// <summary>
/// Development-only demo data for the Top Committee module, built on the shared workflow tables:
/// four standing committee members (CommitteePosition / TopCommittee role), and four tenders with a
/// consolidated EvaluationReport, compliance reviews, manager approvals and CommitteeVote rows.
/// Runs only when DevSeed:TopCommittee is true and DevSeed:Password is set (user-secrets).
/// Safe to run repeatedly: it does nothing once the demo data exists in the current format; demo
/// tenders in an older format (tender numbers starting with DEMO-TC) are replaced.
/// </summary>
public static class TopCommitteeDevSeeder
{
    private const string FirstDemoTenderNumber = "DEMO-TC-001";
    private const string DemoTenderPrefix = "DEMO-TC";
    private const string DemoRequestPrefix = "DEMO-PR";
    private const string ManagerApprovalLevel = "SourcingManager";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var password = config["DevSeed:Password"];
        if (!config.GetValue<bool>("DevSeed:TopCommittee") || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // DevSeed:Reseed = true rebuilds the demo tenders from scratch (for example after testing votes).
        var reseed = config.GetValue<bool>("DevSeed:Reseed");
        if (!reseed && await db.Tenders.AnyAsync(t => t.TenderNumber == FirstDemoTenderNumber && t.EvaluationReports.Any()))
        {
            return;
        }

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // The roles are created by the shared migrations; create them only if a database lacks them.
        foreach (var role in new[] { TopCommitteeConstants.RoleName, "ProcurementOfficer", "EvaluationCommittee" })
        {
            if (!await roles.RoleExistsAsync(role))
            {
                Ensure(await roles.CreateAsync(new IdentityRole(role)));
            }
        }

        var department = await db.Departments.FirstOrDefaultAsync(d => d.Code == "IT-DEMO")
            ?? db.Departments.Add(new Department { Name = "Information Technology (Demo)", Code = "IT-DEMO" }).Entity;
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Name == "IT Equipment (Demo)")
            ?? db.Categories.Add(new Category { Name = "IT Equipment (Demo)" }).Entity;
        await db.SaveChangesAsync();

        var positionNames = new[] { "Chairperson", "Finance Member", "Technical Member", "Legal Member" };
        var members = new List<ApplicationUser>();
        for (var i = 1; i <= TopCommitteeConstants.CommitteeSize; i++)
        {
            var member = await GetOrCreateUserAsync(users, $"topcommittee{i}@stms.local", $"Committee Member {i}", password, null, TopCommitteeConstants.RoleName);
            members.Add(member);

            if (!await db.CommitteePositions.AnyAsync(p => p.UserId == member.Id && p.CommitteeType == CommitteeType.Top))
            {
                db.CommitteePositions.Add(new CommitteePosition
                {
                    CommitteeType = CommitteeType.Top,
                    PositionName = positionNames[i - 1],
                    UserId = member.Id
                });
            }
        }

        var manager = await GetOrCreateUserAsync(users, "sourcing.manager@stms.local", "Demo Sourcing Manager", password, null, "ProcurementOfficer");
        var evaluator = await GetOrCreateUserAsync(users, "demo.evaluator@stms.local", "Demo Evaluator", password, null, "EvaluationCommittee");
        var requester = await GetOrCreateUserAsync(users, "it.requester@stms.local", "Demo IT Requester", password, department.Id, null);

        var suppliers = new[]
        {
            await GetOrCreateSupplierAsync(db, "Al-Nour Technology Co.", "DEMO-REG-1001", "sales@alnour.example"),
            await GetOrCreateSupplierAsync(db, "Petra Systems Ltd.", "DEMO-REG-1002", "bids@petra.example"),
            await GetOrCreateSupplierAsync(db, "Jordan Digital Supplies", "DEMO-REG-1003", "tenders@jds.example")
        };
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var ctx = new Context(db, department, category, requester, manager, evaluator, members);

        var tenders = new List<Tender>
        {
            // 1) Awaiting decision: one of four members already voted.
            BuildTender(ctx, "DEMO-TC-001", "Supply of 120 business laptops", 185_000m, TenderStatus.UnderEvaluation,
                "Business laptop, 16GB RAM, 512GB SSD", 120, now.AddDays(-40),
                [
                    new BidSeed(suppliers[0], 178_800m, 30, 88m, 84m, true, BidStatus.UnderEvaluation, "Best technical match, 3-year on-site warranty."),
                    new BidSeed(suppliers[1], 171_600m, 45, 76m, 91m, false, BidStatus.UnderEvaluation, "Lowest price but longer delivery and 1-year warranty."),
                    new BidSeed(suppliers[2], 192_000m, 21, 72m, 70m, false, BidStatus.UnderEvaluation, "Fast delivery, above the estimated budget.")
                ],
                "Al-Nour Technology has the highest combined score and stays within the estimated budget.",
                ApprovalStatus.Approved, "Recommend Al-Nour Technology: highest combined score and within budget.",
                [CommitteeDecision.Pending, CommitteeDecision.Approved, CommitteeDecision.Pending, CommitteeDecision.Pending],
                decidedAt: now.AddDays(-1)),

            // 2) Awaiting decision: nobody has voted yet.
            BuildTender(ctx, "DEMO-TC-002", "Data center cooling upgrade", 320_000m, TenderStatus.UnderEvaluation,
                "Precision cooling unit, 40kW", 4, now.AddDays(-25),
                [
                    new BidSeed(suppliers[1], 305_000m, 60, 86m, 78m, true, BidStatus.UnderEvaluation, "Meets all specifications, includes installation."),
                    new BidSeed(suppliers[2], 298_500m, 75, 80m, 81m, false, BidStatus.UnderEvaluation, "Slightly cheaper, installation priced separately.")
                ],
                "Scores are close; Petra Systems is recommended because installation is included in the price.",
                ApprovalStatus.Approved, "Scores are close; recommend Petra Systems because installation is included.",
                [CommitteeDecision.Pending, CommitteeDecision.Pending, CommitteeDecision.Pending, CommitteeDecision.Pending],
                decidedAt: null),

            // 3) History: approved unanimously and awarded.
            BuildTender(ctx, "DEMO-TC-003", "Core network switches replacement", 240_000m, TenderStatus.Awarded,
                "48-port managed core switch", 12, now.AddDays(-120),
                [
                    new BidSeed(suppliers[2], 229_000m, 35, 90m, 86m, true, BidStatus.Accepted, "Strongest technical offer with local support."),
                    new BidSeed(suppliers[0], 236_400m, 40, 82m, 81m, false, BidStatus.Rejected, "Compliant but more expensive.")
                ],
                "Jordan Digital Supplies is the strongest offer technically and financially.",
                ApprovalStatus.Approved, "Recommend Jordan Digital Supplies.",
                [CommitteeDecision.Approved, CommitteeDecision.Approved, CommitteeDecision.Approved, CommitteeDecision.Approved],
                decidedAt: now.AddDays(-90)),

            // 4) History: rejected by majority.
            BuildTender(ctx, "DEMO-TC-004", "Enterprise video wall for operations room", 410_000m, TenderStatus.Cancelled,
                "Video wall, 3x4 55-inch panels", 1, now.AddDays(-80),
                [
                    new BidSeed(suppliers[0], 455_000m, 50, 74m, 60m, true, BidStatus.Rejected, "Only compliant offer, 11% over budget.")
                ],
                "A single compliant bid was received, 11% above the estimated budget.",
                ApprovalStatus.Approved, "Single compliant bid; forwarded for committee decision because it exceeds the budget.",
                [CommitteeDecision.Rejected, CommitteeDecision.Rejected, CommitteeDecision.Approved, CommitteeDecision.Rejected],
                decidedAt: now.AddDays(-55))
        };

        await using var transaction = await db.Database.BeginTransactionAsync();

        await DeleteDemoTendersAsync(db);

        db.Tenders.AddRange(tenders);
        await db.SaveChangesAsync();

        // Tender.AwardedBidId points back to a bid, so it is set after the bids exist.
        foreach (var tender in tenders.Where(t => t.Status == TenderStatus.Awarded))
        {
            tender.AwardedBidId = tender.Bids.First(b => b.Status == BidStatus.Accepted).Id;
        }
        await db.SaveChangesAsync();

        await transaction.CommitAsync();
    }

    private sealed record Context(
        ApplicationDbContext Db, Department Department, Category Category, ApplicationUser Requester, ApplicationUser Manager,
        ApplicationUser Evaluator, List<ApplicationUser> Members);

    private sealed record BidSeed(
        Supplier Supplier, decimal Amount, int DeliveryDays, decimal TechnicalScore, decimal FinancialScore,
        bool Recommended, BidStatus Status, string Comment);

    private static Tender BuildTender(
        Context ctx, string number, string title, decimal estimatedValue, TenderStatus status, string itemName,
        decimal quantity, DateTime createdAt, BidSeed[] bids, string summary,
        ApprovalStatus managerStatus, string managerComment, CommitteeDecision[] votes, DateTime? decidedAt)
    {
        var tenderItem = new TenderItem { ItemName = itemName, Quantity = quantity, Unit = "pcs" };
        var criteria = new[]
        {
            new EvaluationCriterion { Name = "Technical compliance", Type = CriterionType.Technical, Weight = 40 },
            new EvaluationCriterion { Name = "Delivery and warranty", Type = CriterionType.Technical, Weight = 20 },
            new EvaluationCriterion { Name = "Price", Type = CriterionType.Financial, Weight = 40 }
        };

        var tender = new Tender
        {
            TenderNumber = number,
            Title = title,
            Description = $"Demo tender for the Top Committee module: {title}.",
            Category = ctx.Category,
            EstimatedValue = estimatedValue,
            Status = status,
            CreatedByUserId = ctx.Manager.Id,
            CreatedAt = createdAt,
            PublishedAt = createdAt.AddDays(1),
            SubmissionDeadline = createdAt.AddDays(15),
            OpeningDate = createdAt.AddDays(16),
            TechnicalWeight = 60,
            FinancialWeight = 40,
            EvaluationCriteriaLockedAt = createdAt.AddDays(1),
            RequiresTopCommittee = true,
            ProcurementRequest = new ProcurementRequest
            {
                RequestNumber = number.Replace(DemoTenderPrefix, DemoRequestPrefix),
                Title = title,
                Description = $"Requested by the IT department: {title}.",
                Department = ctx.Department,
                RequestedByUserId = ctx.Requester.Id,
                RequestedAt = createdAt.AddDays(-10),
                RequiredByDate = createdAt.AddDays(90),
                EstimatedBudget = estimatedValue,
                Priority = RequestPriority.High,
                Status = ProcurementRequestStatus.ConvertedToTender,
                Items = { new ProcurementRequestItem { ItemName = itemName, Quantity = quantity, Unit = "pcs", EstimatedUnitPrice = Math.Round(estimatedValue / quantity, 2) } }
            },
            Items = { tenderItem }
        };

        foreach (var criterion in criteria)
        {
            tender.EvaluationCriteria.Add(criterion);
        }

        var report = new EvaluationReport
        {
            Round = 1,
            PreparedByUser = ctx.Evaluator,
            Summary = summary,
            CreatedAt = createdAt.AddDays(21),
            SubmittedAt = createdAt.AddDays(22)
        };
        tender.EvaluationReports.Add(report);

        var bidIndex = 1;
        foreach (var seed in bids)
        {
            var weighted = Math.Round(
                seed.TechnicalScore * tender.TechnicalWeight / 100 + seed.FinancialScore * tender.FinancialWeight / 100, 2);

            var evaluation = new BidEvaluation
            {
                EvaluatorUserId = ctx.Evaluator.Id,
                TotalScore = weighted,
                Comments = seed.Comment,
                IsRecommended = seed.Recommended,
                EvaluatedAt = createdAt.AddDays(20)
            };
            foreach (var criterion in criteria)
            {
                evaluation.Scores.Add(new BidEvaluationScore
                {
                    EvaluationCriterion = criterion,
                    Score = criterion.Type == CriterionType.Technical ? seed.TechnicalScore : seed.FinancialScore
                });
            }

            var bid = new Bid
            {
                Supplier = seed.Supplier,
                BidNumber = $"{number}-B{bidIndex++}",
                TotalAmount = seed.Amount,
                DeliveryDays = seed.DeliveryDays,
                SubmittedAt = createdAt.AddDays(12),
                ValidUntil = createdAt.AddDays(120),
                Status = seed.Status,
                Items = { new BidItem { TenderItem = tenderItem, UnitPrice = Math.Round(seed.Amount / quantity, 2), TotalPrice = seed.Amount } },
                Evaluations = { evaluation }
            };
            tender.Bids.Add(bid);

            report.Bids.Add(new EvaluationReportBid
            {
                Bid = bid,
                TechnicalScore = seed.TechnicalScore,
                FinancialScore = seed.FinancialScore,
                WeightedTotal = weighted,
                IsCompliant = true,
                Notes = seed.Comment
            });

            if (seed.Recommended)
            {
                report.RecommendedBid = bid;
                report.RecommendationReason = seed.Comment;
            }

            // BidComplianceReview has no collection on Bid, so it is added through the context.
            ctx.Db.BidComplianceReviews.Add(new BidComplianceReview
            {
                Bid = bid,
                ReviewedByUserId = ctx.Evaluator.Id,
                Decision = ComplianceDecision.Compliant,
                Reason = "All mandatory documents and requirements were provided.",
                ReviewedAt = createdAt.AddDays(18),
                Round = 1
            });
        }

        // Stage history: the tender went from the manager's review to the Top Committee (and was awarded).
        ctx.Db.WorkflowTransitions.Add(new WorkflowTransition
        {
            Tender = tender,
            Round = 1,
            FromStage = WorkflowStage.ManagerReview,
            ToStage = WorkflowStage.TopCommittee,
            Action = WorkflowAction.Forwarded,
            PerformedByUserId = ctx.Manager.Id,
            Reason = "High-value tender forwarded to the Top Committee.",
            CreatedAt = createdAt.AddDays(24)
        });
        if (status == TenderStatus.Awarded)
        {
            ctx.Db.WorkflowTransitions.Add(new WorkflowTransition
            {
                Tender = tender,
                Round = 1,
                FromStage = WorkflowStage.TopCommittee,
                ToStage = WorkflowStage.Awarded,
                Action = WorkflowAction.Awarded,
                PerformedByUserId = ctx.Members[0].Id,
                Reason = "Approved by the Top Committee.",
                CreatedAt = decidedAt ?? createdAt.AddDays(30)
            });
        }

        tender.Approvals.Add(new Approval
        {
            EvaluationReport = report,
            Bid = report.RecommendedBid,
            Round = 1,
            ApproverUserId = ctx.Manager.Id,
            ApprovalLevel = ManagerApprovalLevel,
            Sequence = 1,
            Status = managerStatus,
            Comments = managerComment,
            DecidedAt = createdAt.AddDays(24)
        });

        for (var i = 0; i < ctx.Members.Count; i++)
        {
            var voted = votes[i] != CommitteeDecision.Pending;
            report.Votes.Add(new CommitteeVote
            {
                CommitteeType = CommitteeType.Top,
                MemberUserId = ctx.Members[i].Id,
                Decision = votes[i],
                Reason = voted ? $"Demo vote: {votes[i]}." : null,
                DecidedAt = voted ? decidedAt : null
            });
        }

        return tender;
    }

    /// <summary>Removes the demo tenders (and everything that hangs off them) so they can be recreated.</summary>
    private static async Task DeleteDemoTendersAsync(ApplicationDbContext db)
    {
        // Notifications and audit entries produced by the demo users' votes (only the @stms.local demo accounts).
        await db.Notifications.Where(n => n.User.Email!.EndsWith("@stms.local")).ExecuteDeleteAsync();
        await db.AuditLogs
            .Where(a => a.User != null && a.User.Email!.EndsWith("@stms.local") &&
                        (a.EntityName == nameof(CommitteeVote) || a.Action == "CommitteeDecision"))
            .ExecuteDeleteAsync();

        var tenderIds = await db.Tenders
            .Where(t => t.TenderNumber.StartsWith(DemoTenderPrefix))
            .Select(t => t.Id)
            .ToListAsync();
        if (tenderIds.Count == 0)
        {
            return;
        }

        await db.CommitteeVotes.Where(v => tenderIds.Contains(v.EvaluationReport.TenderId)).ExecuteDeleteAsync();
        await db.Approvals.Where(a => tenderIds.Contains(a.TenderId)).ExecuteDeleteAsync();
        await db.EvaluationReportBids.Where(b => tenderIds.Contains(b.EvaluationReport.TenderId)).ExecuteDeleteAsync();
        await db.EvaluationReports.Where(r => tenderIds.Contains(r.TenderId)).ExecuteDeleteAsync();
        await db.Tenders.Where(t => tenderIds.Contains(t.Id)).ExecuteUpdateAsync(s => s.SetProperty(t => t.AwardedBidId, (int?)null));
        await db.BidComplianceReviews.Where(c => tenderIds.Contains(c.Bid.TenderId)).ExecuteDeleteAsync();
        await db.BidEvaluationScores.Where(s => tenderIds.Contains(s.BidEvaluation.Bid.TenderId)).ExecuteDeleteAsync();
        await db.BidEvaluations.Where(e => tenderIds.Contains(e.Bid.TenderId)).ExecuteDeleteAsync();
        await db.BidItems.Where(i => tenderIds.Contains(i.Bid.TenderId)).ExecuteDeleteAsync();
        await db.Bids.Where(b => tenderIds.Contains(b.TenderId)).ExecuteDeleteAsync();
        await db.WorkflowTransitions.Where(w => w.TenderId != null && tenderIds.Contains(w.TenderId.Value)).ExecuteDeleteAsync();
        await db.EvaluationCriteria.Where(c => tenderIds.Contains(c.TenderId)).ExecuteDeleteAsync();
        await db.TenderItems.Where(i => tenderIds.Contains(i.TenderId)).ExecuteDeleteAsync();
        await db.TenderDocuments.Where(d => tenderIds.Contains(d.TenderId)).ExecuteDeleteAsync();
        await db.TenderCommitteeMembers.Where(m => tenderIds.Contains(m.TenderId)).ExecuteDeleteAsync();
        await db.Tenders.Where(t => tenderIds.Contains(t.Id)).ExecuteDeleteAsync();

        var requestIds = await db.ProcurementRequests
            .Where(r => r.RequestNumber.StartsWith(DemoRequestPrefix))
            .Select(r => r.Id)
            .ToListAsync();
        await db.ProcurementRequestReviews.Where(r => requestIds.Contains(r.ProcurementRequestId)).ExecuteDeleteAsync();
        await db.ProcurementRequestItems.Where(i => requestIds.Contains(i.ProcurementRequestId)).ExecuteDeleteAsync();
        await db.ProcurementRequests.Where(r => requestIds.Contains(r.Id)).ExecuteDeleteAsync();
    }

    private static async Task<Supplier> GetOrCreateSupplierAsync(
        ApplicationDbContext db, string name, string registrationNumber, string email)
    {
        var existing = await db.Suppliers.FirstOrDefaultAsync(s => s.RegistrationNumber == registrationNumber);
        if (existing is not null)
        {
            return existing;
        }

        return db.Suppliers.Add(new Supplier
        {
            CompanyName = name,
            RegistrationNumber = registrationNumber,
            Email = email,
            RegistrationStatus = SupplierRegistrationStatus.Approved
        }).Entity;
    }

    private static async Task<ApplicationUser> GetOrCreateUserAsync(
        UserManager<ApplicationUser> users, string email, string fullName, string password, int? departmentId, string? role)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                DepartmentId = departmentId
            };
            Ensure(await users.CreateAsync(user, password));
        }

        if (role is not null && !await users.IsInRoleAsync(user, role))
        {
            Ensure(await users.AddToRoleAsync(user, role));
        }

        return user;
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Top Committee demo seeding failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
