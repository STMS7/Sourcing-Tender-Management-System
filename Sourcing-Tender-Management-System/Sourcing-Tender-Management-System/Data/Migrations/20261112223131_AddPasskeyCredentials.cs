using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sourcing_Tender_Management_System.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPasskeyCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [NormalizedName] = N'DEPARTMENTREQUESTER')
                    INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
                    VALUES (N'ROLE-DEPARTMENT-REQUESTER', N'DepartmentRequester', N'DEPARTMENTREQUESTER', N'ROLE-DEPARTMENT-REQUESTER');
                """);

            migrationBuilder.CreateTable(
                name: "UserPasskeyCredentials",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CredentialId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PublicKey = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    SignCount = table.Column<long>(type: "bigint", nullable: false),
                    DeviceLabel = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPasskeyCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPasskeyCredentials_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPasskeyCredentials_CredentialId",
                table: "UserPasskeyCredentials",
                column: "CredentialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPasskeyCredentials_UserId",
                table: "UserPasskeyCredentials",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPasskeyCredentials");

            migrationBuilder.Sql("""
                DELETE FROM [AspNetRoles]
                WHERE [Id] = N'ROLE-DEPARTMENT-REQUESTER'
                  AND [Id] NOT IN (SELECT [RoleId] FROM [AspNetUserRoles]);
                """);
        }
    }
}
