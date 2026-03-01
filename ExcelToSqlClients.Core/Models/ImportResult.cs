namespace ExcelToSqlClients.Core.Models;

public sealed class ImportResult
{
    public int ReadTotal { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }

    public List<ImportError> Errors { get; } = new();
}