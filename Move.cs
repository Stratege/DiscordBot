/* Module for moving a random selection of users from one voice channel to another
 * 
 * useful for random pulls into a VC
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace borkbot
{
    class Move : CommandHandler
    {
        SocketVoiceChannel vSource;
        SocketVoiceChannel vTarget;
        private static string vSourceFile = "vSource.txt";
        private static string vTargetFile = "vTarget.txt";


        public Move(VirtualServer _server) : base(_server)
        {
            var res = server.FileSetup(vSourceFile);
            if (res.Count > 0)
            {
                vSource = server.getServer().VoiceChannels.FirstOrDefault(x => x.Name == res[0]);
            }
            var res2 = server.FileSetup(vTargetFile);
            if (res2.Count > 0)
            {
                vTarget = server.getServer().VoiceChannels.FirstOrDefault(x => x.Name == res2[0]);
            }
        }

        public override List<Command> getCommands()
        {
            var t = new List<Command>();
            t.Add(Command.AdminCommand(server, "move", move, new HelpMsgStrings("Move n people from source voice channel (currently: " + vSource + ") to target voice channel (currently: " + vTarget + ")","move <n>")));
            t.Add(Command.AdminCommand(server, "originChannel", originchannel, new HelpMsgStrings("Sets the source voice channel for the move command", "originChannel <channel name>")));
            t.Add(Command.AdminCommand(server, "targetChannel", targetchannel, new HelpMsgStrings("Sets the target voice channel for the move command", "targetChannel <channel name>")));
            return t;
        }

        void originchannel(ServerMessage e, String m)
        {
            if (m.Length > 0 && m[0] == '#')
                m = m.Substring(1);
            var res = e.Server.VoiceChannels.FirstOrDefault(x => x.Name == m);
            string message;
            if (res == null)
                message = "Could not find channel: " + m;
            else
            {
                message = "Source channel was set to: " + res.Name;
                vSource = res;
                server.fileCommand(vSourceFile, x => System.IO.File.WriteAllText(x, res.Name));
            }
            server.safeSendMessage(e.Channel, message);
        }

        void targetchannel(ServerMessage e, String m)
        {
            if (m.Length > 0 && m[0] == '#')
                m = m.Substring(1);
            var res = e.Server.VoiceChannels.FirstOrDefault(x => x.Name == m);
            string message;
            if (res == null)
                message = "Could not find channel: " + m;
            else
            {
                message = "Target channel was set to: " + res.Name;
                vTarget = res;
                server.fileCommand(vTargetFile, x => System.IO.File.WriteAllText(x, res.Name));
            }
            server.safeSendMessage(e.Channel, message);
        }

        void moveInternal(ServerMessage e, SocketVoiceChannel source, int moveCount, SocketVoiceChannel target)
        {
            var userCount = source.Users.Count;
            server.safeSendMessage(e.Channel, "users requested: " + moveCount);
            if (userCount < moveCount)
            {
                moveCount = userCount;
            }
            server.safeSendMessage(e.Channel, "users found: " + userCount);
            List<SocketGuildUser> s = new List<SocketGuildUser>();
            var temp = source.Users.ToList();
            var rand = new Random();
            while (s.Count < moveCount)
            {
                var r = rand.Next(temp.Count);
                s.Add(temp[r]);
                temp.RemoveAt(r);
            }
            s.ForEach(x => x.ModifyAsync(y => y.Channel = target));
            var msg = "Moving: ";
            s.ForEach(x => msg += x.Username + " ");
            server.safeSendMessage(e.Channel, msg);
        }
        void movedirect(ServerMessage e, String msg)
        {
            string[] split = msg.Split(" ".ToArray());
            int moveCount = 0;
            if (split[0] == "")
            {//not enough
                server.safeSendMessage(e.Channel, "no parameter passed");
            }
            else if (split.Length < 3)
            {
                server.safeSendMessage(e.Channel, "not enough parameters passed");
            }
            else if (split.Length > 3)
            {//too many
                server.safeSendMessage(e.Channel, "too many values");
            }
            else if (Int32.TryParse(split[1], out moveCount))
            {
                var m = split[0];
                if (m.Length > 0 && m[0] == '#')
                    m = m.Substring(1);
                var source = e.Server.VoiceChannels.FirstOrDefault(x => x.Name == m);
                m = split[2];
                if (m.Length > 0 && m[0] == '#')
                    m = m.Substring(1);
                var target = e.Server.VoiceChannels.FirstOrDefault(x => x.Name == m);
                if (source == null)
                    server.safeSendMessage(e.Channel, "could not find source channel");
                else if (target == null)
                    server.safeSendMessage(e.Channel, "could not find target channel");
                else
                    moveInternal(e, vSource, moveCount, vTarget);
            }
            else
            {
                //not a number
                server.safeSendMessage(e.Channel, "input was not a number");
            }
        }

        void move(ServerMessage e, String m)
        {
            string[] split = m.Split(" ".ToArray());
            int moveCount = 0;
            if (split[0] == "") 
            {//not enough
                server.safeSendMessage(e.Channel, "no parameter passed");
            }
            else if (split.Length > 1)
            {//too many
                server.safeSendMessage(e.Channel, "too many values");
            }
            else if(Int32.TryParse(split[0],out moveCount))
            {
                moveInternal(e,vSource, moveCount, vTarget);
            }
            else
            {
                //not a number
                server.safeSendMessage(e.Channel, "input was not a number");
            }
        }
    }
}
