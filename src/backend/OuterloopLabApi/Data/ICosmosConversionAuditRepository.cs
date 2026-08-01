using OuterloopLabApi.Models;

namespace OuterloopLabApi.Data;

public interface ICosmosConversionAuditRepository
{
    Task<ConversionAuditRecord> UpsertAsync(ConversionAuditRecord record, CancellationToken cancellationToken);

    Task<ConversionAuditRecord?> GetByIdAsync(string conversionId, CancellationToken cancellationToken);
}
