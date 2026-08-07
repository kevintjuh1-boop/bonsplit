using System.Text;
using PrivateExpenses.Application.Abstractions.Persistence;
using PrivateExpenses.Application.Abstractions.Services;
using PrivateExpenses.Application.Dtos;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Application.Services;

public class ExportService(IUnitOfWorkFactory unitOfWorkFactory) : IExportService
{
    public async Task<byte[]> ExportExpensesToCsvAsync(ExpenseFilter filter, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);

        // Every known person gets a column, even inactive ones, so historical expenses involving a
        // deactivated person still export their share correctly.
        var persons = (await uow.Persons.GetAllAsync(includeInactive: true, cancellationToken))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var expenses = await uow.Expenses.GetForExportAsync(filter, cancellationToken);

        var builder = new StringBuilder();

        var headerFields = new List<string> { "Datum", "Merchant", "Categorie", "Totaal", "Betaald door" };
        headerFields.AddRange(persons.Select(p => $"Aandeel {p.Name}"));
        AppendRow(builder, headerFields);

        foreach (var expense in expenses)
        {
            var payerNames = expense.Payments
                .Select(p => p.Person!.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var shareCentsByPerson = expense.Items
                .SelectMany(i => i.Shares)
                .GroupBy(s => s.PersonId)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.AmountCents));

            var fields = new List<string>
            {
                expense.ExpenseDate.ToString("dd-MM-yyyy"),
                expense.MerchantName,
                expense.Category?.Name ?? "",
                FormatCentsAsDecimal(expense.TotalCents),
                string.Join(", ", payerNames),
            };

            fields.AddRange(persons.Select(p =>
                shareCentsByPerson.TryGetValue(p.Id, out var cents) ? FormatCentsAsDecimal(cents) : ""));

            AppendRow(builder, fields);
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(builder.ToString());
        var result = new byte[preamble.Length + body.Length];
        preamble.CopyTo(result, 0);
        body.CopyTo(result, preamble.Length);
        return result;
    }

    private static string FormatCentsAsDecimal(long cents) =>
        (cents / 100m).ToString("F2", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

    private static void AppendRow(StringBuilder builder, IEnumerable<string> fields)
    {
        builder.AppendJoin(';', fields.Select(EscapeCsvField));
        builder.Append("\r\n");
    }

    private static string EscapeCsvField(string value)
    {
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
