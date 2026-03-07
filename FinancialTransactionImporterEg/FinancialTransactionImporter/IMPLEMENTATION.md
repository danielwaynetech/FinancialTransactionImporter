# Financial Transaction Importer — Developer Implementation Guide

This document walks through creating the project from scratch exactly as a
developer would, including all CLI commands, project scaffolding, git commits,
and frontend setup. Run all commands from the repository root unless stated otherwise.

---

## Prerequisites

| Tool          | Version      | Install                                      |
|---------------|--------------|----------------------------------------------|
| .NET SDK      | 10.0         | https://dotnet.microsoft.com/download        |
| Node.js       | 20 LTS+      | https://nodejs.org                           |
| Docker Desktop| Latest       | https://www.docker.com/products/docker-desktop |
| Git           | Any modern   | https://git-scm.com                          |

---

## Step 1 — Initialise the Git Repository

```bash
mkdir FinancialTransactionImporter
cd FinancialTransactionImporter
git init
git branch -M main
```

Create a `.gitignore`:

```bash
dotnet new gitignore
```

Append frontend and Docker entries to the generated `.gitignore`:

```
# Frontend
frontend/node_modules/
frontend/dist/

# SQLite databases
*.db
*.db-shm
*.db-wal

# Docker volumes
db_data/
```

---

## Step 2 — Create the .NET Solution

```bash
dotnet new sln -n FinancialTransactionImporter
```

---

## Step 3 — Create the Core (Domain) Project

```bash
dotnet new classlib -n TransactionImporter.Core \
    -o src/TransactionImporter.Core \
    --framework net10.0

# Remove the default placeholder class
rm src/TransactionImporter.Core/Class1.cs

dotnet sln add src/TransactionImporter.Core/TransactionImporter.Core.csproj
```

Add the required NuGet package:

```bash
dotnet add src/TransactionImporter.Core/TransactionImporter.Core.csproj \
    package Microsoft.Extensions.Options
```

Create the folder structure:

```bash
mkdir -p src/TransactionImporter.Core/Entities
mkdir -p src/TransactionImporter.Core/Interfaces
mkdir -p src/TransactionImporter.Core/Models
mkdir -p src/TransactionImporter.Core/Services
```

Now create the following files (contents from the source tree):

- `Entities/Transaction.cs`
- `Interfaces/ICsvParserService.cs`
- `Interfaces/ITransactionRepository.cs`
- `Interfaces/ITransactionService.cs`
- `Models/CsvSettings.cs`
- `Models/CsvValidationResult.cs`
- `Models/PaginatedResult.cs`
- `Models/UpdateTransactionRequest.cs`
- `Services/CsvParserService.cs`
- `Services/TransactionService.cs`

---

## Step 4 — Create the Infrastructure Project

```bash
dotnet new classlib -n TransactionImporter.Infrastructure \
    -o src/TransactionImporter.Infrastructure \
    --framework net10.0

rm src/TransactionImporter.Infrastructure/Class1.cs

dotnet sln add src/TransactionImporter.Infrastructure/TransactionImporter.Infrastructure.csproj
```

Add project reference to Core:

```bash
dotnet add src/TransactionImporter.Infrastructure/TransactionImporter.Infrastructure.csproj \
    reference src/TransactionImporter.Core/TransactionImporter.Core.csproj
```

Add NuGet packages:

```bash
dotnet add src/TransactionImporter.Infrastructure/TransactionImporter.Infrastructure.csproj \
    package Microsoft.EntityFrameworkCore

dotnet add src/TransactionImporter.Infrastructure/TransactionImporter.Infrastructure.csproj \
    package Microsoft.EntityFrameworkCore.Sqlite

dotnet add src/TransactionImporter.Infrastructure/TransactionImporter.Infrastructure.csproj \
    package Microsoft.EntityFrameworkCore.Design
```

Create the folder structure:

```bash
mkdir -p src/TransactionImporter.Infrastructure/Data
mkdir -p src/TransactionImporter.Infrastructure/Repositories
```

Create the following files:

- `Data/AppDbContext.cs`
- `Repositories/TransactionRepository.cs`

---

## Step 5 — Create the API Project

```bash
dotnet new webapi -n TransactionImporter.API \
    -o src/TransactionImporter.API \
    --framework net10.0 \
    --no-openapi

# Remove generated boilerplate
rm -f src/TransactionImporter.API/Controllers/WeatherForecastController.cs
rm -f src/TransactionImporter.API/WeatherForecast.cs

dotnet sln add src/TransactionImporter.API/TransactionImporter.API.csproj
```

Add project references:

```bash
dotnet add src/TransactionImporter.API/TransactionImporter.API.csproj \
    reference src/TransactionImporter.Core/TransactionImporter.Core.csproj

dotnet add src/TransactionImporter.API/TransactionImporter.API.csproj \
    reference src/TransactionImporter.Infrastructure/TransactionImporter.Infrastructure.csproj
```

Add NuGet packages:

```bash
dotnet add src/TransactionImporter.API/TransactionImporter.API.csproj \
    package Swashbuckle.AspNetCore

dotnet add src/TransactionImporter.API/TransactionImporter.API.csproj \
    package Microsoft.EntityFrameworkCore.Design
```

