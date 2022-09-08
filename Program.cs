/*
 * Setting up interface with the framework and connecting the callbacks to the appropriate Virtual Server.
 * Also handles new server joining.
 * 
 * */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using System.Xml.Serialization;
using System.IO;
using Discord.Net.Providers.WS4Net;

namespace borkbot
{
    class Program
    {

        Dictionary<ulong, VirtualServer> servers;
        DiscordSocketClient DC;
        static void Main(string[] args)
        { new Program(); }

        Program()
        {
            Cruft().GetAwaiter().GetResult();
        }
        private async Task Cruft()
        {
            servers = new Dictionary<ulong, VirtualServer>();
            Console.WriteLine("Booting up");
            DiscordSocketConfig cfgbld = new DiscordSocketConfig();
            cfgbld.MessageCacheSize = 100000;
            Console.WriteLine("WebSocketProvider: " + cfgbld.WebSocketProvider);
            cfgbld.WebSocketProvider = Discord.Net.Providers.WS4Net.WS4NetProvider.Instance;
            Console.WriteLine("WebSocketProvider: " + cfgbld.WebSocketProvider);
            cfgbld.AlwaysDownloadUsers = true;
            DC = new DiscordSocketClient(cfgbld);
            PrivateMessageHandler pmHandler = new PrivateMessageHandler(DC, servers);

            DC.ReactionAdded += async (a, b, reaction) =>
            await Task.Run(() =>
            {
                if (reaction.Channel is SocketTextChannel)
                {
                    servers[(reaction.Channel as SocketTextChannel).Guild.Id].reactionAdded(reaction);
                }

            });

            DC.ReactionRemoved += async (a, b, reaction) =>
            await Task.Run(() =>
            {
                if (reaction.Channel is SocketTextChannel)
                {
                    servers[(reaction.Channel as SocketTextChannel).Guild.Id].reactionRemoved(reaction);
                }

            });

            Func<SocketMessage,Task> msgReceivedHandling = async (SocketMessage msg) =>
                await Task.Run(() =>
                {
                    //we ignore our own messages
                    if (msg.Author.Id == DC.CurrentUser.Id)
                        return;
                    if (msg.GetType() == typeof(SocketUserMessage))
                    {
                        if (msg.Channel.GetType() == typeof(SocketDMChannel))
                        {
                            Console.WriteLine("Got private message by " + msg.Author.Username);
                            pmHandler.messageRecieved((SocketUserMessage)msg);
                        }
                        else
                        {
                            SocketTextChannel stc = (SocketTextChannel)msg.Channel;
                            var convertedMsg = new ServerMessage(stc.Guild, false, stc, (SocketUserMessage)msg, stc.Guild.GetUser(msg.Author.Id));
                            servers[stc.Guild.Id].messageRecieved(convertedMsg);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Did not handle non-user msg: " + msg);
                    }
                });

            DC.MessageReceived += msgReceivedHandling;
            DC.UserJoined += async (user) =>
             await Task.Run(() =>
             {
                 servers[user.Guild.Id].userJoined(user);
             });
            DC.UserLeft += async (guild,user) =>
             await Task.Run(() =>
             {
                 servers[guild.Id].userLeft(guild,user);
             });

            DC.RoleUpdated += async (oldRole, newRole) =>
            await Task.Run(() => {
                servers[oldRole.Guild.Id].roleUpdated(oldRole, newRole);
            });
            DC.RoleDeleted += async (role) =>
            await Task.Run(() => {
                servers[role.Guild.Id].roleDeleted(role);
            });

            bool firstSetup = true;
            DC.Ready += async () =>
            {
                Console.WriteLine("there?");
                if (firstSetup)
                {
                    await Task.Run(() =>
                    {
                        Console.WriteLine("Battlebot operational");
                        firstSetup = false;
                    });
                }
            };

            DC.GuildAvailable += async (sg) =>
            await Task.Run(() =>
            {
                Console.WriteLine("here?");
                if (!servers.ContainsKey(sg.Id))
                    servers.Add(sg.Id, new VirtualServer(DC, sg));
                else
                {
                    servers[sg.Id].updateServer(sg);
                }
                Console.WriteLine(DateTime.Now + " - Connected to: " + sg.Name);
            });

            DC.Connected += () =>
            {
                Console.WriteLine("connected for real");
                return Task.FromResult<object>(null);
            };
            DC.Log += (msg) =>
            {
                Console.WriteLine("Log Message: " + msg);
                Console.WriteLine("test: " + msg.Message);
                //this had to be disabled because discord.net's deserialization code is the biggest jank I have ever seen.
                //TODO: find a way to recover from log messages when the framework got stuck and the bot needs a full reboot
/*                if (msg.Exception != null || msg.Message == null || msg.Message.ToLower() == "failed to resume previous session")
                {
                    //we just, you know, quit
                    System.Environment.Exit(0);
                }*/
                return Task.FromResult<object>(null);
            };


            DC.MessageDeleted += (msg, origin) =>
            {
                if (msg.Value != null)
                    Console.WriteLine("Deleted Message was: " + origin + " " + msg.Value.Timestamp + " " + msg.Value.Author + ": " + msg.Value.Content);
                else
                    Console.WriteLine("Deleted unknown message with Id: " + msg.Id);
                return Task.FromResult<object>(null);
            };

            DC.ThreadCreated += async (stc) =>
            {
                await stc.JoinAsync();
                await Task.Run(() =>
                {
                    servers[stc.Guild.Id].threadCreated(stc);
                });
            };

            DC.ChannelCreated += async (sc) =>
            {
                var sgc = sc as SocketGuildChannel;
                if (sgc != null) {
                    await Task.Run(() =>
                    {
                        servers[sgc.Guild.Id].channelCreated(sgc);
                    });
                }
            };

            DC.MessageUpdated += async (a, msg, b) =>
            {
                if (DateTime.Now - msg.Timestamp <= new TimeSpan(0, 5, 0))
                {
                    await msgReceivedHandling(msg);
                }
            };

            Console.WriteLine("Execute phase");
            var token = File.ReadAllText("token.txt");
            Console.WriteLine("token: '" + token + "'");
            await DC.LoginAsync(Discord.TokenType.Bot, token);
            Console.WriteLine("supposedly connected");
            await DC.StartAsync();
            Console.WriteLine("and start async passed");
            //block forever
            await Task.Delay(-1);
        }
    }

}
