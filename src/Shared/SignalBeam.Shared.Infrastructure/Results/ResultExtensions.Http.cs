using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SignalBeam.Shared.Infrastructure.Results;

/// <summary>
/// Extension methods for converting Result to HTTP responses.
/// </summary>
public static class ResultHttpExtensions
{
    /// <summary>
    /// Converts a Result to an IResult for ASP.NET Core minimal APIs.
    /// </summary>
    public static IResult ToHttpResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return TypedResults.Ok();
        }

        return result.Error!.Type switch
        {
            ErrorType.Validation => TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Detail = result.Error.Message,
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["errorCode"] = result.Error.Code }
            }),
            ErrorType.NotFound => TypedResults.NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = result.Error.Message,
                Status = StatusCodes.Status404NotFound,
                Extensions = { ["errorCode"] = result.Error.Code }
            }),
            ErrorType.Conflict => TypedResults.Conflict(new ProblemDetails
            {
                Title = "Conflict",
                Detail = result.Error.Message,
                Status = StatusCodes.Status409Conflict,
                Extensions = { ["errorCode"] = result.Error.Code }
            }),
            ErrorType.Unauthorized => TypedResults.Unauthorized(),
            ErrorType.Forbidden => TypedResults.Forbid(),
            _ => TypedResults.Problem(new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = result.Error.Message,
                Status = StatusCodes.Status500InternalServerError,
                Extensions = { ["errorCode"] = result.Error.Code }
            })
        };
    }

    /// <summary>
    /// Converts a Result&lt;T&gt; to an IResult for ASP.NET Core minimal APIs.
    /// </summary>
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return TypedResults.Ok(result.Value);
        }

        return result.Error!.Type switch
        {
            ErrorType.Validation => TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Detail = result.Error.Message,
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["errorCode"] = result.Error.Code }
            }),
            ErrorType.NotFound => TypedResults.NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = result.Error.Message,
                Status = StatusCodes.Status404NotFound,
                Extensions = { ["errorCode"] = result.Error.Code }
            }),
            ErrorType.Conflict => TypedResults.Conflict(new ProblemDetails
            {
                Title = "Conflict",
                Detail = result.Error.Message,
                Status = StatusCodes.Status409Conflict,
                Extensions = { ["errorCode"] = result.Error.Code }
            }),
            ErrorType.Unauthorized => TypedResults.Unauthorized(),
            ErrorType.Forbidden => TypedResults.Forbid(),
            _ => TypedResults.Problem(new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = result.Error.Message,
                Status = StatusCodes.Status500InternalServerError,
                Extensions = { ["errorCode"] = result.Error.Code }
            })
        };
    }

    /// <summary>
    /// Converts a Result&lt;T&gt; to an ActionResult for MVC controllers.
    /// </summary>
    public static ActionResult<T> ToActionResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(result.Value);
        }

        return result.Error!.Type switch
        {
            ErrorType.Validation => new BadRequestObjectResult(new ProblemDetails
            {
                Title = "Validation Error",
                Detail = result.Error.Message,
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["errorCode"] = result.Error.Code }
            }),
            ErrorType.NotFound => new NotFoundObjectResult(new ProblemDetails
            {
                Title = "Not Found",
                Detail = result.Error.Message,
                Status = StatusCodes.Status404NotFound,
                Extensions = { ["errorCode"] = result.Error.Code }
            }),
            ErrorType.Conflict => new ConflictObjectResult(new ProblemDetails
            {
                Title = "Conflict",
                Detail = result.Error.Message,
                Status = StatusCodes.Status409Conflict,
                Extensions = { ["errorCode"] = result.Error.Code }
            }),
            ErrorType.Unauthorized => new UnauthorizedResult(),
            ErrorType.Forbidden => new ForbidResult(),
            _ => new ObjectResult(new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = result.Error.Message,
                Status = StatusCodes.Status500InternalServerError,
                Extensions = { ["errorCode"] = result.Error.Code }
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            }
        };
    }
}
