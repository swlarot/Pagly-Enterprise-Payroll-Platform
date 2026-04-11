// RISK: Removing Blazor components - converting to Web API + React SPA architecture
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Stripe;
using System.Text;
using System.Text.Json;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Infrastructure.Configuration;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Infrastructure.Services;
using Vorluno.Planilla.Web.Extensions;
using Vorluno.Planilla.Web.Health;
using Vorluno.Planilla.Application.Mappings;
using Vorluno.Planilla.Web.Middleware;

// CONFIGURACIÓN GLOBAL: Permitir que Npgsql acepte DateTime sin Kind específico
// Esto soluciona el error "Cannot write DateTime with Kind=Unspecified to PostgreSQL type 'timestamp with time zone'"
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// --- INICIO DE LA CONFIGURACI�N DE SERVICIOS ---


// 1. LEER LA CADENA DE CONEXI�N
// --- LOGGING: JSON en producción ---
if (builder.Environment.IsProduction())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options => { options.IncludeScopes = true; });
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// 2. REGISTRAR EL DBCONTEXT PARA INYECCI�N DE DEPENDENCIAS
//    Le decimos a la aplicaci�n c�mo crear instancias de nuestro ApplicationDbContext,
//    configur�ndolo para que use SQL Server con la cadena de conexi�n.
// En entorno de Testing, se configurará In-Memory en CustomWebApplicationFactory
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseNpgsql(connectionString);
        // Ignorar advertencia de modelo pendiente durante desarrollo
        options.ConfigureWarnings(warnings =>
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    });
}

// 3. CONFIGURAR ASP.NET CORE IDENTITY
//    Configura el sistema de usuarios y roles, usando nuestro ApplicationDbContext para almacenar los datos
//    y nuestra clase AppUser como el modelo de usuario.
//    PAGLY: Políticas de contraseña relajadas - sin caracteres especiales requeridos
builder.Services.AddIdentity<AppUser, IdentityRole>(options => {
    // No requerir email confirmado - usuarios creados por admin
    options.SignIn.RequireConfirmedAccount = false;
    // Políticas de contraseña relajadas para Pagly
    // Solo requerimos longitud mínima razonable (8 caracteres)
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;  // Sin caracteres especiales
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// 4. <<<---  AUTOMAPPER ---<<<
builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);

// 5. CONFIGURAR JWT AUTHENTICATION
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Planilla";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Planilla";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment(); // Solo require HTTPS en producción
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero, // Elimina el buffer de 5 minutos por defecto
        RoleClaimType = "tenant_role" // Map "tenant_role" claim to Role for [Authorize(Roles=...)]
    };
});

// 6. CONFIGURAR AUTHORIZATION POLICIES MULTI-TENANT
// Estrategia: solo dos roles a nivel sistema (Owner, User). El resto se controla por permisos (CustomTenantRole).
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireOwner", p => p.RequireRole("Owner"))
    .AddPolicy("RequireAdmin", p => p.RequireRole("Owner")) // Ya no existe Admin; mismo que Owner para compatibilidad
    .AddPolicy("RequireManager", p => p.RequireRole("Owner")) // Acceso por permisos en cada controller
    .AddPolicy("RequireAccountant", p => p.RequireRole("Owner"))
    .AddPolicy("TenantManageUsers", p => p.RequireRole("Owner")) // Solo Owner invita y asigna roles
    .AddPolicy("TenantInvite", p => p.RequireRole("Owner"))
    .AddPolicy("PayrollManage", p => p.RequireRole("Owner"))
    .AddPolicy("ReportsRead", p => p.RequireRole("Owner"))
    .AddPolicy("RequireSystemAdmin", p => p.Requirements.Add(new Vorluno.Planilla.Web.Authorization.SystemAdminRequirement()));

// Registrar el handler de autorización para SystemAdmin
builder.Services.AddSingleton<IAuthorizationHandler, Vorluno.Planilla.Web.Authorization.SystemAdminAuthorizationHandler>();

