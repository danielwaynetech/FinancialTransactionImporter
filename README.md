# Financial Transaction Importer

A full-stack application for importing, validating, storing, and managing financial transaction data from CSV files.

**Stack:** .NET 10 API · SQLite · React + TypeScript · Docker Compose

---

## Quick Start (Docker)

### Prerequisites
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine + Compose v2)

NOTE: All commands in this README.md will assume Windows OS.

### 1. Clone / unzip the project

```bash
cd FinancialTransactionImporter
```

### 2. Start the entire stack

```bash
docker compose up --build
```

This command will:
- Builds the .NET API image (multi-stage build)
- Builds the React frontend (Vite → nginx)
- Starts both containers with the SQLite volume mounted

| Service  | URL                          |
|----------|------------------------------|
| Frontend | http://localhost:3000        |
| API      | http://localhost:8080        |
| Swagger  | http://localhost:8080/swagger|

### 3. Stop the stack

```bash
docker compose down
```

To also remove the persisted database volume:

```bash
docker compose down -v
```

---

## Running Tests

```bash
# From the repository root
dotnet test tests/TransactionImporter.Tests/TransactionImporter.Tests.csproj
```

Or run via Docker:

```bash
docker run --rm \
  -v "$(pwd):/src" \
  -w /src \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  dotnet test tests/TransactionImporter.Tests/TransactionImporter.Tests.csproj
```

---

## Configuration

All configurable values live in `docker-compose.yml` under the `api` service's `environment` block. Alternatively, edit `src/TransactionImporter.API/appsettings.json` for local development.

### Changing the CSV Delimiter

The delimiter used when parsing uploaded CSV files is configured via:

**`docker-compose.yml`:**
```yaml
environment:
  CsvSettings__Delimiter: ","
```

**`appsettings.json`:**
```json
{
  "CsvSettings": {
    "Delimiter": ","
  }
}
```

Supported values:

| Value        | Delimiter character |
|--------------|---------------------|
| `,` or `comma`     | Comma `,`          |
| `;` or `semicolon` | Semicolon `;`      |
| `\|` or `pipe`     | Pipe `\|`           |

> **Note:** After changing the delimiter, restart the container: `docker compose up --build`

---

### Changing the Date Format

The expected timestamp format for `TransactionTime` is configured via:

**`docker-compose.yml`:**
```yaml
environment:
  CsvSettings__DateFormat: "yyyy-MM-dd HH:mm:ss"
```

**`appsettings.json`:**
```json
{
  "CsvSettings": {
    "DateFormat": "yyyy-MM-dd HH:mm:ss"
  }
}
```

Common examples:

| Format string         | Example input           |
|-----------------------|-------------------------|
| `yyyy-MM-dd HH:mm:ss` | `2024-01-15 10:30:00`   |
| `dd/MM/yyyy HH:mm`    | `15/01/2024 10:30`      |
| `MM/dd/yyyy`          | `01/15/2024`            |
| `yyyy-MM-ddTHH:mm:ss` | `2024-01-15T10:30:00`   |

---

### Changing the API Key

**`docker-compose.yml`:**
```yaml
environment:
  ApiKey: "your-secret-key-here"
```

All API requests require the header:
```
X-Api-Key: your-secret-key-here
```

> The frontend reads the key from the `VITE_API_KEY` build arg. Update this in `docker-compose.yml` to match `ApiKey` and rebuild.

---

## API Reference

All endpoints require the `X-Api-Key` header.

| Method | Path                       | Description                        |
|--------|----------------------------|------------------------------------|
| POST   | `/api/transactions/upload` | Upload & import a CSV file         |
| GET    | `/api/transactions`        | Get paginated transaction records  |
| PUT    | `/api/transactions/{id}`   | Update an existing transaction     |
| DELETE | `/api/transactions/{id}`   | (Soft) Delete a transaction               |

### Upload CSV
```http
POST /api/transactions/upload

Local develop API key:
X-Api-Key: dev-api-key

Docker compose ('prod') API key:
transaction-api-key

Content-Type: multipart/form-data

file: <csv file>
```

**Success (200):**
```json
{ "message": "File imported successfully." }
```

**Validation error (400):**
```json
{
  "errors": [
    "Row 5, Column 'Amount': '12.3' must have exactly 2 decimal places (e.g. 123.45)."
  ]
}
```

### Get Transactions (paginated)
```http
GET /api/transactions?pageNumber=1&pageSize=20
X-Api-Key: dev-api-key
```

**Response:**
```json
{
  "items": [...],
  "totalCount": 1050,
  "pageNumber": 1,
  "pageSize": 20,
  "totalPages": 53,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

### Update Transaction
```http
PUT /api/transactions/42
X-Api-Key: dev-api-key
Content-Type: application/json

{
  "transactionTime": "2024-06-15T10:30:00Z",
  "amount": 99.99,
  "description": "Updated description"
}
```

### Delete Transaction
```http
DELETE /api/transactions/42
X-Api-Key: dev-api-key
```

Returns `204 No Content` on success.

---

## Sample CSV

A ready-to-use sample file with 1,050 records is included: **`sample_transactions.csv`**

The expected CSV structure is:

```csv
TransactionTime,Amount,Description,TransactionId
2024-01-01 08:00:00,123.45,Grocery Store,TXN000001
2024-01-01 12:30:00,-50.00,ATM Withdrawal,TXN000002
2024-01-02 09:15:00,2500.00,Salary Deposit,TXN000003
```

### Column Rules

| Column          | Type      | Rules                                              |
|-----------------|-----------|-----------------------------------------------------|
| TransactionTime | Timestamp | Must match the configured date format               |
| Amount          | Decimal   | Numeric, exactly 2 decimal places (e.g. `123.45`)  |
| Description     | String    | Non-empty                                           |
| TransactionId   | String    | Non-empty, unique across the file and the database  |

### Validation Behaviour

- The **entire upload is rejected** if any row fails validation
- The error message identifies the exact **row number and column name** that failed
- Duplicate `TransactionId` values within the file **or** already in the database are rejected

---

## Project Structure

```
FinancialTransactionImporter/
├── src/
│   ├── TransactionImporter.Core/           # Domain entities, interfaces, services
│   │   ├── Entities/Transaction.cs
│   │   ├── Interfaces/                     # ICsvParserService, ITransactionRepository, ITransactionService
│   │   ├── Models/                         # CsvSettings, PaginatedResult, UpdateTransactionRequest
│   │   └── Services/                       # CsvParserService, TransactionService
│   ├── TransactionImporter.Infrastructure/ # EF Core DbContext + repository implementation
│   │   ├── Database/AppDbContext.cs
│   │   └── Repositories/TransactionRepository.cs
│   └── TransactionImporter.API/            # ASP.NET Core Web API
│       ├── Controllers/TransactionsController.cs
│       ├── Middleware/ApiKeyMiddleware.cs, ApiProblemDetailsFactory.cs
│       ├── DTOs/TransactionDto.cs
│       ├── Program.cs
│       └── appsettings.json
├── tests/
│   └── TransactionImporter.Tests/          # xUnit unit tests for CSV parser
│       └── CsvParserServiceTests.cs
├── frontend/                               # React + TypeScript SPA
│   ├── src/
│   │   ├── components/                     # UploadPanel, TransactionTable, EditModal
│   │   ├── hooks/useTransactions.ts
│   │   ├── services/api.ts
│   │   └── types/index.ts
│   ├── nginx.conf
│   └── Dockerfile
├── docker-compose.yml
├── sample_transactions.csv
└── README.md
```