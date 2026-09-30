namespace NZWalks.API.Exceptions
{
    // Thrown when a request points at related records (foreign keys) that do not exist.
    // ExceptionHandlerMiddleware turns it into a 400 ValidationProblemDetails response.
    public class InvalidReferenceException : Exception
    {
        public IDictionary<string, string[]> Errors { get; }

        public InvalidReferenceException(IDictionary<string, string[]> errors)
            : base("One or more referenced records do not exist.")
        {
            Errors = errors;
        }

        public InvalidReferenceException(IDictionary<string, string[]> errors, Exception innerException)
            : base("One or more referenced records do not exist.", innerException)
        {
            Errors = errors;
        }
    }
}
