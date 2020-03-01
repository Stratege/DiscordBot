using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace borkbot
{
    class Someone : CommandHandler
    {
        Random rnd;
        public Someone(VirtualServer _server) : base(_server)
        {
            rnd = new Random();
        }

        public override List<Command> getCommands()
        {
            var ls = new List<Command>();
            ls.Add(Command.AdminCommand(server, "someone", someone, new HelpMsgStrings("Pings someone in this channel", "!someone")));
            return ls;
        }

        async private void someone(ServerMessage arg1, string arg2)
        {
            var res = await arg1.Channel.GetUsersAsync().ToList();
            var res2 = res.SelectMany(x => x).Where(x => !x.IsBot).ToList();
            var u = res2[rnd.Next(res2.Count)];
            server.safeSendMessage(arg1.Channel, u.Mention);
        }
    }
}
