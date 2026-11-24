using ELearning.API.Extensions;
using ELearning.API.Middlewares;
using ELearning.API.Tenancy;
using System.Text.Json.Serialization;

namespace ELearning.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            #region Services
            builder.Services
                .AddControllers()
                .AddJsonOptions(options =>
                {
                    // Send enums as text ("Authenticated") instead of numbers (0). Easier to read and safer to change.
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });

            builder.Services.AddSwaggerDocs();
            builder.Services.AddCorsPolicy(builder.Configuration);
            builder.Services.AddRateLimits(builder.Configuration);
            builder.Services.AddDatabases(builder.Configuration);
            builder.Services.AddAuthenticationAndAuthorization(builder.Configuration);
            builder.Services.AddApplicationServices(builder.Configuration);
            #endregion

            var app = builder.Build();

            #region Startup work
            await app.MigrateDatabasesAsync();
            await app.SeedPlatformAdminAsync();
            #endregion

            #region Request pipeline
            // The order matters: each step runs before the ones below it.

            // 1. Catch any unexpected error and answer with a clean JSON error.
            app.UseMiddleware<ExceptionHandlerMiddleware>();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                {
                    options.DisplayRequestDuration();
                    options.EnableFilter();
                });
            }

            // Basic protective headers on every answer.
            app.Use(async (context, next) =>
            {
                // Do not let the browser guess a file type different from the one we send.
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                // Do not allow other websites to show our pages inside a frame (stops "clickjacking").
                context.Response.Headers["X-Frame-Options"] = "DENY";
                // Do not send our page addresses to other websites.
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                await next();
            });

            app.UseHttpsRedirection();
            app.UseCors();
            app.UseRateLimiter();

            // 2. Check the login token.
            app.UseAuthentication();

            // 3. Find the database of the teacher named in the token.
            app.UseMiddleware<TenantResolutionMiddleware>();

            // 4. Check that the user is allowed to call the endpoint.
            app.UseAuthorization();

            app.MapControllers();
            #endregion

            await app.RunAsync();
        }
    }
}
