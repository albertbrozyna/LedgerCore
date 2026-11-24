using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Collections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "collectionnotices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContractorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Channel = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CalculationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PaymentDeadline = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Principal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Interest = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SentTo = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    LinesJson = table.Column<string>(type: "TEXT", nullable: false),
                    Error = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collectionnotices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "interestrates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AnnualPercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interestrates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mailsettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Host = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    UseTls = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ProtectedPassword = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    FromAddress = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FromName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ReminderSubject = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ReminderBody = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    InterestSubject = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    InterestBody = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mailsettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "collectionnoticeinvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NoticeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collectionnoticeinvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_collectionnoticeinvoices_collectionnotices_NoticeId",
                        column: x => x.NoticeId,
                        principalTable: "collectionnotices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_collectionnoticeinvoices_CompanyId",
                table: "collectionnoticeinvoices",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_collectionnoticeinvoices_CompanyId_InvoiceId",
                table: "collectionnoticeinvoices",
                columns: new[] { "CompanyId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_collectionnoticeinvoices_NoticeId",
                table: "collectionnoticeinvoices",
                column: "NoticeId");

            migrationBuilder.CreateIndex(
                name: "IX_collectionnotices_CompanyId",
                table: "collectionnotices",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_collectionnotices_CompanyId_ContractorId",
                table: "collectionnotices",
                columns: new[] { "CompanyId", "ContractorId" });

            migrationBuilder.CreateIndex(
                name: "IX_interestrates_ValidFrom",
                table: "interestrates",
                column: "ValidFrom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mailsettings_CompanyId",
                table: "mailsettings",
                column: "CompanyId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "collectionnoticeinvoices");

            migrationBuilder.DropTable(
                name: "interestrates");

            migrationBuilder.DropTable(
                name: "mailsettings");

            migrationBuilder.DropTable(
                name: "collectionnotices");
        }
    }
}
