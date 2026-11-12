namespace Sourcing_Tender_Management_System.Models;

public enum ProcurementRequestStatus { Draft, Submitted, Approved, Rejected, ConvertedToTender }
public enum TenderStatus { Draft, Published, Closed, UnderEvaluation, Awarded, Cancelled }
public enum BidStatus { Draft, Submitted, Withdrawn, Disqualified, UnderEvaluation, Accepted, Rejected }
public enum ApprovalStatus { Pending, Approved, Rejected, ReturnedForRevision }
public enum ContractStatus { Draft, Active, Completed, Terminated, Expired }
public enum PurchaseOrderStatus { Draft, Issued, PartiallyDelivered, Delivered, Cancelled }
public enum DeliveryStatus { Pending, PartiallyReceived, Received, Rejected }
public enum NotificationType { Information, ActionRequired, Warning, Success }
