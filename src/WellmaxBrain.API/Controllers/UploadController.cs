using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace WellmaxBrain.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif"
    };

    public UploadController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpPost]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50MB
    public async Task<IActionResult> UploadFile(IFormFile? file, [FromQuery] string type = "chat")
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Vui lòng chọn file tải lên." });

        // Sử dụng ContentRootPath để luôn lưu chuẩn vào wwwroot/uploads của API
        var uploadsFolder = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext)) ext = ".jpg";
        var isImage = ImageExtensions.Contains(ext);

        var uniqueFileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        int maxDimension = (type.ToLowerInvariant() == "avatar") ? 1000 : 1600;

        if (isImage)
        {
            try
            {
                using var inStream = file.OpenReadStream();
                using var image = await Image.LoadAsync(inStream);

                if (image.Width > maxDimension || image.Height > maxDimension)
                {
                    int newWidth = image.Width;
                    int newHeight = image.Height;

                    if (image.Width >= image.Height)
                    {
                        newWidth = maxDimension;
                        newHeight = (int)Math.Round((double)image.Height * maxDimension / image.Width);
                    }
                    else
                    {
                        newHeight = maxDimension;
                        newWidth = (int)Math.Round((double)image.Width * maxDimension / image.Height);
                    }

                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(newWidth, newHeight),
                        Mode = ResizeMode.Max
                    }));
                }

                await image.SaveAsync(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Image Resize Fallback] {ex.Message}");
                using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);
            }
        }
        else
        {
            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);
        }

        var fileInfo = new FileInfo(filePath);
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var fileUrl = $"{baseUrl}/uploads/{uniqueFileName}";

        return Ok(new
        {
            url = fileUrl,
            fileName = file.FileName,
            size = $"{(fileInfo.Length / (1024.0 * 1024.0)):F1} MB",
            ext = ext.TrimStart('.').ToLowerInvariant(),
            type = type
        });
    }
}