using System;
using System.Collections.Generic;
using System.Text;

namespace borkbot
{
    class PersistantValue<T>
    {
        VirtualServer server;
        String filename;
        T val;
        private PersistantValue(VirtualServer _server, String _filename, T _val)
        {
            server = _server;
            filename = _filename;
            val = _val;
        }

        public static PersistantValue<T> load(VirtualServer _server, String _filename)
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
