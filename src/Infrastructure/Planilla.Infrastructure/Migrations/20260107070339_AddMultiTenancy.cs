using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Vorluno.Planilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "TaxBrackets",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TaxBracket_CompanyId_Year",
                table: "TaxBrackets",
                newName: "IX_TaxBracket_TenantId_Year");

            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "SolicitudesVacaciones",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "SaldosVacaciones",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_SaldoVacaciones_CompanyId_EmpleadoId",
                table: "SaldosVacaciones",
                newName: "IX_SaldoVacaciones_TenantId_EmpleadoId");

            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "Prestamos",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "Posiciones",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Posicion_CompanyId_Codigo",
                table: "Posiciones",
                newName: "IX_Posicion_TenantId_Codigo");

            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "PayrollTaxConfigurations",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_PayrollTaxConfiguration_CompanyId_EffectiveStartDate",
                table: "PayrollTaxConfigurations",
                newName: "IX_PayrollTaxConfiguration_TenantId_EffectiveStartDate");

            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "PayrollHeaders",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_PayrollHeader_CompanyId_Status",
                table: "PayrollHeaders",
                newName: "IX_PayrollHeader_TenantId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_PayrollHeader_CompanyId_PayrollNumber",
                table: "PayrollHeaders",
                newName: "IX_PayrollHeader_TenantId_PayrollNumber");

            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "HorasExtra",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "Empleados",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "Departamentos",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Departamento_CompanyId_Codigo",
                table: "Departamentos",
                newName: "IX_Departamento_TenantId_Codigo");

            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "DeduccionesFijas",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "Ausencias",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "CompanyId",
                table: "Anticipos",
                newName: "TenantId");

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "RecibosDeSueldo",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PayrollDetails",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PagosPrestamos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Subdomain = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RUC = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DV = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Settings = table.Column<string>(type: "text", nullable: true),
                    SubscriptionId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    Plan = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TrialEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StripeCustomerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StripeSubscriptionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MonthlyPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    CustomMaxEmployees = table.Column<int>(type: "integer", nullable: false),
                    CustomMaxUsers = table.Column<int>(type: "integer", nullable: false),
                    NextBillingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CanceledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsPendingInvitation = table.Column<bool>(type: "boolean", nullable: false),
                    InvitationToken = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InvitationExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvitedEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantUsers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenantUsers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesVacaciones_TenantId",
                table: "SolicitudesVacaciones",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RecibosDeSueldo_TenantId",
                table: "RecibosDeSueldo",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Prestamos_TenantId",
                table: "Prestamos",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDetails_TenantId",
                table: "PayrollDetails",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PagosPrestamos_TenantId",
                table: "PagosPrestamos",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_HorasExtra_TenantId",
                table: "HorasExtra",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Empleados_TenantId",
                table: "Empleados",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DeduccionesFijas_TenantId",
                table: "DeduccionesFijas",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Ausencias_TenantId",
                table: "Ausencias",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Anticipos_TenantId",
                table: "Anticipos",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscription_Status",
                table: "Subscriptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Subscription_StripeCustomerId",
                table: "Subscriptions",
                column: "StripeCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId",
                table: "Subscriptions",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenant_RUC_DV",
                table: "Tenants",
                columns: new[] { "RUC", "DV" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenant_Subdomain",
                table: "Tenants",
                column: "Subdomain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantUser_InvitationToken",
                table: "TenantUsers",
                column: "InvitationToken");

            migrationBuilder.CreateIndex(
                name: "IX_TenantUser_TenantId_UserId",
                table: "TenantUsers",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_UserId",
                table: "TenantUsers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Anticipos_Tenants_TenantId",
                table: "Anticipos",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Ausencias_Tenants_TenantId",
                table: "Ausencias",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DeduccionesFijas_Tenants_TenantId",
                table: "DeduccionesFijas",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Departamentos_Tenants_TenantId",
                table: "Departamentos",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Empleados_Tenants_TenantId",
                table: "Empleados",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HorasExtra_Tenants_TenantId",
                table: "HorasExtra",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PagosPrestamos_Tenants_TenantId",
                table: "PagosPrestamos",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollDetails_Tenants_TenantId",
                table: "PayrollDetails",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollHeaders_Tenants_TenantId",
                table: "PayrollHeaders",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollTaxConfigurations_Tenants_TenantId",
                table: "PayrollTaxConfigurations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Posiciones_Tenants_TenantId",
                table: "Posiciones",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Prestamos_Tenants_TenantId",
                table: "Prestamos",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RecibosDeSueldo_Tenants_TenantId",
                table: "RecibosDeSueldo",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SaldosVacaciones_Tenants_TenantId",
                table: "SaldosVacaciones",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SolicitudesVacaciones_Tenants_TenantId",
                table: "SolicitudesVacaciones",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaxBrackets_Tenants_TenantId",
                table: "TaxBrackets",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Anticipos_Tenants_TenantId",
                table: "Anticipos");

            migrationBuilder.DropForeignKey(
                name: "FK_Ausencias_Tenants_TenantId",
                table: "Ausencias");

            migrationBuilder.DropForeignKey(
                name: "FK_DeduccionesFijas_Tenants_TenantId",
                table: "DeduccionesFijas");

            migrationBuilder.DropForeignKey(
                name: "FK_Departamentos_Tenants_TenantId",
                table: "Departamentos");

            migrationBuilder.DropForeignKey(
                name: "FK_Empleados_Tenants_TenantId",
                table: "Empleados");

            migrationBuilder.DropForeignKey(
                name: "FK_HorasExtra_Tenants_TenantId",
                table: "HorasExtra");

            migrationBuilder.DropForeignKey(
                name: "FK_PagosPrestamos_Tenants_TenantId",
                table: "PagosPrestamos");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollDetails_Tenants_TenantId",
                table: "PayrollDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollHeaders_Tenants_TenantId",
                table: "PayrollHeaders");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollTaxConfigurations_Tenants_TenantId",
                table: "PayrollTaxConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_Posiciones_Tenants_TenantId",
                table: "Posiciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Prestamos_Tenants_TenantId",
                table: "Prestamos");

            migrationBuilder.DropForeignKey(
                name: "FK_RecibosDeSueldo_Tenants_TenantId",
                table: "RecibosDeSueldo");

            migrationBuilder.DropForeignKey(
                name: "FK_SaldosVacaciones_Tenants_TenantId",
                table: "SaldosVacaciones");

            migrationBuilder.DropForeignKey(
                name: "FK_SolicitudesVacaciones_Tenants_TenantId",
                table: "SolicitudesVacaciones");

            migrationBuilder.DropForeignKey(
                name: "FK_TaxBrackets_Tenants_TenantId",
                table: "TaxBrackets");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "TenantUsers");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_SolicitudesVacaciones_TenantId",
                table: "SolicitudesVacaciones");

            migrationBuilder.DropIndex(
                name: "IX_RecibosDeSueldo_TenantId",
                table: "RecibosDeSueldo");

            migrationBuilder.DropIndex(
                name: "IX_Prestamos_TenantId",
                table: "Prestamos");

            migrationBuilder.DropIndex(
                name: "IX_PayrollDetails_TenantId",
                table: "PayrollDetails");

            migrationBuilder.DropIndex(
                name: "IX_PagosPrestamos_TenantId",
                table: "PagosPrestamos");

            migrationBuilder.DropIndex(
                name: "IX_HorasExtra_TenantId",
                table: "HorasExtra");

            migrationBuilder.DropIndex(
                name: "IX_Empleados_TenantId",
                table: "Empleados");

            migrationBuilder.DropIndex(
                name: "IX_DeduccionesFijas_TenantId",
                table: "DeduccionesFijas");

            migrationBuilder.DropIndex(
                name: "IX_Ausencias_TenantId",
                table: "Ausencias");

            migrationBuilder.DropIndex(
                name: "IX_Anticipos_TenantId",
                table: "Anticipos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RecibosDeSueldo");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PayrollDetails");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PagosPrestamos");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "TaxBrackets",
                newName: "CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_TaxBracket_TenantId_Year",
                table: "TaxBrackets",
                newName: "IX_TaxBracket_CompanyId_Year");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "SolicitudesVacaciones",
                newName: "CompanyId");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "SaldosVacaciones",
                newName: "CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_SaldoVacaciones_TenantId_EmpleadoId",
                table: "SaldosVacaciones",
                newName: "IX_SaldoVacaciones_CompanyId_EmpleadoId");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "Prestamos",
                newName: "CompanyId");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "Posiciones",
                newName: "CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_Posicion_TenantId_Codigo",
                table: "Posiciones",
                newName: "IX_Posicion_CompanyId_Codigo");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "PayrollTaxConfigurations",
                newName: "CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_PayrollTaxConfiguration_TenantId_EffectiveStartDate",
                table: "PayrollTaxConfigurations",
                newName: "IX_PayrollTaxConfiguration_CompanyId_EffectiveStartDate");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "PayrollHeaders",
                newName: "CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_PayrollHeader_TenantId_Status",
                table: "PayrollHeaders",
                newName: "IX_PayrollHeader_CompanyId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_PayrollHeader_TenantId_PayrollNumber",
                table: "PayrollHeaders",
                newName: "IX_PayrollHeader_CompanyId_PayrollNumber");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "HorasExtra",
                newName: "CompanyId");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "Empleados",
                newName: "CompanyId");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "Departamentos",
                newName: "CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_Departamento_TenantId_Codigo",
                table: "Departamentos",
                newName: "IX_Departamento_CompanyId_Codigo");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "DeduccionesFijas",
                newName: "CompanyId");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "Ausencias",
                newName: "CompanyId");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "Anticipos",
                newName: "CompanyId");
        }
    }
}
