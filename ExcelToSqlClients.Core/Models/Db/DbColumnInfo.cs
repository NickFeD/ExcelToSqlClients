namespace ExcelToSqlClients.Core.Models.Db;

public sealed class DbColumnInfo
{
    public string Name { get; init; } = "";
    public string SqlType { get; init; } = "";
    public bool IsNullable { get; init; }
    public bool IsIdentity { get; init; }
    public bool IsComputed { get; init; }

    public short MaxLength { get; init; }      // nvarchar: bytes
    public byte Precision { get; init; }       // decimal/numeric
    public byte Scale { get; init; }           // decimal/numeric
}
