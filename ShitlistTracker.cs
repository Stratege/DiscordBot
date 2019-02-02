using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace borkbot
{
    class ShitlistTracker : CommandHandler
    {
        public ShitlistTracker(VirtualServer _server) : base(_server)
        {
        }

        public override List<Command> getCommands()
        {
            var ret = new List<Command>(1);
            ret.Add(Command.AdminCommand(server, "shitlist", shitlist, new HelpMsgStrings("","")));
            return ret;
        }

        void shitlist(ServerMessage e, String m)
        {
            
        }
    }
}
