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

        public override List<Command> getCommands()
        {
            var cmd = new List<Command>(2);
            cmd.Add(Command.AdminCommand(server, "enable" + module_name, enableroll, new HelpMsgStrings("", "enable"+module_name+" <on/off>")));
            return cmd;
        }

        protected Command makeEnableableCommand(string name, Action<ServerMessage,string> cmd, PrivilegeLevel priv, HelpMsgStrings helpmsgstrings)
        {
            Action<ServerMessage, string> f = (x, y) => { if (!on) return; cmd(x, y); };
            return new Command(server, name, f, priv, helpmsgstrings);
        }

        protected Command makeEnableableAdminCommand(string name, Action<ServerMessage, string> cmd, HelpMsgStrings helpmsgstrings)
        {
            Action<ServerMessage, string> f = (x, y) => { if (!on) return; cmd(x, y); };
            return Command.AdminCommand(server, name, f, helpmsgstrings);
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

