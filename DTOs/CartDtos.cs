using System.ComponentModel.DataAnnotations;

namespace OnlineStore.API.DTOs;

public class CartAddDto
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, 1000)]
    public int Quantity { get; set; } = 1;
}

public class CartUpdateDto
{
    [Range(1, 1000)]
    public int Quantity { get; set; }
}

public class CartItemResponseDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public int Stock { get; set; }
    public decimal LineTotal { get; set; }
}

public class CartResponseDto
{
    public List<CartItemResponseDto> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public decimal TotalAmount { get; set; }
}