using System.ComponentModel.DataAnnotations;

namespace OnlineStore.API.DTOs;

public class CategoryCreateDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }
}

public class CategoryResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ProductsCount { get; set; }
}