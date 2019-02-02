using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace borkbot
{
    class Echo : EnableableCommandModule
    {
        public Echo(VirtualServer _server) : base(_server, "echo")
        {
        }

        public override List<Command> getCommands()
        {
            var x = base.getCommands();
            x.Add(makeEnableableAdminCommand("echo", echo, new HelpMsgStrings("", "")));
            return x;
        }

        private void echo(ServerMessage e, string m)
        {

            //            if (!e.Channel.IsPrivate)
            //                return;
            string[] split = m.Split(" ".ToArray(),2);
            if (split.Length < 2)
                return;
            var y = server.getServer().TextChannels.Where(chn => chn.Name == split[0]).FirstOrDefault();
            if (y == null)
                return;
            server.safeSendMessage(y, split[1]);
        }
    }
}
