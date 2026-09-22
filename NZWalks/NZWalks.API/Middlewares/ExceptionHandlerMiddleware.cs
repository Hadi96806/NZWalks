using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
            catch(Exception ex)
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
                        : "Something went wrong, please contact adminstrator",
                    Instance = context.Request.Path
                };
                problem.Extensions["errorId"] = errorId;
                problem.Extensions["traceId"] = traceId;

                context.Response.Clear();
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
            }
        }

    }
}
