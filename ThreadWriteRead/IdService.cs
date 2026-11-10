using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThreadWriteRead
{
    internal static class IdService
    {
        private static int _id = 0;

        public static int GetId {get => _id++;} 
    }
}
