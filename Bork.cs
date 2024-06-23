/* Module for automated emoji tagging of messages
 * 
 * tags every few messages (configurable) of configured users with a specific emoji
 * 
 * note: this is the original module and is due for a rewrite TODO
 * */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord;

namespace borkbot
{
    class ObjWrapper<T>
    {
        public T Item1;
        public ObjWrapper(T i) { Item1 = i; }
    }

    class Bork : CommandHandler
    {
        Dictionary<ulong, List<String>> borklist;
        String borkFrequencyPath = "borkfrequency.txt";
        String borklistPath = "borklist.xml";
        int frequency = 1;
        Dictionary<ulong, ObjWrapper<int>> lastBorks = new Dictionary<ulong, ObjWrapper<int>>();

        public Bork(VirtualServer _server) : base(_server)
        {
            String str = String.Join("\n", server.FileSetup(borkFrequencyPath));
            if (!Int32.TryParse(str, out frequency))
                frequency = 1;
            if (frequency < 1)
                frequency = 1;
            borklist = server.XMlDictSetup<ulong, List<String>>(borklistPath);
            server.MessageRecieved += async (s, e) =>
            {
                List<String> localborklist;
                if (borklist.TryGetValue(e.Author.Id, out localborklist))
                {
                    ObjWrapper<int> count;
                    if (!lastBorks.TryGetValue(e.Author.Id, out count))
                    {
                        count = new ObjWrapper<int>(frequency);
                        lastBorks.Add(e.Author.Id, count);
                    }
                    count.Item1 = count.Item1 + 1;
                    if (count.Item1 >= frequency)
                    {
                        foreach (var y in localborklist)
                        {
                            Emote emote;

                            if (Emote.TryParse(y, out emote) && server.getServer().Emotes.Select(em => em.Id).Contains(emote.Id))
                            {
                                await e.msg.AddReactionAsync(emote);
                            }else
                            {
                                //todo: figure out how to remove an emoji after adding it failed?
                                await e.msg.AddReactionAsync(new Emoji(y));
                            }
                        }
                        count.Item1 = 0;
                    }
                }
                return true;
            };
        }

        public override List<Command> getCommands()
        {
            var commands = new List<Command>(3);
            commands.Add(Command.AdminCommand(server,"bork",bork, new HelpMsgStrings("","bork <mention-target> <emoticon> <on/off>")));
            commands.Add(Command.AdminCommand(server, "getborks", getborks, new HelpMsgStrings("", "getborks")));
            commands.Add(Command.AdminCommand(server, "setborkfrequency", setborkfrequency, new HelpMsgStrings("", "setborkfrequency <Integer>")));
            return commands;
        }

        void bork(ServerMessage e, String m)
        {
            var split = m.Split(" ".ToCharArray());
            string message = "Unable to comply with command. \n\n bork <mention-target> <emoticon> <on/off>";
            Console.WriteLine("Bork cmd got: "+m);
            if (split.Length == 3 || split.Length == 2)
            {
                if (Funcs.validateMentionTarget(e, split[0]))
                {
                    Emote emote;
                    IEmote emoji = null;
                    if(Emote.TryParse(split[1], out emote))
                    {
                        emoji = emote;
                    }else
                    {
                        emoji = new Emoji(split[1]);
                        server.safeSendMessage(e.Channel,split[1] + " is not a server emote, optimistically assuming it works anyway.");
                    }
                    if (emoji != null)
                    {
                        var user = Funcs.GetUserByMentionOrName(e.Server.Users, split[0]);
                        if (user == null)
                        {
                            message = "Could not find user: " + m;
                        }
                        else
                        {
                            ulong userId = user.Id;
                            if (split.Length == 2)
                            {
                                List<String> individualBorklist;
                                if(borklist.TryGetValue(userId, out individualBorklist))
                                {
                                    message = "currently borking " + emoji + " at " + split[0];
                                }else
                                {
                                    message = "no such bork: " + emoji + " at " + split[0];
                                }
                            }
                            else if (split[2] == "on")
                            {
                                List<String> individualBorklist;
                                if (!borklist.TryGetValue(userId, out individualBorklist))
                                {
                                    individualBorklist = new List<string>();
                                    borklist.Add(userId, individualBorklist);
                                }
                                if (individualBorklist.Contains(emoji.ToString()) || (emote != null && individualBorklist.Find((ems) => ems.Contains(emote.Id.ToString())) != null))
                                {
                                    message = "Already doing this";
                                }
                                else
                                {
                                    individualBorklist.Add(emoji.ToString());
                                    server.XMlDictSerialization(borklistPath, borklist);
                                    message = "Understood, borking " + emoji + " at " + split[0] + " from now on";
                                }
                            }
                            else if (split[2] == "off")
                            {
                                List<String> individualBorklist;
                                if (!borklist.TryGetValue(userId, out individualBorklist))
                                {
                                    message = "I never did that in the first place";
                                }
                                else
                                {
                                    if (emote != null)
                                    {
                                        var t = individualBorklist.Find((ems) => ems.Contains(emote.Id.ToString()));
                                        if (t != null)
                                        {
                                            individualBorklist.Remove(t);
                                        }
                                    }
                                    else
                                    {
                                        individualBorklist.Remove(emoji.ToString());
                                    }
                                    if (individualBorklist.Count == 0)
                                        borklist.Remove(userId);
                                    server.XMlDictSerialization(borklistPath, borklist);
                                    message = "Understood, no more borking " + emoji + " at " + split[0] + " from now on";
                                }
                            }
                        }
                    }
                    else
                    {
                        message = "Invalid Emoji: " + split[1];
                    }
                }
            }
            server.safeSendMessage(e.Channel,message);
        }

        void getborks(ServerMessage e, String m)
        {
            String message = "Currently borking at:";
            foreach (var x in borklist)
            {
                message += "\n";
                var u = server.getServer().GetUser(x.Key);
                if (u != null)
                {
                    message += u.Username;
                }
                else
                {
                    message += "{unknown user: " + x.Key + "}";
                }
                message += " with ";
                foreach(var y in x.Value)
                {
                    message += "<"+y+">";
                }
            }
            server.safeSendMessage(e.Channel,message);
        }

        void setborkfrequency(ServerMessage e, String m)
        {
            int x;
            String message;
            if(!Int32.TryParse(m, out x))
            {
                message = "could not parse " + m + " as a number.";
            }else
            {
                if (x <= 0)
                    message = "could not set frequency below 1 (attempt was: "+x+")";
                else
                {
                    frequency = x;
                    server.fileCommand(borkFrequencyPath, y => System.IO.File.WriteAllText(y, frequency.ToString()));
                    message = "Borking frequency has been set to once every " + frequency.ToString() + " messages per person.";
                }
            }
            server.safeSendMessage(e.Channel, message);
        }
    }
}
