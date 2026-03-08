using Microsoft.AspNetCore.Mvc;
using TransactionImporter.Core.Models;

namespace TransactionImporter.API.Middleware
{
    /// <summary>
    /// Canonical problem type URIs. These are stable identifiers — clients may
    /// use them to programmatically handle specific error categories.
    /// </summary>
    public static class ProblemTypes
    {
        private const string Base = "https://transaction-importer.api/errors";

        public const string CsvValidationFailed = $"{Base}/csv-validation-failed";
        public const string DuplicateTransactionId = $"{Base}/duplicate-transaction-id";
        public const string InvalidRequest = $"{Base}/invalid-request";
        public const string ResourceNotFound = $"{Base}/resource-not-found";
        public const string Unauthorized = $"{Base}/unauthorized";
        public const string Forbidden = $"{Base}/forbidden";
        public const string FileTooLarge = $"{Base}/file-too-large";
        public const string UnsupportedFileType = $"{Base}/unsupported-file-type";
    }

    /// <summary>
    /// Factory methods for constructing RFC 7807-compliant ProblemDetails responses.
    /// All responses include <c>type</c>, <c>title</c>, <c>status</c>, <c>detail</c>,
    /// and <c>instance</c>. Validation failures additionally carry a structured
    /// <c>errors</c> extension field.
    /// </summary>
    public static class ApiProblemDetailsFactory
    {
        // ── 400 Bad Request ────────────────────────────────────────────────────

        public static ObjectResult InvalidRequest(HttpContext ctx, string detail) =>
            Problem(ctx, StatusCodes.Status400BadRequest,
                ProblemTypes.InvalidRequest, "Invalid Request", detail);

        public static ObjectResult UnsupportedFileType(HttpContext ctx, string detail) =>
            Problem(ctx, StatusCodes.Status400BadRequest,
                ProblemTypes.UnsupportedFileType, "Unsupported File Type", detail);

        public static ObjectResult FileTooLarge(HttpContext ctx, string detail) =>
            Problem(ctx, StatusCodes.Status400BadRequest,
                ProblemTypes.FileTooLarge, "File Too Large", detail);

        // ── 401 Unauthorized ──────────────────────────────────────────────────

        public static IResult MissingApiKey() =>
            Results.Problem(
                type: ProblemTypes.Unauthorized,
                title: "API Key Required",
                detail: "The 'X-Api-Key' header is missing. All requests must include a valid API key.",
                statusCode: StatusCodes.Status401Unauthorized);

        // ── 403 Forbidden ─────────────────────────────────────────────────────

        public static IResult InvalidApiKey() =>
            Results.Problem(
                type: ProblemTypes.Forbidden,
                title: "Invalid API Key",
                detail: "The provided API key is not recognised. Verify the 'X-Api-Key' header value.",
                statusCode: StatusCodes.Status403Forbidden);

        // ── 404 Not Found ─────────────────────────────────────────────────────

        public static ObjectResult ResourceNotFound(HttpContext ctx, int id, string resourceType) =>
            Problem(ctx, StatusCodes.Status404NotFound,
                ProblemTypes.ResourceNotFound,
                $"{resourceType} Not Found",
                $"No {resourceType.ToLower()} with ID {id} exists. " +
                $"It may never have been created, or it may have been deleted by a previous request.");

        // ── 422 Unprocessable Content ─────────────────────────────────────────

        /// <summary>
        /// Builds a 422 response for CSV validation failures, embedding the full
        /// structured error list as an extension field so clients can display
        /// per-row, per-column feedback without parsing free-form strings.
        /// </summary>
        public static ObjectResult CsvValidationFailed(
            HttpContext ctx,
            List<ValidationError> errors,
            string fileName)
        {
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = ProblemTypes.CsvValidationFailed,
                Title = "CSV Validation Failed",
                Status = StatusCodes.Status422UnprocessableEntity,
                Detail = $"The file '{fileName}' contained {errors.Count} validation error(s). " +
                         "No records were imported. Correct all errors and re-upload the file.",
                Instance = ctx.Request.Path
            };

            problem.Extensions["errors"] = errors.Select(e => new
            {
                row = e.RowNumber,
                column = e.ColumnName,
                message = e.Message
            });

            problem.Extensions["errorCount"] = errors.Count;

            return new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status422UnprocessableEntity
            };
        }

        /// <summary>
        /// Builds a 422 response for duplicate TransactionId clashes against the database.
        /// </summary>
        public static ObjectResult DuplicateTransactionIds(
            HttpContext ctx,
            List<ValidationError> errors)
        {
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = ProblemTypes.DuplicateTransactionId,
                Title = "Duplicate Transaction IDs",
                Status = StatusCodes.Status422UnprocessableEntity,
                Detail = $"{errors.Count} TransactionId(s) in the uploaded file already exist in the " +
                         "database. TransactionIds must be globally unique. No records were imported.",
                Instance = ctx.Request.Path
            };

            problem.Extensions["errors"] = errors.Select(e => new
            {
                row = e.RowNumber,
                column = e.ColumnName,
                message = e.Message
            });

            problem.Extensions["errorCount"] = errors.Count;

            return new ObjectResult(problem)
            {
                StatusCode = StatusCodes.Status422UnprocessableEntity
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static ObjectResult Problem(
            HttpContext ctx,
            int statusCode,
            string type,
            string title,
            string detail)
        {
            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = type,
                Title = title,
                Status = statusCode,
                Detail = detail,
                Instance = ctx.Request.Path
            };

            return new ObjectResult(problem) { StatusCode = statusCode };
        }
    }
}