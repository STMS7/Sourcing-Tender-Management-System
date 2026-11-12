using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
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
}
