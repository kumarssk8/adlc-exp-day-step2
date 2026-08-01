namespace OuterloopLabApi.Exceptions;

public sealed class CurrencyProviderUnavailableException : Exception
{
    public CurrencyProviderUnavailableException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
