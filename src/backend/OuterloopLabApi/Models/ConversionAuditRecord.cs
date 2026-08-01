using System.Text.Json.Serialization;

namespace OuterloopLabApi.Models;

public sealed class ConversionAuditRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;

    [JsonPropertyName("conversionId")]
    public string ConversionId { get; set; } = default!;

    [JsonPropertyName("fromCurrency")]
    public string FromCurrency { get; set; } = default!;

    [JsonPropertyName("toCurrency")]
    public string ToCurrency { get; set; } = default!;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("rate")]
    public decimal Rate { get; set; }

    [JsonPropertyName("convertedAmount")]
    public decimal ConvertedAmount { get; set; }

    [JsonPropertyName("providerDateMarker")]
    public string ProviderDateMarker { get; set; } = default!;

    [JsonPropertyName("executedAtUtc")]
    public DateTime ExecutedAtUtc { get; set; }

    // Schema-agnostic: store the entire upstream payload as-is so our audit trail remains reconstructable.
    [JsonPropertyName("providerRawJson")]
    public string ProviderRawJson { get; set; } = default!;
}
