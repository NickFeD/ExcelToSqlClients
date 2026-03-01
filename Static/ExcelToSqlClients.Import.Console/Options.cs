using CommandLine;

namespace ExcelToSqlClients.Import.Console;

public sealed class Options
{
    [Option('f', "file", Required = true, HelpText = "Path to Excel file (.xlsx)")]
    public string ExcelPath { get; set; } = "";

    [Option('b', "batch", Required = false, HelpText = "Batch size (default from appsettings Import:BatchSize)")]
    public int? BatchSize { get; set; }
}