namespace WellmaxBrain.Application.DTOs;

public record LoginRequest(string PhoneNumber, string Password);
public record LoginResponse(string Token, UserDto User);
public record UserDto(Guid Id, string FullName, string PhoneNumber, string Department, string RoleGroup, string? Avatar, bool IsAdmin, bool IsActive);
public record ChangePasswordRequest(string UserId, string? PhoneNumber, string NewPassword);

// Bổ sung các DTO bị thiếu cho Update Profile và Quản lý User
public record UpdateProfileDto(string UserId, string? Phone, string? Name, string? Avatar, string? Bio);
public record CreateUserDto(string? Name, string? Phone, string? Password, string? BirthDate, string? Gender, string? Department, string? RoleGroup, string? Avatar, string? Note, bool IsAdmin, bool IsActive);
public record UpdateUserDto(string? Name, string? Phone, string? BirthDate, string? Gender, string? Department, string? RoleGroup, string? Avatar, string? Note, bool IsAdmin, bool IsActive);