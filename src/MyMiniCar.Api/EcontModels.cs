namespace MyMiniCar.Api;

public record ShippingQuoteRequest(
    string DeliveryMode,
    string? OfficeCode,
    string? CityName,
    string? PostCode,
    string? Street,
    string? Num,
    double WeightKg);

public record ShippingQuoteResult(decimal Price, string Currency);

public record OfficeDto(string Code, string Name, string Address, string City, string PostCode);

public record CityDto(int Id, string Name, string? Region, string PostCode, bool HasOffice, bool HasDoorDelivery);

public record CreateLabelRequest(string SessionId);

public record CreateLabelResult(string ShipmentNumber, string? PdfUrl, decimal Price, string Currency);

internal class EcontLabelRequest
{
    public EcontLabel Label { get; set; } = new();

    // Econt requires "mode" as a sibling of "label", NOT inside it.
    public string Mode { get; set; } = "calculate";
}

internal class EcontLabel
{
    public EcontClient? SenderClient { get; set; }
    public EcontWireAddress? SenderAddress { get; set; }
    public string? SenderOfficeCode { get; set; }

    public EcontClient? ReceiverClient { get; set; }
    public EcontWireAddress? ReceiverAddress { get; set; }
    public string? ReceiverOfficeCode { get; set; }

    public int PackCount { get; set; } = 1;
    public string ShipmentType { get; set; } = "pack";
    public double Weight { get; set; }
    public string? ShipmentDescription { get; set; }
}

internal class EcontClient
{
    public string? Name { get; set; }
    public List<string>? Phones { get; set; }
    public string? Email { get; set; }
}

internal class EcontWireAddress
{
    public EcontWireCity? City { get; set; }
    public string? Street { get; set; }
    public string? Num { get; set; }
}

internal class EcontWireCity
{
    public EcontCountry Country { get; set; } = new();
    public string? Name { get; set; }
    public string? PostCode { get; set; }
}

internal class EcontCountry
{
    public string Code3 { get; set; } = "BGR";
}

internal class EcontLabelResponse
{
    public EcontLabelData? Label { get; set; }
    public double? TotalPrice { get; set; }
    public string? Currency { get; set; }
}

internal class EcontLabelData
{
    public string? ShipmentNumber { get; set; }
    public double? TotalPrice { get; set; }
    public string? Currency { get; set; }
    public string? PdfURL { get; set; }
}

internal class EcontGetCitiesRequest
{
    public string CountryCode { get; set; } = "BGR";
}

internal class EcontCitiesResponse
{
    public List<EcontCity>? Cities { get; set; }
}

internal class EcontCity
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? NameEn { get; set; }
    public string? RegionName { get; set; }
    public string? PostCode { get; set; }
    public List<EcontServingOffice>? ServingOffices { get; set; }
}

internal class EcontServingOffice
{
    public string? OfficeCode { get; set; }
    public string? ServingType { get; set; }
}

internal class EcontGetOfficesRequest
{
    public string CountryCode { get; set; } = "BGR";
    public int? CityID { get; set; }
}

internal class EcontOfficesResponse
{
    public List<EcontOfficeWire>? Offices { get; set; }
}

internal class EcontOfficeWire
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? NameEn { get; set; }
    public EcontOfficeAddress? Address { get; set; }
}

internal class EcontOfficeAddress
{
    public string? FullAddress { get; set; }
    public EcontCity? City { get; set; }
}

// TODO #REFACTOR - use real registered Econt sender details before live
public class EcontSenderOptions
{
    public string Name { get; set; } = "AURA Studio";
    public string Phone { get; set; } = "0888888888";
    public string CityName { get; set; } = "София";
    public string PostCode { get; set; } = "1000";
    public string Street { get; set; } = "бул. Витоша";
    public string Num { get; set; } = "1";
}
