using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WellmaxBrain.Api.Hubs;
using WellmaxBrain.Domain.Entities;
using WellmaxBrain.Infrastructure.Data;

namespace WellmaxBrain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ChatHub> _hubContext;

    public UsersController(AppDbContext db, IHubContext<ChatHub> hubContext)
    {
        _db = db;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.Users.AsNoTracking().OrderBy(u => u.CreatedAt).ToListAsync();
        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] User user)
    {
        var exist = await _db.Users.AnyAsync(u => u.PhoneNumber == user.PhoneNumber);
        if (exist) return BadRequest(new { message = "Số điện thoại đã tồn tại trong hệ thống!" });

        user.Id = Guid.NewGuid().ToString();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(string.IsNullOrEmpty(user.PasswordHash) ? "123" : user.PasswordHash);
        user.CreatedAt = DateTime.UtcNow;

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Ok(user);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] User updated)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.FullName = updated.FullName;
        user.Department = updated.Department;
        user.RoleGroup = updated.RoleGroup;
        user.Gender = updated.Gender;
        user.DateOfBirth = updated.DateOfBirth;
        user.Bio = updated.Bio;
        user.Note = updated.Note;
        user.IsActive = updated.IsActive;

        await _db.SaveChangesAsync();
        return Ok(user);
    }

    public record ChangePasswordDto(string NewPassword);

    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(string id, [FromBody] ChangePasswordDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            return BadRequest(new { message = "Mật khẩu mới không được để trống!" });
        }

        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound(new { message = "Không tìm thấy người dùng!" });

        // 1. Mã hóa mật khẩu mới và lưu vào PostgreSQL
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await _db.SaveChangesAsync();

        // 2. Bắn sự kiện SignalR toàn hệ thống để ép logout tài khoản bị đổi mật khẩu
        await _hubContext.Clients.All.SendAsync("UserPasswordChanged", new
        {
            UserId = id,
            Message = "Mật khẩu của bạn đã được thay đổi. Vui lòng đăng nhập lại bằng mật khẩu mới!"
        });

        return Ok(new { message = "Đổi mật khẩu thành công và đã đăng xuất phiên làm việc của tài khoản này!" });
    }
}