// 7. REGISTRAR SERVICIOS DE LA APLICACIÓN (UnitOfWork, etc.)
builder.Services.ConfigureApplicationServices();

// 8. REGISTRAR HTTPCONTEXTACCESSOR (requerido para ITenantContext y JWT claims)
builder.Services.AddHttpContextAccessor();

// 9. REGISTRAR SERVICIOS MULTI-TENANT
builder.Services.AddScoped<Vorluno.Planilla.Application.Interfaces.ITenantContext, Vorluno.Planilla.Infrastructure.Services.TenantContext>();

// 10. CONFIGURAR STRIPE
var stripeOptions = builder.Configuration.GetSection(StripeOptions.SectionName).Get<StripeOptions>();
if (stripeOptions != null)
{
    try
    {
        stripeOptions.Validate();
        builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));
        StripeConfiguration.ApiKey = stripeOptions.SecretKey;
    }
    catch (InvalidOperationException)
    {
        // Stripe no configurado; los endpoints de billing no funcionarán. No usar BuildServiceProvider aquí.
    }
}

// 11. REGISTRAR SERVICIOS STRIPE
builder.Services.AddScoped<IStripeBillingService, StripeBillingService>();
builder.Services.AddScoped<IPlanLimitService, PlanLimitService>();

// API Platform B2B — servicio de gestión de keys (Stripe-like: prefijo + hash SHA256)
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();

// 11.1. REGISTRAR SERVICIO DE EMAIL (BREVO)
builder.Services.AddScoped<IBrevoEmailService, BrevoEmailService>();

// 12. PHASE 3: REGISTRAR SERVICIOS DE TENANT MANAGEMENT
builder.Services.AddScoped<IJwtTokenService, Vorluno.Planilla.Web.Services.JwtTokenService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ITenantManagementService, TenantManagementService>();
builder.Services.AddScoped<IInvitationService, InvitationService>();
builder.Services.AddScoped<global::Planilla.Application.Services.IEmailService, Vorluno.Planilla.Infrastructure.Services.EmailService>();
builder.Services.AddScoped<global::Planilla.Application.Services.IPlanUsageService, Vorluno.Planilla.Infrastructure.Services.PlanUsageService>();
builder.Services.AddScoped<ICustomTenantRoleService, CustomTenantRoleService>();
builder.Services.AddScoped<IEmployeeDeletionValidationService, EmployeeDeletionValidationService>();
builder.Services.AddScoped<Vorluno.Planilla.Application.Interfaces.IAsistenciaCalculationService, Vorluno.Planilla.Infrastructure.Services.AsistenciaCalculationService>();
builder.Services.AddScoped<Vorluno.Planilla.Infrastructure.Services.PanamaHolidayService>();
builder.Services.AddScoped<Vorluno.Planilla.Application.Interfaces.IOvertimeFactorService, Vorluno.Planilla.Infrastructure.Services.OvertimeFactorService>();
builder.Services.AddScoped<Vorluno.Planilla.Application.Interfaces.IDecimoCalculationService, Vorluno.Planilla.Infrastructure.Services.DecimoCalculationService>();

// Liquidaciones laborales (settlements)
builder.Services.AddScoped<Vorluno.Planilla.Application.Services.LiquidacionCalculationService>();

// --- FIN DE NUESTRA CONFIGURACIÓN PRINCIPAL ---

