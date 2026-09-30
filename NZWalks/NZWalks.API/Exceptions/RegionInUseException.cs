namespace NZWalks.API.Exceptions
{
    // Thrown when a Region is deleted while a Walk still references it.
    // ExceptionHandlerMiddleware turns it into a 409 ProblemDetails response.
    public class RegionInUseException : Exception
    {
        public RegionInUseException(Guid regionId)
            : base($"Region '{regionId}' cannot be deleted because it is referenced by one or more walks.")
        {
        }
    }
}
