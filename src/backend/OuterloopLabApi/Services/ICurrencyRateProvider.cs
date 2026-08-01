using OuterloopLabApi.Models;

namespace OuterloopLabApi.Services;

public interface ICurrencyRateProvider
{
    Task<ProviderRateResult> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken);
}
