# Wellmax Brain AI System - Backend Solution (.NET 8 Clean Architecture)

Hệ thống Trí tuệ Nhân tạo Hỗ trợ Quản trị Vận hành, Kênh Chat & Tự động Trích xuất Đơn hàng Bán lẻ - Đa kênh.

## Cấu trúc Solution
- `WellmaxBrain.Domain`: Định nghĩa Entity (User, Channel, ChatMessage, Order, LearnedAlias, Product).
- `WellmaxBrain.Application`: DTOs, Business Interfaces (IAIExtractorService, AuthDTOs).
- `WellmaxBrain.Infrastructure`: EF Core 8 DbContext PostgreSQL, Service Google Gemini REST API.
- `WellmaxBrain.API`: Web API Controllers, SignalR Hubs (`/hubs/chat`), Program.cs.

## Hướng dẫn Khởi chạy
1. Cấu hình chuỗi kết nối PostgreSQL trong `src/WellmaxBrain.API/appsettings.json`.
2. Chạy migration tạo Database:
   ```bash
   dotnet ef database update --project src/WellmaxBrain.Infrastructure --startup-project src/WellmaxBrain.API
   ```
3. Chạy Backend API:
   ```bash
   dotnet run --project src/WellmaxBrain.API
   ```
4. Truy cập Swagger UI: `http://localhost:5000/swagger`.