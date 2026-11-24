using ELearning.Shared.CommonResponses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ELearning.Presentation.Controllers
{
    // Every controller inherits from this one.
    // Services return a Result (success or errors). HandleResult turns it into the right HTTP answer,
    // so controllers stay short and all errors look the same to the frontend.
    [ApiController]
    [Route("api/[controller]")]
    public abstract class ApiBaseController : ControllerBase
    {
        // Result without data: success becomes 204 No Content.
        protected IActionResult HandleResult(Result result)
        {
            return result.IsSuccess ? NoContent() : HandleProblem(result.Errors);
        }

        // Result with data: success becomes 200 OK with the data.
        protected ActionResult<TValue> HandleResult<TValue>(Result<TValue> result)
        {
            return result.IsSuccess ? Ok(result.Value) : HandleProblem(result.Errors);
        }

        private ActionResult HandleProblem(IReadOnlyList<Error> errors)
        {
            // A failed result with no error should never happen. Answer 500 so we notice it.
            if (errors.Count == 0)
                return Problem(title: "An Error Occurred", statusCode: StatusCodes.Status500InternalServerError);

            if (errors.Any(e => e.ErrorType == ErrorType.Validation))
                return HandleValidationErrors(errors);

            return HandleSingleError(errors[0]);
        }

        // The error code goes in "title" (the frontend translates it) and the English text in "detail".
        private ActionResult HandleSingleError(Error error)
        {
            return Problem(
                title: error.Code,
                detail: error.Description,
                type: error.ErrorType.ToString(),
                statusCode: MapErrorTypeIntoStatusCode(error.ErrorType));
        }

        private static int MapErrorTypeIntoStatusCode(ErrorType errorType)
        {
            return errorType switch
            {
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                ErrorType.UnAuthorized => StatusCodes.Status401Unauthorized,
                ErrorType.InvalidCredentials => StatusCodes.Status401Unauthorized,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError,
            };
        }

        // Validation errors from a service use the same shape as validation errors from the request body:
        // a 400 answer with an "errors" object (see the ApiBehaviorOptions setup in the API project).
        private ActionResult HandleValidationErrors(IReadOnlyList<Error> errors)
        {
            var modelState = new ModelStateDictionary();
            foreach (var error in errors)
                modelState.AddModelError(error.Code, error.Description);

            return ValidationProblem(modelState);
        }
    }
}
