using Microsoft.AspNetCore.Identity.EntityFrameworkCore; // <--- USANDO A�ADIDO
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System.Linq.Expressions;
using System.Reflection;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;                         // <--- USANDO A�ADIDO
using Vorluno.Planilla.Domain.Interfaces;

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
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<CustomTenantRole> CustomTenantRoles { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }

    public DbSet<Empleado> Empleados { get; set; }
    public DbSet<ReciboDeSueldo> RecibosDeSueldo { get; set; }

    // Phase A: Configuraci�n de planilla (tasas CSS, SE, ISR)
    public DbSet<PayrollTaxConfiguration> PayrollTaxConfigurations { get; set; }
    public DbSet<TaxBracket> TaxBrackets { get; set; }

    // Phase D: Workflow de planilla
    public DbSet<PayrollHeader> PayrollHeaders { get; set; }
    public DbSet<PayrollDetail> PayrollDetails { get; set; }

    // Phase P1: Horas trabajadas por empleado por planilla
    public DbSet<PayrollEmployeeHours> PayrollEmployeeHours { get; set; } = null!;

    // Organizaci�n: Departamentos y Posiciones
    public DbSet<Departamento> Departamentos { get; set; }
    public DbSet<Posicion> Posiciones { get; set; }

    // Conceptos de N�mina: Pr�stamos, Deducciones y Anticipos
    public DbSet<Prestamo> Prestamos { get; set; }
    public DbSet<DeduccionFija> DeduccionesFijas { get; set; }
    public DbSet<Anticipo> Anticipos { get; set; }
    public DbSet<PagoPrestamo> PagosPrestamos { get; set; }

    // Auditoría de deducciones aplicadas
    public DbSet<DeduccionAplicada> DeduccionesAplicadas { get; set; }

    // Catálogo de acreedores
    public DbSet<Acreedor> Acreedores { get; set; }

    // Asistencia: Horas Extra, Ausencias y Vacaciones
    public DbSet<HoraExtra> HorasExtra { get; set; }
    public DbSet<Ausencia> Ausencias { get; set; }
    public DbSet<SolicitudVacaciones> SolicitudesVacaciones { get; set; }
    public DbSet<SaldoVacaciones> SaldosVacaciones { get; set; }
    public DbSet<SaldoInicialEmpleado> SaldosInicialesEmpleados { get; set; }

    // DEV-26: Planilla de Décimo Tercer Mes
    public DbSet<PlanillaDecimo> PlanillasDecimo { get; set; }
    public DbSet<DetalleDecimo> DetallesDecimo { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Esta l�nea es crucial al heredar de IdentityDbContext
        base.OnModelCreating(modelBuilder);

        // ====================================================================
        // GLOBAL QUERY FILTERS para Multi-Tenancy (SEGURIDAD CRÍTICA)
        // ====================================================================
        // Aplica automáticamente filtro por TenantId a TODAS las entidades que implementan ITenantEntity
        // Esto garantiza que ninguna query pueda acceder accidentalmente a datos de otro tenant
        ApplyGlobalQueryFilters(modelBuilder);

        // ====================================================================
        // ÍNDICES DE PERFORMANCE (CRITICAL PARA PRODUCTION)
        // ====================================================================
        // Configuración de índices para entidades críticas
        modelBuilder.Entity<Empleado>(entity =>
        {
            // Índice único en número de identificación
            entity.HasIndex(e => e.NumeroIdentificacion)
                .IsUnique()
                .HasDatabaseName("IX_Empleado_NumeroIdentificacion");

            // Índice en TenantId para queries frecuentes
            entity.HasIndex(e => e.TenantId)
                .HasDatabaseName("IX_Empleado_TenantId");

            // Índice compuesto para búsquedas por tenant y estado activo
            entity.HasIndex(e => new { e.TenantId, e.EstaActivo })
                .HasDatabaseName("IX_Empleado_TenantId_EstaActivo");

            // Índice compuesto para búsquedas por tenant y departamento
            entity.HasIndex(e => new { e.TenantId, e.DepartamentoId })
                .HasDatabaseName("IX_Empleado_TenantId_DepartamentoId");
        });

        modelBuilder.Entity<ReciboDeSueldo>(entity =>
        {
            // Índice en TenantId
            entity.HasIndex(r => r.TenantId)
                .HasDatabaseName("IX_ReciboDeSueldo_TenantId");

            // Índice compuesto para búsquedas por tenant y empleado
            entity.HasIndex(r => new { r.TenantId, r.EmpleadoId })
                .HasDatabaseName("IX_ReciboDeSueldo_TenantId_EmpleadoId");

            // Índice compuesto para búsquedas por tenant y fecha
            entity.HasIndex(r => new { r.TenantId, r.FechaGeneracion })
                .HasDatabaseName("IX_ReciboDeSueldo_TenantId_FechaGeneracion");
        });

        // PayrollDetail ya tiene índice compuesto en PayrollHeaderId y EmpleadoId
        // PagoPrestamo solo necesita índice en TenantId individual
        modelBuilder.Entity<PagoPrestamo>(entity =>
        {
            entity.HasIndex(pp => pp.TenantId)
                .HasDatabaseName("IX_PagoPrestamo_TenantId");
        });

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
            entity.Property(p => p.SalarioMinimoLegal).HasPrecision(18, 2);

            // Global query filter aplicado automáticamente por ApplyGlobalQueryFilters()
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

            // Global query filter aplicado automáticamente por ApplyGlobalQueryFilters()
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

            // Global query filter aplicado automáticamente por ApplyGlobalQueryFilters()
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
            entity.Property(d => d.PensionAlimenticia).HasPrecision(18, 2);
            entity.Property(d => d.Embargos).HasPrecision(18, 2);
            entity.Property(d => d.DeduccionesVoluntarias).HasPrecision(18, 2);
            entity.Property(d => d.SalarioMinimoLegalAplicado).HasPrecision(18, 2);
            entity.Property(d => d.MontoLimitadoPorSalarioMinimo).HasPrecision(18, 2);
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

        // Phase P1: Configuración de PayrollEmployeeHours
        modelBuilder.Entity<PayrollEmployeeHours>(entity =>
        {
            // Índice único: un registro de horas por empleado por planilla
            entity.HasIndex(e => new { e.PayrollHeaderId, e.EmpleadoId })
                .IsUnique()
                .HasDatabaseName("IX_PayrollEmployeeHours_HeaderId_EmpleadoId");

            // Índice por tenant para queries filtradas
            entity.HasIndex(e => e.TenantId)
                .HasDatabaseName("IX_PayrollEmployeeHours_TenantId");

            // Configuración de precisión para campos decimales
            entity.Property(e => e.RegularHours).HasPrecision(8, 2);
            entity.Property(e => e.SundayHours).HasPrecision(8, 2);
            entity.Property(e => e.HolidayHours).HasPrecision(8, 2);
            entity.Property(e => e.OvertimeDayHours).HasPrecision(8, 2);
            entity.Property(e => e.OvertimeNightHours).HasPrecision(8, 2);
            entity.Property(e => e.AbsenceHours).HasPrecision(8, 2);
            entity.Property(e => e.DisabilityHours).HasPrecision(8, 2);
            entity.Property(e => e.RegularPay).HasPrecision(18, 2);
            entity.Property(e => e.SundayPay).HasPrecision(18, 2);
            entity.Property(e => e.HolidayPay).HasPrecision(18, 2);
            entity.Property(e => e.OvertimeDayPay).HasPrecision(18, 2);
            entity.Property(e => e.OvertimeNightPay).HasPrecision(18, 2);
            entity.Property(e => e.AbsenceDeduction).HasPrecision(18, 2);
            entity.Property(e => e.TotalHoursPay).HasPrecision(18, 2);

            // Relaciones
            entity.HasOne(e => e.PayrollHeader)
                .WithMany()
                .HasForeignKey(e => e.PayrollHeaderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Empleado)
                .WithMany()
                .HasForeignKey(e => e.EmpleadoId)
                .OnDelete(DeleteBehavior.Restrict);

            // Global query filter aplicado automáticamente por ApplyGlobalQueryFilters()
        });

        // Índice de PayPeriodType en PayrollHeader
        modelBuilder.Entity<PayrollHeader>()
            .HasIndex(e => new { e.TenantId, e.PayPeriodType })
            .HasDatabaseName("IX_PayrollHeaders_TenantId_PayPeriodType");

        // Configuración de precisión para nuevos campos en Empleado
        modelBuilder.Entity<Empleado>(entity =>
        {
            entity.Property(e => e.HoursPerPeriod).HasPrecision(8, 2);
            entity.Property(e => e.HourlyRate).HasPrecision(18, 4);
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

            // Global query filter aplicado automáticamente por ApplyGlobalQueryFilters()
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

            // Global query filter aplicado automáticamente por ApplyGlobalQueryFilters()
        });

        // Catálogo de Acreedores
        modelBuilder.Entity<Acreedor>(entity =>
        {
            entity.Property(a => a.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(a => a.Identificacion).HasMaxLength(50);
            entity.Property(a => a.TipoIdentificacion).HasMaxLength(20);
            entity.Property(a => a.Banco).HasMaxLength(100);
            entity.Property(a => a.TipoCuenta).HasMaxLength(30);
            entity.Property(a => a.NumeroCuenta).HasMaxLength(50);
            entity.Property(a => a.IBAN).HasMaxLength(50);
            entity.Property(a => a.Telefono).HasMaxLength(30);
            entity.Property(a => a.Email).HasMaxLength(100);
            entity.Property(a => a.Direccion).HasMaxLength(300);
            entity.Property(a => a.ContactoNombre).HasMaxLength(200);
            entity.Property(a => a.Observaciones).HasMaxLength(500);
            entity.Property(a => a.CreatedBy).HasMaxLength(100);

            entity.HasIndex(a => new { a.TenantId, a.Nombre })
                .HasDatabaseName("IX_Acreedor_TenantId_Nombre");

            entity.HasIndex(a => new { a.TenantId, a.TipoAcreedor })
                .HasDatabaseName("IX_Acreedor_TenantId_TipoAcreedor");

            entity.HasIndex(a => new { a.TenantId, a.Identificacion })
                .HasDatabaseName("IX_Acreedor_TenantId_Identificacion");
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

            // Relación N:1 con Acreedor (opcional)
            entity.HasOne(p => p.Acreedor)
                .WithMany(a => a.Prestamos)
                .HasForeignKey(p => p.AcreedorId)
                .OnDelete(DeleteBehavior.Restrict);
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
            entity.Property(d => d.MontoTotalACobrar).HasPrecision(18, 2);
            entity.Property(d => d.MontoCobradoAcumulado).HasPrecision(18, 2);

            // Relaci�n N:1 con Empleado
            entity.HasOne(d => d.Empleado)
                .WithMany()
                .HasForeignKey(d => d.EmpleadoId)
                .OnDelete(DeleteBehavior.Restrict); // NO borrar empleado si tiene deducciones

            // Relación N:1 con Acreedor (opcional)
            entity.HasOne(d => d.Acreedor)
                .WithMany(a => a.Deducciones)
                .HasForeignKey(d => d.AcreedorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Auditoría: Configuración de DeduccionAplicada
        modelBuilder.Entity<DeduccionAplicada>(entity =>
        {
            entity.HasIndex(da => da.PayrollDetailId)
                .HasDatabaseName("IX_DeduccionAplicada_PayrollDetailId");

            entity.HasIndex(da => da.DeduccionFijaId)
                .HasDatabaseName("IX_DeduccionAplicada_DeduccionFijaId");

            entity.HasIndex(da => new { da.TenantId, da.PayrollDetailId })
                .HasDatabaseName("IX_DeduccionAplicada_TenantId_PayrollDetailId");

            entity.Property(da => da.MontoSolicitado).HasPrecision(18, 2);
            entity.Property(da => da.MontoAplicado).HasPrecision(18, 2);
            entity.Property(da => da.MontoLimitado).HasPrecision(18, 2);
            entity.Property(da => da.SaldoDisponibleAntes).HasPrecision(18, 2);
            entity.Property(da => da.SaldoDisponibleDespues).HasPrecision(18, 2);

            entity.HasOne(da => da.PayrollDetail)
                .WithMany(pd => pd.DeduccionesAplicadas)
                .HasForeignKey(da => da.PayrollDetailId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(da => da.DeduccionFija)
                .WithMany()
                .HasForeignKey(da => da.DeduccionFijaId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(da => da.Prestamo)
                .WithMany()
                .HasForeignKey(da => da.PrestamoId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(da => da.Anticipo)
                .WithMany()
                .HasForeignKey(da => da.AnticipoId)
                .OnDelete(DeleteBehavior.SetNull);
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
            // Global query filter aplicado automáticamente por ApplyGlobalQueryFilters()
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

            // Global query filter aplicado automáticamente por ApplyGlobalQueryFilters()
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

            // Global query filter aplicado automáticamente por ApplyGlobalQueryFilters()
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

            // Global query filter aplicado automáticamente por ApplyGlobalQueryFilters()
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

            // Global query filter aplicado automáticamente por ApplyGlobalQueryFilters()
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
            // Global query filter aplicado automáticamente por ApplyGlobalQueryFilters()
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
            // Global query filter aplicado automáticamente por ApplyGlobalQueryFilters()
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
            // Global query filter aplicado automáticamente por ApplyGlobalQueryFilters()
        });

        // RefreshToken Configuration
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            // Índice único en Token para búsquedas rápidas
            entity.HasIndex(rt => rt.Token)
                .IsUnique()
                .HasDatabaseName("IX_RefreshToken_Token");

            // Índice compuesto para búsquedas por usuario
            entity.HasIndex(rt => new { rt.UserId, rt.IsRevoked, rt.ExpiresAt })
                .HasDatabaseName("IX_RefreshToken_UserId_IsRevoked_ExpiresAt");

            // Índice para cleanup de tokens expirados
            entity.HasIndex(rt => new { rt.ExpiresAt, rt.IsRevoked })
                .HasDatabaseName("IX_RefreshToken_ExpiresAt_IsRevoked");

            // Relación con User
            entity.HasOne(rt => rt.User)
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relación con Tenant
            entity.HasOne(rt => rt.Tenant)
                .WithMany()
                .HasForeignKey(rt => rt.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ====================================================================
        // CUSTOM ROLES AND PERMISSIONS SYSTEM
        // ====================================================================

        // CustomTenantRole Configuration
        modelBuilder.Entity<CustomTenantRole>(entity =>
        {
            // Índice único compuesto: nombre de rol único por tenant
            entity.HasIndex(r => new { r.TenantId, r.Name })
                .IsUnique()
                .HasDatabaseName("IX_CustomTenantRole_TenantId_Name");

            // Índice para búsquedas por tenant y sistema
            entity.HasIndex(r => new { r.TenantId, r.IsSystem })
                .HasDatabaseName("IX_CustomTenantRole_TenantId_IsSystem");

            // Relación con Tenant
            entity.HasOne(r => r.Tenant)
                .WithMany()
                .HasForeignKey(r => r.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relación 1:N con Permissions
            entity.HasMany(r => r.Permissions)
                .WithOne(p => p.Role)
                .HasForeignKey(p => p.CustomTenantRoleId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relación 1:N con Users
            entity.HasMany(r => r.Users)
                .WithOne(u => u.CustomRole)
                .HasForeignKey(u => u.CustomTenantRoleId)
                .OnDelete(DeleteBehavior.SetNull);

            // Query filter por TenantId aplicado automáticamente
        });

        // RolePermission Configuration
        modelBuilder.Entity<RolePermission>(entity =>
        {
            // Índice compuesto para búsquedas por rol y permiso
            entity.HasIndex(p => new { p.CustomTenantRoleId, p.Permission })
                .HasDatabaseName("IX_RolePermission_RoleId_Permission");

            // Índice por tenant
            entity.HasIndex(p => p.TenantId)
                .HasDatabaseName("IX_RolePermission_TenantId");

            // Relación con Tenant
            entity.HasOne(p => p.Tenant)
                .WithMany()
                .HasForeignKey(p => p.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            // Query filter por TenantId aplicado automáticamente
        });

        // TenantUser - Agregar relación con CustomTenantRole
        modelBuilder.Entity<TenantUser>(entity =>
        {
            // Relación con CustomTenantRole (ya configurada en CustomTenantRole)
            // Solo agregamos la navegación inversa si no existe
        });
    }

    /// <summary>
    /// Aplica automáticamente query filters por TenantId a todas las entidades que implementan ITenantEntity.
    /// Este método usa reflexión para detectar entidades multi-tenant y aplicar el filtro globalmente.
    /// CRÍTICO PARA SEGURIDAD: Garantiza que ninguna query pueda acceder a datos de otro tenant.
    /// </summary>
    private void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        // Obtener todas las entity types del modelo
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Verificar si la entidad implementa ITenantEntity
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                // Crear expresión lambda: e => e.TenantId == _tenantContext.TenantId
                var parameter = Expression.Parameter(entityType.ClrType, "e");

                // Acceso a la propiedad TenantId de la entidad
                var tenantIdProperty = Expression.Property(parameter, nameof(ITenantEntity.TenantId));

                // Acceso a _tenantContext.TenantId
                var tenantContextField = Expression.Field(Expression.Constant(this), nameof(_tenantContext));
                var tenantContextTenantId = Expression.Property(tenantContextField, nameof(ITenantContext.TenantId));

                // Condición: _tenantContext == null || _tenantContext.TenantId == 0 || e.TenantId == _tenantContext.TenantId
                var tenantContextNullCheck = Expression.Equal(tenantContextField, Expression.Constant(null, typeof(ITenantContext)));
                var tenantIdZeroCheck = Expression.Equal(tenantContextTenantId, Expression.Constant(0));
                var tenantIdMatch = Expression.Equal(tenantIdProperty, tenantContextTenantId);

                var filterExpression = Expression.OrElse(
                    Expression.OrElse(tenantContextNullCheck, tenantIdZeroCheck),
                    tenantIdMatch
                );

                // Crear lambda: e => (_tenantContext == null || _tenantContext.TenantId == 0 || e.TenantId == _tenantContext.TenantId)
                var lambda = Expression.Lambda(filterExpression, parameter);

                // Aplicar el query filter usando reflexión
                // Necesitamos llamar a Entity<T>() para obtener EntityTypeBuilder<T>
                var entityMethod = typeof(ModelBuilder)
                    .GetMethods()
                    .First(m => m.Name == "Entity" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
                var genericEntityMethod = entityMethod.MakeGenericMethod(entityType.ClrType);
                var entityTypeBuilder = genericEntityMethod.Invoke(modelBuilder, null);

                // Ahora llamamos HasQueryFilter en EntityTypeBuilder<T>
                var entityTypeBuilderType = typeof(EntityTypeBuilder<>).MakeGenericType(entityType.ClrType);
                var hasQueryFilterMethod = entityTypeBuilderType.GetMethod("HasQueryFilter", new[] { lambda.GetType() });
                hasQueryFilterMethod!.Invoke(entityTypeBuilder, new object[] { lambda });
            }
        }
    }
}