using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace borkbot
{
    abstract class CommandHandler
    {
        protected VirtualServer server;
        public abstract List<Tuple<String, Command>> getCommands();
        public CommandHandler(VirtualServer _server)
        {
            server = _server;
        }
    }
}
