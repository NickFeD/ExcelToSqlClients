using ExcelToSqlClients.Core.Models;

namespace ExcelToSqlClients.Core.Abstractions;

public interface IClientImportService
{
    Task<ImportResult> ImportAsync(string excelPath, int batchSize, CancellationToken ct);
}