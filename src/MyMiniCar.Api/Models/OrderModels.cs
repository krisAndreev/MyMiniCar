namespace MyMiniCar.Api.Models;

/// <summary>A single purchased line, normalized from a Stripe line item.</summary>
public sealed record OrderLineInput(string Name, decimal UnitPrice, int Quantity);

/// <summary>Everything needed to persist one paid order. Built in the webhook
/// from the Stripe session + its line items.</summary>
public sealed record PaidOrderInput(
    Guid? UserId,
    string StripeSessionId,
    string? Email,
    string? CustomerName,
    string? CustomerPhone,
    decimal Subtotal,
    decimal ShippingAmount,
    decimal Total,
    string Currency,
    string? Carrier,
    string? ShippingMethod,
    string ShippingJson,                 // jsonb payload (address/office/etc.)
    IReadOnlyList<OrderLineInput> Items);

/// <summary>One line of a past order, returned to the account page.</summary>
public sealed record OrderItemView(string Name, decimal UnitPrice, int Quantity);

/// <summary>An order summary for the account/order-history page.</summary>
public sealed record OrderView(
    Guid Id,
    string Status,
    decimal Total,
    string Currency,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemView> Items);
