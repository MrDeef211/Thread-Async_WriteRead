namespace ThreadWriteRead
{
    internal static class Serializer
    {
        public static void Write(string path, string text, bool append = true)
        {
            using StreamWriter writer = new StreamWriter(path, append: append);
            writer.Write(text);
        }

        public static void WriteLine(string path, string text, bool append = true)
        {
            using StreamWriter writer = new StreamWriter(path, append: append);
            writer.WriteLine(text);
        }

        public static void Serialize<T>(string path, T obj)
        {
            throw new NotImplementedException();
        }
    }
}
