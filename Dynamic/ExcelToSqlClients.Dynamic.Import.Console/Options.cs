using CommandLine;

namespace ExcelToSqlClients.Dynamic.Import.Console;

public class Options
{
    [Option('f', "file", Required = true, HelpText = "Path to Excel file (.xlsx).")]
    public string ExcelPath { get; set; } = "";

    [Option('b', "batch", Required = false, HelpText = "Batch size (default from appsettings Import:BatchSize).")]
    public int? BatchSize { get; set; }

    [Option("ignore-errors", Required = false, HelpText = "Ignore import errors and continue.")]
    public bool IgnoreErrors { get; set; }
}
