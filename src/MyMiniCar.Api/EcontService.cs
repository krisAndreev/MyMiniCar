using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyMiniCar.Api;

public class EcontService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string LabelPath = "Shipments/LabelService.createLabel.json";
    private const string CitiesPath = "Nomenclatures/NomenclaturesService.getCities.json";
    private const string OfficesPath = "Nomenclatures/NomenclaturesService.getOffices.json";

    private static readonly StringComparer BgComparer =
        StringComparer.Create(CultureInfo.GetCultureInfo("bg-BG"), ignoreCase: true);

    // TODO #REFACTOR - shared/distributed cache for multi-instance
    private static List<CityDto>? _citiesCache;
    private static Dictionary<int, List<OfficeDto>>? _officesByCity;
    private static readonly SemaphoreSlim NomenclatureLock = new(1, 1);

    // getCities is ~18 MB / ~50s from Econt, so the processed result is cached to disk
    // and reused across restarts (refetched only when older than the TTL).
    private static readonly string CacheFile = Path.Combine(Path.GetTempPath(), "mymincar-econt-nomenclature.json");
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(7);

    private record NomenclatureCache(DateTimeOffset SavedAt, List<CityDto> Cities, Dictionary<int, List<OfficeDto>> OfficesByCity);

    private readonly HttpClient _http;
    private readonly EcontSenderOptions _sender;
    private readonly decimal _handlingFee;

    public EcontService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _sender = config.GetSection("Econt:Sender").Get<EcontSenderOptions>() ?? new EcontSenderOptions();
        _handlingFee = config.GetValue<decimal?>("Shipping:HandlingFee") ?? 1.50m;
    }

    public async Task<ShippingQuoteResult?> CalculateAsync(ShippingQuoteRequest req)
    {
        var label = BuildLabel(
            req.DeliveryMode, req.OfficeCode, req.CityName, req.PostCode, req.Street, req.Num,
            req.WeightKg, receiverName: "Quote", receiverPhone: "0000000000", receiverEmail: null,
            description: "Price quote");

        var resp = await PostAsync<EcontLabelResponse>(LabelPath, new EcontLabelRequest { Label = label, Mode = "calculate" });

        var price = resp?.Label?.TotalPrice ?? resp?.TotalPrice;
        if (resp is null || price is null) return null;

        var currency = resp.Label?.Currency ?? resp.Currency ?? "EUR";
        return new ShippingQuoteResult((decimal)price.Value + _handlingFee, currency);
    }

    public async Task<CreateLabelResult?> CreateLabelAsync(
        string deliveryMode, string? officeCode, string? cityName, string? postCode, string? street, string? num,
        double weightKg, string receiverName, string receiverPhone, string? receiverEmail, string? description)
    {
        var label = BuildLabel(
            deliveryMode, officeCode, cityName, postCode, street, num, weightKg,
            receiverName, receiverPhone, receiverEmail, description: description);

        var resp = await PostAsync<EcontLabelResponse>(LabelPath, new EcontLabelRequest { Label = label, Mode = "create" });

        var number = resp?.Label?.ShipmentNumber;
        if (resp is null || string.IsNullOrEmpty(number)) return null;

        var price = resp.Label?.TotalPrice ?? resp.TotalPrice ?? 0d;
        var currency = resp.Label?.Currency ?? resp.Currency ?? "EUR";
        return new CreateLabelResult(number!, resp.Label?.PdfURL, (decimal)price, currency);
    }

    public async Task<List<CityDto>> GetCitiesAsync()
    {
        await EnsureNomenclatureAsync();
        return _citiesCache!;
    }

    public async Task<List<OfficeDto>> GetOfficesAsync(int cityId)
    {
        await EnsureNomenclatureAsync();
        return _officesByCity!.TryGetValue(cityId, out var offices) ? offices : new List<OfficeDto>();
    }

    private async Task EnsureNomenclatureAsync()
    {
        if (_citiesCache is not null && _officesByCity is not null) return;

        await NomenclatureLock.WaitAsync();
        try
        {
            if (_citiesCache is not null && _officesByCity is not null) return;
            if (TryLoadFromDisk()) return;

            var officesTask = PostAsync<EcontOfficesResponse>(OfficesPath, new EcontGetOfficesRequest());
            var citiesTask = PostAsync<EcontCitiesResponse>(CitiesPath, new EcontGetCitiesRequest());
            await Task.WhenAll(officesTask, citiesTask);

            var officesResp = officesTask.Result;
            var byCity = new Dictionary<int, List<OfficeDto>>();
            foreach (var o in officesResp?.Offices ?? new List<EcontOfficeWire>())
            {
                var cityId = o.Address?.City?.Id ?? 0;
                if (string.IsNullOrEmpty(o.Code) || cityId == 0) continue;

                if (!byCity.TryGetValue(cityId, out var list))
                    byCity[cityId] = list = new List<OfficeDto>();

                list.Add(new OfficeDto(
                    o.Code!,
                    o.Name ?? o.NameEn ?? o.Code!,
                    o.Address?.FullAddress ?? string.Empty,
                    o.Address?.City?.Name ?? string.Empty,
                    o.Address?.City?.PostCode ?? string.Empty));
            }
            foreach (var list in byCity.Values)
                list.Sort((a, b) => BgComparer.Compare(a.Name, b.Name));

            var citiesResp = citiesTask.Result;
            var cities = (citiesResp?.Cities ?? new List<EcontCity>())
                .Where(c => !string.IsNullOrEmpty(c.Name))
                .Where(c => c.PostCode is { Length: 4 } pc && pc.All(char.IsDigit))
                .Select(c => new CityDto(
                    c.Id,
                    c.Name!,
                    c.RegionName,
                    c.PostCode!,
                    HasOffice: byCity.ContainsKey(c.Id),
                    HasDoorDelivery: c.ServingOffices?.Any(s => s.ServingType == "to_door_courier") == true))
                .OrderBy(c => c.Name, BgComparer)
                .ToList();

            _citiesCache = cities;
            _officesByCity = byCity;
            SaveToDisk(cities, byCity);
        }
        finally
        {
            NomenclatureLock.Release();
        }
    }

    private static bool TryLoadFromDisk()
    {
        try
        {
            if (!File.Exists(CacheFile)) return false;

            var cached = JsonSerializer.Deserialize<NomenclatureCache>(File.ReadAllText(CacheFile), Json);
            if (cached is null || cached.Cities.Count == 0) return false;
            if (DateTimeOffset.UtcNow - cached.SavedAt > CacheTtl) return false;

            _citiesCache = cached.Cities;
            _officesByCity = cached.OfficesByCity;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void SaveToDisk(List<CityDto> cities, Dictionary<int, List<OfficeDto>> officesByCity)
    {
        try
        {
            File.WriteAllText(CacheFile, JsonSerializer.Serialize(new NomenclatureCache(DateTimeOffset.UtcNow, cities, officesByCity), Json));
        }
        catch
        {
        }
    }

    private EcontLabel BuildLabel(
        string deliveryMode, string? officeCode, string? cityName, string? postCode, string? street, string? num,
        double weightKg, string receiverName, string receiverPhone, string? receiverEmail, string? description)
    {
        var label = new EcontLabel
        {
            Weight = weightKg <= 0 ? 0.1 : Math.Round(weightKg, 3),
            PackCount = 1,
            ShipmentType = "pack",
            ShipmentDescription = description,
            SenderClient = new EcontClient { Name = _sender.Name, Phones = new List<string> { _sender.Phone } },
            SenderAddress = new EcontWireAddress
            {
                City = new EcontWireCity { Name = _sender.CityName, PostCode = _sender.PostCode },
                Street = _sender.Street,
                Num = _sender.Num
            },
            ReceiverClient = new EcontClient
            {
                Name = receiverName,
                Phones = new List<string> { receiverPhone },
                Email = receiverEmail
            }
        };

        if (string.Equals(deliveryMode, "office", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(officeCode))
        {
            label.ReceiverOfficeCode = officeCode;
        }
        else
        {
            label.ReceiverAddress = new EcontWireAddress
            {
                City = new EcontWireCity { Name = cityName, PostCode = postCode },
                Street = string.IsNullOrWhiteSpace(street) ? "." : street,
                Num = string.IsNullOrWhiteSpace(num) ? "1" : num
            };
        }

        return label;
    }

    private async Task<T?> PostAsync<T>(string path, object body)
    {
        using var resp = await _http.PostAsJsonAsync(path, body, Json);
        if (!resp.IsSuccessStatusCode) return default;
        return await resp.Content.ReadFromJsonAsync<T>(Json);
    }
}
