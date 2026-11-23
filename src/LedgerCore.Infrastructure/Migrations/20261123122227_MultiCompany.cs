using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultiCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
migrationBuilder.DropIndex(
                name: "IX_transactiontemplates_Name",
                table: "transactiontemplates");

            migrationBuilder.DropIndex(
                name: "IX_transactions_Number",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_invoices_Type_Number_ContractorId",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_accounts_Code",
                table: "accounts");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "transactiontemplates",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "transactions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "periodlocks",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "payments",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "journalentries",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "invoices",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "invoicelines",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "contractors",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "auditentries",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "attachments",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "accounts",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("11111111-1111-1111-1111-111111111111"));

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Nip = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    BankAccount = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    InvoiceFooter = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "usercompanies",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usercompanies", x => new { x.UserId, x.CompanyId });
                });

            // --- Migracja danych z wersji jednofirmowej: istniejące rekordy należą do "firmy domyślnej" (CompanyId = 1111...) ---
            migrationBuilder.Sql(@"INSERT INTO companies (Id, Name, Nip, Address, Email, Phone, BankAccount, InvoiceFooter, IsActive, CreatedAt)
                SELECT '11111111-1111-1111-1111-111111111111', COALESCE(NULLIF(Name, ''), 'Moja firma'), Nip, Address, Email, Phone, BankAccount, InvoiceFooter, 1, datetime('now')
                FROM companysettings LIMIT 1;");
            migrationBuilder.Sql(@"INSERT INTO companies (Id, Name, IsActive, CreatedAt)
                SELECT '11111111-1111-1111-1111-111111111111', 'Moja firma', 1, datetime('now')
                WHERE NOT EXISTS (SELECT 1 FROM companies) AND EXISTS (SELECT 1 FROM accounts);");
            migrationBuilder.Sql(@"INSERT INTO usercompanies (UserId, CompanyId)
                SELECT Id, '11111111-1111-1111-1111-111111111111' FROM aspnetusers
                WHERE EXISTS (SELECT 1 FROM companies WHERE Id = '11111111-1111-1111-1111-111111111111');");
            migrationBuilder.DropTable(name: "companysettings");


            migrationBuilder.CreateIndex(
                name: "IX_transactiontemplates_CompanyId",
                table: "transactiontemplates",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_transactiontemplates_CompanyId_Name",
                table: "transactiontemplates",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_CompanyId",
                table: "transactions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_CompanyId_Number",
                table: "transactions",
                columns: new[] { "CompanyId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_periodlocks_CompanyId",
                table: "periodlocks",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_CompanyId",
                table: "payments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_journalentries_CompanyId",
                table: "journalentries",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_CompanyId",
                table: "invoices",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_CompanyId_Type_Number_ContractorId",
                table: "invoices",
                columns: new[] { "CompanyId", "Type", "Number", "ContractorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoicelines_CompanyId",
                table: "invoicelines",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_contractors_CompanyId",
                table: "contractors",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_auditentries_CompanyId",
                table: "auditentries",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_attachments_CompanyId",
                table: "attachments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_CompanyId",
                table: "accounts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_CompanyId_Code",
                table: "accounts",
                columns: new[] { "CompanyId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "companies");

            migrationBuilder.DropTable(
                name: "usercompanies");

            migrationBuilder.DropIndex(
                name: "IX_transactiontemplates_CompanyId",
                table: "transactiontemplates");

            migrationBuilder.DropIndex(
                name: "IX_transactiontemplates_CompanyId_Name",
                table: "transactiontemplates");

            migrationBuilder.DropIndex(
                name: "IX_transactions_CompanyId",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_CompanyId_Number",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_periodlocks_CompanyId",
                table: "periodlocks");

            migrationBuilder.DropIndex(
                name: "IX_payments_CompanyId",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_journalentries_CompanyId",
                table: "journalentries");

            migrationBuilder.DropIndex(
                name: "IX_invoices_CompanyId",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoices_CompanyId_Type_Number_ContractorId",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_invoicelines_CompanyId",
                table: "invoicelines");

            migrationBuilder.DropIndex(
                name: "IX_contractors_CompanyId",
                table: "contractors");

            migrationBuilder.DropIndex(
                name: "IX_auditentries_CompanyId",
                table: "auditentries");

            migrationBuilder.DropIndex(
                name: "IX_attachments_CompanyId",
                table: "attachments");

            migrationBuilder.DropIndex(
                name: "IX_accounts_CompanyId",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "IX_accounts_CompanyId_Code",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "transactiontemplates");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "periodlocks");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "journalentries");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "invoicelines");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "contractors");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "auditentries");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "accounts");

            migrationBuilder.CreateTable(
                name: "companysettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    BankAccount = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    InvoiceFooter = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Nip = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companysettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transactiontemplates_Name",
                table: "transactiontemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_Number",
                table: "transactions",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_Type_Number_ContractorId",
                table: "invoices",
                columns: new[] { "Type", "Number", "ContractorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_Code",
                table: "accounts",
                column: "Code",
                unique: true);
        }
    }
}
