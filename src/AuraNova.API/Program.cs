using Microsoft.EntityFrameworkCore;
using AuraNova.Infrastructure.Persistence;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using AuraNova.API.Middlewares;
using AuraNova.API.Configuration;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Configure Swagger to accept Bearer token
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter 'Bearer {token}'"
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
            }, []
        }
    });
});

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found. Configure it in appsettings or user secrets.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
);

// JWT configuration
builder.Services.Configure<AuraNova.Infrastructure.Auth.JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<AuraNova.Infrastructure.Auth.JwtSettings>();
if (jwtSettings == null || string.IsNullOrWhiteSpace(jwtSettings.SecretKey))
    throw new InvalidOperationException("JWT SecretKey not configured. Use user-secrets or environment variables to set JwtSettings:SecretKey.");

// Authentication & Authorization
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var key = System.Text.Encoding.UTF8.GetBytes(jwtSettings.SecretKey);
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true
        };
    });

builder.Services.AddAuthorization();

// register auth services
builder.Services.AddScoped<AuraNova.Application.Auth.Interfaces.IPasswordHasherService, AuraNova.Infrastructure.Auth.PasswordHasherService>();
builder.Services.AddScoped<AuraNova.Application.Auth.Interfaces.IJwtService, AuraNova.Infrastructure.Auth.JwtService>();
builder.Services.AddScoped<AuraNova.Application.Auth.Interfaces.IAuthService, AuraNova.Infrastructure.Auth.AuthService>();

// register product services
builder.Services.AddScoped<AuraNova.Application.Products.Interfaces.IProductService, AuraNova.Infrastructure.Products.ProductService>();

// register order services
builder.Services.AddScoped<AuraNova.Application.Orders.Interfaces.IOrderService, AuraNova.Infrastructure.Orders.OrderService>();

// register delivery zone services
builder.Services.AddScoped<AuraNova.Application.DeliveryZones.Interfaces.IDeliveryZoneService, AuraNova.Infrastructure.DeliveryZones.DeliveryZoneService>();

// register meeting point services
builder.Services.AddScoped<AuraNova.Application.MeetingPoints.Interfaces.IMeetingPointService, AuraNova.Infrastructure.MeetingPoints.MeetingPointService>();

// register quote services
builder.Services.AddScoped<AuraNova.Application.Quotes.Interfaces.IQuoteService, AuraNova.Infrastructure.Quotes.QuoteService>();

// register whatsapp message service (no Meta API — generates text + wa.me URL only)
builder.Services.AddScoped<AuraNova.Application.WhatsApp.Interfaces.IWhatsAppMessageService, AuraNova.Infrastructure.WhatsApp.WhatsAppMessageService>();

// Payment settings


// Supabase settings
builder.Services.Configure<AuraNova.Infrastructure.Storage.SupabaseSettings>(builder.Configuration.GetSection("Supabase"));

// register storage services
builder.Services.AddHttpClient<AuraNova.Application.Storage.Interfaces.IFileStorageService, AuraNova.Infrastructure.Storage.SupabaseStorageService>();

// register payment services
builder.Services.AddScoped<AuraNova.Application.Payments.Interfaces.IPaymentService, AuraNova.Infrastructure.Payments.PaymentService>();

// register order status transition service (state machine)
builder.Services.AddSingleton<AuraNova.Application.Orders.Interfaces.IOrderStatusTransitionService, AuraNova.Infrastructure.Orders.OrderStatusTransitionService>();

// register order status service
builder.Services.AddScoped<AuraNova.Application.Orders.Interfaces.IOrderStatusService, AuraNova.Infrastructure.Orders.OrderStatusService>();

// register public tracking service
builder.Services.AddScoped<AuraNova.Application.Orders.Interfaces.IOrderTrackingService, AuraNova.Infrastructure.Orders.OrderTrackingService>();

// Public App settings


