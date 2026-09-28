using System.Text.Json.Serialization;

namespace EGeier.Sprit;

/// <summary>A gas station as returned by the station search.</summary>
public sealed record GasStation
{
    /// <summary>Station id assigned by the Spritpreisrechner.</summary>
    public long Id { get; init; }

    /// <summary>Station name, often including the brand.</summary>
    public required string Name { get; init; }

    /// <summary>Address and coordinates.</summary>
    public StationLocation? Location { get; init; }

    /// <summary>Contact details.</summary>
    public StationContact? Contact { get; init; }

    /// <summary>Opening hours per weekday and public holiday.</summary>
    public IReadOnlyList<OpeningHour> OpeningHours { get; init; } = [];

    /// <summary>Service mode (attended, self-service, unattended).</summary>
    public OfferInformation? OfferInformation { get; init; }

    /// <summary>Accepted payment methods.</summary>
    public PaymentMethods? PaymentMethods { get; init; }

    /// <summary>Club cards and access restrictions.</summary>
    public PaymentArrangements? PaymentArrangements { get; init; }

    /// <summary>Free-text list of additional services (shop, car wash, ...).</summary>
    public string? OtherServiceOffers { get; init; }

    /// <summary>1-based rank in the result, cheapest first.</summary>
    public int Position { get; init; }

    /// <summary>Whether the station is open right now.</summary>
    [JsonPropertyName("open")]
    public bool IsOpen { get; init; }

    /// <summary>
    /// Straight-line distance in kilometres from the search coordinates.
    /// Always 0 for region searches.
    /// </summary>
    [JsonPropertyName("distance")]
    public double DistanceKm { get; init; }

    /// <summary>
    /// Reported prices. Empty for stations outside the cheapest few, because the
    /// Spritpreisrechner only publishes prices for the cheapest stations of a search.
    /// </summary>
    public IReadOnlyList<Price> Prices { get; init; } = [];

    /// <summary>Returns the price for <paramref name="fuelType"/>, or <see langword="null"/> if not reported.</summary>
    public decimal? GetPrice(FuelType fuelType) =>
        Prices.FirstOrDefault(p => p.FuelType == fuelType)?.Amount;
}

/// <summary>Address and coordinates of a gas station.</summary>
public sealed record StationLocation
{
    /// <summary>Street and number.</summary>
    public string? Address { get; init; }

    /// <summary>Postal code.</summary>
    public string? PostalCode { get; init; }

    /// <summary>City.</summary>
    public string? City { get; init; }

    /// <summary>Latitude (WGS 84).</summary>
    public double Latitude { get; init; }

    /// <summary>Longitude (WGS 84).</summary>
    public double Longitude { get; init; }
}

/// <summary>Contact details of a gas station.</summary>
public sealed record StationContact
{
    /// <summary>Telephone number as delivered by the API (often without leading +).</summary>
    public string? Telephone { get; init; }

    /// <summary>Fax number.</summary>
    public string? Fax { get; init; }

    /// <summary>E-mail address.</summary>
    public string? Mail { get; init; }

    /// <summary>Website.</summary>
    public string? Website { get; init; }
}

/// <summary>A price for one fuel type.</summary>
public sealed record Price
{
    /// <summary>Raw fuel type code as delivered by the API.</summary>
    [JsonPropertyName("fuelType")]
    public required string FuelTypeCode { get; init; }

    /// <summary>The fuel type, or <see langword="null"/> if the API returned an unknown code.</summary>
    [JsonIgnore]
    public FuelType? FuelType => FuelTypes.TryParseApiCode(FuelTypeCode, out var fuelType) ? fuelType : null;

    /// <summary>Price in euro per litre (CNG: per kilogram).</summary>
    public decimal Amount { get; init; }

    /// <summary>Display label, e.g. "Diesel".</summary>
    public string? Label { get; init; }
}

/// <summary>Opening hours for one day.</summary>
public sealed record OpeningHour
{
    /// <summary>Day of the week, or public holiday.</summary>
    public required OpeningDay Day { get; init; }

    /// <summary>German display label, e.g. "Montag".</summary>
    public string? Label { get; init; }

    /// <summary>Sort order (1 = Monday ... 8 = public holiday).</summary>
    public int Order { get; init; }

    /// <summary>Opening time, <c>HH:mm</c>.</summary>
    public string? From { get; init; }

    /// <summary>Closing time, <c>HH:mm</c>; <c>24:00</c> means midnight.</summary>
    public string? To { get; init; }
}

/// <summary>Day used in opening hours.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<OpeningDay>))]
public enum OpeningDay
{
    /// <summary>Montag.</summary>
    [JsonStringEnumMemberName("MO")] Monday,

    /// <summary>Dienstag.</summary>
    [JsonStringEnumMemberName("DI")] Tuesday,

    /// <summary>Mittwoch.</summary>
    [JsonStringEnumMemberName("MI")] Wednesday,

    /// <summary>Donnerstag.</summary>
    [JsonStringEnumMemberName("DO")] Thursday,

    /// <summary>Freitag.</summary>
    [JsonStringEnumMemberName("FR")] Friday,

    /// <summary>Samstag.</summary>
    [JsonStringEnumMemberName("SA")] Saturday,

    /// <summary>Sonntag.</summary>
    [JsonStringEnumMemberName("SO")] Sunday,

    /// <summary>Feiertag (public holiday).</summary>
    [JsonStringEnumMemberName("FE")] PublicHoliday,
}

/// <summary>Service mode of a gas station.</summary>
public sealed record OfferInformation
{
    /// <summary>Attended service.</summary>
    public bool Service { get; init; }

    /// <summary>Self-service.</summary>
    public bool SelfService { get; init; }

    /// <summary>Unattended (automatic) station.</summary>
    public bool Unattended { get; init; }
}

/// <summary>Accepted payment methods.</summary>
public sealed record PaymentMethods
{
    /// <summary>Cash accepted.</summary>
    public bool Cash { get; init; }

    /// <summary>Debit card accepted.</summary>
    public bool DebitCard { get; init; }

    /// <summary>Credit card accepted.</summary>
    public bool CreditCard { get; init; }

    /// <summary>Free-text list of other accepted cards.</summary>
    public string? Others { get; init; }
}

/// <summary>Club cards, cooperatives and access restrictions.</summary>
public sealed record PaymentArrangements
{
    /// <summary>Station belongs to a cooperative (members only).</summary>
    public bool Cooperative { get; init; }

    /// <summary>Access restriction description.</summary>
    [JsonPropertyName("accessMod")]
    public string? AccessModality { get; init; }

    /// <summary>Club card discounts are available.</summary>
    public bool ClubCard { get; init; }

    /// <summary>Which club card, e.g. "ARBÖ".</summary>
    public string? ClubCardText { get; init; }
}
