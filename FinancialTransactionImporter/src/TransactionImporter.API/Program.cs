using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using TransactionImporter.API.Middleware;
using TransactionImporter.Core.Interfaces;
using TransactionImporter.Core.Models;
using TransactionImporter.Core.Services;
using TransactionImporter.Infrastructure.Database;
using TransactionImporter.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ─── Configuration ────────────────────────────────────────────────────────────
builder.Services.Configure<CsvSettings>(
    builder.Configuration.GetSection(CsvSettings.SectionName));

// ─── Database ─────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=/app/data/transactions.db";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// ─── Application Services ─────────────────────────────────────────────────────
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<ICsvParserService, CsvParserService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();

// ─── Web Layer ────────────────────────────────────────────────────────────────
builder.Services.AddControllers();

builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;

    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Financial Transaction Importer API",
            Version = "v1",
            Description = "Import, validate, and manage financial transactions from CSV files."
        };

        // Register the API key security scheme in the components section
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = "X-Api-Key",
            Description = "All requests require an API key passed in the X-Api-Key header."
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("ApiKey", document)] = []
        });

        return Task.CompletedTask;
    });
});

// ─── CORS ─────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000", "http://localhost:5173", "http://localhost:80", "http://frontend"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ─── Build & Configure Pipeline ───────────────────────────────────────────────
var app = builder.Build();

// Ensure the database exists on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var dbPath = connectionString.Replace("Data Source=", string.Empty).Trim();
    var dbDirectory = Path.GetDirectoryName(dbPath);
    if (!string.IsNullOrEmpty(dbDirectory))
        Directory.CreateDirectory(dbDirectory);

    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Financial Transaction Importer API";
        options.Theme = ScalarTheme.DeepSpace;
        options.DefaultHttpClient = new(ScalarTarget.Http, ScalarClient.HttpClient);
        options.AddPreferredSecuritySchemes("ApiKey");
    });
}

// Lightweight health check — always available, no auth required
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
   .ExcludeFromDescription();

app.UseCors("AllowFrontend");

app.UseMiddleware<ApiKeyMiddleware>();

app.MapControllers();

app.Run();

// Make Program accessible to test projects
public partial class Program { }
