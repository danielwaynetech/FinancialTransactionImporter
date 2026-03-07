using Microsoft.AspNetCore.Mvc;
using TransactionImporter.API.DTOs;
using TransactionImporter.API.ProblemDetails;
using TransactionImporter.Core.Interfaces;
using TransactionImporter.Core.Models;

namespace TransactionImporter.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json", "application/problem+json")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;
    private readonly ILogger<TransactionsController> _logger;

    private const long MaxFileSize = 50 * 1024 * 1024; // 50 MB

    public TransactionsController(
        ITransactionService transactionService,
        ILogger<TransactionsController> logger)
    {
        _transactionService = transactionService;
        _logger             = logger;
    }

    /// <summary>
    /// Uploads and imports a CSV file containing financial transactions.
    /// All rows are validated before any data is written — the response
    /// contains the complete list of errors across all rows.
    /// </summary>
    /// <response code="200">All rows valid; records imported successfully.</response>
    /// <response code="400">Malformed request (no file, wrong type, file too large).</response>
    /// <response code="401">Missing X-Api-Key header.</response>
    /// <response code="403">Invalid X-Api-Key value.</response>
    /// <response code="422">File was parseable but contained validation errors. No records imported.</response>
    [HttpPost("upload")]
    [RequestSizeLimit(52_428_800)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadCsv(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return ApiProblemDetailsFactory.InvalidRequest(HttpContext,
                "No file was provided or the uploaded file is empty.");

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return ApiProblemDetailsFactory.UnsupportedFileType(HttpContext,
                $"The file '{file.FileName}' is not a CSV file. Only files with a .csv extension are accepted.");

        if (file.Length > MaxFileSize)
            return ApiProblemDetailsFactory.FileTooLarge(HttpContext,
                $"The file '{file.FileName}' is {file.Length / (1024 * 1024):F1} MB, " +
                $"which exceeds the maximum allowed size of 50 MB.");

        _logger.LogInformation("Processing CSV upload: {FileName} ({Size} bytes)", file.FileName, file.Length);

        using var stream = file.OpenReadStream();
        var (success, errors) = await _transactionService.ImportAsync(stream);

        if (!success)
        {
            _logger.LogWarning("Import failed for {FileName}: {ErrorCount} error(s)", file.FileName, errors.Count);

            // Separate CSV structural/data errors from database duplicate violations
            // so the client receives the most actionable problem type URI.
            bool hasDuplicateDbErrors = errors.Any(e =>
                e.ColumnName == "TransactionId" &&
                e.Message.Contains("already exists in the database"));

            return hasDuplicateDbErrors
                ? ApiProblemDetailsFactory.DuplicateTransactionIds(HttpContext, errors)
                : ApiProblemDetailsFactory.CsvValidationFailed(HttpContext, errors, file.FileName);
        }

        _logger.LogInformation("CSV import successful for {FileName}", file.FileName);
        return Ok(new { message = "File imported successfully." });
    }

    /// <summary>
    /// Returns a paginated list of stored transaction records.
    /// </summary>
    /// <response code="200">Paginated transaction list.</response>
    /// <response code="400">Invalid pagination parameters.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        if (pageNumber < 1)
            return ApiProblemDetailsFactory.InvalidRequest(HttpContext,
                $"'pageNumber' must be 1 or greater. Received: {pageNumber}.");

        if (pageSize < 1 || pageSize > 200)
            return ApiProblemDetailsFactory.InvalidRequest(HttpContext,
                $"'pageSize' must be between 1 and 200. Received: {pageSize}.");

        var result = await _transactionService.GetTransactionsAsync(pageNumber, pageSize);

        return Ok(new
        {
            items = result.Items.Select(TransactionDto.FromEntity),
            result.TotalCount,
            result.PageNumber,
            result.PageSize,
            result.TotalPages,
            result.HasPreviousPage,
            result.HasNextPage
        });
    }

    /// <summary>
    /// Updates the mutable fields of an existing transaction (TransactionTime, Amount, Description).
    /// TransactionId is immutable and cannot be changed.
    /// </summary>
    /// <response code="200">Transaction updated.</response>
    /// <response code="400">Request body failed model validation.</response>
    /// <response code="404">No transaction with the given ID exists.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTransaction(int id, [FromBody] UpdateTransactionRequest request)
    {
        if (!ModelState.IsValid)
            return ApiProblemDetailsFactory.InvalidRequest(HttpContext,
                "One or more fields in the request body are invalid. " +
                string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

        var (success, error, reason) = await _transactionService.UpdateTransactionAsync(id, request);

        if (!success)
            return ApiProblemDetailsFactory.ResourceNotFound(HttpContext, id, "Transaction");

        return Ok(new { message = "Transaction updated successfully." });
    }

    /// <summary>
    /// Permanently deletes a transaction record by its internal database ID.
    /// </summary>
    /// <response code="204">Transaction deleted.</response>
    /// <response code="404">No transaction with the given ID exists.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTransaction(int id)
    {
        var (success, error, reason) = await _transactionService.DeleteTransactionAsync(id);

        if (!success)
            return ApiProblemDetailsFactory.ResourceNotFound(HttpContext, id, "Transaction");

        return NoContent();
    }
}
