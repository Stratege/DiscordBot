using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace borkbot
{
    class Roulette : EnableableCommandModule
    {
        Random rnd;
        public Roulette(VirtualServer _server) : base(_server, "roulette")
        {
            rnd = new Random();
        }

        public override List<Tuple<string, Command>> getCommands()
        {
            var cmd = base.getCommands();
            cmd.Add(new Tuple<string, Command>("roulette", new Command(server,roulette, PrivilegeLevel.Everyone, "roulette")));
            return cmd;
        }

        private void roulette(SocketUserMessage e, string m)
        {
            if (on)
            {
                if (rnd.Next(6) == 0)
                {
                    server.safeSendMessage(e.Channel, "BANG!");
                }
                else
                {
                    server.safeSendMessage(e.Channel, "Click!");
                }
            }
        }
    }
}
