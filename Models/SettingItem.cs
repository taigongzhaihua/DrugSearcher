using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DrugSearcher.Models;

[Table("Settings")]
public class SettingItem
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string ValueType { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Value { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    public bool IsReadOnly { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 用于多用户支持（可选）
    [MaxLength(100)]
    public string? UserId { get; set; }
}