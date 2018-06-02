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
        private async Task Cruft() { 
            servers = new Dictionary<ulong, VirtualServer>();
            Console.WriteLine("Booting up");
            DiscordSocketConfig cfgbld = new DiscordSocketConfig();
            //            DiscordConfigBuilder cfgbld = new DiscordConfigBuilder();
            cfgbld.MessageCacheSize = 100000;
            Console.WriteLine("WebSocketProvider: " + cfgbld.WebSocketProvider);
            cfgbld.WebSocketProvider = Discord.Net.Providers.WS4Net.WS4NetProvider.Instance;
            Console.WriteLine("WebSocketProvider: "+cfgbld.WebSocketProvider);
            DC = new DiscordSocketClient(cfgbld);
            PrivateMessageHandler pmHandler = new PrivateMessageHandler(DC, servers);
            
            DC.MessageReceived += async (msg) =>
                await Task.Run(() => {
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
                    }else
                    {
                        Console.WriteLine("Did not handle non-user msg: " + msg);
                    }
                });
            DC.UserJoined += async (user) =>
             await Task.Run(() => {
                 servers[user.Guild.Id].userJoined(user);
            });
            DC.UserLeft += async (user) =>
             await Task.Run(() => {
                 servers[user.Guild.Id].userLeft(user);
             });

            DC.UserUpdated += async (userOld,userNew) =>
             await Task.Run(() => {
                 if (userNew.GetType() == typeof(SocketGuildUser))
                 {
                     servers[((SocketGuildUser)userNew).Guild.Id].userUpdated((SocketGuildUser)userOld,(SocketGuildUser)userNew);
                 }else
                 {
                     Console.WriteLine("Unhandled User Update received:\n" + userOld + "\nand\n" + userNew);
                 }
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
            await Task.Run(() => {
                Console.WriteLine("here?");
                if (!servers.ContainsKey(sg.Id))
                    servers.Add(sg.Id, new VirtualServer(DC, sg));
                else
                {
                    servers[sg.Id].updateServer(sg);
                }
                Console.WriteLine(DateTime.Now + " - Connected to: " + sg.Name);
            });

            /*            this memleaks everywhere, well really it's more the adding bit but whatever, when dormant it should be harmless
                        DC.ServerUnavailable += (o, e) =>
                        {
                            servers.Remove(e.Server);
                        };
                        */
            DC.Connected += () =>
            {
                Console.WriteLine("connected for real");
                return Task.FromResult<object>(null);
            };
            DC.Log += (msg) =>
            {
                Console.WriteLine("Log Message: " + msg);
                if(msg.Message.ToLower() == "failed to resume previous session")
                {
                    //we just, you know, quit
                    System.Environment.Exit(0);
                }
                return Task.FromResult<object>(null);
            };


            DC.MessageDeleted += (msg, origin) =>
            {
                if (msg.Value != null)
                    Console.WriteLine("Deleted Message was: " +origin+" "+msg.Value.Timestamp+" "+ msg.Value.Author +": "+msg.Value.Content);
                else
                    Console.WriteLine("Deleted unknown message with Id: " + msg.Id);
                return Task.FromResult<object>(null);
            };

            Console.WriteLine("Execute phase");
            var token = File.ReadAllText("token.txt");
            Console.WriteLine("token: '" + token + "'");
            await DC.LoginAsync(Discord.TokenType.Bot,token);
            Console.WriteLine("supposedly connected");
            await DC.StartAsync();
            Console.WriteLine("and start async passed");
            //block forever
            await Task.Delay(-1);
        }

        

        



    }
    /*
    public class EmojiAddRequest : Discord.API.IRestRequest
    {
        string Discord.API.IRestRequest.Method => "PUT";
        string Discord.API.IRestRequest.Endpoint => $"channels/{ChannelId}/messages/{MessageId}/reactions/{Emoji}/@me";
        object Discord.API.IRestRequest.Payload => null;

        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
        public string Emoji { get; set; }

        public EmojiAddRequest(ulong channelId, ulong messageId, string emoji)
        {
            ChannelId = channelId;
            MessageId = messageId;
            Emoji = emoji;
            Console.WriteLine(((Discord.API.IRestRequest)this).Endpoint);
        }
    }
    */
    public class DictSerHelper<T, K>
    {
        public T car;
        public K cdr;
        public DictSerHelper(T _car, K _cdr) { car = _car; cdr = _cdr; }
        public DictSerHelper() { }
    }

    public static class Funcs
    {


        public static T XMLSetup<T>(String filepath)
        {
            using (var filestream = File.Open(filepath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                if (filestream.Length > 0)
                    return (T)new XmlSerializer(typeof(T)).Deserialize(filestream);
                else
                    return default(T);
            }
        }

        public static Dictionary<T, K> XMlDictSetup<T, K>(String filepath)
        {
            var xs = XMLSetup<List<DictSerHelper<T, K>>>(filepath);
            if (xs == null)
                return new Dictionary<T, K>();
            var dict = new Dictionary<T, K>(xs.Count);
            foreach (var x in xs)
            {
                dict.Add(x.car, x.cdr);
            }
            return dict;
        }

        public static void XMLSerialization<T>(String filepath, T obj)
        {
            using (var filestream = File.Open(filepath, FileMode.Create, FileAccess.Write))
            {
                new XmlSerializer(typeof(T)).Serialize(filestream, obj);
            }
        }

        public static void XMlDictSerialization<T, K>(String filepath, Dictionary<T, K> dict)
        {
            var xs = new List<DictSerHelper<T, K>>(dict.Count);
            foreach (var x in dict)
            {
                xs.Add(new DictSerHelper<T, K>(x.Key, x.Value));
            }
            XMLSerialization<List<DictSerHelper<T, K>>>(filepath, xs);
        }


        public static bool validateMentionTarget(ServerMessage e, String m)
        {
            var users = e.msg.MentionedUsers.Where(x => !x.IsBot).Select(x => Tuple.Create(x.Mention, x.Id, x)).ToList();
            //turning this lax... let's hope it works
            //return (users.Count != 0 && m == users[0].Item1);
            return (users.Count != 0);
        }

        public static List<string> splitKeepDelimiters(string input, string[] delimiters)
        {
            int[] nextPosition = delimiters.Select(d => input.IndexOf(d)).ToArray();
            List<string> result = new List<string>();
            int pos = 0;
            while (true)
            {
                int firstPos = int.MaxValue;
                string delimiter = null;
                for (int i = 0; i < nextPosition.Length; i++)
                {
                    if (nextPosition[i] != -1 && nextPosition[i] < firstPos)
                    {
                        firstPos = nextPosition[i];
                        delimiter = delimiters[i];
                    }
                }
                if (firstPos != int.MaxValue)
                {
                    result.Add(input.Substring(pos, firstPos - pos));
                    result.Add(delimiter);
                    pos = firstPos + delimiter.Length;
                    for (int i = 0; i < nextPosition.Length; i++)
                    {
                        if (nextPosition[i] != -1 && nextPosition[i] < pos)
                        {
                            nextPosition[i] = input.IndexOf(delimiters[i], pos);
                        }
                    }
                }
                else
                {
                    result.Add(input.Substring(pos));
                    break;
                }
            }
            return result;
        }


        //taken straight from: http://stackoverflow.com/a/9461311
        public static R WithTimeout<R>(Func<R> proc, int millisecondsDuration)
        {
            var reset = new System.Threading.AutoResetEvent(false);
            var r = default(R);
            Exception ex = null;

            var t = new System.Threading.Thread(() =>
            {
                try
                {
                    r = proc();
                }
                catch (Exception e)
                {
                    ex = e;
                }
                reset.Set();
            });

            t.Start();

            // not sure if this is really needed in general
            while (t.ThreadState != System.Threading.ThreadState.Running)
            {
                System.Threading.Thread.Sleep(0);
            }

            if (!reset.WaitOne(millisecondsDuration))
            {
                t.Abort();
                throw new TimeoutException();
            }

            if (ex != null)
            {
                throw ex;
            }

            return r;
        }

        static public SocketGuildUser GetUserByMentionOrName(IEnumerable<SocketGuildUser> users, String str)
        {
            String mentionString;
            if (str[0] == '<' && str[1] == '@' && str[2] != '!')
            {
                mentionString = "<@!" + str.Substring(2);
            }else
            {
                mentionString = str;
            }
            return users.FirstOrDefault(x => x.Mention == mentionString /*|| x.NicknameMention == split[0]*/ || x.Username == str || x.Nickname == str);
        }

    }
}
