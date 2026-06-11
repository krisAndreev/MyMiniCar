namespace MyMiniCar.Api.Models;

/// <summary>A design to save. Config is an opaque JSON blob owned by the client.</summary>
public sealed record DesignCreate(string? Name, string ConfigJson);

/// <summary>A saved design returned to the account/studio.</summary>
public sealed record DesignView(Guid Id, string? Name, string Config, DateTime CreatedAt);
