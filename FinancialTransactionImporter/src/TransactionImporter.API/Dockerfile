# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Copy project files first for layer caching of restore step
COPY src/TransactionImporter.Core/TransactionImporter.Core.csproj               src/TransactionImporter.Core/
COPY src/TransactionImporter.Infrastructure/TransactionImporter.Infrastructure.csproj src/TransactionImporter.Infrastructure/
COPY src/TransactionImporter.API/TransactionImporter.API.csproj                 src/TransactionImporter.API/
COPY tests/TransactionImporter.Tests/TransactionImporter.Tests.csproj           tests/TransactionImporter.Tests/

RUN dotnet restore src/TransactionImporter.API/TransactionImporter.API.csproj

# Copy all source code
COPY src/ src/
COPY tests/ tests/

# Build and publish
WORKDIR /src/src/TransactionImporter.API
RUN dotnet publish TransactionImporter.API.csproj -c Release -o /app/publish --no-restore -r linux-x64 --self-contained false

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create the data directory for SQLite
RUN mkdir -p /app/data

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "TransactionImporter.API.dll"]
