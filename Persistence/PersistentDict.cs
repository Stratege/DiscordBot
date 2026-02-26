/* Contains PersistentDictionary, a variant on a dictionary with the logic to serialize and deserialize as needed
 */

using System.Collections.Generic;

namespace DiscordBot.Persistence
{
    class PersistentDict<T,K> : Dictionary<T,K>
    {
        VirtualServer server;
        string filename;
        private PersistentDict(VirtualServer _server, string _filename, Dictionary<T,K> d) : base(d)
        {
            server = _server;
            filename = _filename;
        }

        public static PersistentDict<T,K> load(VirtualServer _server, string _filename)
        {
            var x = _server.XMlDictSetup<T, K>(_filename);
            return new PersistentDict<T,K>(_server, _filename,x);
        }

        public void persist()
        {
            server.XMlDictSerialization(filename, this);
        }
    }
}
