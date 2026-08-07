using PrivateExpenses.Application.Dtos;

namespace PrivateExpenses.Application.Abstractions.Services;

public interface IExportService
{
    /// <summary>Builds a semicolon-delimited, UTF-8 (with BOM) CSV of the filtered expenses — the
    /// delimiter and nl-NL decimal comma match what Dutch-locale Excel expects to open correctly
    /// without a manual import wizard (section 67).</summary>
    Task<byte[]> ExportExpensesToCsvAsync(ExpenseFilter filter, CancellationToken cancellationToken = default);
}
