using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace borkbot
{
    class PrivateMessageHandler
    {
        DiscordSocketClient DC;
        Dictionary<ulong, VirtualServer> servers;
        Dictionary<ulong, ulong> UserToServerMapping;
        string userToServerMappingFileName = "userToServerMapping";

        public PrivateMessageHandler(DiscordSocketClient _DC, Dictionary<ulong, VirtualServer> _servers)
        {
            DC = _DC;
            servers = _servers;
            UserToServerMapping = Funcs.XMlDictSetup<ulong,ulong>(userToServerMappingFileName);
        }

        private void persistState()
        {
            Funcs.XMlDictSerialization(userToServerMappingFileName, UserToServerMapping);
        }


        public void messageRecieved(SocketUserMessage e)
        {
            try
            {
                if (DC == null)
                    throw new Exception("oops no DC");
                if (DC.CurrentUser == null)
                    throw new Exception("oops no CurrentUser");
                if (!(e.Channel.GetType() != typeof(SocketDMChannel)))
                {
                    Console.WriteLine("error in the code! Sending non-private messages to PMHandler!");
                    return;
                }
                if (serverSelectCommand(e))
                {
                    Console.WriteLine("handled server Select Command");
                    return;
                }
                ulong serverId;
                if (UserToServerMapping.TryGetValue(e.Author.Id, out serverId))
                {
                    VirtualServer curServ;
                    if (servers.TryGetValue(serverId, out curServ))
                    {
                        if (curServ.getServer().Users.FirstOrDefault(z => z.Id == e.Author.Id) != null)
                        {
                            Console.WriteLine("accepting PM");
                            curServ.messageRecieved(e);
                            return;
                        }
                        else
                        {
                            safeSendPM((SocketDMChannel)e.Channel, "You are not part of your set server anymore.");
                        }
                    }
                    else
                    {
                        safeSendPM((SocketDMChannel)e.Channel, "This Bot is not part of your set server anymore.");
                    }
                }
                var x = servers.Values.Where(y => y.getServer().Users.FirstOrDefault(z => z.Id == e.Author.Id) != null).ToList();
                if (x.Count == 0)
                {
                    safeSendPM((SocketDMChannel)e.Channel, "Sorry, you are not part of any server this bot is in.");
                    return;
                }
                else if (x.Count == 1)
                {
                    safeSendPM((SocketDMChannel)e.Channel, "Set your server context to " + x[0].getServer().Name + ". Please resend your command.");
                    SetUserServerMapping(e.Author.Id, x[0].getServer().Id);
                    return;
                }
                else
                {
                    string m = "Please select your channel via \"!selectServer <servername>\" from this list: ";
                    foreach (var s in x)
                    {
                        m += "\n" + s.getServer().Name;
                    }
                    safeSendPM((SocketDMChannel)e.Channel, m);
                }
            }
            catch(Exception except)
            {
                Console.WriteLine("Error in PM handling code: "+except);
            }
        }

        private void SetUserServerMapping(ulong user, ulong server)
        {
            try
            {
                if (UserToServerMapping.ContainsKey(user))
                    UserToServerMapping.Remove(user);
                UserToServerMapping.Add(user,server);
                persistState();
            }
            catch(Exception e)
            {
                Console.WriteLine("Error with setting User Server Mapping: " + e.Message);
            }
        }

        private void safeSendPM(SocketDMChannel c, String m)
        {
            try
            {
                c.SendMessageAsync(m);
            }
            catch(Exception e)
            {
                Console.WriteLine("Writing to channel with Id " + c.Id + " failed with: " + e.Message);
            }
        }

        private bool serverSelectCommand(SocketMessage m)
        {
            var s = m.Content;
            var xs = s.Split(" ".ToCharArray(),2,StringSplitOptions.RemoveEmptyEntries);
            string server;
            if (xs.Length == 2 && xs[0] == "!selectServer")
            {
                server = xs[1];
            }
            else
            {
                xs = s.Split(" ".ToCharArray(), 3, StringSplitOptions.RemoveEmptyEntries);
                if (xs.Length == 3 && (xs[0] == DC.CurrentUser.Mention /*|| xs[0] == DC.CurrentUser.NicknameMention*/) && xs[1] == "selectServer")
                {
                    server = xs[2];
                }
                else
                {
                    Console.WriteLine("could not parse: " + m.Content);
                    return false;
                }
            }
            var validServers = servers.Values.Select(x => x.getServer()).Where(y => y.Users.Select(z => z.Id).Contains(m.Author.Id));
            var selectedServer = validServers.Where(y => y.Name == server).FirstOrDefault();
            if(selectedServer == null)
            {
                string msg = "Tried to select invalid server: " + selectedServer+"\nAvailable list:";
                foreach (var serv in validServers)
                {
                    msg += "\n" + serv.Name;
                }
                safeSendPM((SocketDMChannel)m.Channel, msg);
            }
            else
            {
                SetUserServerMapping(m.Author.Id, selectedServer.Id);
                safeSendPM((SocketDMChannel)m.Channel,"Successfully updated your message server to: " + selectedServer.Name);
            }
            return true;
        }
    }
}
