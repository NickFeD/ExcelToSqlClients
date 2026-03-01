using ExcelToSqlClients.Core.Models;

namespace ExcelToSqlClients.Core.Abstractions;

public interface IClientReader
{
    IEnumerable<ClientReadResult> Read(string path);
}