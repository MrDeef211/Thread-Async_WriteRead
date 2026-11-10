using System.Security.Cryptography;
using System.Text;
using ThreadWriteRead;

internal class Program
{
    static readonly EventWaitHandle _readHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
    static readonly EventWaitHandle _writeHandle = new EventWaitHandle(true, EventResetMode.AutoReset);

    private static CountdownEvent? _countdown;

    static bool _exit = false;

    static string _text = "";
    static int _hash => Validator.GetHash(_text);

    static private string _path = $"C:\\siam\\_BAA\\projects\\ThreadWriteRead\\ThreadWriteRead\\Logs";
    static private string _textFile = "Text.txt";

    private static void Main(string[] args)
    {

        int Readers = Random.Shared.Next(25, 25);
        int Writers = Random.Shared.Next(25, 25);

        List<Thread> writeThreads = new List<Thread>();
        List<Thread> readThreads = new List<Thread>();

        Thread buffThread;

        _countdown = new CountdownEvent(Readers);

        try
        {

            for (int i = 0; i < Writers; i++)
            {
                buffThread = new Thread(WriteLoop);
                writeThreads.Add(buffThread);
                buffThread.Start();
            }

            for (int i = 0; i < Readers; i++)
            {
                buffThread = new Thread(ReadLoop);
                readThreads.Add(buffThread);
                buffThread.Start();
            }

            new Thread(() =>
            {
                Console.ReadKey();
                _exit = true;
            }).Start();


            foreach (Thread thread in writeThreads)
            {
                thread.Join();
            }

            foreach (Thread thread in readThreads)
            {
                thread.Join();
            }
        }
        finally
        {
            _readHandle.Dispose();
            _writeHandle.Dispose();

            File.WriteAllText(Path.Combine(_path, _textFile), _text);

            Console.WriteLine("Программа закончила работу...");
        }
    }

    private static void WriteLoop()
    {
        int id = Thread.CurrentThread.ManagedThreadId;
        using var fileStream = new StreamWriter(Path.Combine(_path, "Writer_" + id + ".txt"), false);
        while (!_exit)
        {
            _writeHandle.WaitOne();
            try
            {
                if (_exit)
                {
                    _writeHandle.Set();
                    break;
                }

                string line = RandomString(64);
                string newText = $"{id} " + line + $". {id}" + "\"\n";
                string expectedText = _text + newText;
                foreach (char c in newText)
                {
                    _text += c;
                    fileStream.Write(c);
                }

                if (!Validator.Validate(expectedText, _hash))
                {
                    throw new AuthenticationTagMismatchException("Данные были повреждены при записи");
                }
            }
            catch (AuthenticationTagMismatchException ex)
            {
                Console.WriteLine(ex.Message);
                _exit = true;
            }
            finally
            {
                _countdown?.Reset();
                _readHandle.Set();
            }
        }
    }

    private static void ReadLoop()
    {
        int id = Thread.CurrentThread.ManagedThreadId;
        using var fileStream = new StreamWriter(Path.Combine(_path, "Reader_" + id + ".txt"), false);
        while (!_exit)
        {
            _readHandle.WaitOne();

            RandomSleeper.Sleep();
            var readText = _text.Split('\n');
            int number = Random.Shared.Next(0, readText.Length - 1);
            string readLine = readText[number];
            fileStream.WriteLine($"{id} [{DateTime.Now}] Read random line: \"{readLine}\" {id}");

            _readHandle.Reset();

            if (_countdown?.Signal() ?? false)
            {
                _writeHandle.Set();
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
}