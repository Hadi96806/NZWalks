namespace NZWalks.API.Constants
{
    public static class RoleNames
    {
        //const (not static readonly) so they can be used inside [Authorize(Roles = RoleNames.Admin)]
        public const string Reader = "Reader";
        public const string Writer = "Writer";
        public const string Admin = "Admin";

        //The roles seeded in NZWalksAuthDbContext; the only ones an Admin may grant
        public static readonly string[] All = { Reader, Writer, Admin };
    }
}
