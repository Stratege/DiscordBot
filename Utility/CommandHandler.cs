/* Base class from which all Modules need to derive
 * */

using System.Collections.Generic;

namespace DiscordBot.Utility
{
    abstract class CommandHandler
    {
        protected VirtualServer server;
        public bool sameServer(VirtualServer vs) { return vs == server; }
        public abstract List<Command> getCommands();
        public CommandHandler(VirtualServer _server)
        {
            server = _server;
        }
    }
}
