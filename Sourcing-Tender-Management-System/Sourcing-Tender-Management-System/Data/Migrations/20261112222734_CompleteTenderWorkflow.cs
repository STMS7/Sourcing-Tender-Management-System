using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sourcing_Tender_Management_System.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompleteTenderWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AwardedBidId",
                table: "Tenders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EvaluationCriteriaLockedAt",
                table: "Tenders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinancialWeight",
                table: "Tenders",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 40m);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresTopCommittee",
                table: "Tenders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "TechnicalWeight",
                table: "Tenders",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 60m);

            migrationBuilder.AddColumn<int>(
                name: "CommitteeType",
                table: "TenderCommitteeMembers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RegistrationStatus",
                table: "Suppliers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReviewReason",
                table: "Suppliers",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByUserId",
                table: "Suppliers",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "ProcurementRequests",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "ProcurementRequests",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "ProcurementRequestItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "EvaluationCriteria",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BidId",
                table: "Approvals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EvaluationReportId",
                table: "Approvals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Round",
                table: "Approvals",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("UPDATE [Suppliers] SET [RegistrationStatus] = 1 WHERE [IsApproved] = 1;");
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [NormalizedName] = N'SOURCINGOFFICER')
                    INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES (N'ROLE-SOURCING-OFFICER', N'SourcingOfficer', N'SOURCINGOFFICER', N'ROLE-SOURCING-OFFICER');
                IF NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [NormalizedName] = N'SOURCINGMANAGER')
                    INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES (N'ROLE-SOURCING-MANAGER', N'SourcingManager', N'SOURCINGMANAGER', N'ROLE-SOURCING-MANAGER');
                IF NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [NormalizedName] = N'FINANCIALREPRESENTATIVE')
                    INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES (N'ROLE-FINANCIAL-REP', N'FinancialRepresentative', N'FINANCIALREPRESENTATIVE', N'ROLE-FINANCIAL-REP');
                IF NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [NormalizedName] = N'TECHNICALREPRESENTATIVE')
                    INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES (N'ROLE-TECHNICAL-REP', N'TechnicalRepresentative', N'TECHNICALREPRESENTATIVE', N'ROLE-TECHNICAL-REP');
                IF NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [NormalizedName] = N'DEPARTMENTREPRESENTATIVE')
                    INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp]) VALUES (N'ROLE-DEPARTMENT-REP', N'DepartmentRepresentative', N'DEPARTMENTREPRESENTATIVE', N'ROLE-DEPARTMENT-REP');
                """);

            migrationBuilder.CreateTable(
                name: "BidComplianceReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BidId = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Round = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BidComplianceReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BidComplianceReviews_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BidComplianceReviews_Bids_BidId",
                        column: x => x.BidId,
                        principalTable: "Bids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommitteePositions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommitteeType = table.Column<int>(type: "int", nullable: false),
                    PositionName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitteePositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommitteePositions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenderId = table.Column<int>(type: "int", nullable: false),
                    Round = table.Column<int>(type: "int", nullable: false),
                    PreparedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RecommendationReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RecommendedBidId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationReports_AspNetUsers_PreparedByUserId",
                        column: x => x.PreparedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationReports_Bids_RecommendedBidId",
                        column: x => x.RecommendedBidId,
                        principalTable: "Bids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationReports_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcessMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcurementRequestId = table.Column<int>(type: "int", nullable: true),
                    TenderId = table.Column<int>(type: "int", nullable: true),
                    BidId = table.Column<int>(type: "int", nullable: true),
                    SenderUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RecipientUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessMessages_AspNetUsers_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessMessages_AspNetUsers_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessMessages_Bids_BidId",
                        column: x => x.BidId,
                        principalTable: "Bids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessMessages_ProcurementRequests_ProcurementRequestId",
                        column: x => x.ProcurementRequestId,
                        principalTable: "ProcurementRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessMessages_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementRequestReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcurementRequestId = table.Column<int>(type: "int", nullable: false),
                    ReviewerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementRequestReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementRequestReviews_AspNetUsers_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementRequestReviews_ProcurementRequests_ProcurementRequestId",
                        column: x => x.ProcurementRequestId,
                        principalTable: "ProcurementRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierDocuments_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierReviews_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReviews_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenderInvitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenderId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    InvitedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InvitedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponseReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderInvitations_AspNetUsers_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderInvitations_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenderInvitations_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ProcurementRequestId = table.Column<int>(type: "int", nullable: true),
                    TenderId = table.Column<int>(type: "int", nullable: true),
                    AssignedToUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AssignedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkAssignments_AspNetUsers_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkAssignments_AspNetUsers_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkAssignments_ProcurementRequests_ProcurementRequestId",
                        column: x => x.ProcurementRequestId,
                        principalTable: "ProcurementRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkAssignments_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcurementRequestId = table.Column<int>(type: "int", nullable: true),
                    TenderId = table.Column<int>(type: "int", nullable: true),
                    Round = table.Column<int>(type: "int", nullable: false),
                    FromStage = table.Column<int>(type: "int", nullable: false),
                    ToStage = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    PerformedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AssignedToUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_AspNetUsers_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_AspNetUsers_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_ProcurementRequests_ProcurementRequestId",
                        column: x => x.ProcurementRequestId,
                        principalTable: "ProcurementRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowTransitions_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommitteeVotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EvaluationReportId = table.Column<int>(type: "int", nullable: false),
                    CommitteeType = table.Column<int>(type: "int", nullable: false),
                    MemberUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitteeVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommitteeVotes_AspNetUsers_MemberUserId",
                        column: x => x.MemberUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommitteeVotes_EvaluationReports_EvaluationReportId",
                        column: x => x.EvaluationReportId,
                        principalTable: "EvaluationReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationReportBids",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EvaluationReportId = table.Column<int>(type: "int", nullable: false),
                    BidId = table.Column<int>(type: "int", nullable: false),
                    TechnicalScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    FinancialScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    WeightedTotal = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    IsCompliant = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationReportBids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationReportBids_Bids_BidId",
                        column: x => x.BidId,
                        principalTable: "Bids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationReportBids_EvaluationReports_EvaluationReportId",
                        column: x => x.EvaluationReportId,
                        principalTable: "EvaluationReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessMessageAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessMessageId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessMessageAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessMessageAttachments_ProcessMessages_ProcessMessageId",
                        column: x => x.ProcessMessageId,
                        principalTable: "ProcessMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenders_AwardedBidId",
                table: "Tenders",
                column: "AwardedBidId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_ReviewedByUserId",
                table: "Suppliers",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequestItems_CategoryId",
                table: "ProcurementRequestItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_BidId",
                table: "Approvals",
                column: "BidId");

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_EvaluationReportId",
                table: "Approvals",
                column: "EvaluationReportId");

            migrationBuilder.CreateIndex(
                name: "IX_BidComplianceReviews_BidId",
                table: "BidComplianceReviews",
                column: "BidId");

            migrationBuilder.CreateIndex(
                name: "IX_BidComplianceReviews_ReviewedByUserId",
                table: "BidComplianceReviews",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteePositions_CommitteeType_PositionName_IsActive",
                table: "CommitteePositions",
                columns: new[] { "CommitteeType", "PositionName", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CommitteePositions_UserId",
                table: "CommitteePositions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeVotes_EvaluationReportId_CommitteeType_MemberUserId",
                table: "CommitteeVotes",
                columns: new[] { "EvaluationReportId", "CommitteeType", "MemberUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeVotes_MemberUserId",
                table: "CommitteeVotes",
                column: "MemberUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationReportBids_BidId",
                table: "EvaluationReportBids",
                column: "BidId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationReportBids_EvaluationReportId_BidId",
                table: "EvaluationReportBids",
                columns: new[] { "EvaluationReportId", "BidId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationReports_PreparedByUserId",
                table: "EvaluationReports",
                column: "PreparedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationReports_RecommendedBidId",
                table: "EvaluationReports",
                column: "RecommendedBidId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationReports_TenderId_Round",
                table: "EvaluationReports",
                columns: new[] { "TenderId", "Round" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessMessageAttachments_ProcessMessageId",
                table: "ProcessMessageAttachments",
                column: "ProcessMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessMessages_BidId_SentAt",
                table: "ProcessMessages",
                columns: new[] { "BidId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessMessages_ProcurementRequestId_SentAt",
                table: "ProcessMessages",
                columns: new[] { "ProcurementRequestId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessMessages_RecipientUserId",
                table: "ProcessMessages",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessMessages_SenderUserId",
                table: "ProcessMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessMessages_TenderId_SentAt",
                table: "ProcessMessages",
                columns: new[] { "TenderId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequestReviews_ProcurementRequestId",
                table: "ProcurementRequestReviews",
                column: "ProcurementRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementRequestReviews_ReviewerUserId",
                table: "ProcurementRequestReviews",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDocuments_SupplierId",
                table: "SupplierDocuments",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReviews_ReviewedByUserId",
                table: "SupplierReviews",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReviews_SupplierId",
                table: "SupplierReviews",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderInvitations_InvitedByUserId",
                table: "TenderInvitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderInvitations_SupplierId",
                table: "TenderInvitations",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderInvitations_TenderId_SupplierId",
                table: "TenderInvitations",
                columns: new[] { "TenderId", "SupplierId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkAssignments_AssignedByUserId",
                table: "WorkAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkAssignments_AssignedToUserId_CompletedAt",
                table: "WorkAssignments",
                columns: new[] { "AssignedToUserId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkAssignments_ProcurementRequestId",
                table: "WorkAssignments",
                column: "ProcurementRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkAssignments_TenderId",
                table: "WorkAssignments",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_AssignedToUserId",
                table: "WorkflowTransitions",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_PerformedByUserId",
                table: "WorkflowTransitions",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_ProcurementRequestId_CreatedAt",
                table: "WorkflowTransitions",
                columns: new[] { "ProcurementRequestId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_TenderId_CreatedAt",
                table: "WorkflowTransitions",
                columns: new[] { "TenderId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Approvals_Bids_BidId",
                table: "Approvals",
                column: "BidId",
                principalTable: "Bids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Approvals_EvaluationReports_EvaluationReportId",
                table: "Approvals",
                column: "EvaluationReportId",
                principalTable: "EvaluationReports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementRequestItems_Categories_CategoryId",
                table: "ProcurementRequestItems",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_AspNetUsers_ReviewedByUserId",
                table: "Suppliers",
                column: "ReviewedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tenders_Bids_AwardedBidId",
                table: "Tenders",
                column: "AwardedBidId",
                principalTable: "Bids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Approvals_Bids_BidId",
                table: "Approvals");

            migrationBuilder.DropForeignKey(
                name: "FK_Approvals_EvaluationReports_EvaluationReportId",
                table: "Approvals");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementRequestItems_Categories_CategoryId",
                table: "ProcurementRequestItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_AspNetUsers_ReviewedByUserId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Tenders_Bids_AwardedBidId",
                table: "Tenders");

            migrationBuilder.DropTable(
                name: "BidComplianceReviews");

            migrationBuilder.DropTable(
                name: "CommitteePositions");

            migrationBuilder.DropTable(
                name: "CommitteeVotes");

            migrationBuilder.DropTable(
                name: "EvaluationReportBids");

            migrationBuilder.DropTable(
                name: "ProcessMessageAttachments");

            migrationBuilder.DropTable(
                name: "ProcurementRequestReviews");

            migrationBuilder.DropTable(
                name: "SupplierDocuments");

            migrationBuilder.DropTable(
                name: "SupplierReviews");

            migrationBuilder.DropTable(
                name: "TenderInvitations");

            migrationBuilder.DropTable(
                name: "WorkAssignments");

            migrationBuilder.DropTable(
                name: "WorkflowTransitions");

            migrationBuilder.DropTable(
                name: "EvaluationReports");

            migrationBuilder.DropTable(
                name: "ProcessMessages");

            migrationBuilder.DropIndex(
                name: "IX_Tenders_AwardedBidId",
                table: "Tenders");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_ReviewedByUserId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_ProcurementRequestItems_CategoryId",
                table: "ProcurementRequestItems");

            migrationBuilder.DropIndex(
                name: "IX_Approvals_BidId",
                table: "Approvals");

            migrationBuilder.DropIndex(
                name: "IX_Approvals_EvaluationReportId",
                table: "Approvals");

            migrationBuilder.DropColumn(
                name: "AwardedBidId",
                table: "Tenders");

            migrationBuilder.DropColumn(
                name: "EvaluationCriteriaLockedAt",
                table: "Tenders");

            migrationBuilder.DropColumn(
                name: "FinancialWeight",
                table: "Tenders");

            migrationBuilder.DropColumn(
                name: "RequiresTopCommittee",
                table: "Tenders");

            migrationBuilder.DropColumn(
                name: "TechnicalWeight",
                table: "Tenders");

            migrationBuilder.DropColumn(
                name: "CommitteeType",
                table: "TenderCommitteeMembers");

            migrationBuilder.DropColumn(
                name: "RegistrationStatus",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "ReviewReason",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "ProcurementRequests");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "ProcurementRequests");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "ProcurementRequestItems");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "EvaluationCriteria");

            migrationBuilder.DropColumn(
                name: "BidId",
                table: "Approvals");

            migrationBuilder.DropColumn(
                name: "EvaluationReportId",
                table: "Approvals");

            migrationBuilder.DropColumn(
                name: "Round",
                table: "Approvals");
        }
    }
}
