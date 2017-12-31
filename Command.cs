using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;


namespace borkbot
{
    enum PrivilegeLevel { BotAdmin, Everyone };

    class Command
    {
        VirtualServer server;
        PrivilegeLevel priv;
        Action<ServerMessage, string> cmd;
        public string syntaxmessage;

        public Command(VirtualServer _server, Action<ServerMessage, string> _cmd, PrivilegeLevel _priv, string _syntaxmessage)
        {
            server = _server;
            syntaxmessage = _syntaxmessage;
            priv = _priv;
            cmd = _cmd;
        }

        public void invoke(ServerMessage e, string m)
        {
            if (checkPrivilege(e.Author, e.Channel))
            {
                Console.WriteLine("Invoking " + m);
                cmd(e, m);
            }
            else
                Console.WriteLine(e.Author.Username + " has insufficient privileges for " + m);

        }

        private bool checkPrivilege(SocketUser u, ISocketMessageChannel c)
        {
            return (priv == PrivilegeLevel.BotAdmin && server.isAdmin(u,c)) || priv == PrivilegeLevel.Everyone;
        }

        public static Command AdminCommand(VirtualServer _server, Action<ServerMessage, String> _cmd, string _syntaxmessage)
        {
            return new Command(_server,_cmd, PrivilegeLevel.BotAdmin,_syntaxmessage);
        }
    }
}
