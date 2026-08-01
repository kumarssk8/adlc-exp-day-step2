using Microsoft.Azure.Cosmos;
using OuterloopLabApi.Models;

namespace OuterloopLabApi.Data;

public sealed class CosmosConversionAuditRepository : ICosmosConversionAuditRepository
{
    private readonly Container _container;

    public CosmosConversionAuditRepository(Container container)
    {
        _container = container;
    }

    public Task<ConversionAuditRecord> UpsertAsync(ConversionAuditRecord record, CancellationToken cancellationToken)
    {
        return _container.UpsertItemAsync(record, new PartitionKey(record.ConversionId), cancellationToken: cancellationToken)
            .ContinueWith(t => t.Result.Resource, cancellationToken);
    }

    public async Task<ConversionAuditRecord?> GetByIdAsync(string conversionId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.ReadItemAsync<ConversionAuditRecord>(
                id: conversionId,
                partitionKey: new PartitionKey(conversionId),
                cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
