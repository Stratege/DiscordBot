/* A very simple "russian roulette" type module, inspired by old IRC bots
 */
using System;
using System.Collections.Generic;
using DiscordBot.Utility;

namespace DiscordBot.Modules
{
    class Roulette : EnableableCommandModule
    {
        Random rnd;
        public Roulette(VirtualServer _server) : base(_server, "roulette")
        {
            rnd = new Random();
        }

        public override List<Command> getCommands()
        {
            var cmd = base.getCommands();
            cmd.Add(makeEnableableCommand("roulette", roulette, PrivilegeLevel.Everyone, new HelpMsgStrings("", "roulette")));
            return cmd;
        }

        private void roulette(ServerMessage e, string m)
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
