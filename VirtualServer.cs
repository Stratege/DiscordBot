/* Primary Interface of the Bot.
 *
 * Abstracts and Insulates all interactions to be contained to a specific discord server
 * handles authentification of messages, parsing into commands and calling the correct command handler
 * - to add new commands add the module in the constructor
 * - to send messages use safeSendMessage or safeSendEmbed
 * - to read/store data use filecommand
 * as long as those 3 practices are being followed any module added will be properly insulated
 * */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using System.IO;
using System.Xml.Serialization;
using System.Threading;
using Discord;
using Discord.Rest;

namespace borkbot
{
    public class ServerMessage
    {
        public SocketGuild Server;
        public bool isDM;
        public bool isProxy;
        public ISocketMessageChannel Channel; //guild text channel or DM channel
        public SocketUserMessage msg;
        public SocketGuildUser Author;
        public ServerMessage(SocketGuild _Server, bool _isDM, bool _isProxy, ISocketMessageChannel _Channel, SocketUserMessage _msg, SocketGuildUser _Author)
        {   
            Server = _Server;
            isDM = _isDM;
            isProxy = _isProxy;
            Channel = _Channel;
            msg = _msg;
            Author = _Author;
        }
    }

    public class VirtualServer
    {
        internal Dictionary<String, Command> Commandlist;
        SocketGuild server;
        public DiscordSocketClient DC;
        public PersistantList Admins;
        String adminfilePath = "admins.txt";
        string serverpath;
        AlternativeCommand altCommand;

