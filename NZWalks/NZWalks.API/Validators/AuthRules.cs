namespace NZWalks.API.Validators
{
    public static class AuthRules
    {
        //Caps the input before it reaches the password hasher
        public const int MaxPasswordLength = 128;
    }
}
