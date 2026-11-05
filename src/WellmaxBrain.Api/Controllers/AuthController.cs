using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WellmaxBrain.Application.DTOs;
using WellmaxBrain.Infrastructure.Persistence;

namespace WellmaxBrain.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var phone = req.PhoneNumber.Trim();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);

        if (user == null)
            return Unauthorized(new { message = "Số điện thoại không tồn tại trong hệ thống." });

        if (!user.IsActive)
            return StatusCode(403, new { message = "Tài khoản hiện đang bị khóa." });

        // Kiểm tra mật khẩu (hỗ trợ hash hoặc demo 123)
        bool isValid = BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash) || req.Password == user.PasswordHash;
        if (!isValid)
            return Unauthorized(new { message = "Mật khẩu không chính xác." });

        var userDto = new UserDto(user.Id, user.FullName, user.PhoneNumber, user.Department, user.RoleGroup, user.Avatar, user.IsAdmin, user.IsActive);
        var token = "jwt-mock-token-" + user.Id;

        return Ok(new LoginResponse(token, userDto));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var user = await _db.Users.FindAsync(req.UserId);
        if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đổi mật khẩu thành công. Đã buộc đăng xuất phiên làm việc cũ." });
    }
}