namespace OuterloopLabApi.Exceptions;

public sealed class ConversionNotFoundException : Exception
{
    public ConversionNotFoundException(string conversionId)
        : base($"Conversion not found: {conversionId}")
    {
    }
}
