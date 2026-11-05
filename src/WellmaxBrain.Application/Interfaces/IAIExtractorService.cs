using WellmaxBrain.Application.DTOs;

namespace WellmaxBrain.Application.Interfaces;

public interface IAIExtractorService
{
    Task<OrderExtractionDto?> ExtractOrderFromTextAsync(string rawText, string tenantId, string apiKey, string modelName);
}