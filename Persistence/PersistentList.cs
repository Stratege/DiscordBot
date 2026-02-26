/* Contains PersistentList, a variant on a List with the logic to serialize and deserialize as needed
 */
using System.Collections;
using System.Collections.Generic;

namespace DiscordBot.Persistence
{
    public class PersistentList : IEnumerable<string>
    {
        List<string> content;
        string filename;
        VirtualServer server;

        public PersistentList(List<string> content, string filename, VirtualServer server)
        {
            this.content = content;
            this.filename = filename;
            this.server = server;
        }

        internal static PersistentList Create(VirtualServer server, string filename)
        {
            var content = server.FileSetup(filename);
            return new PersistentList(content, filename, server);
        }

        public void Add(string elm)
        {
            content.Add(elm);
            persist();
        }

        public bool Remove(string elm)
        {
            var res = content.Remove(elm);
            if(res) persist();
            return res;
        }

        public void AddRange(IEnumerable<string> elm)
        {
            content.AddRange(elm);
        }

        public bool Contains(string elm)
        {
            return content.Contains(elm);
        }

        private void persist()
        {
            server.fileCommand(filename, x => System.IO.File.WriteAllLines(x, content.ToArray()));
        }

        public List<string>.Enumerator GetEnumerator()
        {
            return content.GetEnumerator();
        }

        IEnumerator<string> IEnumerable<string>.GetEnumerator()
        {
            return content.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
