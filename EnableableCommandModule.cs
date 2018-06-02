using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace borkbot
{
    class EnableableCommandModule : CommandHandler
    {
        protected bool on = false;
        string module_name;
        string filesuffix = "enableStatus.txt";
        public EnableableCommandModule(VirtualServer _server, string _module_name) : base(_server)
        {
            module_name = _module_name;
            string statusString = String.Join("\n", server.FileSetup(module_name + filesuffix));
            if (statusString == "on")
            {
                on = true;
            }
        }

        public override List<Tuple<string, Command>> getCommands()
        {
            var cmd = new List<Tuple<string, Command>>(2);
            cmd.Add(new Tuple<string, Command>("enable"+module_name, Command.AdminCommand(server,enableroll, "enable"+module_name+" <on/off>")));
            return cmd;
        }

        protected Command makeEnableableCommand(Action<ServerMessage,string> cmd, PrivilegeLevel priv, string syntaxmsg)
        {
            Action<ServerMessage, string> f = (x, y) => { if (!on) return; cmd(x, y); };
            return new Command(server, f, priv, syntaxmsg);
        }

        protected Command makeEnableableAdminCommand(Action<ServerMessage, string> cmd, string syntaxmsg)
        {
            Action<ServerMessage, string> f = (x, y) => { if (!on) return; cmd(x, y); };
            return Command.AdminCommand(server, f, syntaxmsg);
        }

        private void enableroll(ServerMessage e, string m)
        {
            if (m == "on")
            {
                if(on)
                {
                    server.safeSendMessage(e.Channel, module_name + " was already enabled.");
                }
                else
                {
                    on = true;
                    server.safeSendMessage(e.Channel, "Enabled " + module_name);
                    persistState();
                }
            }
            else
            {
                if (on)
                {
                    on = false;
                    server.safeSendMessage(e.Channel, "Disabled " + module_name);
                    persistState();
                }
                else
                {
                    server.safeSendMessage(e.Channel, module_name + " was already disabled.");
                }
            }
        }

        private void persistState()
        {
            server.fileCommand(module_name + filesuffix, x => System.IO.File.WriteAllText(x,(on ? "on" : "off")));
        }

    }
}

