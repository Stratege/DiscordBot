using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace borkbot
{
    public class PersistantList
    {
        List<String> content;
        String filename;
        VirtualServer server;

        public PersistantList(List<String> content, string filename, VirtualServer server)
        {
            this.content = content;
            this.filename = filename;
            this.server = server;
        }

        internal static PersistantList Create(VirtualServer server, string filename)
        {
            var content = server.FileSetup(filename);
            return new PersistantList(content, filename, server);
        }

        public void Add(String elm)
        {
            content.Add(elm);
            persist();
        }

        public bool Remove(String elm)
        {
            var res = content.Remove(elm);
            if(res) persist();
            return res;
        }

        public void AddRange(IEnumerable<String> elm)
        {
            content.AddRange(elm);
        }

        public bool Contains(String elm)
        {
            return content.Contains(elm);
        }

        private void persist()
        {
            server.fileCommand(filename, x => System.IO.File.WriteAllLines(x, content.ToArray()));
        }

        public List<String>.Enumerator GetEnumerator()
        {
            return content.GetEnumerator();
        }
    }
}