// register notification services
builder.Services.AddScoped<AuraNova.Application.Notifications.Interfaces.ITrackingUrlService, AuraNova.Infrastructure.Notifications.TrackingUrlService>();
builder.Services.AddScoped<AuraNova.Application.Notifications.Interfaces.INotificationTemplateService, AuraNova.Infrastructure.Notifications.NotificationTemplateService>();
builder.Services.AddScoped<AuraNova.Application.Notifications.Interfaces.INotificationService, AuraNova.Infrastructure.Notifications.NotificationService>();

// register dashboard services
builder.Services.AddScoped<AuraNova.Application.Dashboard.Interfaces.IDashboardService, AuraNova.Infrastructure.Dashboard.DashboardService>();

// register admin order query services
builder.Services.AddScoped<AuraNova.Application.AdminOrders.Interfaces.IAdminOrderQueryService, AuraNova.Infrastructure.Orders.AdminOrderQueryService>();

// register business settings services
builder.Services.AddScoped<AuraNova.Application.BusinessSettings.Interfaces.IBusinessSettingsService, AuraNova.Infrastructure.BusinessSettings.BusinessSettingsService>();

// register audit service
builder.Services.AddScoped<AuraNova.Application.Audit.Interfaces.IAdminAuditService, AuraNova.Infrastructure.Audit.AdminAuditService>();

// Security Settings
builder.Services.Configure<SecuritySettings>(builder.Configuration.GetSection("Security"));
var securitySettings = builder.Configuration.GetSection("Security").Get<SecuritySettings>() ?? new SecuritySettings();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (securitySettings.AllowedOrigins != null && securitySettings.AllowedOrigins.Length > 0 && securitySettings.AllowedOrigins[0] != "*")
        {
            policy.WithOrigins(securitySettings.AllowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        await context.HttpContext.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Title = "Too Many Requests",
            Status = 429,
            Detail = "Se alcanzó el límite de solicitudes.",
            Type = "https://api.auranova.pe/errors/too_many_requests",
            Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier }
        }, cancellationToken: token);
    };

    options.AddFixedWindowLimiter("login_policy", opt => { opt.PermitLimit = 5; opt.Window = TimeSpan.FromMinutes(1); });
    options.AddFixedWindowLimiter("tracking_policy", opt => { opt.PermitLimit = 30; opt.Window = TimeSpan.FromMinutes(1); });
    options.AddFixedWindowLimiter("create_order_policy", opt => { opt.PermitLimit = 20; opt.Window = TimeSpan.FromMinutes(1); });
    options.AddFixedWindowLimiter("evidence_upload_policy", opt => { opt.PermitLimit = 5; opt.Window = TimeSpan.FromMinutes(10); });
    options.AddFixedWindowLimiter("accept_quote_policy", opt => { opt.PermitLimit = 10; opt.Window = TimeSpan.FromMinutes(1); });
    options.AddFixedWindowLimiter("admin_policy", opt => { opt.PermitLimit = 100; opt.Window = TimeSpan.FromMinutes(1); });
});

// Exception Handler and Problem Details
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

var app = builder.Build();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseCors();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/api/health");
app.MapHealthChecks("/health");

app.MapControllers();

// Apply migrations automatically
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Initial admin bootstrap
var initialAdmin = builder.Configuration.GetSection("InitialAdmin");
if (initialAdmin.GetValue<bool>("Enabled"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<AuraNova.Application.Auth.Interfaces.IPasswordHasherService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var email = initialAdmin.GetValue<string>("Email")?.Trim().ToLowerInvariant();
    var name = initialAdmin.GetValue<string>("Name");
    var password = initialAdmin.GetValue<string>("Password");

    if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
    {
        var exists = db.AdminUsers.Any(u => u.Email.ToLower() == email);
        if (!exists)
        {
            var admin = new AuraNova.Domain.Entities.AdminUser { Email = email, Name = name ?? "Admin", IsActive = true };
            admin.PasswordHash = hasher.HashPassword(admin, password);
            db.AdminUsers.Add(admin);
            db.SaveChanges();
            logger.LogInformation("Initial admin created: {Email}", email);
        }
    }
}



app.Run();


// Make the auto-generated Program class accessible to integration tests
public partial class Program;
