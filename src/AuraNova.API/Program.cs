using Microsoft.EntityFrameworkCore;
using AuraNova.Infrastructure.Persistence;
using AuraNova.API.Middlewares;
using AuraNova.API.Configuration;
using System.Text.Json.Serialization;

// Local development: load the repo's .env (only for the API itself and `dotnet ef`, never under the test host)
if (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name is "AuraNova.API" or "ef")
    DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Real-Time Live Broadcast (SignalR)
builder.Services.AddSignalR();
builder.Services.AddSingleton<AuraNova.Application.LiveBroadcast.Interfaces.ILiveBroadcastStateService, AuraNova.Infrastructure.LiveBroadcast.LiveBroadcastStateService>();
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

// Database: appsettings "Database" section + Database:Password (env var / .env)
var connectionString = DatabaseConnection.Resolve(builder.Configuration);

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

        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return System.Threading.Tasks.Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// register auth services
builder.Services.AddScoped<AuraNova.Application.Auth.Interfaces.IPasswordHasherService, AuraNova.Infrastructure.Auth.PasswordHasherService>();
builder.Services.AddScoped<AuraNova.Application.Auth.Interfaces.IJwtService, AuraNova.Infrastructure.Auth.JwtService>();
builder.Services.AddScoped<AuraNova.Application.Auth.Interfaces.IAuthService, AuraNova.Infrastructure.Auth.AuthService>();

// --- User Context ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuraNova.Application.Auth.Interfaces.ICurrentUserService, AuraNova.Infrastructure.Auth.CurrentUserService>();

// register product services
builder.Services.AddScoped<AuraNova.Application.Products.Interfaces.IProductService, AuraNova.Infrastructure.Products.ProductService>();
builder.Services.AddScoped<AuraNova.Application.Orders.Interfaces.IProductPriceResolver, AuraNova.Infrastructure.Orders.ProductPriceResolver>();

// register category services
builder.Services.AddScoped<AuraNova.Application.Categories.Interfaces.ICategoryService, AuraNova.Infrastructure.Categories.CategoryService>();

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
// SupabaseStorageService sube los archivos tal cual; el decorador OptimizingFileStorageService valida y convierte
// toda imagen a WebP optimizado antes de subirla. El resto de la app solo ve IFileStorageService (decorado).
builder.Services.AddHttpClient<AuraNova.Infrastructure.Storage.SupabaseStorageService>();
builder.Services.AddSingleton<AuraNova.Application.Storage.Interfaces.IImageOptimizer, AuraNova.Infrastructure.Storage.SkiaImageOptimizer>();
builder.Services.AddSingleton<AuraNova.Application.Storage.Interfaces.IStorageUrlResolver, AuraNova.Infrastructure.Storage.StorageUrlResolver>();
builder.Services.AddScoped<AuraNova.Application.Storage.Interfaces.IFileStorageService>(sp =>
    new AuraNova.Infrastructure.Storage.OptimizingFileStorageService(
        sp.GetRequiredService<AuraNova.Infrastructure.Storage.SupabaseStorageService>(),
        sp.GetRequiredService<AuraNova.Application.Storage.Interfaces.IImageOptimizer>(),
        sp.GetRequiredService<ILogger<AuraNova.Infrastructure.Storage.OptimizingFileStorageService>>()));
builder.Services.AddScoped<AuraNova.Application.Storage.Interfaces.IReferenceImageService, AuraNova.Infrastructure.Storage.ReferenceImageService>();
builder.Services.AddScoped<AuraNova.Application.Storage.Interfaces.IImageMaintenanceService>(sp =>
    new AuraNova.Infrastructure.Storage.ImageMaintenanceService(
        sp.GetRequiredService<AppDbContext>(),
        sp.GetRequiredService<AuraNova.Infrastructure.Storage.SupabaseStorageService>(), // sin decorador: evita doble compresión
        sp.GetRequiredService<AuraNova.Application.Storage.Interfaces.IImageOptimizer>(),
        sp.GetRequiredService<AuraNova.Application.Storage.Interfaces.IReferenceImageService>(),
        sp.GetRequiredService<AuraNova.Application.Storage.Interfaces.IStorageUrlResolver>(),
        sp.GetRequiredService<ILogger<AuraNova.Infrastructure.Storage.ImageMaintenanceService>>()));

// register payment services
builder.Services.AddScoped<AuraNova.Application.Payments.Interfaces.IPaymentService, AuraNova.Infrastructure.Payments.PaymentService>();

