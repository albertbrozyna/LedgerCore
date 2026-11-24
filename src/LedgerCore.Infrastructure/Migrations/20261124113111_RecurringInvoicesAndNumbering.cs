using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RecurringInvoicesAndNumbering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RecurringInvoiceId",
                table: "invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecurringPeriod",
                table: "invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesNumberPattern",
                table: "companies",
                type: "TEXT",
                maxLength: 60,
                nullable: false,
                defaultValue: "FV/{RRRR}/{NNNN}");

            migrationBuilder.AddColumn<int>(
                name: "SalesNumberReset",
                table: "companies",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "recurringinvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ContractorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Frequency = table.Column<int>(type: "INTEGER", nullable: false),
                    DayOfMonth = table.Column<int>(type: "INTEGER", nullable: false),
                    DueDays = table.Column<int>(type: "INTEGER", nullable: false),
                    NextDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ResultAccountId = table.Column<Guid>(type: "TEXT", nullable: true),
                    KpirKind = table.Column<int>(type: "INTEGER", nullable: true),
                    RyczaltRate = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurringinvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recurringinvoices_contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recurringinvoicelines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecurringInvoiceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Net = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    VatRate = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurringinvoicelines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recurringinvoicelines_recurringinvoices_RecurringInvoiceId",
                        column: x => x.RecurringInvoiceId,
                        principalTable: "recurringinvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Wcześniejsze wersje nie miały unikalności numerów sprzedaży w firmie. Gdyby w istniejącej bazie powtórzył się numer,
            // późniejszej fakturze dopisujemy końcówkę, żeby unikalny indeks nie zablokował aktualizacji (dane zostają, numer jest rozróżnialny).
            migrationBuilder.Sql(@"UPDATE invoices SET Number = substr(Number, 1, 40) || '-dup-' || substr(Id, 1, 4)
                WHERE Type = 0 AND EXISTS (SELECT 1 FROM invoices j WHERE j.Type = 0 AND j.CompanyId = invoices.CompanyId AND j.Number = invoices.Number
                    AND (j.CreatedAt < invoices.CreatedAt OR (j.CreatedAt = invoices.CreatedAt AND j.Id < invoices.Id)));");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Company_RecurringPeriod",
                table: "invoices",
                columns: new[] { "CompanyId", "RecurringInvoiceId", "RecurringPeriod" },
                unique: true,
                filter: "\"RecurringInvoiceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Company_SalesNumber",
                table: "invoices",
                columns: new[] { "CompanyId", "Number" },
                unique: true,
                filter: "\"Type\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_recurringinvoicelines_CompanyId",
                table: "recurringinvoicelines",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_recurringinvoicelines_RecurringInvoiceId",
                table: "recurringinvoicelines",
                column: "RecurringInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_recurringinvoices_CompanyId",
                table: "recurringinvoices",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_recurringinvoices_CompanyId_IsActive_NextDate",
                table: "recurringinvoices",
                columns: new[] { "CompanyId", "IsActive", "NextDate" });

            migrationBuilder.CreateIndex(
                name: "IX_recurringinvoices_ContractorId",
                table: "recurringinvoices",
                column: "ContractorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recurringinvoicelines");

            migrationBuilder.DropTable(
                name: "recurringinvoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_Company_RecurringPeriod",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_Company_SalesNumber",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "RecurringInvoiceId",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "RecurringPeriod",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "SalesNumberPattern",
                table: "companies");

            migrationBuilder.DropColumn(
                name: "SalesNumberReset",
                table: "companies");
        }
    }
}
