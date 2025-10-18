using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace borkbot
{
    class Experience : CommandHandler
    {
        PersistantDict<ulong, int> uidToXP;
        PersistantValue<int> charsForEvent;
        PersistantValue<int> xpPerEvent;
        Dictionary<ulong, int> overflowChars = new Dictionary<ulong, int>(); //not getting logged, but that's a-okay since that's never even an entire xp

        public static List<ulong> enabledServerIds = new List<ulong>() { 1338290337398915097, 857970340385783828 };

        public String excludeComment(String s)
        {
            var depth = 0;
            StringBuilder sb = new StringBuilder();
            while(s.Length > 0)
            {
                if(s.StartsWith("(("))
                {
                    depth++;
                    s = s.Substring(2);
                } else if(s.StartsWith("))") && depth > 0)
                {
                    depth--;
                    s = s.Substring(2);
                } else
                {
                    if(depth == 0)
                    {
                        sb.Append(s[0]);
                    }
                    s = s.Substring(1);
                }
            }
            return sb.ToString();
        }

        public Experience(VirtualServer _server) : base(_server) {
            Console.WriteLine("Checking Experience initializing for server " + _server.getServer().Name + " of Id "+_server.getServer().Id);
            if (!enabledServerIds.Contains(_server.getServer().Id))
            {
                return;
            }
            Console.WriteLine("Experience initalizing for server "+_server.getServer().Name);
            uidToXP = PersistantDict<ulong,int>.load(_server, "uidtoxp");
            charsForEvent = PersistantValue<int>.load(_server, "charsForEvent");
            xpPerEvent = PersistantValue<int>.load(_server, "xpPerEvent");

            Func<VirtualServer,Tuple<ServerMessage,string>,Task<bool>> f = async (VirtualServer s, Tuple<ServerMessage,string> t) =>
            {
                var e = t.Item1;
                var m = t.Item2;
                if(e.isProxy)
                {
                    var filteredMsg = excludeComment(m);
                    var len = filteredMsg.Length;
                    var events = len / charsForEvent.get();
                    var gain = events * xpPerEvent.get();
                    var overflow = len - events * charsForEvent.get();
                    Console.WriteLine("len "+len+",events "+events+",gain "+gain+", overflow " + overflow);
                    if (overflow > 0)
                    {
                        var id = e.Author.Id;
                        if (!overflowChars.ContainsKey(id))
                        {
                            overflowChars[id] = 0;
                        }
                        Console.WriteLine("2");
                        overflowChars[id] += overflow;
                        Console.WriteLine("3");
                        if (overflowChars[id] >= charsForEvent.get())
                        {
                            Console.WriteLine("4");
                            overflowChars[id] -= charsForEvent.get();
                            Console.WriteLine("5");
                            gain += xpPerEvent.get();
                            Console.WriteLine("6");
                        }
                    }
                    Console.WriteLine("gain after overflow " + gain);
                    if(gain > 0)
                    {
                        addXP(e.Author,gain);
                    }
                }
                return true;
            };
            _server.MessageRecieved += f;
        }
        public override List<Command> getCommands()
        {
            if (!enabledServerIds.Contains(server.getServer().Id))
            {
                return new List<Command>();
            }

            var xs = new List<Command>();
            xs.Add(new Command(server, "xp", xp, PrivilegeLevel.Everyone, new HelpMsgStrings("Check, Add or Remove RP XP","xp; xp add <number>; xp remove <number>")));
            xs.Add(new Command(server, "playerxp", playerxp, PrivilegeLevel.BotAdmin, new HelpMsgStrings("Check, Add or Remove a player's RP XP", "playerxp <name> <rest as xp>")));
            xs.Add(new Command(server, "xprate", xprate, PrivilegeLevel.BotAdmin, new HelpMsgStrings("Set RP xprate", "xprate <xp per gain event> <chars for gain event>")));
            return xs;
        }

        private int getXP(SocketGuildUser user)
        {
            var uid = user.Id;
            if (!uidToXP.ContainsKey(uid))
            {
                uidToXP[uid] = 0;
            }
            return uidToXP[uid];
        }

        private void addXP(SocketGuildUser user, int amount)
        {
            Console.WriteLine("adding xp", user.DisplayName, amount);
            var uid = user.Id;
            if (!uidToXP.ContainsKey(uid))
            {
                uidToXP[uid] = 0;
            }
            uidToXP[uid] += amount;
            uidToXP.persist();
        }

        private void removeXP(SocketGuildUser user, int amount)
        {
            var uid = user.Id;
            if (!uidToXP.ContainsKey(uid))
            {
                uidToXP[uid] = 0;
            }
            uidToXP[uid] -= amount;
            uidToXP.persist();
        }

        private async Task xp(ServerMessage e, String msg)
        {
            if(msg == "")
            {
                var xp = getXP(e.Author);
                await server.safeSendMessage(e.Channel, "You have " + xp + " RP XP currently.");
                return;
            }
            var msgSplit = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (msgSplit.Length != 2)
            {
                await server.safeSendMessage(e.Channel, "need exactly 0 or 2 parameters in command");
                return;
            }
            var subCommand = msgSplit[0];
            if(Int32.TryParse(msgSplit[1], out Int32 num))
            {
                if(subCommand == "add")
                {
                    addXP(e.Author, num);
                    var newXP = getXP(e.Author);
                    await server.safeSendMessage(e.Channel, num+" RP XP added. Your new total is " + newXP + " RP XP.");
                } else if (subCommand == "remove")
                {
                    removeXP(e.Author, num);
                    var newXP = getXP(e.Author);
                    await server.safeSendMessage(e.Channel, num+" RP XP removed. Your new total is " + newXP + " RP XP.");
                }
                else
                {
                    await server.safeSendMessage(e.Channel, "first parameter needs to be add or remove");
                    return;
                }
            } else
            {
                await server.safeSendMessage(e.Channel, "second parameter needs to be a number");
                return;
            }
        }

        private async Task playerxp(ServerMessage e, String msg)
        {
            var msgSplit = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (msgSplit.Length == 0)
            {
                await server.safeSendMessage(e.Channel, "needs parameters");
                return;
            }
            var target = Funcs.GetUserByMentionOrName(e.Server.Users, msgSplit[0]);
            if (target == null)
            {
                await server.safeSendMessage(e.Channel, "first parameter needs to be a valid target");
                return;
            }
            if (msgSplit.Length == 1)
            {
                var xp = getXP(target);
                await server.safeSendMessage(e.Channel, target.DisplayName + " has " + xp + " RP XP currently.");
                return;
            }
            if (msgSplit.Length != 3)
            {
                await server.safeSendMessage(e.Channel, "need exactly 1 or 3 parameters in command");
                return;
            }
            //todo: fix copying?
            var subCommand = msgSplit[1];
            if (Int32.TryParse(msgSplit[2], out Int32 num))
            {
                if (subCommand == "add")
                {
                    addXP(target, num);
                    var newXP = getXP(target);
                    await server.safeSendMessage(e.Channel, num+ " RP XP added. "+target.DisplayName+ "'s new total is " + newXP + " RP XP.");
                }
                else if (subCommand == "remove")
                {
                    removeXP(target, num);
                    var newXP = getXP(target);
                    await server.safeSendMessage(e.Channel, num + " RP XP removed. " + target.DisplayName + "'s new total is " + newXP + " RP XP.");
                }
                else
                {
                    await server.safeSendMessage(e.Channel, "second parameter needs to be add or remove");
                    return;
                }
            }
            else
            {
                await server.safeSendMessage(e.Channel, "third parameter needs to be a number");
                return;
            }
        }

        private async Task xprate(ServerMessage e, String msg)
        {
            var msgSplit = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (msgSplit.Length != 2)
            {
                await server.safeSendMessage(e.Channel, "need exactly 2 parameters in command");
                return;
            }
            if (Int32.TryParse(msgSplit[0], out int newXpPerEvent) && Int32.TryParse(msgSplit[1], out int newCharsForEvent))
            {
                xpPerEvent.set(newXpPerEvent);
                charsForEvent.set(newCharsForEvent);
                await server.safeSendMessage(e.Channel, "new rate set to " + xpPerEvent.get() + " RP XP per " + charsForEvent.get() + " characters");
            } else
            {
                await server.safeSendMessage(e.Channel, "both parameters need to be numbers");
                return;
            }

        }
    }
}
