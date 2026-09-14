using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OctalPulse.API.Authorization;
using OctalPulse.API.Services;
using OctalPulse.Application;
using OctalPulse.Application.Interface.Services;
using OctalPulse.Domain.Entities;
using OctalPulse.Infrastructure;
using OctalPulse.Infrastructure.Persistence;
using OctalPulse.Infrastructure.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ──────────────────────────────────────────────────────────────────
var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs");
Directory.CreateDirectory(logDirectory);

var logFilePath = Path.Combine(logDirectory, $"log-{DateTime.Now:yyyyMMdd-HHmmss}.log");

builder.Host.UseSerilog((_, configuration) =>
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}")
        .WriteTo.File(
            logFilePath,
            outputTemplate:
            "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] [{SourceContext}] {Message:lj} | Details: {Details}{NewLine}{Exception}"));

// ── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services
    .AddIdentityCore<User>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequiredLength = 8;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddDefaultTokenProviders()
    .AddEntityFrameworkStores<ApplicationDbContext>();

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(jwtSettings.Key))
{
    jwtSettings.Key = "octal-developer-only-signing-key-0123456789abcdef";
    builder.Configuration["Jwt:Key"] = jwtSettings.Key;
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs/collaboration"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddSignalR();

builder.Services.AddScoped<OctalPulse.Application.Interface.Services.IRealtimeNotifier, OctalPulse.API.Notifications.SignalRNotifier>();

builder.Services.AddSingleton<ILog, SerilogLogger>();

builder.Services.AddScoped<IAuthorizationHandler, AdminOnlyAuthorizationHandler>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.Requirements.Add(new AdminOnlyRequirement()));
});

// ── Swagger / OpenAPI ────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OctalPulse API",
        Version = "v1",
        Description = "Team collaboration API for OctalTeammate."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT access token."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});

// ── App ──────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var baseUrl = app.Urls.FirstOrDefault()?.TrimEnd('/');
        if (!string.IsNullOrEmpty(baseUrl))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"{baseUrl}/swagger")
            {
                UseShellExecute = true
            });
        }
    });
}

Log.Information("OctalPulse started | log file -> {LogFilePath}", logFilePath);

app.UseMiddleware<OctalPulse.API.Middleware.OutcomeLoggingMiddleware>();
app.UseMiddleware<OctalPulse.API.Middleware.ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "OctalPulse API v1");
    });
}

// Serve uploaded user images from the Resources folder (public, no auth, before HTTPS redirect).
var resourcesRoot = Path.Combine(Directory.GetCurrentDirectory(), "Resources");
Directory.CreateDirectory(resourcesRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(resourcesRoot),
    RequestPath = "/Resources"
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<OctalPulse.API.Middleware.ApiAvailabilityMiddleware>();
app.UseMiddleware<OctalPulse.API.Middleware.UserOperationLockMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHub<OctalPulse.API.Hubs.CollaborationHub>("/hubs/collaboration");

app.Run();

Log.CloseAndFlush();
