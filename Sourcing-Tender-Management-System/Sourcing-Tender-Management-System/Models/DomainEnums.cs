namespace Sourcing_Tender_Management_System.Models;

public enum ProcurementRequestStatus { Draft, Submitted, Approved, Rejected, ConvertedToTender }
public enum TenderStatus { Draft, Published, Closed, UnderEvaluation, Awarded, Cancelled }
public enum BidStatus { Draft, Submitted, Withdrawn, Disqualified, UnderEvaluation, Accepted, Rejected }
public enum ApprovalStatus { Pending, Approved, Rejected, ReturnedForRevision }
public enum ContractStatus { Draft, Active, Completed, Terminated, Expired }
public enum PurchaseOrderStatus { Draft, Issued, PartiallyDelivered, Delivered, Cancelled }
public enum DeliveryStatus { Pending, PartiallyReceived, Received, Rejected }
public enum NotificationType { Information, ActionRequired, Warning, Success }
public enum SupplierRegistrationStatus { Pending, Approved, Rejected, Suspended }
public enum RequestPriority { Low, Normal, High, Urgent }
public enum RequestReviewDecision { Approved, Rejected, Returned }
public enum AssignmentKind { ProcurementRequest, Tender }
public enum InvitationStatus { Invited, Accepted, Declined, Expired }
public enum ComplianceDecision { Pending, Compliant, NonCompliant, PendingClarification }
public enum CriterionType { Technical, Financial }
public enum CommitteeType { Evaluation, Top }
public enum CommitteeDecision { Pending, Approved, Rejected, ReturnedForRevision }
public enum WorkflowStage { RequestReview, Sourcing, SupplierBidding, Compliance, Evaluation, ManagerReview, TopCommittee, Awarded, Closed }
public enum WorkflowAction { Submitted, Assigned, Approved, Rejected, Returned, ClarificationRequested, ClarificationReceived, Forwarded, Awarded }
public enum MessageStatus { Sent, Read, Closed }
