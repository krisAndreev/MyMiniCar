namespace MyMiniCar.Api.Models;

/// <summary>A single purchased line, normalized from a Stripe line item.</summary>
public sealed record OrderLineInput(string Name, decimal UnitPrice, int Quantity);

/// <summary>Everything needed to persist one paid order. Built in the webhook
/// from the Stripe session + its line items.</summary>
public sealed record PaidOrderInput(
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
