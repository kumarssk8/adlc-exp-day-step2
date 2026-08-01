using System.Net;
using Microsoft.AspNetCore.Mvc;
using OuterloopLabApi.Controllers;
using OuterloopLabApi.Data;
using OuterloopLabApi.Exceptions;
using OuterloopLabApi.Models;
using OuterloopLabApi.Services;

namespace OuterloopLabApi.Tests;

public sealed class ConversionTests
{
    private sealed class FakeProvider : ICurrencyRateProvider
    {
        private readonly ProviderRateResult _result;
        private readonly Exception? _toThrow;

        public FakeProvider(ProviderRateResult result)
        {
            _result = result;
        }

        public FakeProvider(Exception toThrow)
        {
            _toThrow = toThrow;
            _result = null!;
        }

        public Task<ProviderRateResult> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken)
        {
            if (_toThrow is not null) throw _toThrow;
            return Task.FromResult(_result);
        }
    }

    private sealed class InMemoryRepo : ICosmosConversionAuditRepository
    {
        private readonly Dictionary<string, ConversionAuditRecord> _store = new();

        public Task<ConversionAuditRecord> UpsertAsync(ConversionAuditRecord record, CancellationToken cancellationToken)
        {
            _store[record.ConversionId] = record;
            return Task.FromResult(record);
        }

        public Task<ConversionAuditRecord?> GetByIdAsync(string conversionId, CancellationToken cancellationToken)
        {
            _store.TryGetValue(conversionId, out var record);
            return Task.FromResult(record);
        }
    }

    [Fact]
    public async Task ConvertAndPersist_HappyPath_PersistsAndReturnsResult()
    {
        var repo = new InMemoryRepo();
        var provider = new FakeProvider(new ProviderRateResult(
            Rate: 0.92m,
            ProviderDateMarker: "2026-08-01",
            FromCurrency: "USD",
            ToCurrency: "EUR",
            RawJson: "{\"rate\":0.92}"));

        var service = new CurrencyConversionService(provider, repo);
        var request = new CreateConversionRequest("USD", "EUR", 100.00m);

        var result = await service.ConvertAndPersistAsync(request, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.ConversionId));
        Assert.Equal("USD", result.FromCurrency);
        Assert.Equal("EUR", result.ToCurrency);
        Assert.Equal(92.00m, result.ConvertedAmount);
        Assert.Equal("2026-08-01", result.ProviderDateMarker);
    }

    [Fact]
    public async Task CreateController_ProviderFailure_Returns503_AndDoesNotBubbleRawExceptions()
    {
        var providerFailure = new CurrencyProviderUnavailableException("boom");
        var repo = new InMemoryRepo();
        var provider = new FakeProvider(providerFailure);
        var service = new CurrencyConversionService(provider, repo);
        var controller = new ConversionsController(service);

        var request = new CreateConversionRequest("USD", "EUR", 10m);

        var actionResult = await controller.Create(request, CancellationToken.None);
        var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, objectResult.StatusCode);

        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Currency provider unavailable", problem.Title);
    }
}
