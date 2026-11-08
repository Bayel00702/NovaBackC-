using OnlineStore.API.Models;

namespace OnlineStore.API.DTOs;

public class OrderItemResponseDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class OrderResponseDto
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int UserId { get; set; }
    public List<OrderItemResponseDto> Items { get; set; } = new();
}

public class OrderStatusUpdateDto
{
    public OrderStatus Status { get; set; }
}