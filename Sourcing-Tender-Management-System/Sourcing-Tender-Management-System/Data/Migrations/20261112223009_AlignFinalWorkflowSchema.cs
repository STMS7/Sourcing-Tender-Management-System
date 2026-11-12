using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sourcing_Tender_Management_System.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignFinalWorkflowSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenderCommitteeMembers_TenderId_UserId",
                table: "TenderCommitteeMembers");

            migrationBuilder.DropIndex(
                name: "IX_Approvals_TenderId_Sequence",
                table: "Approvals");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Suppliers");

            migrationBuilder.CreateIndex(
                name: "IX_TenderCommitteeMembers_TenderId_CommitteeType_Role",
                table: "TenderCommitteeMembers",
                columns: new[] { "TenderId", "CommitteeType", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenderCommitteeMembers_TenderId_CommitteeType_UserId",
                table: "TenderCommitteeMembers",
                columns: new[] { "TenderId", "CommitteeType", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_TenderId_Round_Sequence",
                table: "Approvals",
                columns: new[] { "TenderId", "Round", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenderCommitteeMembers_TenderId_CommitteeType_Role",
                table: "TenderCommitteeMembers");

            migrationBuilder.DropIndex(
                name: "IX_TenderCommitteeMembers_TenderId_CommitteeType_UserId",
                table: "TenderCommitteeMembers");

            migrationBuilder.DropIndex(
                name: "IX_Approvals_TenderId_Round_Sequence",
                table: "Approvals");

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE [Suppliers] SET [IsApproved] = 1 WHERE [RegistrationStatus] = 1;");

            migrationBuilder.CreateIndex(
                name: "IX_TenderCommitteeMembers_TenderId_UserId",
                table: "TenderCommitteeMembers",
                columns: new[] { "TenderId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_TenderId_Sequence",
                table: "Approvals",
                columns: new[] { "TenderId", "Sequence" },
                unique: true);
        }
    }
}
