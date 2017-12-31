using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace borkbot
{
    class KillUserMessages : EnableableCommandModule
    {
        public KillUserMessages(VirtualServer _server) : base(_server, "killmessages")
        {
        }

        public override List<Tuple<string, Command>> getCommands()
        {
            var xs = base.getCommands();
            xs.Add(new Tuple<string, Command>("killusermessages", Command.AdminCommand(server, kill, "killusermessages <usermention>")));
            return xs;
        }

        private void kill(ServerMessage e, string m)
        {
            if (!on)
                return;
            var now = e.msg.Timestamp;
            var fifteenMins = new TimeSpan(0, 15, 0);
            try
            {
                if (Funcs.validateMentionTarget(e, m))
                {
                    foreach (var c in e.Server.TextChannels)
                    {
                        List<ulong> messagesToDelete = new List<ulong>();
                        Console.Write(1);
                        foreach (var mes in c.CachedMessages.Reverse())
                        {
                            Console.Write(2);
                            if (now.Subtract(mes.Timestamp).CompareTo(fifteenMins) >= 0)
                            {
                                break;
                            }
                            if (mes.Author.Mention == m /* || mes.User.NicknameMention == m */)
                            {
                                Console.WriteLine("found message: " + mes.Content);
                                messagesToDelete.Add(mes.Id);
                            }
                        }
                        if (messagesToDelete.Count > 0)
                            c.DeleteMessagesAsync(messagesToDelete.ToArray());
                    }
                }
                else
                {
                    Console.WriteLine("unable to validate: " + m);
                }
            }
            catch (Exception exception)
            {
                server.safeSendMessage(e.Channel, exception.ToString());
            }
        }
    }
}