// register order status transition service (state machine)
builder.Services.AddSingleton<AuraNova.Application.Orders.Interfaces.IOrderStatusTransitionService, AuraNova.Infrastructure.Orders.OrderStatusTransitionService>();

// register order status service
builder.Services.AddScoped<AuraNova.Application.Orders.Interfaces.IOrderStatusService, AuraNova.Infrastructure.Orders.OrderStatusService>();

// Optional auto-cancellation of unpaid catalog orders (releases stock). Disabled unless Orders:UnpaidExpirationHours > 0
builder.Services.Configure<AuraNova.Infrastructure.Orders.OrderExpirationSettings>(builder.Configuration.GetSection("Orders"));
builder.Services.AddScoped<AuraNova.Infrastructure.Orders.UnpaidOrderExpirationService>();
builder.Services.AddHostedService<AuraNova.API.Services.UnpaidOrderExpirationWorker>();

// register public tracking service
builder.Services.AddScoped<AuraNova.Application.Orders.Interfaces.IOrderTrackingService, AuraNova.Infrastructure.Orders.OrderTrackingService>();

// Public App settings


// Public site URL used in customer messages (tracking links)
builder.Services.Configure<AuraNova.Infrastructure.Notifications.PublicSiteSettings>(builder.Configuration.GetSection("PublicSite"));

// register notification services
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

// register campaign service
builder.Services.AddScoped<AuraNova.Application.Campaigns.Interfaces.ICampaignService, AuraNova.Infrastructure.Campaigns.CampaignService>();

// Security Settings
builder.Services.Configure<SecuritySettings>(builder.Configuration.GetSection("Security"));
var securitySettings = builder.Configuration.GetSection("Security").Get<SecuritySettings>() ?? new SecuritySettings();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (securitySettings.AllowedOrigins is { Length: > 0 } && securitySettings.AllowedOrigins[0] != "*")
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

// Real client IP behind Render's proxy. Without this RemoteIpAddress is the proxy for every request,
// so per-IP rate limits would still be shared by everyone.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                               | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = securitySettings.ForwardedHeadersForwardLimit;
    // The API is only reachable through the platform proxy
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Rate Limiting (partitioned per client IP, or per admin user)
static Microsoft.AspNetCore.RateLimiting.RateLimiterOptions AddPerClientWindow(
    Microsoft.AspNetCore.RateLimiting.RateLimiterOptions options, string policy, int permitLimit, TimeSpan window,
    Func<HttpContext, string> partitionKey)
{
    options.AddPolicy(policy, context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        partitionKey(context),
        _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window }));
    return options;
}

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

    string ByIp(HttpContext context) => ClientIp.Resolve(context, securitySettings.ClientIpHeader);
    string ByUserOrIp(HttpContext context) => ClientIp.ResolveUserOrIp(context, securitySettings.ClientIpHeader);

    AddPerClientWindow(options, "login_policy", 5, TimeSpan.FromMinutes(1), ByIp);
    // The tracking page polls every 15s; several customers can share a mobile carrier IP (CGNAT)
    AddPerClientWindow(options, "tracking_policy", 60, TimeSpan.FromMinutes(1), ByIp);
    AddPerClientWindow(options, "create_order_policy", 15, TimeSpan.FromMinutes(1), ByIp);
    AddPerClientWindow(options, "evidence_upload_policy", 5, TimeSpan.FromMinutes(10), ByIp);
    AddPerClientWindow(options, "accept_quote_policy", 10, TimeSpan.FromMinutes(1), ByIp);
    AddPerClientWindow(options, "admin_policy", 200, TimeSpan.FromMinutes(1), ByUserOrIp);
});

// Output cache for the public catalog (invalidated by [EvictCatalogCache] on admin writes)
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy(CatalogCache.Policy, policy => policy
        .Expire(CatalogCache.Duration)
        .SetVaryByRouteValue("id")
        .Tag(CatalogCache.Tag));
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

app.UseAuthentication();
// After authentication so admin_policy can partition by user
app.UseRateLimiter();
app.UseAuthorization();
app.UseOutputCache();

app.MapHealthChecks("/api/health");
app.MapHealthChecks("/health");

app.MapControllers();
app.MapHub<AuraNova.API.Hubs.SuperAdminLiveHub>("/hubs/live");

// Apply migrations automatically
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // Integration tests use the InMemory provider, which has no migrations.
    // Database__ApplyMigrationsOnStartup=false lets a local instance point at production without changing its schema.
    if (db.Database.IsRelational() && builder.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true))
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
