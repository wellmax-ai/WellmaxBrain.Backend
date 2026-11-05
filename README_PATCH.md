# Backend Patch - Wellmax Brain AI System
Các file thay đổi trong bản vá này:
- `src/WellmaxBrain.Api/Controllers/UsersController.cs`: Cập nhật endpoint đổi mật khẩu, mã hóa BCrypt lưu database và gửi tín hiệu SignalR ép logout tài khoản bị đổi mật khẩu.
