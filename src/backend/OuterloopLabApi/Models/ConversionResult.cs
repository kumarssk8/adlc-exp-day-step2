namespace OuterloopLabApi.Models;

public sealed record ConversionResult(
    string ConversionId,
    string FromCurrency,
    string ToCurrency,
    decimal Amount,
    decimal Rate,
    decimal ConvertedAmount,
    string ProviderDateMarker,
    DateTime ExecutedAtUtc);
