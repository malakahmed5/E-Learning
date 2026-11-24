using Microsoft.AspNetCore.Mvc;

namespace ELearning.API.Middlewares
{
    public class ExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlerMiddleware> _logger;
        private readonly IHostEnvironment _environment;

        public ExceptionHandlerMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlerMiddleware> logger,
            IHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next.Invoke(context);

                // Endpoint doesn't exist
                await HandleNotFoundEndpointAsync(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception while processing {Path}", context.Request.Path);

                var problem = new ProblemDetails
                {
                    Title = "An Unexpected Error Occurred",
                    // Exception details are only exposed in Development
                    Detail = _environment.IsDevelopment() ? ex.Message : "Something went wrong, please try again later",
                    Instance = context.Request.Path,
                    Status = StatusCodes.Status500InternalServerError,
                };

                context.Response.StatusCode = problem.Status.Value;
                await context.Response.WriteAsJsonAsync(problem);
            }
        }

        private static async Task HandleNotFoundEndpointAsync(HttpContext context)
        {
            if (context.Response.StatusCode == StatusCodes.Status404NotFound && !context.Response.HasStarted)
            {
                var problem = new ProblemDetails
                {
                    Title = "Endpoint Not Found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = $"Endpoint with path '{context.Request.Path}' was not found",
                    Instance = context.Request.Path,
                };
                await context.Response.WriteAsJsonAsync(problem);
            }
        }
    }
}
