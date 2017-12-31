using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace borkbot
{
    class Dice : EnableableCommandModule
    {
        Random rnd;
        public Dice(VirtualServer _server) : base(_server,"roll")
        {
            rnd = new Random();
        }

        public override List<Tuple<string, Command>> getCommands()
        {
            var cmd = base.getCommands();
            cmd.Add(new Tuple<string, Command>("roll", new Command(server,roll,PrivilegeLevel.Everyone,"roll XdY")));
            return cmd;
        }

        private void roll(SocketUserMessage e, string m)
        {
            if (on)
            {
                var split = m.Split("d".ToArray(), 2);
                int diceNum;
                int diceSides;
                if (split.Length == 2 && int.TryParse(split[0], out diceNum) && int.TryParse(split[1], out diceSides))
                {
                    var res = roll(rnd, diceNum, diceSides);
                    string message = m + " = " + res.Item1 + " (" + res.Item2 + ")";

                    server.safeSendMessage(e.Channel, message);
                }
            }
        }

        public static Tuple<int,string> roll(Random rnd, int diceNum, int diceSides)
        {
            if(diceNum > 0 && diceNum < 30 && diceSides > 0 && diceSides < 1000)
            {
                string message2 = "";
                int totalRes = 0;
                for (int i = 0; i < diceNum; i++)
                {
                    var res = rnd.Next(1, diceSides + 1);
                    totalRes += res;
                    message2 += res.ToString();
                    if (i < diceNum - 1)
                        message2 += " + ";
                }
                return new Tuple<int, string>(totalRes, message2);
            }
            return null;
        }
    }
}