// 13. CONFIGURAR CORS (desarrollo: localhost; producción: Cors:AllowedOrigins desde env/config)
var corsOrigins = builder.Configuration["Cors:AllowedOrigins"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Where(s => !string.IsNullOrWhiteSpace(s))
    .ToArray() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        if (corsOrigins.Length > 0)
        {
            policy.WithOrigins(corsOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else if (builder.Environment.IsDevelopment())
        {
            policy.WithOrigins(
                    "http://localhost:5173",
                    "http://localhost:5174",
                    "http://localhost:5175",
                    "http://localhost:5176",
                    "http://localhost:5177")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else if (builder.Environment.IsDevelopment())
        {
            // Desarrollo sin variable de entorno: denegar todo (configuración incompleta)
            policy.WithOrigins("http://localhost:0"); // origen imposible → bloquea todo
        }
        else
        {
            // DEV-30: En producción, si Cors__AllowedOrigins no está configurada, fallar al arrancar.
            // Nunca abrir AllowAnyOrigin en producción.
            throw new InvalidOperationException(
                "CORS no configurado: la variable de entorno Cors__AllowedOrigins debe estar seteada en producción. " +
                "Ejemplo: https://app.pagly.io  Verifique la configuración en CapRover antes de desplegar.");
        }
    });
});

// 13.1 HEALTH CHECKS (PostgreSQL + multi-tenant)
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("postgres", tags: new[] { "db", "ready" })
    .AddCheck<MultiTenantHealthCheck>("multi_tenant", tags: new[] { "ready" });

// RISK: Removing Blazor services - Web API + Identity backend only
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Configurar para ignorar ciclos de referencia en serialización JSON
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        // Opcional: usar naming policy camelCase para consistencia con frontend
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Pagly API",
        Version = "v1",
        Description = "Multi-tenant Payroll SaaS API for Panama - Admin-managed users only"
    });

    // Configure JWT Bearer authentication
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Enable XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = System.IO.Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (System.IO.File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
// RISK: Creating inline no-op email sender since Blazor Components.Account was removed
builder.Services.AddSingleton<IEmailSender<AppUser>>(provider =>
    new NoOpEmailSender());

// QuestPDF Community license — configurar una sola vez al arrancar
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// TODO: React will handle authentication UI - backend provides Identity API endpoints

var app = builder.Build();

// --- MIGRACIONES Y SEEDING ---
// Aplicar migraciones pendientes y ejecutar seed de configuración
// Skip for Testing environment (integration tests use in-memory database)
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();

        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();

            // Aplicar migraciones (CRÍTICO - debe exitoso)
            logger.LogInformation("Aplicando migraciones pendientes...");
            await context.Database.MigrateAsync();
            logger.LogInformation("Migraciones aplicadas correctamente");

            // Ejecutar seed (NO CRÍTICO - permitir que app arranque incluso si falla)
            try
            {
                logger.LogInformation("Ejecutando seed de configuración multi-tenant...");
                await PayrollConfigSeeder.SeedAsync(context, logger);
                logger.LogInformation("✓ Seed de configuración completado exitosamente");
            }
            catch (Exception seedEx)
            {
                logger.LogWarning(seedEx, "⚠ Seed falló, pero la aplicación continuará. " +
                    "La configuración se puede crear manualmente o al registrar tenants.");
            }

            // Ejecutar seed de administradores del sistema
            try
            {
                logger.LogInformation("Ejecutando seed de administradores del sistema...");
                var userManager = services.GetRequiredService<UserManager<AppUser>>();
                await SystemAdminSeeder.SeedAsync(userManager, logger);
                logger.LogInformation("✓ Seed de administradores completado exitosamente");
            }
            catch (Exception adminSeedEx)
            {
                logger.LogWarning(adminSeedEx, "⚠ Seed de administradores falló, pero la aplicación continuará.");
            }

            // Ejecutar seed de roles del sistema para todos los tenants
            try
            {
                logger.LogInformation("Ejecutando seed de roles del sistema...");
                var dbContextForRoles = services.GetRequiredService<ApplicationDbContext>();
                await CustomRolesSeeder.SeedAsync(dbContextForRoles, logger);
                logger.LogInformation("✓ Seed de roles del sistema completado exitosamente");
            }
            catch (Exception rolesSeedEx)
            {
                logger.LogWarning(rolesSeedEx, "⚠ Seed de roles falló, pero la aplicación continuará.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error CRÍTICO durante migraciones. La aplicación no puede continuar.");
            throw;  // Solo throw si falla migración
        }
    }
}

