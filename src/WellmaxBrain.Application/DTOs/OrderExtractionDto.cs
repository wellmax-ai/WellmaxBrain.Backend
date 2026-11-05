namespace WellmaxBrain.Application.DTOs;

public record OrderExtractionDto(
    string CustomerName,
    string PhoneNumber,
    string Address,
    List<OrderItemExtractionDto> Items,
    decimal TotalAmount
);

public record OrderItemExtractionDto(
    string Sku,
    string Name,
    int Quantity,
    decimal Price,
    bool IsGift
);