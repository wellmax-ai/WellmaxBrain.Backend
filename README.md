# Wellmax Brain AI System - Backend (ASP.NET Core 8)

Hệ thống Backend cho Wellmax Brain AI System:
- **Clean Architecture**: `Domain`, `Infrastructure`, `Api`.
- **Entity Framework Core 8** với PostgreSQL.
- **SignalR ChatHub** cho kênh chat đa luồng thời gian thực.
- **OrderAiService** kết nối Google Gemini API và bộ nhớ tri thức tự học (`LearnedAliases`).
- **REST APIs**: `AuthController` (đăng nhập bằng SĐT), `UsersController` (quản trị nhân sự & phân quyền RBAC).

## Hướng dẫn cài đặt và khởi chạy:

1. Mở terminal và giải nén:
   ```bash
   unzip WellmaxBrain_Backend_Complete.zip -d backend
   cd backend
   ```

2. Cập nhật chuỗi kết nối PostgreSQL và Gemini API Key trong `src/WellmaxBrain.Api/appsettings.json`.

3. Chạy migrations để sinh cơ sở dữ liệu:
   ```bash
   dotnet ef migrations add InitialCreate --project src/WellmaxBrain.Infrastructure --startup-project src/WellmaxBrain.Api
   dotnet ef database update --project src/WellmaxBrain.Infrastructure --startup-project src/WellmaxBrain.Api
   ```

4. Khởi động Backend Web API:
   ```bash
   dotnet run --project src/WellmaxBrain.Api
   ```
   Backend sẽ chạy tại: `https://localhost:7001` (hoặc `http://localhost:5000`).
   Tài liệu Swagger: `https://localhost:7001/swagger`.
