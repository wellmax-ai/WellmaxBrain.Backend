using WellmaxBrain.Application.DTOs;

namespace WellmaxBrain.Application.Interfaces;

public interface ILlmService
{
    /// <summary>Gửi prompt tới LLM đang cấu hình (Gemini hoặc DeepSeek), trả về text thô.</summary>
    Task<LlmResultDto> GenerateAsync(string prompt, string systemPrompt, double temperature, CancellationToken ct = default);

    /// <summary>Kiểm tra kết nối tới provider đang cấu hình bằng 1 prompt tối giản.</summary>
    Task<LlmResultDto> TestConnectionAsync(CancellationToken ct = default);
}