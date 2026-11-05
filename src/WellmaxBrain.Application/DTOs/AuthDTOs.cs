namespace WellmaxBrain.Application.DTOs;

public record LoginRequest(string PhoneNumber, string Password);
public record LoginResponse(string Token, UserDto User);
public record UserDto(Guid Id, string FullName, string PhoneNumber, string Department, string RoleGroup, string? Avatar, bool IsAdmin, bool IsActive);
public record ChangePasswordRequest(Guid UserId, string NewPassword);