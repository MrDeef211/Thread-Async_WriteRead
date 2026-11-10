using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreadWriteRead
{
    internal class Writer
    {
        private string _path => $"C:\\siam\\_BAA\\projects\\ThreadWriteRead\\ThreadWriteRead\\Logs\\{Name}.txt";

        public string Name => $"Writer_{_localId}";

        public int Id { get;} = IdService.GetId;

        private int _localId = LocalIdCounter++;

        private static int LocalIdCounter = 0;

        public Writer() 
        {
            using (File.Create(_path)) { }
        }

        public void Write(ref string text, string line)
        {
            string newText = $"{Id} " + line + $" {Id}";
            foreach (char c in newText)
            {
                RandomSleeper.Sleep();
                text += c;
                Console.Write(c);
            }
            text += "\n";
            Console.WriteLine();

            //text += newText + "\n";
            //Console.WriteLine($"<{Id} [{DateTime.Now}] New line: \"{newText}\" {Id}>");
            //Serializer.WriteLine(_path, $"<{Id} [{DateTime.Now}] New line: \"{newText}\" {Id}>");
        }
    }
}
