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

        public override List<Tuple<string, Command>> getCommands()
        {
            var x = base.getCommands();
            x.Add(new Tuple<string, Command>("echo", new Command(server,echo, PrivilegeLevel.BotAdmin, "")));
            return x;
        }

        private void echo(SocketUserMessage e, string m)
        {
            if (!on)
                return;

            //            if (!e.Channel.IsPrivate)
            //                return;
            string[] split = m.Split(" ".ToArray(),2);
            if (split.Length < 2)
                return;
            var y = server.getServer().FindChannels(split[0], null, true).FirstOrDefault();
            if (y == null)
                return;
            server.safeSendMessage(y, split[1]);
        }
    }
}
