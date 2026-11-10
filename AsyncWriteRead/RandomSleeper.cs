namespace AsyncWriteRead
{
    internal static class RandomSleeper
    {
        public static void Sleep(int mult = 1)
        {
            int time = Random.Shared.Next(0, 256);
            Thread.Sleep(time * mult);
        }

        public static void Sleep()
        {
            int time = Random.Shared.Next(0, 10);
            Thread.Sleep(time);
        }
    }
}
