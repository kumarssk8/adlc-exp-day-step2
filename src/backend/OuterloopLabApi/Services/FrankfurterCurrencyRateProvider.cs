using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using OuterloopLabApi.Exceptions;
using OuterloopLabApi.Models;

namespace OuterloopLabApi.Services;

public sealed class FrankfurterCurrencyRateProvider : ICurrencyRateProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _settings;

    public FrankfurterCurrencyRateProvider(IHttpClientFactory httpClientFactory, AppSettings settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
    }

    public async Task<ProviderRateResult> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken)
    {
        var baseUrl = _settings.CurrencyApiBaseUrl.TrimEnd('/');

        // Frankfurter's hostname is often given as frankfurter.dev, but the actual JSON API is served from api.frankfurter.dev.
        // We derive the actual API host from the configured base URL so all calls remain rooted in CURRENCY_API_BASE_URL.
        if (baseUrl.Equals("https://frankfurter.dev", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = "https://api.frankfurter.dev";
        }

        var url = $"{baseUrl}/v2/rate/{fromCurrency}/{toCurrency}";

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd("application/json");

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new CurrencyProviderUnavailableException(
                    $"Currency provider returned HTTP {(int)response.StatusCode}");
            }

            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            var rate = ExtractRate(root, toCurrency);
            var providerDateMarker = ExtractProviderDateMarker(root);

            return new ProviderRateResult(
                Rate: rate,
                ProviderDateMarker: providerDateMarker,
                FromCurrency: root.TryGetProperty("base", out var baseProp) && baseProp.ValueKind == JsonValueKind.String
                    ? baseProp.GetString()!
                    : fromCurrency,
                ToCurrency: root.TryGetProperty("quote", out var quoteProp) && quoteProp.ValueKind == JsonValueKind.String
                    ? quoteProp.GetString()!
                    : toCurrency,
                RawJson: rawJson);
        }
        catch (CurrencyProviderUnavailableException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new CurrencyProviderUnavailableException("Currency provider call failed.", ex);
        }
        catch (Exception ex)
        {
            throw new CurrencyProviderUnavailableException("Currency provider call failed.", ex);
        }
    }

    private static decimal ExtractRate(JsonElement root, string toCurrency)
    {
        if (root.TryGetProperty("rate", out var rateProp) && rateProp.TryGetDecimal(out var rate))
        {
            return rate;
        }

        // Flexible mapping: handle potential schema variants.
        if (TryExtractFromRateObjects(root, "rates", toCurrency, out var ratesRate)) return ratesRate;
        if (TryExtractFromRateObjects(root, "conversion_rates", toCurrency, out var convRates)) return convRates;

        throw new CurrencyProviderUnavailableException("Currency provider returned no usable rate.");
    }

    private static bool TryExtractFromRateObjects(JsonElement root, string propertyName, string toCurrency, out decimal rate)
    {
        rate = default;
        if (!root.TryGetProperty(propertyName, out var rates) || rates.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        // Frankfurter-like shape may use the quote currency as key.
        if (rates.TryGetProperty(toCurrency, out var direct) && direct.TryGetDecimal(out var directRate))
        {
            rate = directRate;
            return true;
        }

        foreach (var prop in rates.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDecimal(out rate))
            {
                // Fallback: use the first numeric value.
                return true;
            }
        }

        return false;
    }

    private static string ExtractProviderDateMarker(JsonElement root)
    {
        if (root.TryGetProperty("date", out var dateProp) && dateProp.ValueKind == JsonValueKind.String)
        {
            return dateProp.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("timestamp", out var timestampProp) && timestampProp.ValueKind == JsonValueKind.String)
        {
            return timestampProp.GetString() ?? string.Empty;
        }

        // If the provider doesn't expose a date, return empty so the audit record is still stored deterministically.
        return string.Empty;
    }
}