// --- CONFIGURACI�N DEL PIPELINE DE PETICIONES HTTP (MIDDLEWARE) ---
// El orden aqu� es importante.

if (app.Environment.IsDevelopment())
{
    // En desarrollo, muestra p�ginas de error detalladas para la base de datos.
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseMigrationsEndPoint();
}
else
{
    // En producci�n, usa un manejador de errores gen�rico y fuerza el uso de HTTPS.
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// RequestId debe ir ANTES de cualquier middleware que quiera loguearlo o incluirlo
// en responses de error. Es cheap (asigna un GUID por request).
app.UseRequestIdMiddleware();

// ApiProblemDetailsMiddleware: RFC 7807 + errorCode + requestId, solo para /v1/*
// (futuro API Platform B2B). Para otras rutas delega al ExceptionHandlingMiddleware
// legacy sin interferir con el shape de error del SaaS actual.
app.UseApiProblemDetailsMiddleware();

app.UseExceptionHandlingMiddleware();

app.UseHttpsRedirection();
// Vite genera app.js, app.css y chunks JS/CSS con nombres fijos sin hash de contenido.
// Cache-Control: no-cache fuerza revalidación en cada request para todos los JS/CSS,
// evitando que los usuarios vean versiones desactualizadas después de un deploy.
// Los assets de imagen y fuentes sí pueden cachearse de forma agresiva (1 año).
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var fileName = ctx.File.Name;
        if (fileName.EndsWith(".js") || fileName.EndsWith(".css"))
        {
            ctx.Context.Response.Headers["Cache-Control"] = "no-cache, must-revalidate";
        }
        else if (!fileName.EndsWith(".html"))
        {
            // Imágenes, fuentes y otros assets binarios: caché agresivo (tienen nombres estables)
            ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
        }
        // .html: sin header explícito → el navegador usa ETag/Last-Modified
    }
});
app.UseRouting();

// CORS must be after UseRouting and before UseAuthentication
app.UseCors("AllowReactApp");

// RISK: Removing UseAntiforgery - React SPA will handle CSRF via API tokens
app.UseAuthentication(); // Must come before UseAuthorization

// Multi-Tenant Middleware - debe ir DESPUÉS de UseAuthentication
app.UseTenantMiddleware();

app.UseAuthorization();

// Health: /health con checks (PostgreSQL + multi-tenant) en JSON para CapRover
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var json = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            environment = app.Environment.EnvironmentName,
            version = "1.0.0",
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                data = e.Value.Data
            })
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await context.Response.WriteAsync(json);
    }
}).AllowAnonymous();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName,
    version = "1.0.0"
})).AllowAnonymous();

// API Controllers for React SPA
app.MapControllers();

// ✅ CRITICAL FIX: Capturar rutas /api/* no encontradas y retornar 404 JSON
// Esto previene que MapFallbackToFile devuelva index.html para /api/* inexistentes
app.MapFallback("/api/{**catch-all}", (HttpContext context) =>
{
    return Results.Json(new
    {
        error = "Not Found",
        path = context.Request.Path.Value,
        timestamp = DateTime.UtcNow
    }, statusCode: 404);
});

// ✅ SPA fallback - SOLO para rutas UI que NO son /api/*
// Solo rutas UI no coincidentes (como /empleados, /dashboard) sirven index.html
app.MapFallbackToFile("index.html");

// Inicia y ejecuta la aplicaci�n.
app.Run();

// RISK: Inline NoOp email sender replacement for removed Blazor Components.Account
public class NoOpEmailSender : IEmailSender<AppUser>
{
    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink) => Task.CompletedTask;
    public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink) => Task.CompletedTask;
    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode) => Task.CompletedTask;
}

// Make the Program class accessible to integration tests
public partial class Program { }