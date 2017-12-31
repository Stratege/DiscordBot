using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace borkbot
{
    class UntaggedUsers : CommandHandler
    {
        public UntaggedUsers(VirtualServer _server) : base(_server)
        {
        }

        public override List<Tuple<string, Command>> getCommands()
        {
            var cmds = new List<Tuple<string, Command>>(2);
            cmds.Add(new Tuple<string, Command>("getuntagged", Command.AdminCommand(server, getUntagged, "getuntagged")));
            cmds.Add(new Tuple<string, Command>("getuserswithtag", Command.AdminCommand(server, getWithTag, "getuserswithtag <role>")));
            return cmds;
        }

        void getWithTag(ServerMessage e, String m)
        {
            var roles = server.getServer().Roles.Where(role => role.Name == m);
            String message = "";
            if (roles.Count() != 1)
                message = "Could not find role: " + m;
            else {
                var role = roles.First();
                var x = server.getServer().Users.Where(u => u.Roles.Contains(role));
                message = "Currently has role " + role.Name + ": ";
                message = SharedCode(e, x, message);
            }
            server.safeSendMessage(e.Channel, message);
        }

        void getUntagged(ServerMessage e, String m)
        {
            var x = server.getServer().Users.Where(u => u.Roles.Count() == 1);
            String message = "Currently untagged: ";
            message = SharedCode(e, x, message);
            server.safeSendMessage(e.Channel, message);
        }

        String SharedCode(ServerMessage e, IEnumerable<SocketGuildUser> x, String message)
        {
            foreach (var u in x)
            {
                message += "\n";
                message += u.Username;
            }
            if (message.Length > 2000)
            {
                server.safeSendMessage(e.Channel, "too many users. Truncating message.");
                message = message.Substring(0, 2000);
            }
            return message;
        }
    }
}
