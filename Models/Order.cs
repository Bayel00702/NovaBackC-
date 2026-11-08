namespace OnlineStore.API.Models;

public class Order
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal TotalAmount { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public List<OrderItem> Items { get; set; } = new();
}