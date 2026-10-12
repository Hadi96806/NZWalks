using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NZWalks.API.Exceptions;
using System.Diagnostics;
using System.Net;

namespace NZWalks.API.Middlewares
{
    public class ExceptionHandlerMiddleware
    {
        private readonly ILogger<ExceptionHandlerMiddleware> logger;
        private readonly IHostEnvironment environment;
        private readonly RequestDelegate Request;

        public ExceptionHandlerMiddleware(ILogger<ExceptionHandlerMiddleware> logger, IHostEnvironment environment, RequestDelegate request)
        {
            this.logger = logger;
            this.environment = environment;
            this.Request = request;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await this.Request(context);
            }
            catch (InvalidReferenceException ex)// 400
            {
                //Client sent ids of records that don't exist - a bad request, not a server fault
                logger.LogWarning("Invalid reference on {Path}: {@Errors}", context.Request.Path, ex.Errors);

                if (context.Response.HasStarted)
                {
                    throw;
                }

                await WriteProblemAsync(context, ValidationProblem(context, ex.Errors, ex.Message), (int)HttpStatusCode.BadRequest);
            }
            catch (ValidationException ex)// 400
            {
                //A validator called by hand (ValidateAndThrow) - same 400 as auto-validation, not a 500
                var errors = ex.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

                logger.LogWarning("Validation failed on {Path}: {@Errors}", context.Request.Path, errors);

                if (context.Response.HasStarted)
                {
                    throw;
                }

                await WriteProblemAsync(context, ValidationProblem(context, errors, detail: null), (int)HttpStatusCode.BadRequest);
            }
            catch (BadHttpRequestException ex)// 400 / 413 ...
            {
                //Kestrel rejected the request itself (body over the size limit, malformed request) - the client's fault
                logger.LogWarning("Bad request on {Path}: {Message}", context.Request.Path, ex.Message);

                if (context.Response.HasStarted)
                {
                    throw;
                }

                var problem = new ProblemDetails
                {
                    Title = ex.StatusCode == StatusCodes.Status413PayloadTooLarge
                        ? "The request body is too large."
                        : "The request could not be read.",
                    Status = ex.StatusCode,
                    Detail = ex.Message,
                    Instance = context.Request.Path
                };
                problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

                await WriteProblemAsync(context, problem, ex.StatusCode);
            }
            catch (RegionInUseException ex)// 409
            {
                //Request conflicts with the current state of the data
                logger.LogWarning("Conflict on {Path}: {Message}", context.Request.Path, ex.Message);

                if (context.Response.HasStarted)
                {
                    throw;
                }

                var problem = new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                    Title = "The request conflicts with the current state of the resource.",
                    Status = (int)HttpStatusCode.Conflict,
                    Detail = ex.Message,
                    Instance = context.Request.Path
                };

                await WriteProblemAsync(context, problem, (int)HttpStatusCode.Conflict);
            }
            catch (Exception ex)//500
            {
                var errorId = Guid.NewGuid();
                var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

                //Log the exception
                logger.LogError(ex, "{ErrorId} (trace {TraceId}) : {Message}", errorId, traceId, ex.Message);

                //Headers and status can't be changed once the response has started streaming
                if (context.Response.HasStarted)
                {
                    throw;
                }

                //Return RFC 7807 ProblemDetails response
                var problem = new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                    Title = "An unexpected error occurred.",
                    Status = (int)HttpStatusCode.InternalServerError,
                    Detail = environment.IsDevelopment()
                        ? ex.Message
                        : "Something went wrong, please contact the administrator.",
                    Instance = context.Request.Path
                };
                problem.Extensions["errorId"] = errorId;
                problem.Extensions["traceId"] = traceId;

                await WriteProblemAsync(context, problem, (int)HttpStatusCode.InternalServerError);
            }
        }

        //Every 400 with field errors uses this shape - the same one [ApiController] returns for invalid models
        private static ValidationProblemDetails ValidationProblem(HttpContext context, IDictionary<string, string[]> errors, string? detail)
        {
            var problem = new ValidationProblemDetails(errors)
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Title = "One or more validation errors occurred.",
                Status = (int)HttpStatusCode.BadRequest,
                Detail = detail,
                Instance = context.Request.Path
            };
            problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
            return problem;
        }

        private static async Task WriteProblemAsync(HttpContext context, ProblemDetails problem, int statusCode)
        {
            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            //Serialize as the runtime type: WriteAsJsonAsync<ProblemDetails> would drop ValidationProblemDetails.Errors
            await context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, contentType: "application/problem+json");
        }
    }
}
