using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace borkbot
{
    class Admingreet : WelcomeMessageCommandHandler
    {
        public Admingreet(VirtualServer _server, AdminMessageChannel amc) : base(_server, "admingreet", new HelpMsgStrings("", "admingreet <on/off> <admingreet message>"))
        {
            if (amc == null || !amc.sameServer(this.server))
                throw new Exception("setup error");
            server.UserJoined += (s, u) =>
            {
                if (on && u.Guild.Id == server.getServer().Id && amc.channel != null)
                {
                    server.safeSendMessage(amc.channel, wmls.Response(u));
                }
            };
        }
    }

    class Adminleave : WelcomeMessageCommandHandler
    {
        public Adminleave(VirtualServer _server, AdminMessageChannel amc) : base(_server, "adminleave", new HelpMsgStrings("", "adminleave <on/off> <adminleave message>"))
        {
            if (amc == null || !amc.sameServer(this.server))
                throw new Exception("setup error");
            server.UserLeft += (s, u) =>
            {
                if (on && amc.channel != null)
                {
                    server.safeSendMessage(amc.channel, wmls.Response(u));
                }
            };
        }
    }


    class AdminMessageChannel : CommandHandler
    {
        public SocketTextChannel channel;
        private static string savefile = "Adminchannel.txt";
        public AdminMessageChannel(VirtualServer _server) : base(_server)
        {
            var res = server.FileSetup(savefile);
            if (res.Count > 0)
            {
                channel = server.getServer().TextChannels.FirstOrDefault(x => x.Name == res[0]);
            }
        }

        public override List<Command> getCommands()
        {
            var cmds = new List<Command>();
            cmds.Add(Command.AdminCommand(server, "setadminchannel", setadminchannel, new HelpMsgStrings("Sets the channel which", "setadminchannel <channel>").addArg("channel - mandatory arg, name of the channel")));
            return cmds;
        }

        private void setadminchannel(ServerMessage e, string m)
        {
            if (m.Length > 0 && m[0] == '#')
                m = m.Substring(1);
            var res = e.Server.TextChannels.FirstOrDefault(x => x.Name == m);
            string message;
            if (res == null)
                message = "Could not find channel: " + m;
            else
            {
                message = "Channel was set to: " + res.Name;
                channel = res;
                server.fileCommand(savefile, x => System.IO.File.WriteAllText(x, res.Name));
            }
            server.safeSendMessage(e.Channel, message);
        }
    }

    class LobbyGreet : WelcomeMessageCommandHandler
    {
        protected SocketTextChannel channel;
        private static string savefile = "Lobbychannel.txt";
        public LobbyGreet(VirtualServer _server) : base(_server, "lobbygreet", new HelpMsgStrings("", "lobbygreet <channel> <on/off> <lobbygreet message>"))
        {
            var res = server.FileSetup(savefile);
            if (res.Count > 0)
            {
                channel = server.getServer().TextChannels.FirstOrDefault(x => x.Name == res[0]);
            }

            server.UserJoined += (s, u) =>
            {
                if (on && u.Guild.Id == server.getServer().Id && channel != null)
                {
                    server.safeSendMessage(channel, wmls.Response(u));
                }
            };
        }
        
        public SocketTextChannel getChannel() { return channel; }

        protected override void cmd(ServerMessage e, string m)
        {
            string message;
            m = m.Trim();
            var m2 = m.Split(" ".ToArray(), 2, StringSplitOptions.RemoveEmptyEntries);
            if (m2.Length < 2)
                message = "Syntaxerror. Correct command: " + helpmsgstring.getFormat();
            else
            {
                var chanName = m2[0];
                SocketTextChannel res;
                if (chanName.Length > 1 && chanName[0] == '<' && chanName[1] == '#')
                {
                    res = e.Server.TextChannels.FirstOrDefault(x => x.Id.ToString() == chanName.Trim("<>#".ToArray()));
                }
                else
                    res = e.Server.TextChannels.FirstOrDefault(x => x.Name == chanName);
                if (res == null)
                    message = "Could not find channel: " + chanName;
                else
                {
                    message = "Channel was set to: " + res.Name;
                    channel = res;
                    server.fileCommand(savefile, x => System.IO.File.WriteAllText(x, res.Name));
                    base.cmd(e, m2[1]);
                }
            }
            server.safeSendMessage(e.Channel, message);
        }
    }
}
