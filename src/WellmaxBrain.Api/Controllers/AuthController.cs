using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using WellmaxBrain.API.Hubs;
using WellmaxBrain.Application.DTOs;
using WellmaxBrain.Domain.Entities;
using WellmaxBrain.Infrastructure.Persistence;

namespace WellmaxBrain.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ChatHub> _hubContext;

    public AuthController(AppDbContext db, IHubContext<ChatHub> hubContext)
    {
        _db = db;
        _hubContext = hubContext;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var phone = (req.PhoneNumber ?? "").Trim();
        var password = (req.Password ?? "").Trim();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);

        if (user == null)
            return Unauthorized(new { message = "Số điện thoại không tồn tại trong hệ thống Wellmax." });

        if (!user.IsActive)
            return StatusCode(403, new { message = "Tài khoản hiện đang bị khóa. Vui lòng liên hệ Admin." });

        bool isValid = false;
        try
        {
            isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        }
        catch
        {
            isValid = (password == user.PasswordHash);
        }

        if (!isValid && password == user.PasswordHash)
        {
            isValid = true;
        }

        if (!isValid)
            return Unauthorized(new { message = "Mật khẩu không chính xác. Vui lòng thử lại." });

        var userDto = new
        {
            id = user.Id.ToString(),
            name = user.FullName,
            phone = user.PhoneNumber,
            birthDate = user.BirthDate,
            gender = user.Gender,
            department = user.Department,
            roleGroup = user.RoleGroup,
            avatar = user.Avatar ?? "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&w=80&q=80",
            note = user.Note,
            bio = user.Bio,
            isAdmin = user.IsAdmin,
            isActive = user.IsActive,
            passwordFingerprint = string.IsNullOrEmpty(user.PasswordHash) ? "" : user.PasswordHash.Substring(Math.Max(0, user.PasswordHash.Length - 12))
        };

        var token = "jwt-mock-token-" + user.Id;

        return Ok(new { token, user = userDto });
    }

    // FIX CS0246 & 400 Bad Request: cho phép phone và fingerprint nullable, không throw lỗi 400
    [HttpGet("check-session")]
    public async Task<IActionResult> CheckSession([FromQuery] string? phone = null, [FromQuery] string? fingerprint = null)
    {
        if (string.IsNullOrWhiteSpace(phone)) 
        {
            return Ok(new { valid = true });
        }

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.PhoneNumber == phone.Trim());
        if (user == null) 
        {
            return Ok(new { valid = false, reason = "Tài khoản không tồn tại trên hệ thống." });
        }

        if (!user.IsActive)
        {
            return Ok(new { valid = false, reason = "Tài khoản đã bị khóa bởi Quản trị viên." });
        }

        // Nếu client có gửi fingerprint thì đối chiếu
        if (!string.IsNullOrWhiteSpace(fingerprint) && !string.IsNullOrEmpty(user.PasswordHash))
        {
            var currentFingerprint = user.PasswordHash.Substring(Math.Max(0, user.PasswordHash.Length - 12));
            if (fingerprint.Trim() != currentFingerprint)
            {
                return Ok(new { valid = false, reason = "Mật khẩu tài khoản đã được thay đổi. Mọi phiên đăng nhập cũ bị thu hồi." });
            }
        }

        return Ok(new { valid = true });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _db.Users
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                id = u.Id.ToString(),
                name = u.FullName,
                phone = u.PhoneNumber,
                birthDate = u.BirthDate,
                gender = u.Gender,
                department = u.Department,
                roleGroup = u.RoleGroup,
                avatar = u.Avatar ?? "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&w=80&q=80",
                note = u.Note,
                bio = u.Bio,
                isAdmin = u.IsAdmin,
                isActive = u.IsActive
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto req)
    {
        User? user = null;
        if (Guid.TryParse(req.UserId, out var parsedGuid))
        {
            user = await _db.Users.FindAsync(parsedGuid);
        }
        if (user == null && !string.IsNullOrEmpty(req.Phone))
        {
            user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == req.Phone.Trim());
        }

        if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

        if (!string.IsNullOrEmpty(req.Name)) user.FullName = req.Name.Trim();
        if (req.Avatar != null) user.Avatar = req.Avatar;
        if (req.Bio != null) user.Bio = req.Bio.Trim();

        await _db.SaveChangesAsync();

        return Ok(new { message = "Cập nhật hồ sơ thành công!", user = new {
            id = user.Id.ToString(),
            name = user.FullName,
            phone = user.PhoneNumber,
            avatar = user.Avatar,
            bio = user.Bio
        }});
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto req)
    {
        var phone = (req.Phone ?? "").Trim();
        if (string.IsNullOrEmpty(phone)) return BadRequest(new { message = "Số điện thoại là bắt buộc." });

        if (await _db.Users.AnyAsync(u => u.PhoneNumber == phone))
            return BadRequest(new { message = $"Số điện thoại {phone} đã được sử dụng." });

        var pass = string.IsNullOrEmpty(req.Password) ? "123" : req.Password;
        var newUser = new User
        {
            Id = Guid.NewGuid(),
            TenantId = "wellmax-default",
            FullName = req.Name?.Trim() ?? "Nhân viên mới",
            PhoneNumber = phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(pass),
            BirthDate = req.BirthDate,
            Gender = req.Gender ?? "Nam",
            Department = req.Department ?? "Kinh Doanh & Sale",
            RoleGroup = req.RoleGroup ?? "Nhân viên kinh doanh (Sale Rep)",
            Avatar = req.Avatar ?? "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&w=80&q=80",
            Note = req.Note,
            Bio = $"{req.Department} - {req.RoleGroup}",
            IsAdmin = req.IsAdmin,
            IsActive = req.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(newUser);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Thêm nhân viên thành công!", user = newUser });
    }

    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserDto req)
    {
        User? user = null;
        if (Guid.TryParse(id, out var parsedGuid))
        {
            user = await _db.Users.FindAsync(parsedGuid);
        }
        if (user == null)
        {
            user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == id);
        }

        if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

        if (!string.IsNullOrEmpty(req.Phone) && req.Phone != user.PhoneNumber)
        {
            if (await _db.Users.AnyAsync(u => u.PhoneNumber == req.Phone && u.Id != user.Id))
                return BadRequest(new { message = $"Số điện thoại {req.Phone} đã tồn tại trong hệ thống." });
            user.PhoneNumber = req.Phone.Trim();
        }

        if (!string.IsNullOrEmpty(req.Name)) user.FullName = req.Name.Trim();
        if (!string.IsNullOrEmpty(req.Department)) user.Department = req.Department;
        if (!string.IsNullOrEmpty(req.RoleGroup)) user.RoleGroup = req.RoleGroup;
        if (req.BirthDate != null) user.BirthDate = req.BirthDate;
        if (req.Gender != null) user.Gender = req.Gender;
        if (req.Avatar != null) user.Avatar = req.Avatar;
        if (req.Note != null) user.Note = req.Note;
        user.IsAdmin = req.IsAdmin;
        user.IsActive = req.IsActive;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Cập nhật nhân viên thành công!", user });
    }

    [HttpPut("users/{id}/toggle-active")]
    public async Task<IActionResult> ToggleUserActive(string id)
    {
        User? user = null;
        if (Guid.TryParse(id, out var parsedGuid))
        {
            user = await _db.Users.FindAsync(parsedGuid);
        }
        if (user == null)
        {
            user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == id);
        }

        if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync();

        if (!user.IsActive)
        {
            await _hubContext.Clients.All.SendAsync("ForceLogoutNotification", new
            {
                phoneNumber = user.PhoneNumber,
                userId = user.Id.ToString()
            });
        }

        return Ok(new { message = $"Tài khoản đã {(user.IsActive ? "được mở khóa" : "bị khóa")}.", isActive = user.IsActive });
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var newPass = (req.NewPassword ?? "").Trim();

        if (newPass.Length < 6)
        {
            return BadRequest(new { message = "Mật khẩu phải có tối thiểu 6 ký tự." });
        }

        bool hasLetter = Regex.IsMatch(newPass, @"[a-zA-Z]");
        bool hasDigit = Regex.IsMatch(newPass, @"[0-9]");
        if (!hasLetter || !hasDigit)
        {
            return BadRequest(new { message = "Mật khẩu phải bao gồm cả chữ cái và số." });
        }

        User? user = null;
        if (Guid.TryParse(req.UserId, out var parsedGuid))
        {
            user = await _db.Users.FindAsync(parsedGuid);
        }

        if (user == null && !string.IsNullOrEmpty(req.PhoneNumber))
        {
            user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == req.PhoneNumber.Trim());
        }

        if (user == null)
        {
            var phoneMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "me", "0988888888" },
                { "u-1", "0912345671" },
                { "u-2", "0912345672" },
                { "u-3", "0912345673" },
                { "u-4", "0912345674" }
            };

            var safeUserId = req.UserId ?? string.Empty;
            if (phoneMap.TryGetValue(safeUserId, out var mappedPhone))
            {
                user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == mappedPhone);
            }
        }

        if (user == null)
        {
            return NotFound(new { message = "Không tìm thấy người dùng trong hệ thống." });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPass);
        await _db.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("ForceLogoutNotification", new
        {
            phoneNumber = user.PhoneNumber,
            userId = user.Id.ToString()
        });

        return Ok(new { 
            message = "Đổi mật khẩu thành công. Đã buộc đăng xuất phiên làm việc cũ.",
            userId = user.Id.ToString(),
            phoneNumber = user.PhoneNumber
        });
    }
}