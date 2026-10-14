namespace NZWalks.API.Constants
{
    public static class RateLimitPolicies
    {
        //const so it can be used inside [EnableRateLimiting(RateLimitPolicies.Auth)]
        //Credential endpoints (login, register): 5 requests per minute per IP
        public const string Auth = "Auth";
    }
}
