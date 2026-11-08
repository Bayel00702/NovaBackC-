namespace OnlineStore.API.Models;

public enum UserRole
{
    User,
    Admin
}

public enum OrderStatus
{
    Pending,
    Paid,
    Shipped,
    Delivered,
    Cancelled
}