namespace OuterloopLabApi.Models;

public sealed record ProviderRateResult(
    decimal Rate,
    string ProviderDateMarker,
    string FromCurrency,
    string ToCurrency,
    string RawJson);