        public VirtualServer(DiscordSocketClient _DC, SocketGuild _server)
        {
            try
            {
                serverpath = _server.Id + "/";
                if (!Directory.Exists(serverpath))
                    Directory.CreateDirectory(serverpath);
                DC = _DC;
                server = _server;
                Commandlist = new Dictionary<string, Command>();

                var virtualServerCommands = new List<Command>(4);
                virtualServerCommands.Add(new Command(this, "help", help, PrivilegeLevel.Everyone, new HelpMsgStrings("", ""))); //help is very very special
                virtualServerCommands.Add(new Command(this, "shutdown", shutdown, PrivilegeLevel.BotAdmin, new HelpMsgStrings("", "shutdown <on/off>")));
                virtualServerCommands.Add(new Command(this, "addbotadmin", addBotAdmin, PrivilegeLevel.BotAdmin, new HelpMsgStrings("", "addbotadmin <mention-target>")));
                virtualServerCommands.Add(new Command(this, "removebotadmin", removeBotAdmin, PrivilegeLevel.BotAdmin, new HelpMsgStrings("", "removebotadmin <mention-target>")));
                addCommands(virtualServerCommands);
                Admins = PersistantList.Create(this,adminfilePath);
                var adminChannel = new AdminMessageChannel(this);
                addCommands(adminChannel.getCommands());
                addCommands(new Admingreet(this, adminChannel).getCommands());
                addCommands(new Adminleave(this, adminChannel).getCommands());
                addCommands(new Autogreet(this).getCommands());
                var lobbyGreet = new LobbyGreet(this);
                addCommands(lobbyGreet.getCommands());
                addCommands(new Dice(this).getCommands());
                addCommands(new Roulette(this).getCommands());
                addCommands(new Echo(this).getCommands());
                addCommands(new Math(this).getCommands());
                addCommands(new Bork(this).getCommands());
                addCommands(new KillUserMessages(this).getCommands());
                addCommands(new UntaggedUsers(this).getCommands());
                addCommands(new Reminder(this).getCommands());
                addCommands(new RunQuery(this).getCommands());
                altCommand = new AlternativeCommand(this);
                addCommands(altCommand.getCommands());
                addCommands(new GloriousDirectDemocracy(this).getCommands());
                addCommands(new RoleCommand(this, lobbyGreet).getCommands());
                addCommands(new StrikeModule(this).getCommands());
                addCommands(new EmoteModule(this).getCommands());
                addCommands(new ReportModule(this).getCommands());
//                addCommands(new Inktober(this).getCommands());
                addCommands(new Userinfo(this).getCommands());
                addCommands(new Someone(this).getCommands());
                addCommands(new Move(this).getCommands());
                addCommands(new Store(this).getCommands());
                addCommands(new Proxy(this).getCommands());
                addCommands(new Scryfall(this).getCommands());
                addCommands(new Experience(this).getCommands());
                
                var temp = new List<Command>();
                Action<ServerMessage, string> h = (x, y) =>
                {
                    String[] split = y.Split(new String[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    Console.WriteLine("invoking " + split.Length + " commands");
                    foreach(string s in split)
                    {
                        var res = parseMessageString(s);
                        Commandlist[res.Item1].invoke(x, res.Item2);
                    }
                    Console.WriteLine("invocation complete");
                };
                temp.Add(Command.AdminCommand(this, "multicommand", h, new HelpMsgStrings("", "")));
                addCommands(temp);
            }
            catch (Exception e)
            {
                Console.WriteLine("something went wrong setting up the server: " + e);
            }

            Console.WriteLine("Connected with server: " + server.Name);
        }



        public void updateServer(SocketGuild newS)
        {
            if (newS.Id != server.Id)
            {
                Console.WriteLine("Tried to reassign server but different Id: " + newS.Name + " and " + server.Id);
                return;
            }
            server = newS;
        }

        void addBotAdmin(ServerMessage e, String m)
        {
            adminAbstract(e, m, (mention, id, user) => {
                if (Admins.Contains(id.ToString()))
                    return user.Username + " is already an admin.";
                else
                {
                    Admins.Add(id.ToString());
/*                    using (var f = fileCommand(adminfilePath, System.IO.File.AppendText))
                    {
                        f.WriteLine(id.ToString());
                    }*/
                    return user.Username + " has successfully been added as an admin.";
                }
            });
        }

        void removeBotAdmin(ServerMessage e, String m)
        {
            adminAbstract(e, m, (mention, id, user) =>
            {
                if (!Admins.Contains(id.ToString()))
                    return user.Username + " is not an admin.";
                else
                {
                    Admins.Remove(id.ToString());
//                    fileCommand(adminfilePath, x => System.IO.File.WriteAllLines(x,Admins.ToArray()));
                    return user.Username + " has successfully been removed from the admin list.";
                }
            });
        }

        void adminAbstract(ServerMessage e, String m, Func<String, ulong, SocketUser, String> f)
        {
            var users = e.msg.MentionedUsers.Where(x => !x.IsBot).Select(x => Tuple.Create(x.Mention, x.Id, x)).ToList();
            var message = "";
            if (!Funcs.validateMentionTarget(e, m))
                message = "Unable to comply with command. \n\n"; //+ botInfo;
            else
            {
                var m2 = f(users[0].Item1, users[0].Item2, users[0].Item3);
                if (m2 != "")
                    message = m2;
            }
            safeSendMessage(e.Channel, message);
        }

        void addCommands(List<Command> cmdls)
        {
            foreach (var cmd in cmdls)
            {
                try
                {
                    Commandlist.Add(cmd.name.ToLower(), cmd);
//                    botInfo += cmd.Item2.syntaxmessage + "\n";
                }
                catch (Exception e)
                {
                    Console.WriteLine("Tried to add " + cmd.name + " but failed.");
                    Console.WriteLine(e);
                }
            }
        }


        bool isShutdown = false;
        internal AsyncEventHandler<VirtualServer,SocketGuildUser> UserJoined = new AsyncEventHandler<VirtualServer, SocketGuildUser>();
        internal AsyncEventHandler<VirtualServer,Tuple<ServerMessage,string>,bool> MessageRecieved = new AsyncEventHandler<VirtualServer, Tuple<ServerMessage,string>, bool>();
        internal AsyncEventHandler<VirtualServer, Tuple<SocketGuild, SocketUser>> UserLeft = new AsyncEventHandler<VirtualServer, Tuple<SocketGuild, SocketUser>>();
        internal AsyncEventHandler<VirtualServer, Tuple<SocketGuildUser, SocketGuildUser>> UserUpdated = new AsyncEventHandler<VirtualServer, Tuple<SocketGuildUser, SocketGuildUser>>();
        internal AsyncEventHandler<VirtualServer, SocketReaction> ReactionAdded = new AsyncEventHandler<VirtualServer, SocketReaction>();
        internal AsyncEventHandler<VirtualServer, SocketReaction> ReactionRemoved = new AsyncEventHandler<VirtualServer, SocketReaction>();
        internal AsyncEventHandler<VirtualServer, Tuple<SocketRole, SocketRole>> RoleUpdated = new AsyncEventHandler<VirtualServer, Tuple<SocketRole, SocketRole>>();
        internal AsyncEventHandler<VirtualServer, SocketRole> RoleDeleted = new AsyncEventHandler<VirtualServer, SocketRole>();
        internal AsyncEventHandler<VirtualServer, SocketThreadChannel> ThreadCreated = new AsyncEventHandler<VirtualServer, SocketThreadChannel>();
        internal AsyncEventHandler<VirtualServer, SocketGuildChannel> ChannelCreated = new AsyncEventHandler<VirtualServer, SocketGuildChannel>();

        public async Task reactionAdded(SocketReaction reaction)
        {
            await ReactionAdded.Invoke(this, reaction);
        }

        public async Task reactionRemoved(SocketReaction reaction)
        {
            await ReactionRemoved.Invoke(this, reaction);
        }

        public async Task userJoined(SocketGuildUser e)
        {
            await UserJoined.Invoke(this, e);
        }

        public async Task channelCreated(SocketGuildChannel sgc)
        {
            await ChannelCreated.Invoke(this, sgc);
        }

        internal async Task userLeft(SocketGuild guild, SocketUser user)
        {
            await UserLeft.Invoke(this, Tuple.Create(guild, user));
        }

        internal async Task userUpdated(SocketGuildUser oldUser, SocketGuildUser newUser)
        {
            await UserUpdated.Invoke(this, Tuple.Create(oldUser,newUser));
        }

        internal async Task roleUpdated(SocketRole oldRole, SocketRole newRole)
        {
            await RoleUpdated.Invoke(this, Tuple.Create(oldRole,newRole));
        }

        internal async Task roleDeleted(SocketRole role)
        {
            await RoleDeleted.Invoke(this, role);
        }

        //Todo: Create unified notion of channel settings
        internal async Task threadCreated(SocketThreadChannel stc)
        {
            await ThreadCreated.Invoke(this, stc);
        }

        async Task shutdown(ServerMessage e, String m)
        {
            isShutdown = !isShutdown;
            if (isShutdown)
                await safeSendMessage(e.Channel, "Shutting down.");
            else
                await safeSendMessage(e.Channel, "Back online.");
        }


        bool shouldDisplayHelp(Command com, SocketGuildUser user, ISocketMessageChannel c)
        {
            return com.checkPrivilege(user, c) && com.helpmessage.getFormat() != "";
        }

        string getCommandListForUser(SocketGuildUser user, ISocketMessageChannel c)
        {
            var AvailableComs = Commandlist.Where(x => shouldDisplayHelp(x.Value,user,c)).SelectMany(x => x.Key + ", ");
            var k = AvailableComs.Take(AvailableComs.Count() - 2).ToArray();
            return new string(k);
        }

        async Task help(ServerMessage e, String m)
        {
            /*            var eb = new EmbedBuilder();
                        eb = eb.WithAuthor("The Overbork", this.DC.CurrentUser.GetAvatarUrl()).WithCurrentTimestamp().WithTitle("Help");
                        const int maxFieldSize = 1024;
                        for(int i = 0; i < ((botInfo.Length + (maxFieldSize-1)) / maxFieldSize); i++)
                        {
                            int remLen = botInfo.Length - i * maxFieldSize;
                            int len = remLen < maxFieldSize ? remLen : maxFieldSize;
                            eb = eb.AddField("help" + i, botInfo.Substring(i * maxFieldSize, len));
                        }
                        safeSendEmbed(e.Channel, eb.Build());*/
            /*            safeSendMessage(e.Channel,
            "Standing by for orders, " + e.Author.Username + @"!

            " + botInfo, true);*/
            var parts = m.Split(" ".ToArray(), StringSplitOptions.RemoveEmptyEntries);
            Command com;
            if (parts.Length > 0 && Commandlist.TryGetValue(parts[0],out com) && shouldDisplayHelp(com,e.Author,e.Channel))
            {
                await safeSendEmbed(e.Channel, com.getHelpMessageEmbed());
            }else
            {
                var eb = new Discord.EmbedBuilder().WithAuthor("The Overbork", this.DC.CurrentUser.GetAvatarUrl()).WithCurrentTimestamp();
                eb.WithTitle("Available commands").WithDescription("This is a list of all commands currently available to you. For help with a particular command, try !help followed by that command name. ```" + getCommandListForUser(e.Author, e.Channel) + "```");
                await safeSendEmbed(e.Channel, eb.Build());
            }
        }

        internal SocketGuild getServer()
        {
            return server;
        }

        async public Task<RestUserMessage> safeSendEmbed(IMessageChannel c, Embed embed)
        {
            return await safeSendMessage(c, "", false, embed);
        }

        async public Task<RestUserMessage> safeSendMessage(IMessageChannel c, string m, bool splitMessage = false, Embed embed = null)
        {
            if(c == null)
            {
                Console.WriteLine("tried to send to no channel at all: " + m);
                return null;
            }
            /*
            if(c.Server == null)
            {
                if(!c.IsPrivate)
                {
                    Console.WriteLine("tried to send a non-private message to no server at all: " + c + " - " + m);
                    return;
                }
            }
            */
            if(server == null)
            {
                Console.WriteLine("Virtualserver's server was null at the time of sending! " + m);
                return null;
            }
            SocketTextChannel stc = c as SocketTextChannel;
            if (stc != null && stc.Guild.Id != server.Id)
            {
                Console.WriteLine("something tried to send an illegal message: " + m);
                return null;
            }
            if(!splitMessage && m.Length >= 2000)
            {
                return await stc.SendMessageAsync("Response too large - aborting");
            }
            while(splitMessage && m.Length >= 2000)
            {
                await safeSendMessage(c, m.Substring(0, 1999), false);
                m = m.Substring(1999);
            }
            try
            {
                if (stc != null)
                {
                    return await stc.SendMessageAsync(m, false, embed);
                }else if(c.GetType() == typeof(SocketDMChannel))
                {
                    return await ((SocketDMChannel)c).SendMessageAsync(m,false, embed);
                }
                else if (c is RestDMChannel)
                {
                    return await ((RestDMChannel)c).SendMessageAsync(m,false, embed);
                }
                else
                {
                    Console.WriteLine("safeSendMessage was unable to deal with passed in msg of type: " + c.GetType());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return null;
        }

        bool threadResyncDone = false;

        async Task resyncThreads()
        {
            foreach(var c in server.TextChannels)
            {
                foreach(var t in c.Threads)
                {
                    if(!t.HasJoined)
                    {
                        await t.JoinAsync();
                        await ThreadCreated.Invoke(this,t);
                    }
                }
            }
        }

        public async Task messageRecieved(ServerMessage e)
        {
            if(!threadResyncDone)
            {
                threadResyncDone = true;
                await resyncThreads();
            }

            if (e.Author == null)
                return;

            var msgContent = e.msg.Content;
            var gc = e.Channel as SocketTextChannel;
            if (/*!e.Author.GuildPermissions.MentionEveryone && */ gc != null && !e.Author.GetPermissions(gc).MentionEveryone)
                msgContent = msgContent.Replace("@everyone", "ATeveryone").Replace("@here", "AThere");


            //call all our things that listen to recieved messages directly
            var m = MessageRecieved.Fold<Task<bool>>((x, f) => {
                return async (o, t) => {
                    var b = await x(o, t);
                    if (!b) return b;
                    return await f(o, t);
                };
            }, (o, t) => Task.FromResult(true));
            var shouldContinue = await m(this, Tuple.Create(e,msgContent));
            if (!shouldContinue) return; //something has handled the msg in a way that says we should not handle it via command handler

            if (!e.Author.IsWebhook && !e.Author.IsBot && (e.msg.MentionedUsers.Count(x => x.Id == DC.CurrentUser.Id) > 0 || (altCommand.isOn && msgContent.StartsWith(altCommand.alternativeSyntax))))
            {
                var res = parseMessage(msgContent);
                if (res != null)
                    Console.WriteLine(res.Item1 + " - " + res.Item2);
                try
                {
                    if (res != null)
                    {
                        var payload = res.Item2;


                        await Commandlist[res.Item1].invoke(e, payload);
                    }
                }
                catch (Exception exp)
                {
                    Console.WriteLine(exp);
                }
            }
        }

        public bool isAdmin(SocketUser user, IChannel channel)
        {
            var type = channel.GetType();
            if (type == typeof(SocketDMChannel)) {
                // nothing special needs to be done
            }else if (type == typeof(SocketTextChannel) || type == typeof(SocketVoiceChannel) || type == typeof(SocketForumChannel)) {
                var stc = (SocketGuildChannel)channel;
                if (stc.Guild == null || server == null)
                {
                    Console.WriteLine("isAdmin check with one server being null!");
                    return false;
                }
                if (stc.Guild.Id != server.Id)
                    return false;
            }else if(type == typeof(SocketThreadChannel)) {
                var stc = (SocketThreadChannel)channel;
                return isAdmin(user, stc.ParentChannel);
            }else{
                Console.WriteLine("Got isAdmin check on channel type " + type + " this is atm unhandled");
                return false;
            }

            return ((server.GetUser(user.Id).GuildPermissions.Administrator || Admins.Contains(user.Id.ToString())));
        }

        Tuple<String, String> parseMessage(String m)
        {
            Console.WriteLine("Parsing: " + m);
            String raw = m;//m.RawText;
            return parseMessageString(raw);
        }

        internal Tuple<String, String> parseMessageString(String raw)
        {
            string[] split;
            string command;
            string payload;
            var nickmention = DC.CurrentUser.Mention;
            var nonnickmention = nickmention.Substring(0, 2) + nickmention.Substring(3);
            if (raw.Trim().StartsWith(nickmention) || raw.Trim().StartsWith(nonnickmention))
            {
                split = raw.Split(" \n  ".ToCharArray(), 3, StringSplitOptions.RemoveEmptyEntries);
                if(split.Length >= 2)
                {
                    command = split[1].ToLower();
                    payload = split.Length == 3 ? split[2] : "";
                }
                else
                {
                    Console.WriteLine(split.Length);
                    foreach (var s in split)
                        Console.WriteLine(s);
                    return null;
                }
            }
            else if(altCommand.isOn && raw.StartsWith(altCommand.alternativeSyntax))
            {
                split = raw.Substring(altCommand.alternativeSyntax.Length).Split(" \n  ".ToCharArray(), 2, StringSplitOptions.RemoveEmptyEntries);
                if (split.Length > 0)
                {
                    command = split[0].ToLower();
                    payload = split.Length == 2 ? split[1] : "";
                }else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
            if (!Commandlist.Keys.Contains(command))
            {
                Console.WriteLine("unknown command: " + command);
                return null;
            }
            return Tuple.Create(command, payload);
        }


        public string toEmojiString(ServerMessage e, string m)
        {
            if (m.Length >= 4)
            {
                var split = m.Split(":".ToCharArray());
                if (split.Length == 3)
                {
                    var emojiIdStr = split[2].Trim(" >".ToCharArray());
                    ulong emojiId;
                    if (UInt64.TryParse(emojiIdStr, out emojiId))
                    {
                        return m.Trim().Trim("<>".ToCharArray());
                    }
                }
            }
            safeSendMessage(e.Channel, "Invalid Emoji: " + m);
            Console.WriteLine("invalid emoji: " + m);
            return null;
        }

        public T fileCommand<T> (string path, Func<string,T> command)
        {
            return command(serverpath + path);
        }

        public void fileCommand (string path, Action<string> command)
        {
            command(serverpath + path);
        }

        public List<String> FileSetup(String name)
        {
            try
            {
                return fileCommand(name,System.IO.File.ReadLines).ToList();
            }
            catch
            {
                Console.WriteLine("Did not find file: " + name + ", creating it.");
                try
                {
                    fileCommand(name, System.IO.File.Create);
                }
                catch
                {
                    Console.WriteLine("Unable to create file, optimistically continuing... since sometimes there's creating twice bugs. TODO btw.");
                }
                return new List<string>();
            }
        }
        
        public T XMLSetup<T>(String filepath)
        {
            return fileCommand(filepath, Funcs.XMLSetup<T>);
        }

        public Dictionary<T, K> XMlDictSetup<T, K>(String filepath)
        {
            return fileCommand(filepath, Funcs.XMlDictSetup<T, K>);
        }

        public void XMLSerialization<T>(String filepath, T obj)
        {
            fileCommand(filepath, x => Funcs.XMLSerialization(x, obj));
        }

        public void XMlDictSerialization<T, K>(String filepath, Dictionary<T, K> dict)
        {
            fileCommand(filepath, x => Funcs.XMlDictSerialization(x, dict));
        }

    }
}
