namespace NZWalks.API.Constants
{
    public static class PolicyNames
    {
        //const so they can be used inside [Authorize(Policy = PolicyNames.CanRead)];
        //a policy name is only a string, so a typo in a literal fails at request time, not at compile time
        //Reader, Writer or Admin
        public const string CanRead = "CanRead";
        //Writer or Admin
        public const string CanWrite = "CanWrite";
        //Admin only: granting and revoking roles (UsersController)
        public const string CanAdmin = "CanAdmin";
    }
}
