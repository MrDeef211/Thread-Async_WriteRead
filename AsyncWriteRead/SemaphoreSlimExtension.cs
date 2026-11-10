namespace AsyncWriteRead;

public static class SemaphoreSlimExtensions
{

    public static async Task<IDisposable> UseWaitAsync(
        this SemaphoreSlim semaphore,
        int millisecondsTimeout)
    {
        return (await semaphore.WaitAsync(millisecondsTimeout).ConfigureAwait(false)) ?
            new ReleaseWrapper(semaphore) : throw new TimeoutException();
    }

    public static async Task<IDisposable> UseWaitAsync(
        this SemaphoreSlim semaphore,
        int millisecondsTimeout, CancellationToken ct)
    {
        return (await semaphore.WaitAsync(millisecondsTimeout, ct).ConfigureAwait(false)) ?
            new ReleaseWrapper(semaphore) : throw new TimeoutException();
    }

    public static async Task<IDisposable> UseWaitAsync(
        this SemaphoreSlim semaphore,
        CancellationToken cancelToken = default)
    {
        await semaphore.WaitAsync(cancelToken).ConfigureAwait(false);
        return new ReleaseWrapper(semaphore);
    }



    private class ReleaseWrapper : IDisposable
    {
        public ReleaseWrapper(SemaphoreSlim semaphore)

        {
            _sem = semaphore;
        }

        public void Dispose()
        {
            _sem?.Release();
        }

        private SemaphoreSlim? _sem;
    }
}
