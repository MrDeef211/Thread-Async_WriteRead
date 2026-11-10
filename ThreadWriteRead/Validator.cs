namespace ThreadWriteRead
{
    internal static class Validator
    {
        public static int GetHash(string text)
        {
            return text.GetHashCode();
        }

        public static bool Validate(string text, int hash) => hash == GetHash(text);

    }
}
