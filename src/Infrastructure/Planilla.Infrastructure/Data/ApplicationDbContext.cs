using Microsoft.AspNetCore.Identity.EntityFrameworkCore; // <--- USANDO A�ADIDO
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;                         // <--- USANDO A�ADIDO

namespace Vorluno.Planilla.Infrastructure.Data;

// CAMBIO CLAVE: Heredamos de IdentityDbContext<AppUser> en lugar de solo DbContext
public class ApplicationDbContext : IdentityDbContext<AppUser>
{
    private readonly ICurrentUserService? _currentUserService;
    private readonly ITenantContext? _tenantContext;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService? currentUserService = null,
        ITenantContext? tenantContext = null) : base(options)
    {
        _currentUserService = currentUserService;
        _tenantContext = tenantContext;
    }

    // Multi-Tenant Entities
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<TenantUser> TenantUsers { get; set; }
    public DbSet<TenantInvitation> TenantInvitations { get; set; }
    public DbSet<AuditLogEntry> AuditLogEntries { get; set; }
    public DbSet<StripeWebhookEvent> StripeWebhookEvents { get; set; }

    public DbSet<Empleado> Empleados { get; set; }
    public DbSet<ReciboDeSueldo> RecibosDeSueldo { get; set; }

    // Phase A: Configuraci�n de planilla (tasas CSS, SE, ISR)
    public DbSet<PayrollTaxConfiguration> PayrollTaxConfigurations { get; set; }
    public DbSet<TaxBracket> TaxBrackets { get; set; }

    // Phase D: Workflow de planilla
    public DbSet<PayrollHeader> PayrollHeaders { get; set; }
    public DbSet<PayrollDetail> PayrollDetails { get; set; }

    // Organizaci�n: Departamentos y Posiciones
    public DbSet<Departamento> Departamentos { get; set; }
    public DbSet<Posicion> Posiciones { get; set; }

    // Conceptos de N�mina: Pr�stamos, Deducciones y Anticipos
    public DbSet<Prestamo> Prestamos { get; set; }
    public DbSet<DeduccionFija> DeduccionesFijas { get; set; }
    public DbSet<Anticipo> Anticipos { get; set; }
    public DbSet<PagoPrestamo> PagosPrestamos { get; set; }

    // Asistencia: Horas Extra, Ausencias y Vacaciones
    public DbSet<HoraExtra> HorasExtra { get; set; }
    public DbSet<Ausencia> Ausencias { get; set; }
    public DbSet<SolicitudVacaciones> SolicitudesVacaciones { get; set; }
    public DbSet<SaldoVacaciones> SaldosVacaciones { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Esta l�nea es crucial al heredar de IdentityDbContext
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Empleado>()
            .HasIndex(e => e.NumeroIdentificacion)
            .IsUnique();

        // Phase A: Configuraci�n de PayrollTaxConfiguration
        modelBuilder.Entity<PayrollTaxConfiguration>(entity =>
        {
            // �ndice compuesto para b�squedas por tenant y fecha efectiva
            entity.HasIndex(p => new { p.TenantId, p.EffectiveStartDate })
                .HasDatabaseName("IX_PayrollTaxConfiguration_TenantId_EffectiveStartDate");

            // Configuraci�n de precisi�n para campos decimales (moneda)
            entity.Property(p => p.CssEmployeeRate).HasPrecision(5, 2);
            entity.Property(p => p.CssEmployerBaseRate).HasPrecision(5, 2);
            entity.Property(p => p.CssRiskRateLow).HasPrecision(5, 2);
            entity.Property(p => p.CssRiskRateMedium).HasPrecision(5, 2);
            entity.Property(p => p.CssRiskRateHigh).HasPrecision(5, 2);
            entity.Property(p => p.CssMaxContributionBaseStandard).HasPrecision(18, 2);
            entity.Property(p => p.CssMaxContributionBaseIntermediate).HasPrecision(18, 2);
            entity.Property(p => p.CssMaxContributionBaseHigh).HasPrecision(18, 2);
            entity.Property(p => p.CssIntermediateMinAvgSalary).HasPrecision(18, 2);
            entity.Property(p => p.CssHighMinAvgSalary).HasPrecision(18, 2);
            entity.Property(p => p.EducationalInsuranceEmployeeRate).HasPrecision(5, 2);
            entity.Property(p => p.EducationalInsuranceEmployerRate).HasPrecision(5, 2);
            entity.Property(p => p.DependentDeductionAmount).HasPrecision(18, 2);

            // Global query filter para multi-tenancy por TenantId
            entity.HasQueryFilter(p =>
                _tenantContext == null ||
                _tenantContext.TenantId == 0 ||
                p.TenantId == _tenantContext.TenantId);
        });

        // Phase A: Configuraci�n de TaxBracket
        modelBuilder.Entity<TaxBracket>(entity =>
        {
            // �ndice compuesto para b�squedas por tenant y a�o fiscal
            entity.HasIndex(t => new { t.TenantId, t.Year })
                .HasDatabaseName("IX_TaxBracket_TenantId_Year");

            // Configuraci�n de precisi�n para campos decimales (moneda)
            entity.Property(t => t.MinIncome).HasPrecision(18, 2);
            entity.Property(t => t.MaxIncome).HasPrecision(18, 2);
            entity.Property(t => t.Rate).HasPrecision(5, 2);
            entity.Property(t => t.FixedAmount).HasPrecision(18, 2);

            // Global query filter para multi-tenancy por TenantId
            entity.HasQueryFilter(t =>
                _tenantContext == null ||
                _tenantContext.TenantId == 0 ||
                t.TenantId == _tenantContext.TenantId);
        });

        // Phase D: Configuraci�n de PayrollHeader
        modelBuilder.Entity<PayrollHeader>(entity =>
        {
            // �ndice �nico compuesto: PayrollNumber debe ser �nico por compa��a
            entity.HasIndex(p => new { p.TenantId, p.PayrollNumber })
                .IsUnique()
                .HasDatabaseName("IX_PayrollHeader_TenantId_PayrollNumber");

            // �ndice para b�squedas por estado
            entity.HasIndex(p => new { p.TenantId, p.Status })
                .HasDatabaseName("IX_PayrollHeader_TenantId_Status");

            // Concurrencia optimista usando xmin de PostgreSQL
            entity.Property<uint>("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            // Configuraci�n de precisi�n para campos decimales (moneda)
            entity.Property(p => p.TotalGrossPay).HasPrecision(18, 2);
            entity.Property(p => p.TotalDeductions).HasPrecision(18, 2);
            entity.Property(p => p.TotalNetPay).HasPrecision(18, 2);
            entity.Property(p => p.TotalEmployerCost).HasPrecision(18, 2);

            // Relaci�n 1:N con PayrollDetail
            entity.HasMany(p => p.Details)
                .WithOne(d => d.PayrollHeader)
                .HasForeignKey(d => d.PayrollHeaderId)
                .OnDelete(DeleteBehavior.Cascade); // Borrar detalles si se borra el header

            // Phase E: Global query filter para multi-tenancy
            entity.HasQueryFilter(p =>
                _tenantContext == null ||
                _tenantContext.TenantId == 0 ||
                p.TenantId == _tenantContext.TenantId);
        });

        // Phase D: Configuraci�n de PayrollDetail
        modelBuilder.Entity<PayrollDetail>(entity =>
        {
            // �ndice �nico compuesto: Un empleado solo puede aparecer una vez por planilla
            entity.HasIndex(d => new { d.PayrollHeaderId, d.EmpleadoId })
                .IsUnique()
                .HasDatabaseName("IX_PayrollDetail_PayrollHeaderId_EmpleadoId");

            // Configuraci�n de precisi�n para campos decimales (moneda)
            entity.Property(d => d.GrossPay).HasPrecision(18, 2);
            entity.Property(d => d.BaseSalary).HasPrecision(18, 2);
            entity.Property(d => d.OvertimePay).HasPrecision(18, 2);
            entity.Property(d => d.Bonuses).HasPrecision(18, 2);
            entity.Property(d => d.Commissions).HasPrecision(18, 2);
            entity.Property(d => d.CssEmployee).HasPrecision(18, 2);
            entity.Property(d => d.CssEmployer).HasPrecision(18, 2);
            entity.Property(d => d.RiskContribution).HasPrecision(18, 2);
            entity.Property(d => d.EducationalInsuranceEmployee).HasPrecision(18, 2);
            entity.Property(d => d.EducationalInsuranceEmployer).HasPrecision(18, 2);
            entity.Property(d => d.IncomeTax).HasPrecision(18, 2);
            entity.Property(d => d.OtherDeductions).HasPrecision(18, 2);
            entity.Property(d => d.DeduccionesFijas).HasPrecision(18, 2);
            entity.Property(d => d.Prestamos).HasPrecision(18, 2);
            entity.Property(d => d.Anticipos).HasPrecision(18, 2);
            entity.Property(d => d.TotalDeductions).HasPrecision(18, 2);
            entity.Property(d => d.NetPay).HasPrecision(18, 2);
            entity.Property(d => d.EmployerCost).HasPrecision(18, 2);
            // Asistencia: Horas Extra
            entity.Property(d => d.HorasExtraDiurnas).HasPrecision(5, 2);
            entity.Property(d => d.HorasExtraNocturnas).HasPrecision(5, 2);
            entity.Property(d => d.HorasExtraDomingoFeriado).HasPrecision(5, 2);
            entity.Property(d => d.MontoHorasExtra).HasPrecision(18, 2);
            // Asistencia: Ausencias
            entity.Property(d => d.DiasAusenciaInjustificada).HasPrecision(5, 2);
            entity.Property(d => d.MontoDescuentoAusencias).HasPrecision(18, 2);
            // Asistencia: Vacaciones
            entity.Property(d => d.DiasVacaciones).HasPrecision(5, 2);
            entity.Property(d => d.MontoVacaciones).HasPrecision(18, 2);

            // Relaci�n N:1 con Empleado
            entity.HasOne(d => d.Empleado)
                .WithMany()
                .HasForeignKey(d => d.EmpleadoId)
                .OnDelete(DeleteBehavior.Restrict); // NO borrar empleado si tiene detalles de planilla
        });

        // Organizaci�n: Configuraci�n de Departamento
        modelBuilder.Entity<Departamento>(entity =>
        {
            // �ndice �nico compuesto: C�digo debe ser �nico por compa��a
            entity.HasIndex(d => new { d.TenantId, d.Codigo })
                .IsUnique()
                .HasDatabaseName("IX_Departamento_TenantId_Codigo");

            // Relaci�n con Manager (jefe del departamento) - opcional
            entity.HasOne(d => d.Manager)
                .WithMany()
                .HasForeignKey(d => d.ManagerId)
                .OnDelete(DeleteBehavior.SetNull); // Si se borra el manager, poner NULL

            // Relaci�n 1:N con Empleados
            entity.HasMany(d => d.Empleados)
                .WithOne(e => e.Departamento)
                .HasForeignKey(e => e.DepartamentoId)
                .OnDelete(DeleteBehavior.SetNull); // Si se borra departamento, poner NULL en empleados

            // Relaci�n 1:N con Posiciones
            entity.HasMany(d => d.Posiciones)
                .WithOne(p => p.Departamento)
                .HasForeignKey(p => p.DepartamentoId)
                .OnDelete(DeleteBehavior.Restrict); // NO borrar departamento si tiene posiciones

            // Global query filter para multi-tenancy
            entity.HasQueryFilter(d =>
                _tenantContext == null ||
                _tenantContext.TenantId == 0 ||
                d.TenantId == _tenantContext.TenantId);
        });

        // Organizaci�n: Configuraci�n de Posicion
        modelBuilder.Entity<Posicion>(entity =>
        {
            // �ndice �nico compuesto: C�digo debe ser �nico por compa��a
            entity.HasIndex(p => new { p.TenantId, p.Codigo })
                .IsUnique()
                .HasDatabaseName("IX_Posicion_TenantId_Codigo");

            // Configuraci�n de precisi�n para campos decimales (salarios)
            entity.Property(p => p.SalarioMinimo).HasPrecision(18, 2);
            entity.Property(p => p.SalarioMaximo).HasPrecision(18, 2);

            // Relaci�n 1:N con Empleados
            entity.HasMany(p => p.Empleados)
                .WithOne(e => e.Posicion)
                .HasForeignKey(e => e.PosicionId)
                .OnDelete(DeleteBehavior.SetNull); // Si se borra posici�n, poner NULL en empleados

            // Global query filter para multi-tenancy
            entity.HasQueryFilter(p =>
                _tenantContext == null ||
                _tenantContext.TenantId == 0 ||
                p.TenantId == _tenantContext.TenantId);
        });

        // Conceptos de N�mina: Configuraci�n de Prestamo
        modelBuilder.Entity<Prestamo>(entity =>
        {
            // �ndice compuesto para b�squedas por empleado y estado
            entity.HasIndex(p => new { p.EmpleadoId, p.Estado })
                .HasDatabaseName("IX_Prestamo_EmpleadoId_Estado");

            // Configuraci�n de precisi�n para campos decimales (moneda)
            entity.Property(p => p.MontoOriginal).HasPrecision(18, 2);
            entity.Property(p => p.MontoPendiente).HasPrecision(18, 2);
            entity.Property(p => p.CuotaMensual).HasPrecision(18, 2);
            entity.Property(p => p.TasaInteres).HasPrecision(5, 2);

            // Relaci�n N:1 con Empleado
            entity.HasOne(p => p.Empleado)
                .WithMany()
                .HasForeignKey(p => p.EmpleadoId)
                .OnDelete(DeleteBehavior.Restrict); // NO borrar empleado si tiene pr�stamos

            // Relaci�n 1:N con PagosPrestamo
            entity.HasMany(p => p.PagosPrestamo)
                .WithOne(pp => pp.Prestamo)
                .HasForeignKey(pp => pp.PrestamoId)
                .OnDelete(DeleteBehavior.Cascade); // Borrar pagos si se borra el pr�stamo

            // Global query filter para multi-tenancy
            entity.HasQueryFilter(p =>
                _tenantContext == null ||
                _tenantContext.TenantId == 0 ||
                p.TenantId == _tenantContext.TenantId);
        });

        // Conceptos de N�mina: Configuraci�n de DeduccionFija
        modelBuilder.Entity<DeduccionFija>(entity =>
        {
            // �ndice compuesto para b�squedas por empleado y estado
            entity.HasIndex(d => new { d.EmpleadoId, d.EstaActivo })
                .HasDatabaseName("IX_DeduccionFija_EmpleadoId_EstaActivo");

            // �ndice para b�squedas por tipo
            entity.HasIndex(d => d.TipoDeduccion)
                .HasDatabaseName("IX_DeduccionFija_TipoDeduccion");

            // Configuraci�n de precisi�n para campos decimales
            entity.Property(d => d.Monto).HasPrecision(18, 2);
            entity.Property(d => d.Porcentaje).HasPrecision(5, 2);

            // Relaci�n N:1 con Empleado
            entity.HasOne(d => d.Empleado)
                .WithMany()
                .HasForeignKey(d => d.EmpleadoId)
                .OnDelete(DeleteBehavior.Restrict); // NO borrar empleado si tiene deducciones

            // Global query filter para multi-tenancy
            entity.HasQueryFilter(d =>
                _tenantContext == null ||
                _tenantContext.TenantId == 0 ||
                d.TenantId == _tenantContext.TenantId);
        });

        // Conceptos de N�mina: Configuraci�n de Anticipo
        modelBuilder.Entity<Anticipo>(entity =>
        {
            // �ndice compuesto para b�squedas por empleado y estado
            entity.HasIndex(a => new { a.EmpleadoId, a.Estado })
                .HasDatabaseName("IX_Anticipo_EmpleadoId_Estado");

            // �ndice para b�squedas por fecha de descuento
            entity.HasIndex(a => new { a.FechaDescuento, a.Estado })
                .HasDatabaseName("IX_Anticipo_FechaDescuento_Estado");

            // Configuraci�n de precisi�n para campos decimales
            entity.Property(a => a.Monto).HasPrecision(18, 2);

            // Relaci�n N:1 con Empleado
            entity.HasOne(a => a.Empleado)
                .WithMany()
                .HasForeignKey(a => a.EmpleadoId)
                .OnDelete(DeleteBehavior.Restrict); // NO borrar empleado si tiene anticipos

            // Global query filter para multi-tenancy
            entity.HasQueryFilter(a =>
                _tenantContext == null ||
                _tenantContext.TenantId == 0 ||
                a.TenantId == _tenantContext.TenantId);
        });

        // Conceptos de N�mina: Configuraci�n de PagoPrestamo
        modelBuilder.Entity<PagoPrestamo>(entity =>
        {
            // �ndice para b�squedas por pr�stamo
            entity.HasIndex(pp => pp.PrestamoId)
                .HasDatabaseName("IX_PagoPrestamo_PrestamoId");

            // Configuraci�n de precisi�n para campos decimales
            entity.Property(pp => pp.MontoPagado).HasPrecision(18, 2);
            entity.Property(pp => pp.SaldoAnterior).HasPrecision(18, 2);
            entity.Property(pp => pp.SaldoNuevo).HasPrecision(18, 2);
        });

        // Asistencia: Configuraci�n de HoraExtra
        modelBuilder.Entity<HoraExtra>(entity =>
        {
            entity.HasIndex(h => new { h.EmpleadoId, h.Fecha })
                .HasDatabaseName("IX_HoraExtra_EmpleadoId_Fecha");

            entity.HasIndex(h => new { h.EstaAprobada, h.Fecha })
                .HasDatabaseName("IX_HoraExtra_EstaAprobada_Fecha");

            entity.Property(h => h.CantidadHoras).HasPrecision(5, 2);
            entity.Property(h => h.FactorMultiplicador).HasPrecision(4, 2);
            entity.Property(h => h.MontoCalculado).HasPrecision(18, 2);

            entity.HasOne(h => h.Empleado)
                .WithMany()
                .HasForeignKey(h => h.EmpleadoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(h =>
                _tenantContext == null ||
                _tenantContext.TenantId == 0 ||
                h.TenantId == _tenantContext.TenantId);
        });

        // Asistencia: Configuraci�n de Ausencia
        modelBuilder.Entity<Ausencia>(entity =>
        {
            entity.HasIndex(a => new { a.EmpleadoId, a.FechaInicio })
                .HasDatabaseName("IX_Ausencia_EmpleadoId_FechaInicio");

            entity.Property(a => a.DiasAusencia).HasPrecision(5, 2);
            entity.Property(a => a.MontoDescontado).HasPrecision(18, 2);

            entity.HasOne(a => a.Empleado)
                .WithMany()
                .HasForeignKey(a => a.EmpleadoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(a =>
                _tenantContext == null ||
                _tenantContext.TenantId == 0 ||
                a.TenantId == _tenantContext.TenantId);
        });

        // Asistencia: Configuraci�n de SolicitudVacaciones
        modelBuilder.Entity<SolicitudVacaciones>(entity =>
        {
            entity.HasIndex(v => new { v.EmpleadoId, v.Estado })
                .HasDatabaseName("IX_SolicitudVacaciones_EmpleadoId_Estado");

            entity.HasIndex(v => new { v.FechaInicio, v.FechaFin })
                .HasDatabaseName("IX_SolicitudVacaciones_Fechas");

            entity.Property(v => v.DiasProporcionales).HasPrecision(5, 2);

            entity.HasOne(v => v.Empleado)
                .WithMany()
                .HasForeignKey(v => v.EmpleadoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(v =>
                _tenantContext == null ||
                _tenantContext.TenantId == 0 ||
                v.TenantId == _tenantContext.TenantId);
        });

        // Asistencia: Configuraci�n de SaldoVacaciones
        modelBuilder.Entity<SaldoVacaciones>(entity =>
        {
            // �ndice �nico: un empleado solo puede tener un saldo de vacaciones
            entity.HasIndex(s => new { s.TenantId, s.EmpleadoId })
                .IsUnique()
                .HasDatabaseName("IX_SaldoVacaciones_TenantId_EmpleadoId");

            entity.Property(s => s.DiasAcumulados).HasPrecision(6, 2);
            entity.Property(s => s.DiasTomados).HasPrecision(6, 2);
            entity.Property(s => s.DiasDisponibles).HasPrecision(6, 2);

            entity.HasOne(s => s.Empleado)
                .WithMany()
                .HasForeignKey(s => s.EmpleadoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(s =>
                _tenantContext == null ||
                _tenantContext.TenantId == 0 ||
                s.TenantId == _tenantContext.TenantId);
        });

        // ====================================================================
        // MULTI-TENANT: Configuraci�n de entidades SaaS
        // ====================================================================

        // Tenant Configuration
        modelBuilder.Entity<Tenant>(entity =>
        {
            // �ndice �nico para subdomain
            entity.HasIndex(t => t.Subdomain)
                .IsUnique()
                .HasDatabaseName("IX_Tenant_Subdomain");

            // �ndice para RUC (�nico por RUC+DV)
            entity.HasIndex(t => new { t.RUC, t.DV })
                .IsUnique()
                .HasDatabaseName("IX_Tenant_RUC_DV");

            // Relaci�n 1:1 con Subscription
            entity.HasOne(t => t.Subscription)
                .WithOne(s => s.Tenant)
                .HasForeignKey<Subscription>(s => s.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relaci�n 1:N con TenantUsers
            entity.HasMany(t => t.Users)
                .WithOne(tu => tu.Tenant)
                .HasForeignKey(tu => tu.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relaci�n 1:N con Empleados
            entity.HasMany(t => t.Empleados)
                .WithOne(e => e.Tenant)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relaci�n 1:N con PayrollHeaders
            entity.HasMany(t => t.PayrollHeaders)
                .WithOne(p => p.Tenant)
                .HasForeignKey(p => p.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // NO aplicar query filter a Tenant (necesitamos acceso global para admin)
        });

        // Subscription Configuration
        modelBuilder.Entity<Subscription>(entity =>
        {
            // �ndice para b�squedas por status
            entity.HasIndex(s => s.Status)
                .HasDatabaseName("IX_Subscription_Status");

            // �ndice para Stripe Customer ID
            entity.HasIndex(s => s.StripeCustomerId)
                .HasDatabaseName("IX_Subscription_StripeCustomerId");

            // Configuraci�n de precisi�n para MonthlyPrice
            entity.Property(s => s.MonthlyPrice).HasPrecision(10, 2);

            // NO aplicar query filter a Subscription (se filtra por Tenant)
        });

        // TenantUser Configuration
        modelBuilder.Entity<TenantUser>(entity =>
        {
            // �ndice compuesto: TenantId + UserId (�nico)
            entity.HasIndex(tu => new { tu.TenantId, tu.UserId })
                .IsUnique()
                .HasDatabaseName("IX_TenantUser_TenantId_UserId");

            // �ndice para invitation token
            entity.HasIndex(tu => tu.InvitationToken)
                .HasDatabaseName("IX_TenantUser_InvitationToken");

            // Relaci�n con AppUser
            entity.HasOne(tu => tu.User)
                .WithMany()
                .HasForeignKey(tu => tu.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Query filter por TenantId
            entity.HasQueryFilter(tu =>
                _tenantContext == null ||
                _tenantContext.TenantId == 0 ||
                tu.TenantId == _tenantContext.TenantId);
        });

        // StripeWebhookEvent Configuration
        modelBuilder.Entity<StripeWebhookEvent>(entity =>
        {
            // Índice único para Stripe Event ID (idempotency)
            entity.HasIndex(e => e.StripeEventId)
                .IsUnique()
                .HasDatabaseName("IX_StripeWebhookEvent_StripeEventId");

            // Índice para búsquedas por TenantId
            entity.HasIndex(e => e.TenantId)
                .HasDatabaseName("IX_StripeWebhookEvent_TenantId");

            // Índice para búsquedas por Status
            entity.HasIndex(e => e.Status)
                .HasDatabaseName("IX_StripeWebhookEvent_Status");

            // Relación con Tenant (nullable)
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.SetNull);

            // NO aplicar query filter (necesitamos procesar todos los webhooks)
        });

        // ====================================================================
        // PHASE 3: Role and Permission Management
        // ====================================================================

        // TenantInvitation Configuration
        modelBuilder.Entity<TenantInvitation>(entity =>
        {
            // Índice único para token de invitación
            entity.HasIndex(i => i.Token)
                .IsUnique()
                .HasDatabaseName("IX_TenantInvitation_Token");

            // Índice compuesto para búsquedas por tenant y email
            entity.HasIndex(i => new { i.TenantId, i.Email })
                .HasDatabaseName("IX_TenantInvitation_TenantId_Email");

            // Índice para búsquedas por estado (no aceptadas, no expiradas, no revocadas)
            entity.HasIndex(i => new { i.TenantId, i.AcceptedAt, i.ExpiresAt, i.IsRevoked })
                .HasDatabaseName("IX_TenantInvitation_Status");

            // Relación con Tenant
            entity.HasOne(i => i.Tenant)
                .WithMany()
                .HasForeignKey(i => i.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relación con CreatedBy (AppUser)
            entity.HasOne(i => i.CreatedBy)
                .WithMany()
                .HasForeignKey(i => i.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Query filter por TenantId
            entity.HasQueryFilter(i =>
                _tenantContext == null ||
                _tenantContext.TenantId == 0 ||
                i.TenantId == _tenantContext.TenantId);
        });

        // AuditLogEntry Configuration
        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            // Índice compuesto para búsquedas por tenant y fecha
            entity.HasIndex(a => new { a.TenantId, a.CreatedAt })
                .HasDatabaseName("IX_AuditLogEntry_TenantId_CreatedAt");

            // Índice para búsquedas por acción
            entity.HasIndex(a => new { a.TenantId, a.Action })
                .HasDatabaseName("IX_AuditLogEntry_TenantId_Action");

            // Índice para búsquedas por entidad
            entity.HasIndex(a => new { a.TenantId, a.EntityType, a.EntityId })
                .HasDatabaseName("IX_AuditLogEntry_TenantId_Entity");

            // Índice para búsquedas por actor
            entity.HasIndex(a => new { a.TenantId, a.ActorUserId })
                .HasDatabaseName("IX_AuditLogEntry_TenantId_ActorUserId");

            // Relación con Tenant
            entity.HasOne(a => a.Tenant)
                .WithMany()
                .HasForeignKey(a => a.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            // Query filter por TenantId
            entity.HasQueryFilter(a =>
                _tenantContext == null ||
                _tenantContext.TenantId == 0 ||
                a.TenantId == _tenantContext.TenantId);
        });
    }
}