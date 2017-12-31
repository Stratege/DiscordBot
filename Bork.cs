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
            server.MessageRecieved += (s, e) =>
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
//                            try
//                            {

                            //Todo: Rewrite this so that non-server emotes work as well!
                            IEmote emote = server.getServer().Emotes.Where(x => { Console.WriteLine("Emote: " + x + " has Name " + x.Name + " and Url " + x.Url); return x.Name == y; }).FirstOrDefault();
                            if (emote != null)
                                e.msg.AddReactionAsync(emote);
                            else
                            {
                                Console.WriteLine("could not find emote: " + y);
                            }
                                    //bit of a hack to send a special msg
                                    /*                                var x = server.getServer().Client.ClientAPI.Send(new EmojiAddRequest(e.Channel.Id, e.Message.Id, y));
                                                                    x.Wait();
                                    */
/*                                }
                            catch (Exception exception)
                            {
                                Console.WriteLine(exception);
                            }*/
                        }
                        count.Item1 = 0;
                    }
                }
            };
        }

        public override List<Tuple<string, Command>> getCommands()
        {
            var commands = new List<Tuple<string, Command>>(2);
            commands.Add(new Tuple<string, Command>("bork", Command.AdminCommand(server,bork, "bork <mention-target> <emoticon> <on/off>")));
            commands.Add(new Tuple<string, Command>("getborks", Command.AdminCommand(server,getborks, "getborks")));
            commands.Add(new Tuple<string, Command>("setborkfrequency", Command.AdminCommand(server,setborkfrequency, "setborkfrequency <Integer>")));
            return commands;
        }

        void bork(ServerMessage e, String m)
        {
            var split = m.Split(" ".ToCharArray());
            string message = "Unable to comply with command. \n\n bork <mention-target> <emoticon> <on/off>";
            if (split.Length == 3)
            {
                if (Funcs.validateMentionTarget(e, split[0]))
                {
                    string emoji = server.toEmojiString(e, split[1]);
                    if (emoji != null)
                    {
                        ulong userId = e.Server.Users.First(x => x.Mention == split[0] /*|| x.NicknameMention == split[0]*/ || x.Username == split[0] || x.Nickname == split[0]).Id;
                        if (userId == 0)
                        {
                            message = "Could not find user: " + m;
                        }
                        else
                        {
                            if (split[2] == "on")
                            {
                                //                            var x = DC.ClientAPI.Send(new EmojiAddRequest(e.Channel.Id, e.Message.Id, ));
                                //                            x.Wait();
                                List<String> individualBorklist;
                                if (!borklist.TryGetValue(userId, out individualBorklist))
                                {
                                    individualBorklist = new List<string>();
                                    borklist.Add(userId, individualBorklist);
                                }
                                if (individualBorklist.Contains(emoji))
                                {
                                    message = "Already doing this";
                                }
                                else
                                {
                                    individualBorklist.Add(emoji);
                                    server.XMlDictSerialization(borklistPath, borklist);
                                    message = "Understood, borking <" + emoji + "> at " + split[0] + " from now on";
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
                                    individualBorklist.Remove(emoji);
                                    if (individualBorklist.Count == 0)
                                        borklist.Remove(userId);
                                    server.XMlDictSerialization(borklistPath, borklist);
                                    message = "Understood, no more borking <" + emoji + "> at " + split[0] + " from now on";
                                }
                            }
                        }
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
