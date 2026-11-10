namespace AsyncWriteRead
{
    internal static class IdService
    {
        private static object _lockObject = new();

        private static int _id = 0;

        public static int GetId
        {
            get
            {
                int id;
                lock (_lockObject)
                {
                    id = _id++;
                }
                return id;
            }
        }
    }
}
