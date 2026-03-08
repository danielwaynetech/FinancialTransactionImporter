namespace TransactionImporter.API.Middleware
{
    /// <summary>
    /// Middleware that enforces API key authentication on all requests.
    /// Returns RFC 7807 ProblemDetails on authentication failure.
    /// </summary>
    public class ApiKeyMiddleware
    {
        private const string ApiKeyHeaderName = "X-Api-Key";

        private readonly RequestDelegate _next;
        private readonly string _configuredApiKey;

        public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuredApiKey = configuration["ApiKey"]
                ?? throw new InvalidOperationException("ApiKey is not configured in appsettings.json.");
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/swagger"))
            {
                await _next(context);
                return;
            }

            if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(ApiProblemDetailsFactory.MissingApiKey());
                return;
            }

            if (!string.Equals(extractedApiKey, _configuredApiKey, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(ApiProblemDetailsFactory.InvalidApiKey());
                return;
            }

            await _next(context);
        }
    }
}