Create the folder structure:

```bash
mkdir -p src/TransactionImporter.API/Controllers
mkdir -p src/TransactionImporter.API/Middleware
mkdir -p src/TransactionImporter.API/DTOs
```

Create the following files:

- `Controllers/TransactionsController.cs`
- `Middleware/ApiKeyMiddleware.cs`
- `DTOs/TransactionDto.cs`
- `Program.cs`
- `appsettings.json`
- `appsettings.Development.json`
- `Dockerfile`

---

## Step 6 — Create the Test Project

```bash
dotnet new nunit -n TransactionImporter.Tests \
    -o tests/TransactionImporter.Tests \
    --framework net10.0

# Remove the default test placeholder
rm tests/TransactionImporter.Tests/UnitTest1.cs

dotnet sln add tests/TransactionImporter.Tests/TransactionImporter.Tests.csproj
```

Add project reference to Core:

```bash
dotnet add tests/TransactionImporter.Tests/TransactionImporter.Tests.csproj \
    reference src/TransactionImporter.Core/TransactionImporter.Core.csproj
```

Add NuGet packages:

```bash
dotnet add tests/TransactionImporter.Tests/TransactionImporter.Tests.csproj \
    package FluentAssertions

dotnet add tests/TransactionImporter.Tests/TransactionImporter.Tests.csproj \
    package Moq
```

Create the following file:

- `CsvParserServiceTests.cs`

Verify the tests build and pass:

```bash
dotnet test tests/TransactionImporter.Tests/TransactionImporter.Tests.csproj --verbosity normal
```

---

## Step 7 — First Git Commit (Backend)

```bash
git add .
git commit -m "feat: scaffold .NET 10 solution with Core, Infrastructure, API, and Tests projects"
```

---

## Step 8 — Create the React Frontend

```bash
# Scaffold a Vite + React + TypeScript app into the frontend/ directory
npm create vite@latest frontend -- --template react-ts
cd frontend
```

Install runtime dependencies:

```bash
npm install axios
```

Install dev dependencies (already included by Vite template, verify):

```bash
npm install
```

Return to the repository root:

```bash
cd ..
```

Replace the scaffolded files with the project source files:

- `frontend/index.html`
- `frontend/vite.config.ts`
- `frontend/tsconfig.json`
- `frontend/tsconfig.node.json`
- `frontend/nginx.conf`
- `frontend/Dockerfile`
- `frontend/src/main.tsx`
- `frontend/src/App.tsx`
- `frontend/src/styles.css`
- `frontend/src/types/index.ts`
- `frontend/src/services/api.ts`
- `frontend/src/hooks/useTransactions.ts`
- `frontend/src/components/UploadPanel.tsx`
- `frontend/src/components/TransactionTable.tsx`
- `frontend/src/components/EditModal.tsx`

Remove Vite boilerplate that is no longer needed:

```bash
rm -f frontend/src/App.css
rm -f frontend/src/assets/react.svg
rm -f frontend/public/vite.svg
```

Verify the frontend builds cleanly:

```bash
cd frontend
npm run build
cd ..
```

---

## Step 9 — Second Git Commit (Frontend)

```bash
git add .
git commit -m "feat: add React + TypeScript frontend with upload panel, data table, and pagination"
```

---

## Step 10 — Add Docker Compose

Create `docker-compose.yml` in the repository root (contents from the source tree).

Verify the compose file is valid:

```bash
docker compose config
```

---

## Step 11 — Add Sample Data and README

Create the following files in the repository root:

- `README.md`
- `sample_transactions.csv`

---

## Step 12 — Final Git Commit

```bash
git add .
git commit -m "feat: add Docker Compose configuration, README, and sample CSV"
```

---

## Step 13 — Run the Full Stack

```bash
docker compose up --build
```

| Service  | URL                           |
|----------|-------------------------------|
| Frontend | http://localhost:3000         |
| API      | http://localhost:8080         |
| Swagger  | http://localhost:8080/swagger |

---

## Step 14 — Run Tests Locally (Optional)

```bash
dotnet test tests/TransactionImporter.Tests/TransactionImporter.Tests.csproj \
    --verbosity normal \
    --logger "console;verbosity=detailed"
```

---

## Final Repository Structure

```
FinancialTransactionImporter/
├── .gitignore
├── FinancialTransactionImporter.sln
├── docker-compose.yml
├── README.md
├── sample_transactions.csv
├── src/
│   ├── TransactionImporter.Core/
│   │   ├── Entities/
│   │   ├── Interfaces/
│   │   ├── Models/
│   │   └── Services/
│   ├── TransactionImporter.Infrastructure/
│   │   ├── Data/
│   │   └── Repositories/
│   └── TransactionImporter.API/
│       ├── Controllers/
│       ├── DTOs/
│       ├── Middleware/
│       └── Dockerfile
├── tests/
│   └── TransactionImporter.Tests/
└── frontend/
    ├── src/
    │   ├── components/
    │   ├── hooks/
    │   ├── services/
    │   └── types/
    ├── nginx.conf
    └── Dockerfile
```
