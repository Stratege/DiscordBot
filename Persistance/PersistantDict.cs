/* Contains PersistantDictionary, a variant on a dictionary with the logic to serialize and deserialize as needed
 */

using System.Collections.Generic;

namespace DiscordBot.Persistance
{
    class PersistantDict<T,K> : Dictionary<T,K>
    {
        VirtualServer server;
        string filename;
        private PersistantDict(VirtualServer _server, string _filename, Dictionary<T,K> d) : base(d)
        {
            server = _server;
            filename = _filename;
        }

        public static PersistantDict<T,K> load(VirtualServer _server, string _filename)
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
