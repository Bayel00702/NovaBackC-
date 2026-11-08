using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineStore.API.Data;
using OnlineStore.API.DTOs;
using OnlineStore.API.Models;

namespace OnlineStore.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public OrdersController(ApplicationDbContext db)
    {
        _db = db;
    }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Допустимые переходы статусов
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Transitions = new()
    {
        [OrderStatus.Pending] = new[] { OrderStatus.Paid, OrderStatus.Cancelled },
        [OrderStatus.Paid] = new[] { OrderStatus.Shipped, OrderStatus.Cancelled },
        [OrderStatus.Shipped] = new[] { OrderStatus.Delivered },
        [OrderStatus.Delivered] = Array.Empty<OrderStatus>(),
        [OrderStatus.Cancelled] = Array.Empty<OrderStatus>()
    };

    // Оформить заказ из корзины
    [HttpPost]
    public async Task<ActionResult<OrderResponseDto>> Checkout()
    {
        var userId = UserId;

        var cart = await _db.CartItems
            .Include(c => c.Product)
            .Where(c => c.UserId == userId)
            .ToListAsync();

        if (cart.Count == 0)
            return BadRequest(new { message = "Корзина пуста" });

        await using var tx = await _db.Database.BeginTransactionAsync();

        var order = new Order { UserId = userId, Status = OrderStatus.Pending };

        foreach (var c in cart)
        {
            var productId = c.ProductId;
            var qty = c.Quantity;

            // Уменьшаем остаток одним запросом с условием: так два покупателя
            // не смогут одновременно купить последний товар
            var updated = await _db.Products
                .Where(p => p.Id == productId && p.Stock >= qty)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - qty));

            if (updated == 0)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = $"Недостаточно товара «{c.Product!.Name}» на складе" });
            }

            order.Items.Add(new OrderItem
            {
                ProductId = productId,
                Quantity = qty,
                UnitPrice = c.Product!.Price // цена фиксируется на момент покупки
            });
        }

        order.TotalAmount = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        _db.Orders.Add(order);
        _db.CartItems.RemoveRange(cart);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        var dto = await LoadOrderAsync(order.Id);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, dto);
    }

    // Мои заказы
    [HttpGet]
    public async Task<ActionResult<List<OrderResponseDto>>> GetMine()
    {
        var userId = UserId;

        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return Ok(orders.Select(ToDto).ToList());
    }

    // Один заказ: владелец или админ
    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponseDto>> GetById(int id)
    {
        var dto = await LoadOrderAsync(id);
        if (dto is null) return NotFound();

        if (dto.UserId != UserId && !User.IsInRole("Admin"))
            return Forbid();

        return Ok(dto);
    }

    // Все заказы (только админ), фильтр: /api/orders/all?status=Pending
    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<OrderResponseDto>>> GetAll([FromQuery] OrderStatus? status)
    {
        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
        return Ok(orders.Select(ToDto).ToList());
    }

    // Пользователь отменяет свой заказ, пока он в статусе Pending
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null || order.UserId != UserId) return NotFound();

        if (order.Status != OrderStatus.Pending)
            return BadRequest(new { message = "Отменить можно только заказ в статусе Pending" });

        await CancelAndRestockAsync(order);
        return NoContent();
    }

    // Админ меняет статус
    [HttpPut("{id:int}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatusUpdateDto dto)
    {
        if (!Enum.IsDefined(dto.Status))
            return BadRequest(new { message = "Неизвестный статус" });

        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return NotFound();

        if (!Transitions[order.Status].Contains(dto.Status))
            return BadRequest(new { message = $"Нельзя перевести заказ из {order.Status} в {dto.Status}" });

        if (dto.Status == OrderStatus.Cancelled)
        {
            await CancelAndRestockAsync(order);
        }
        else
        {
            order.Status = dto.Status;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }

    // Отмена заказа с возвратом товара на склад
    private async Task CancelAndRestockAsync(Order order)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        foreach (var item in order.Items)
        {
            var productId = item.ProductId;
            var qty = item.Quantity;
            await _db.Products
                .Where(p => p.Id == productId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + qty));
        }

        order.Status = OrderStatus.Cancelled;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private async Task<OrderResponseDto?> LoadOrderAsync(int id)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        return order is null ? null : ToDto(order);
    }

    private static OrderResponseDto ToDto(Order o) => new()
    {
        Id = o.Id,
        CreatedAt = o.CreatedAt,
        Status = o.Status.ToString(),
        TotalAmount = o.TotalAmount,
        UserId = o.UserId,
        Items = o.Items.Select(i => new OrderItemResponseDto
        {
            ProductId = i.ProductId,
            ProductName = i.Product?.Name ?? string.Empty,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            LineTotal = i.UnitPrice * i.Quantity
        }).ToList()
    };
}
