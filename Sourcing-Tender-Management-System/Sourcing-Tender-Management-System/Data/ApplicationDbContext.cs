using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ProcurementRequest> ProcurementRequests => Set<ProcurementRequest>();
    public DbSet<ProcurementRequestItem> ProcurementRequestItems => Set<ProcurementRequestItem>();
    public DbSet<Tender> Tenders => Set<Tender>();
    public DbSet<TenderItem> TenderItems => Set<TenderItem>();
    public DbSet<TenderDocument> TenderDocuments => Set<TenderDocument>();
    public DbSet<TenderCommitteeMember> TenderCommitteeMembers => Set<TenderCommitteeMember>();
    public DbSet<EvaluationCriterion> EvaluationCriteria => Set<EvaluationCriterion>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierCategory> SupplierCategories => Set<SupplierCategory>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<BidItem> BidItems => Set<BidItem>();
    public DbSet<BidDocument> BidDocuments => Set<BidDocument>();
    public DbSet<BidEvaluation> BidEvaluations => Set<BidEvaluation>();
    public DbSet<BidEvaluationScore> BidEvaluationScores => Set<BidEvaluationScore>();
    public DbSet<Clarification> Clarifications => Set<Clarification>();
    public DbSet<ClarificationResponse> ClarificationResponses => Set<ClarificationResponse>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractMilestone> ContractMilestones => Set<ContractMilestone>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<DeliveryItem> DeliveryItems => Set<DeliveryItem>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Department>().HasIndex(x => x.Name).IsUnique();
        builder.Entity<Department>().HasIndex(x => x.Code).IsUnique().HasFilter("[Code] IS NOT NULL");
        builder.Entity<Category>().HasIndex(x => new { x.ParentCategoryId, x.Name }).IsUnique();
        builder.Entity<Category>()
            .HasOne(x => x.ParentCategory)
            .WithMany(x => x.Subcategories)
            .HasForeignKey(x => x.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ProcurementRequest>().HasIndex(x => x.RequestNumber).IsUnique();
        builder.Entity<ProcurementRequest>()
            .HasOne(x => x.Department)
            .WithMany(x => x.ProcurementRequests)
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ProcurementRequest>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Tender>().HasIndex(x => x.TenderNumber).IsUnique();
        builder.Entity<Tender>()
            .HasOne(x => x.ProcurementRequest)
            .WithMany(x => x.Tenders)
            .HasForeignKey(x => x.ProcurementRequestId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Tender>()
            .HasOne(x => x.Category)
            .WithMany(x => x.Tenders)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Tender>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<TenderCommitteeMember>().HasIndex(x => new { x.TenderId, x.UserId }).IsUnique();
        builder.Entity<TenderCommitteeMember>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<EvaluationCriterion>().HasIndex(x => new { x.TenderId, x.Name }).IsUnique();

        builder.Entity<Supplier>().HasIndex(x => x.RegistrationNumber).IsUnique();
        builder.Entity<Supplier>().HasIndex(x => x.Email).IsUnique();
        builder.Entity<SupplierCategory>().HasKey(x => new { x.SupplierId, x.CategoryId });
        builder.Entity<SupplierCategory>()
            .HasOne(x => x.Supplier)
            .WithMany(x => x.SupplierCategories)
            .HasForeignKey(x => x.SupplierId);
        builder.Entity<SupplierCategory>()
            .HasOne(x => x.Category)
            .WithMany(x => x.SupplierCategories)
            .HasForeignKey(x => x.CategoryId);

        builder.Entity<Bid>().HasIndex(x => x.BidNumber).IsUnique();
        builder.Entity<Bid>().HasIndex(x => new { x.TenderId, x.SupplierId }).IsUnique();
        builder.Entity<Bid>()
            .HasOne(x => x.Tender)
            .WithMany(x => x.Bids)
            .HasForeignKey(x => x.TenderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Bid>()
            .HasOne(x => x.Supplier)
            .WithMany(x => x.Bids)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<BidItem>().HasIndex(x => new { x.BidId, x.TenderItemId }).IsUnique();
        builder.Entity<BidItem>()
            .HasOne(x => x.TenderItem)
            .WithMany(x => x.BidItems)
            .HasForeignKey(x => x.TenderItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<BidEvaluation>().HasIndex(x => new { x.BidId, x.EvaluatorUserId }).IsUnique();
        builder.Entity<BidEvaluation>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.EvaluatorUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<BidEvaluationScore>()
            .HasIndex(x => new { x.BidEvaluationId, x.EvaluationCriterionId }).IsUnique();
        builder.Entity<BidEvaluationScore>()
            .HasOne(x => x.EvaluationCriterion)
            .WithMany(x => x.Scores)
            .HasForeignKey(x => x.EvaluationCriterionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ClarificationResponse>().HasIndex(x => x.ClarificationId).IsUnique();
        builder.Entity<Clarification>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.AskedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Approval>().HasIndex(x => new { x.TenderId, x.Sequence }).IsUnique();
        builder.Entity<Approval>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.ApproverUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Contract>().HasIndex(x => x.ContractNumber).IsUnique();
        builder.Entity<Contract>().HasIndex(x => x.BidId).IsUnique();
        builder.Entity<Contract>()
            .HasOne(x => x.Bid)
            .WithOne()
            .HasForeignKey<Contract>(x => x.BidId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Contract>()
            .HasOne(x => x.Supplier)
            .WithMany(x => x.Contracts)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<PurchaseOrder>().HasIndex(x => x.PurchaseOrderNumber).IsUnique();
        builder.Entity<PurchaseOrderItem>()
            .HasOne(x => x.BidItem)
            .WithMany()
            .HasForeignKey(x => x.BidItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Delivery>().HasIndex(x => x.DeliveryNumber).IsUnique();
        builder.Entity<Delivery>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.ReceivedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<DeliveryItem>().HasIndex(x => new { x.DeliveryId, x.PurchaseOrderItemId }).IsUnique();
        builder.Entity<DeliveryItem>()
            .HasOne(x => x.PurchaseOrderItem)
            .WithMany(x => x.DeliveryItems)
            .HasForeignKey(x => x.PurchaseOrderItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Notification>().HasIndex(x => new { x.UserId, x.IsRead });
        builder.Entity<Notification>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<AuditLog>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.Entity<AuditLog>().HasIndex(x => new { x.EntityName, x.EntityId });
        builder.Entity<AuditLog>().HasIndex(x => x.CreatedAt);
    }
}
