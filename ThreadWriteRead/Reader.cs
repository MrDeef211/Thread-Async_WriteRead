using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace ThreadWriteRead
{
    internal class Reader
    {
        private string _path => $"C:\\siam\\_BAA\\projects\\ThreadWriteRead\\ThreadWriteRead\\Logs\\{Name}.txt";

        public string Name => $"Reader_{_localId}";

        public int Id { get; } = IdService.GetId;

        private int _localId = LocalIdCounter++;

        private static int LocalIdCounter = 0;

        public Reader()
        {
            
        }

        public void Read(ref string text)
        {
            RandomSleeper.Sleep(2);
            var readText = text.Split('\n');
            int number = Random.Shared.Next(0, readText.Length - 1);
            string readLine = readText[number];
            Console.WriteLine($"<{Id} [{DateTime.Now}] Read random line: \"{readLine}\" {Id}>");


            //Serializer.WriteLine(_path, $"<{Id} [{DateTime.Now}] Read random line: \"{readLine}\" {Id}>");
        }
    }
}
