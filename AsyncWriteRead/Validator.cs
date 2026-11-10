namespace AsyncWriteRead
{
    internal static class Validator
    {
        public static int GetHash(byte[] text)
        {
            var hash = new HashCode();
            hash.AddBytes(text);
            return hash.ToHashCode();
        }

        public static bool Validate(byte[] text, int hash) => hash == GetHash(text);
    }
}
