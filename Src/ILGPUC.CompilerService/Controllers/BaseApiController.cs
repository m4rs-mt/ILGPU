using ILGPUC.CompilerService.Models;
using Microsoft.AspNetCore.Mvc;

namespace ILGPUC.CompilerService.Controllers;

/// <summary>
/// Abstract base controller that provides shared response-building helpers
/// for all API controllers in the service.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// Returns a 200 OK response with the given data payload.
    /// </summary>
    /// <typeparam name="T">The type of the response data.</typeparam>
    /// <param name="data">The data to include in the response body.</param>
    protected static ActionResult OkResponse<T>(T data)
        => new OkObjectResult(data);

    /// <summary>
    /// Returns a 400 Bad Request response with a structured error body.
    /// </summary>
    /// <param name="code">A machine-readable error code.</param>
    /// <param name="message">A human-readable error message.</param>
    protected static ActionResult BadRequestResponse(string code, string message)
        => new BadRequestObjectResult(new ErrorResponse
        {
            Error = new ErrorDetail { Code = code, Message = message },
        });

    /// <summary>
    /// Returns a 404 Not Found response with a structured error body.
    /// </summary>
    /// <param name="code">A machine-readable error code.</param>
    /// <param name="message">A human-readable error message.</param>
    protected static ActionResult NotFoundResponse(string code, string message)
        => new NotFoundObjectResult(new ErrorResponse
        {
            Error = new ErrorDetail { Code = code, Message = message },
        });

    /// <summary>
    /// Returns a 500 Internal Server Error response with a structured error
    /// body.
    /// </summary>
    /// <param name="code">A machine-readable error code.</param>
    /// <param name="message">A human-readable error message.</param>
    protected static ActionResult ErrorResult(string code, string message)
        => new ObjectResult(new ErrorResponse
        {
            Error = new ErrorDetail { Code = code, Message = message },
        })
        {
            StatusCode = StatusCodes.Status500InternalServerError,
        };
}
