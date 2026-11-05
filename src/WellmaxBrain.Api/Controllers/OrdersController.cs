using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WellmaxBrain.Domain.Entities;
using WellmaxBrain.Infrastructure.Persistence;

namespace WellmaxBrain.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;

    public OrdersController(AppDbContext db) => _db = db;

    [HttpGet("channel/{channelId}")]
    public async Task<IActionResult> GetOrdersByChannel(Guid channelId)
    {
        var orders = await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.ChannelId == channelId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return Ok(orders);
    }

    [HttpPut("{id}/sync-pos")]
    public async Task<IActionResult> SyncToPos(Guid id)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound();

        order.Status = OrderStatus.SyncedPOS;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã đồng bộ đơn hàng sang POS/ERP thành công!", order });
    }
}