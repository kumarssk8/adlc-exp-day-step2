using System.ComponentModel.DataAnnotations;

namespace OuterloopLabApi.Models;

public sealed record CreateConversionRequest(
    [property: Required] string FromCurrency,
    [property: Required] string ToCurrency,
    [property: Range(typeof(decimal), "0.00000000000001", "79228162514264337593543950335")] decimal Amount);
