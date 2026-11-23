using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicingAuditPeriodLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auditentries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    At = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UserName = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auditentries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "contractors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Nip = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contractors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "periodlocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LockedThrough = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_periodlocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Number = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ContractorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SaleDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Net = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Vat = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Gross = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TransactionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResultAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsCanceled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_invoices_contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoicelines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Net = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    VatRate = table.Column<int>(type: "INTEGER", nullable: false),
                    Vat = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoicelines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_invoicelines_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TransactionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payments_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_auditentries_At",
                table: "auditentries",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_contractors_Nip",
                table: "contractors",
                column: "Nip");

            migrationBuilder.CreateIndex(
                name: "IX_invoicelines_InvoiceId",
                table: "invoicelines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_ContractorId",
                table: "invoices",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_IssueDate",
                table: "invoices",
                column: "IssueDate");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_Type_Number_ContractorId",
                table: "invoices",
                columns: new[] { "Type", "Number", "ContractorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_InvoiceId",
                table: "payments",
                column: "InvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auditentries");

            migrationBuilder.DropTable(
                name: "invoicelines");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "periodlocks");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "contractors");
        }
    }
}
