namespace WellmaxBrain.Application.DTOs;

public record LlmResultDto(bool IsSuccess, string Content, string Provider, string ModelName, int LatencyMs, string? ErrorMessage);

public record AiProviderConfigDto(
    Guid Id,
    string Provider,
    string ModelName,
    string BaseUrl,
    string SystemPrompt,
    double Temperature,
    bool IsActive,
    bool HasApiKey,
    string MaskedApiKey,
    DateTime UpdatedAt
);

public record SaveAiProviderConfigRequest(
    string Provider,
    string ApiKey,
    string ModelName,
    string? BaseUrl,
    string? SystemPrompt,
    double Temperature,
    Guid? UpdatedByUserId
);

public record TestAiConnectionResult(
    bool IsSuccess,
    string Provider,
    string ModelName,
    int LatencyMs,
    string? Message,
    string? Reply
);

public record CreateTrainingRequest(
    string Title,
    string Content,
    string SourceType,
    string? SourceRef,
    string? ChannelId,
    Guid? CreatedByUserId,
    int Priority
);

public record TrainingDataDto(
    Guid Id,
    string Title,
    string Content,
    string SourceType,
    string? SourceRef,
    string? ChannelId,
    int Priority,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record AiActivityLogDto(
    Guid Id,
    string Action,
    string Provider,
    string ModelName,
    string? RequestSummary,
    string? ResponseSummary,
    bool IsSuccess,
    int LatencyMs,
    string? ErrorMessage,
    DateTime CreatedAt
);

public record AiAgentChatRequest(string ChannelId, string Message, string? SenderId, string? SenderName, Guid? RequestingUserId);

public record AiAgentChatResponse(bool IsSuccess, string Reply, string Provider, string ModelName, int LatencyMs, OrderExtractionDto? ExtractedOrder, string? ErrorMessage);