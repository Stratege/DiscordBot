/* Contains PersistantValue, a class that stores a singular value with the logic to serialize and deserialize as needed
 */

namespace DiscordBot.Persistance
{
    class PersistantValue<T>
    {
        VirtualServer server;
        string filename;
        T val;
        private PersistantValue(VirtualServer _server, string _filename, T _val)
        {
            server = _server;
            filename = _filename;
            val = _val;
        }

        public static PersistantValue<T> load(VirtualServer _server, string _filename)
        {
            var x = _server.XMLSetup<T>(_filename);
            return new PersistantValue<T>(_server, _filename, x);
        }

        void persist()
        {
            server.XMLSerialization(filename, val);
        }

        public T get() { return val; }
        public void set(T newVal) { val = newVal; persist(); }
    }
}
