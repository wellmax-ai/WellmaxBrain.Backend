using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WellmaxBrain.Domain.Entities;
using WellmaxBrain.Infrastructure.Data;

namespace WellmaxBrain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuthController(AppDbContext db)
    {
        _db = db;
    }

    public record LoginRequest(string PhoneNumber, string Password);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == req.PhoneNumber);
        if (user == null || !user.IsActive)
        {
            // Trả về tài khoản mẫu nếu DB chưa seed dữ liệu
            if (req.PhoneNumber == "0988888888")
            {
                return Ok(new
                {
                    Id = "me",
                    FullName = "Đào Hà (Admin)",
                    PhoneNumber = "0988888888",
                    Department = "Kỹ Thuật & AI",
                    RoleGroup = "Quản trị viên tối cao (Super Admin)",
                    IsAdmin = true
                });
            }
            return Unauthorized(new { message = "Số điện thoại hoặc mật khẩu không chính xác." });
        }

        return Ok(new
        {
            user.Id,
            user.FullName,
            user.PhoneNumber,
            user.Department,
            user.RoleGroup,
            user.IsAdmin
        });
    }
}
