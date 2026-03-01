using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExcelToSqlClients.Core.Entities;

[Table("Clients")]
public class Client
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string CardCode { get; set; } = null!;

    [MaxLength(100)]
    public string? LastName { get; set; }

    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? SurName { get; set; }

    [MaxLength(20)]
    public string? PhoneMobile { get; set; }

    [MaxLength(254)]
    public string? Email { get; set; }

    public byte? GenderId { get; set; } // 1=муж, 2=жен

    [Column(TypeName = "date")]
    public DateOnly? Birthday { get; set; }

    [MaxLength(200)]
    public string? City { get; set; }

    [MaxLength(20)]
    public string? Pincode { get; set; }

    public int? Bonus { get; set; }

    public long? Turnover { get; set; }
}