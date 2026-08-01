using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using OuterloopLabApi.Exceptions;
using OuterloopLabApi.Models;
using OuterloopLabApi.Services;

namespace OuterloopLabApi.Controllers;

[ApiController]
[Route("api/conversions")]
public sealed class ConversionsController : ControllerBase
{
    private readonly CurrencyConversionService _service;

    public ConversionsController(CurrencyConversionService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<ConversionResult>> Create([FromBody] CreateConversionRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        try
        {
            var result = await _service.ConvertAndPersistAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid conversion request",
                Detail = ex.Message
            });
        }
        catch (CurrencyProviderUnavailableException ex)
        {
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, new ProblemDetails
            {
                Title = "Currency provider unavailable",
                Detail = "The external currency provider could not be reached or returned usable data."
            });
        }
        catch
        {
            return StatusCode(500, new ProblemDetails
            {
                Title = "Internal error"
            });
        }
    }

    [HttpGet("{conversionId}")]
    public async Task<ActionResult<ConversionResult>> GetById([FromRoute] string conversionId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.GetByIdAsync(conversionId, cancellationToken);
            return Ok(result);
        }
        catch (ConversionNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Conversion not found"
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid conversion id",
                Detail = ex.Message
            });
        }
        catch
        {
            return StatusCode(500, new ProblemDetails
            {
                Title = "Internal error"
            });
        }
    }
}
