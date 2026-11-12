using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sourcing_Tender_Management_System.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompleteFeatureModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clarifications_Bids_BidId",
                table: "Clarifications");

            migrationBuilder.DropIndex(
                name: "IX_ClarificationResponses_ClarificationId",
                table: "ClarificationResponses");

            migrationBuilder.AddColumn<string>(
                name: "TermsAndConditions",
                table: "Tenders",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoleType",
                table: "TenderCommitteeMembers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegalVerificationStatus",
                table: "Suppliers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LicenseExpiresAt",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseNumber",
                table: "Suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "SupplierDocuments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "SupplierDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationNotes",
                table: "SupplierDocuments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VerificationStatus",
                table: "SupplierDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "SupplierDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifiedByUserId",
                table: "SupplierDocuments",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "EvaluationReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "BidId",
                table: "Clarifications",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "ProcurementRequestId",
                table: "Clarifications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientUserId",
                table: "Clarifications",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "Clarifications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Clarifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "Clarifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TenderId",
                table: "Clarifications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RespondedByUserId",
                table: "ClarificationResponses",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComplianceStatus",
                table: "Bids",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DisqualificationReason",
                table: "Bids",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FeesAmount",
                table: "Bids",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InstallationAmount",
                table: "Bids",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaintenanceAmount",
                table: "Bids",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentTerms",
                table: "Bids",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingAmount",
                table: "Bids",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "Bids",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalSummary",
                table: "Bids",
                type: "nvarchar(3000)",
                maxLength: 3000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "BidDocuments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                table: "BidDocuments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TenderDocumentRequirementId",
                table: "BidDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Stage",
                table: "Approvals",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BidAnalyses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BidId = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExtractedDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Strengths = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    Weaknesses = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    PreliminaryRecommendation = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    AnalyzedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BidAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BidAnalyses_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BidAnalyses_Bids_BidId",
                        column: x => x.BidId,
                        principalTable: "Bids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContactMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenderDocumentRequirements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenderId = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderDocumentRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderDocumentRequirements_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserTasks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Link = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProcurementRequestId = table.Column<int>(type: "int", nullable: true),
                    TenderId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTasks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserTasks_ProcurementRequests_ProcurementRequestId",
                        column: x => x.ProcurementRequestId,
                        principalTable: "ProcurementRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserTasks_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDocuments_VerifiedByUserId",
                table: "SupplierDocuments",
                column: "VerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Clarifications_ProcurementRequestId",
                table: "Clarifications",
                column: "ProcurementRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Clarifications_RecipientUserId",
                table: "Clarifications",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Clarifications_TenderId",
                table: "Clarifications",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_ClarificationResponses_ClarificationId",
                table: "ClarificationResponses",
                column: "ClarificationId");

            migrationBuilder.CreateIndex(
                name: "IX_ClarificationResponses_RespondedByUserId",
                table: "ClarificationResponses",
                column: "RespondedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BidDocuments_TenderDocumentRequirementId",
                table: "BidDocuments",
                column: "TenderDocumentRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_BidAnalyses_BidId",
                table: "BidAnalyses",
                column: "BidId");

            migrationBuilder.CreateIndex(
                name: "IX_BidAnalyses_RequestedByUserId",
                table: "BidAnalyses",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderDocumentRequirements_TenderId_DocumentType",
                table: "TenderDocumentRequirements",
                columns: new[] { "TenderId", "DocumentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTasks_ProcurementRequestId",
                table: "UserTasks",
                column: "ProcurementRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTasks_TenderId",
                table: "UserTasks",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTasks_UserId",
                table: "UserTasks",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BidDocuments_TenderDocumentRequirements_TenderDocumentRequirementId",
                table: "BidDocuments",
                column: "TenderDocumentRequirementId",
                principalTable: "TenderDocumentRequirements",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ClarificationResponses_AspNetUsers_RespondedByUserId",
                table: "ClarificationResponses",
                column: "RespondedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Clarifications_AspNetUsers_RecipientUserId",
                table: "Clarifications",
                column: "RecipientUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Clarifications_Bids_BidId",
                table: "Clarifications",
                column: "BidId",
                principalTable: "Bids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Clarifications_ProcurementRequests_ProcurementRequestId",
                table: "Clarifications",
                column: "ProcurementRequestId",
                principalTable: "ProcurementRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Clarifications_Tenders_TenderId",
                table: "Clarifications",
                column: "TenderId",
                principalTable: "Tenders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierDocuments_AspNetUsers_VerifiedByUserId",
                table: "SupplierDocuments",
                column: "VerifiedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BidDocuments_TenderDocumentRequirements_TenderDocumentRequirementId",
                table: "BidDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_ClarificationResponses_AspNetUsers_RespondedByUserId",
                table: "ClarificationResponses");

            migrationBuilder.DropForeignKey(
                name: "FK_Clarifications_AspNetUsers_RecipientUserId",
                table: "Clarifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Clarifications_Bids_BidId",
                table: "Clarifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Clarifications_ProcurementRequests_ProcurementRequestId",
                table: "Clarifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Clarifications_Tenders_TenderId",
                table: "Clarifications");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierDocuments_AspNetUsers_VerifiedByUserId",
                table: "SupplierDocuments");

            migrationBuilder.DropTable(
                name: "BidAnalyses");

            migrationBuilder.DropTable(
                name: "ContactMessages");

            migrationBuilder.DropTable(
                name: "TenderDocumentRequirements");

            migrationBuilder.DropTable(
                name: "UserTasks");

            migrationBuilder.DropIndex(
                name: "IX_SupplierDocuments_VerifiedByUserId",
                table: "SupplierDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Clarifications_ProcurementRequestId",
                table: "Clarifications");

            migrationBuilder.DropIndex(
                name: "IX_Clarifications_RecipientUserId",
                table: "Clarifications");

            migrationBuilder.DropIndex(
                name: "IX_Clarifications_TenderId",
                table: "Clarifications");

            migrationBuilder.DropIndex(
                name: "IX_ClarificationResponses_ClarificationId",
                table: "ClarificationResponses");

            migrationBuilder.DropIndex(
                name: "IX_ClarificationResponses_RespondedByUserId",
                table: "ClarificationResponses");

            migrationBuilder.DropIndex(
                name: "IX_BidDocuments_TenderDocumentRequirementId",
                table: "BidDocuments");

            migrationBuilder.DropColumn(
                name: "TermsAndConditions",
                table: "Tenders");

            migrationBuilder.DropColumn(
                name: "RoleType",
                table: "TenderCommitteeMembers");

            migrationBuilder.DropColumn(
                name: "LegalVerificationStatus",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "LicenseExpiresAt",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "LicenseNumber",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "SupplierDocuments");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "SupplierDocuments");

            migrationBuilder.DropColumn(
                name: "VerificationNotes",
                table: "SupplierDocuments");

            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                table: "SupplierDocuments");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "SupplierDocuments");

            migrationBuilder.DropColumn(
                name: "VerifiedByUserId",
                table: "SupplierDocuments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "EvaluationReports");

            migrationBuilder.DropColumn(
                name: "ProcurementRequestId",
                table: "Clarifications");

            migrationBuilder.DropColumn(
                name: "RecipientUserId",
                table: "Clarifications");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "Clarifications");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Clarifications");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "Clarifications");

            migrationBuilder.DropColumn(
                name: "TenderId",
                table: "Clarifications");

            migrationBuilder.DropColumn(
                name: "RespondedByUserId",
                table: "ClarificationResponses");

            migrationBuilder.DropColumn(
                name: "ComplianceStatus",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "DisqualificationReason",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "FeesAmount",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "InstallationAmount",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "MaintenanceAmount",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "PaymentTerms",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "ShippingAmount",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "TechnicalSummary",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "BidDocuments");

            migrationBuilder.DropColumn(
                name: "IsVerified",
                table: "BidDocuments");

            migrationBuilder.DropColumn(
                name: "TenderDocumentRequirementId",
                table: "BidDocuments");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "Approvals");

            migrationBuilder.AlterColumn<int>(
                name: "BidId",
                table: "Clarifications",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClarificationResponses_ClarificationId",
                table: "ClarificationResponses",
                column: "ClarificationId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Clarifications_Bids_BidId",
                table: "Clarifications",
                column: "BidId",
                principalTable: "Bids",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
