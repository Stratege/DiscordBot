/* Contains PersistentValue, a class that stores a singular value with the logic to serialize and deserialize as needed
 */

namespace DiscordBot.Persistence
{
    class PersistentValue<T>
    {
        VirtualServer server;
        string filename;
        T val;
        private PersistentValue(VirtualServer _server, string _filename, T _val)
        {
            server = _server;
            filename = _filename;
            val = _val;
        }

        public static PersistentValue<T> load(VirtualServer _server, string _filename)
        {
            var x = _server.XMLSetup<T>(_filename);
            return new PersistentValue<T>(_server, _filename, x);
        }

        void persist()
        {
            server.XMLSerialization(filename, val);
        }

        public T get() { return val; }
        public void set(T newVal) { val = newVal; persist(); }
    }
}
