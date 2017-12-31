using System;
using System.Collections.Generic;
//using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace borkbot
{
    class PersistantDict<T,K> : Dictionary<T,K>
    {
        VirtualServer server;
        String filename;
        private PersistantDict(VirtualServer _server, String _filename, Dictionary<T,K> d) : base(d)
        {
            server = _server;
            filename = _filename;
        }

        public static PersistantDict<T,K> load(VirtualServer _server, String _filename)
        {
            var x = _server.XMlDictSetup<T, K>(_filename);
            return new PersistantDict<T,K>(_server, _filename,x);
        }

        public void persist()
        {
            server.XMlDictSerialization(filename, this);
        }
    }
}
