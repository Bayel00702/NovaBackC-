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
public class CartController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public CartController(ApplicationDbContext db)
    {
        _db = db;
    }

    // id пользователя берём из токена, а не из запроса
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<CartResponseDto>> Get()
    {
        return Ok(await BuildCartAsync());
    }

    [HttpPost]
    public async Task<ActionResult<CartResponseDto>> Add(CartAddDto dto)
    {
        var product = await _db.Products.FindAsync(dto.ProductId);
        if (product is null)
            return NotFound(new { message = "Товар не найден" });

        var item = await _db.CartItems
            .FirstOrDefaultAsync(c => c.UserId == UserId && c.ProductId == dto.ProductId);

        var newQuantity = (item?.Quantity ?? 0) + dto.Quantity;
        if (newQuantity > product.Stock)
            return BadRequest(new { message = $"Недостаточно товара на складе. Доступно: {product.Stock}" });

        if (item is null)
        {
            _db.CartItems.Add(new CartItem
            {
                UserId = UserId,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity
            });
        }
        else
        {
            item.Quantity = newQuantity;
        }

        await _db.SaveChangesAsync();
        return Ok(await BuildCartAsync());
    }

    [HttpPut("{productId:int}")]
    public async Task<ActionResult<CartResponseDto>> UpdateQuantity(int productId, CartUpdateDto dto)
    {
        var item = await _db.CartItems
            .Include(c => c.Product)
            .FirstOrDefaultAsync(c => c.UserId == UserId && c.ProductId == productId);

        if (item is null)
            return NotFound(new { message = "Товара нет в корзине" });

        if (dto.Quantity > item.Product!.Stock)
            return BadRequest(new { message = $"Недостаточно товара на складе. Доступно: {item.Product.Stock}" });

        item.Quantity = dto.Quantity;
        await _db.SaveChangesAsync();

        return Ok(await BuildCartAsync());
    }

    [HttpDelete("{productId:int}")]
    public async Task<ActionResult<CartResponseDto>> Remove(int productId)
    {
        var item = await _db.CartItems
            .FirstOrDefaultAsync(c => c.UserId == UserId && c.ProductId == productId);

        if (item is null)
            return NotFound(new { message = "Товара нет в корзине" });

        _db.CartItems.Remove(item);
        await _db.SaveChangesAsync();

        return Ok(await BuildCartAsync());
    }

    [HttpDelete]
    public async Task<IActionResult> Clear()
    {
        await _db.CartItems.Where(c => c.UserId == UserId).ExecuteDeleteAsync();
        return NoContent();
    }

    private async Task<CartResponseDto> BuildCartAsync()
    {
        var userId = UserId;

        var items = await _db.CartItems
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Id)
            .Select(c => new CartItemResponseDto
            {
                ProductId = c.ProductId,
                ProductName = c.Product!.Name,
                ImageUrl = c.Product.ImageUrl,
                UnitPrice = c.Product.Price,
                Quantity = c.Quantity,
                Stock = c.Product.Stock,
                LineTotal = c.Quantity * c.Product.Price
            })
            .ToListAsync();

        return new CartResponseDto
        {
            Items = items,
            TotalItems = items.Sum(i => i.Quantity),
            TotalAmount = items.Sum(i => i.LineTotal)
        };
    }
}