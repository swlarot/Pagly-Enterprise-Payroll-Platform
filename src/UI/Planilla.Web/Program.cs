// RISK: Removing Blazor components - converting to Web API + React SPA architecture
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Stripe;
using System.Text;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Infrastructure.Configuration;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Infrastructure.Services;
using Vorluno.Planilla.Web.Extensions;
using Vorluno.Planilla.Application.Mappings;
using Vorluno.Planilla.Web.Middleware;

// CONFIGURACIÓN GLOBAL: Permitir que Npgsql acepte DateTime sin Kind específico
// Esto soluciona el error "Cannot write DateTime with Kind=Unspecified to PostgreSQL type 'timestamp with time zone'"
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// --- INICIO DE LA CONFIGURACI�N DE SERVICIOS ---


// 1. LEER LA CADENA DE CONEXI�N
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// 2. REGISTRAR EL DBCONTEXT PARA INYECCI�N DE DEPENDENCIAS
//    Le decimos a la aplicaci�n c�mo crear instancias de nuestro ApplicationDbContext,
//    configur�ndolo para que use SQL Server con la cadena de conexi�n.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    // Ignorar advertencia de modelo pendiente durante desarrollo
    options.ConfigureWarnings(warnings =>
        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

// 3. CONFIGURAR ASP.NET CORE IDENTITY
//    Configura el sistema de usuarios y roles, usando nuestro ApplicationDbContext para almacenar los datos
//    y nuestra clase AppUser como el modelo de usuario.
builder.Services.AddIdentity<AppUser, IdentityRole>(options => {
    options.SignIn.RequireConfirmedAccount = true;
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
        ClockSkew = TimeSpan.Zero // Elimina el buffer de 5 minutos por defecto
    };
});

// 6. CONFIGURAR AUTHORIZATION POLICIES MULTI-TENANT
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireOwner", p => p.RequireRole("Owner"))
    .AddPolicy("RequireAdmin", p => p.RequireRole("Owner", "Admin"))
    .AddPolicy("RequireManager", p => p.RequireRole("Owner", "Admin", "Manager"))
    .AddPolicy("RequireAccountant", p => p.RequireRole("Owner", "Admin", "Manager", "Accountant"));

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
    catch (InvalidOperationException ex)
    {
        var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Stripe no está configurado correctamente. Los endpoints de billing no funcionarán.");
    }
}

// 11. REGISTRAR SERVICIOS STRIPE
builder.Services.AddScoped<IStripeBillingService, StripeBillingService>();
builder.Services.AddScoped<IPlanLimitService, PlanLimitService>();

// --- FIN DE NUESTRA CONFIGURACIÓN PRINCIPAL ---


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
        Title = "Planilla API",
        Version = "v1",
        Description = "Multi-tenant Payroll SaaS API for Panama"
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

            logger.LogInformation("Aplicando migraciones pendientes...");
            await context.Database.MigrateAsync();
            logger.LogInformation("Migraciones aplicadas correctamente");

            logger.LogInformation("Ejecutando seed de configuración...");
            await PayrollConfigSeeder.SeedAsync(context, logger);
            logger.LogInformation("Seed de configuración completado");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error durante migraciones o seeding");
            throw;
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

app.UseHttpsRedirection();
app.UseStaticFiles(); // Serves static files including React SPA from wwwroot
app.UseRouting();

// RISK: Removing UseAntiforgery - React SPA will handle CSRF via API tokens
app.UseAuthentication(); // Must come before UseAuthorization

// Multi-Tenant Middleware - debe ir DESPUÉS de UseAuthentication
app.UseTenantMiddleware();

app.UseAuthorization();

// API Controllers for React SPA
app.MapControllers();

// SPA fallback - serve React app for client-side routing
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