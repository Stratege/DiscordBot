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

        public override List<Tuple<string, Command>> getCommands()
        {
            var ret = new List<Tuple<string, Command>>(1);
            ret.Add(new Tuple<string, Command>("shitlist", Command.AdminCommand(server, shitlist, "")));
            return ret;
        }

        void shitlist(ServerMessage e, String m)
        {
            
        }
    }
}
