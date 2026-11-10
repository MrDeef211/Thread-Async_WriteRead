using System.Text;
using AsyncWriteRead;

internal class Program
{
    static SemaphoreSlim _varSemaphore = new(1, 1);
    static SemaphoreSlim _readWriteSemaphore = new(1, 1);
    static SemaphoreSlim _writeSemaphore = new(1, 1);

    static int _readersCount = 0;
    static int _leaveCount = 0;
    static bool _writerIsWaiting = false;

    static CancellationTokenSource _cts = new();

    static byte[] _text = new byte[64];
    static int _hash;

    static private string _path = $"C:\\siam\\_BAA\\projects\\ThreadWriteRead\\AsyncWriteRead\\Logs";
    static private string _textFile = "Text.txt";

    private static async Task Main(string[] args)
    {
        int readers = Random.Shared.Next(2, 5);
        int writers = Random.Shared.Next(2, 5);

        List<Task> tasks = new List<Task>();

        Console.WriteLine($"{readers} читателей, {writers} писателей");

        await Task.Delay(100);

        _hash = Validator.GetHash(_text);

        try
        {
            for (int i = 0; i < writers; i++)
            {
                tasks.Add(Task.Run(async () => await WriteLoopAsync())
                    .ContinueWith(t => { if (t.IsFaulted) Console.WriteLine(t.Exception.Message); }));
            }

            for (int i = 0; i < readers; i++)
            {
                tasks.Add(Task.Run(async () => await ReadLoop(readers))
                    .ContinueWith(t => { if (t.IsFaulted) Console.WriteLine(t.Exception.Message); }));
            }

            tasks.Add(Task.Run(() =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    var c = Console.ReadKey();
                    _text[0] = Encoding.UTF8.GetBytes([c.KeyChar]).First();
                }
            }));

            await Task.WhenAll(tasks);
        }
        catch (DataMissmatchExeption ex)
        {
            Console.WriteLine(ex.Message);
        }
        catch (Exception)
        {

        }
        finally
        {
            var sem = Interlocked.Exchange(ref _readWriteSemaphore!, null);
            sem?.Dispose();

            sem = Interlocked.Exchange(ref _writeSemaphore!, null);
            sem?.Dispose();

            sem = Interlocked.Exchange(ref _varSemaphore!, null);
            sem?.Dispose();

            Console.WriteLine("Программа закончила работу...");
        }
    }

    private static async Task WriteLoopAsync()
    {
        int id = IdService.GetId;
        using var fileStream = new StreamWriter(
            Path.Combine(_path, "Writer_" + id + ".txt"), false);

        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await _writeSemaphore.WaitAsync(_cts.Token);

                //Console.WriteLine($"Писатель {id} ждёт читателей");
                using var writeLock = await _readWriteSemaphore.UseWaitAsync(_cts.Token);

                _cts.Token.ThrowIfCancellationRequested();

                //Console.WriteLine($"Писатель {id} начал работу");

                string line = RandomString(64);
                _hash = Validator.GetHash(Encoding.UTF8.GetBytes(line));
                byte[] buffer = Encoding.UTF8.GetBytes(line);
                for (int i = 0; i < 64; i++)
                {
                    _text[i] = buffer[i];
                    await Task.Delay(20);
                }

                fileStream.WriteLine(
                    $"[{DateTime.Now}] Write line: \"{line}\" ");
                //Console.WriteLine(line);

                //Console.WriteLine($"Писатель {id} закончил работу");

                //await Task.Delay(500);

                //Console.Clear();

            }
            catch (DataMissmatchExeption)
            {
                _cts?.Cancel();
                throw;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                Interlocked.Exchange(ref _writerIsWaiting, false);

                // Если таймаутов (TimeoutException) нет, значит читателей в очереди нет
                try
                {
                    using var rwLock = await _readWriteSemaphore.UseWaitAsync(10, _cts.Token);
                    using var varLock = await _varSemaphore.UseWaitAsync(10, _cts.Token);

                    if (_writeSemaphore.CurrentCount == 0 && !_writerIsWaiting)
                    {
                        Console.WriteLine($"Читателей нет в очереди, писатель {id} " +
                            $"передал очередь следующему писателю");
                        Interlocked.Exchange(ref _writerIsWaiting, true);
                        _writeSemaphore.Release();
                    }
                }
                catch (TimeoutException) { }
            }
        }
    }



    private static async Task ReadLoop(int readers)
    {
        int id = IdService.GetId;
        using var fileStream = new StreamWriter(
            Path.Combine(_path, "Reader_" + id + ".txt"), false);

        byte[] readLine = new byte[64];

        while (!_cts.IsCancellationRequested)
        {
            bool didRead = false;
            try
            {
                using (await _varSemaphore.UseWaitAsync(_cts.Token))
                {

                    if (_readersCount == readers) continue;

                    if (_readersCount == 0)
                    {
                        await _readWriteSemaphore.WaitAsync(_cts.Token);
                        //Console.WriteLine("Читатели начинают");
                    }

                    _readersCount++;

                    if (_readersCount == readers 
                        && !_writerIsWaiting 
                        && _writeSemaphore.CurrentCount == 0)
                    {
                        Interlocked.Exchange(ref _writerIsWaiting, true);
                        _writeSemaphore.Release();
                        //Console.WriteLine($"{_readersCount} читателей заканчивают");
                    }
                }

                //Console.WriteLine($"Читатель {id} начал работу");

                for (var i = 0; i < 64; i++)
                {
                    await Task.Delay(20);
                    readLine[i] = _text[i];
                }

                string decodedText = Encoding.UTF8.GetString(readLine);

                await fileStream.WriteLineAsync(
                    $"[{DateTime.Now}] Read line: \"{decodedText}\"");

                if (!Validator.Validate(readLine, _hash))
                {
                    throw new DataMissmatchExeption(
                        "Данные были повреждены при записи");
                }

                didRead = true;
                //Console.WriteLine($"Читатель {id} закончил работу");
            }
            catch (DataMissmatchExeption)
            {
                _cts?.Cancel();
                throw;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if (didRead)
                {
                    using (await _varSemaphore.UseWaitAsync(_cts.Token))
                    {
                        if (++_leaveCount == readers)
                        {
                            _leaveCount = 0;
                            _readersCount = 0;

                            if (_readWriteSemaphore.CurrentCount == 0)
                            {
                                _readWriteSemaphore.Release();
                            }
                        }
                    }
                }
            }
        }
    }

    private static string RandomString(int targetLength)
    {
        const string upperChars = "ABCDEFGHIKJLMNOPQRSTUVWXYZ";
        const string chars = "abcdefghijklmnopqrstuvwxyz";
        StringBuilder sb = new StringBuilder();

        sb.Append(upperChars[Random.Shared.Next(upperChars.Length)]);

        while (sb.Length < targetLength)
        {
            int wordLength = Random.Shared.Next(2, 9);

            for (int i = 0; i < wordLength && sb.Length < targetLength; i++)
            {
                sb.Append(chars[Random.Shared.Next(chars.Length)]);
            }

            if (sb.Length < targetLength - 1)
            {
                sb.Append(' ');
            }
        }

        return sb.ToString();
    }

    private class DataMissmatchExeption : Exception
    {
        public DataMissmatchExeption(string message) : base(message) { }
    }
}