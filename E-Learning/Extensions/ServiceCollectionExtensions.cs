using ELearning.API.Security;
using ELearning.API.Tenancy;
using ELearning.Core.Contracts;
using ELearning.Infrastructure.Data;
using ELearning.Infrastructure.Data.DbContexts;
using ELearning.Services.Teaching;
using ELearning.Infrastructure.Tenancy;
using ELearning.Presentation.Controllers;
using ELearning.Services.Abstraction;
using ELearning.Services.Admin;
using ELearning.Services.Auth;
using ELearning.Services.Codes;
using ELearning.Services.Options;
using ELearning.Services.Profile;
using ELearning.Services.Security;
using ELearning.Shared.Security;
using ELearningHub.Core.Contracts;
using ELearningHub.Core.Entities;
using ELearningHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;
using System.Text;
using System.Threading.RateLimiting;

namespace ELearning.API.Extensions
{
    // All "builder.Services.Add..." calls live here, grouped by topic, so Program.cs stays short.
    public static class ServiceCollectionExtensions
    {
        // The two kinds of databases: one central hub, and one database per teacher.
        public static IServiceCollection AddDatabases(this IServiceCollection services, IConfiguration configuration)
        {
            // Hub: accounts, teachers, and where each teacher's database is.
            services.AddDbContext<HubDbContext>(options =>
            {
                options.UseNpgsql(configuration.GetConnectionString("HubConnection"))
                       .UseSnakeCaseNamingConvention();
            });
            services.AddScoped<IHubDbContext>(provider => provider.GetRequiredService<HubDbContext>());

            // Teacher database: chosen per request from the teacher in the login token.
            services.AddScoped<TenantContext>();
            services.AddScoped<ITenantProvider>(provider => provider.GetRequiredService<TenantContext>());
            services.AddDbContext<ELearningDbContext>((provider, options) =>
            {
                var connectionString = provider.GetRequiredService<TenantContext>().ConnectionString;

                // "dotnet ef" builds the context without a request. It never connects.
                if (connectionString is null && EF.IsDesignTime)
                    connectionString = "Host=localhost;Database=elearning_design_time;Username=postgres";

                if (connectionString is null)
                    throw new InvalidOperationException("This request has no teacher. Log in and choose a teacher first.");

                TenantDbContextFactory.Configure(options, connectionString);

                // Writes the activity log for everything an assistant changes.
                options.AddInterceptors(provider.GetRequiredService<AuditInterceptor>());
            });
            services.AddScoped<AuditInterceptor>();
            services.AddScoped<IELearningDbContext>(provider => provider.GetRequiredService<ELearningDbContext>());

            // Used to create a new teacher's database.
            services.Configure<TenancyOptions>(configuration.GetSection("Tenancy"));
            services.AddSingleton<ITenantDatabaseManager, TenantDatabaseManager>();

            return services;
        }

        // Login tokens (JWT) and the rules for who may call what.
        public static IServiceCollection AddAuthenticationAndAuthorization(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtSection = configuration.GetSection("JWTOptions");
            services.Configure<JwtOptions>(jwtSection);

            var jwtOptions = jwtSection.Get<JwtOptions>()
                ?? throw new InvalidOperationException("The JWTOptions settings are missing.");

            // A short signing key can be guessed. Refuse to start with one.
            if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey) || jwtOptions.SecretKey.Length < 32)
                throw new InvalidOperationException("JWTOptions:SecretKey must be at least 32 characters.");

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    // Keep our own short claim names ("sub", "role") instead of the long Microsoft names.
                    options.MapInboundClaims = false;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,

                        ValidIssuer = jwtOptions.Issuer,
                        ValidAudience = jwtOptions.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),

                        // Allow only 30 seconds of clock difference between servers (the default is 5 minutes).
                        ClockSkew = TimeSpan.FromSeconds(30),

                        NameClaimType = AppClaims.AccountId,
                        RoleClaimType = AppClaims.Role,
                    };
                });

            services.AddAuthorization(options =>
            {
                // Secure by default: every endpoint needs a logged-in user
                // unless it is marked [AllowAnonymous].
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();

                options.AddPolicy(AppPolicies.PlatformAdmin, policy =>
                    policy.RequireClaim(AppClaims.IsPlatformAdmin, "true"));

                options.AddPolicy(AppPolicies.TenantMember, policy =>
                    policy.RequireClaim(AppClaims.TenantId));
            });

            return services;
        }

        // Our own services (business logic).
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<AuthOptions>(configuration.GetSection("Auth"));
            services.Configure<SeedAdminOptions>(configuration.GetSection("SeedAdmin"));

            // TimeProvider is "the clock". Services ask it for the time, so tests can set the time.
            services.AddSingleton(TimeProvider.System);

            services.AddHttpContextAccessor();
            services.AddScoped<CurrentUser>();
            services.AddScoped<ICurrentUser>(provider => provider.GetRequiredService<CurrentUser>());
            services.AddScoped<IAuditActor>(provider => provider.GetRequiredService<CurrentUser>());

            // Hashes passwords with a slow, salted algorithm (PBKDF2). Passwords are never stored as text.
            services.AddSingleton<IPasswordHasher<Account>, PasswordHasher<Account>>();

            // Encrypts two-factor secrets before they are saved.
            services.AddDataProtection();

            services.AddSingleton<TokenService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IProfileService, ProfileService>();
            services.AddScoped<ITenantAdminService, TenantAdminService>();
            services.AddScoped<IAcademicTermService, AcademicTermService>();
            services.AddScoped<PlatformAdminSeeder>();

            // Teacher management (inside one teacher's database).
            services.AddScoped<ICenterService, CenterService>();
            services.AddScoped<IGroupService, GroupService>();
            services.AddScoped<IStudentService, StudentService>();
            services.AddScoped<IAssistantService, AssistantService>();
            services.AddScoped<IAuditLogService, AuditLogService>();

            // Subscription codes.
            services.AddScoped<CodeRedeemer>();
            services.AddScoped<ICodeAdminService, CodeAdminService>();
            services.AddScoped<ISubscriptionService, SubscriptionService>();
            services.AddScoped<IActivationService, ActivationService>();

            return services;
        }

        // Limits how often one address can call the login endpoints, to slow down password guessing.
        public static IServiceCollection AddRateLimits(this IServiceCollection services, IConfiguration configuration)
        {
            var authCallsPerMinute = configuration.GetValue("RateLimiting:AuthCallsPerMinute", 20);

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.AddPolicy(RateLimitPolicies.Auth, httpContext =>
                {
                    var clientAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                    return RateLimitPartition.GetFixedWindowLimiter(clientAddress, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = authCallsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                    });
                });
            });

            return services;
        }

        // Which websites may call this API from a browser.
        public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
        {
            var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

            services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    // Only the listed frontends are allowed. An empty list means no browser origin is allowed.
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            return services;
        }

        public static IServiceCollection AddSwaggerDocs(this IServiceCollection services)
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "ELearning.API", Version = "v1" });

                options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
                {
                    Description = "Paste the access token like this: bearer {token}",
                    In = ParameterLocation.Header,
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey
                });

                // Shows the lock icon only on endpoints that need a login.
                options.OperationFilter<SecurityRequirementsOperationFilter>();
            });

            return services;
        }
    }
}
