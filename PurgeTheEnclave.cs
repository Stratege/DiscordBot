using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using System.Timers;

namespace borkbot
{
    class PurgeTheEnclave : EnableableCommandModule
    {
        Role role;
        private static string enclaveRoleFile = "Enclaverole.txt";
        private static string adminChan = "Adminchannel.txt";
        Dictionary<ulong, DateTime> watchlist;
        String watchlistPath = "enclavewatch.xml";
        Channel channel;
        TimeSpan span = new TimeSpan(3, 0, 0);

        public PurgeTheEnclave(VirtualServer _server) : base(_server, "enclavepurge")
        {
            try
            {

                var chan = server.FileSetup(adminChan);
                if (chan.Count > 0)
                {
                    channel = server.getServer().AllChannels.FirstOrDefault(x => x.Name == chan[0]);
                }
                var str = server.FileSetup(enclaveRoleFile);
                ulong roleId;
                if (str.Count > 0)
                {
                    if (UInt64.TryParse(str[0], out roleId))
                    {
                        role = server.getServer().Roles.FirstOrDefault(x => x.Id == roleId);
                    }
                }
                watchlist = server.XMlDictSetup<ulong, DateTime>(watchlistPath);
                watchlist.ToList().Select(x =>
                {
                    Console.WriteLine("Watchlist element");
                    TimeSpan y = DateTime.Now.Subtract(x.Value.Add(span));
                    var res = y.TotalMilliseconds;
                    if (res <= 0)
                        sendMessage(null, x.Key);
                    else
                    {
                        var z = new Timer(res);
                        z.Elapsed += (s, e) => sendMessage((Timer)s, x.Key);
                        z.Start();
                    }
                    return 0;
                });

                server.UserUpdated += (o, e) =>
                {
                    if (role != null)
                    {
                        try
                        {
                            bool wasEnclave = e.Before.HasRole(role);
                            bool isEnclave = e.After.HasRole(role);
                            if (wasEnclave && !isEnclave && watchlist.ContainsKey(e.After.Id))
                            {
                                Console.WriteLine("no more Enclave for: " + e.After.Name);
                                watchlist.Remove(e.After.Id);
                            }
                            else if (!wasEnclave && isEnclave && !watchlist.ContainsKey(e.After.Id))
                            {
                                Console.WriteLine("now Enclave: " + e.After.Name);
                                watchlist.Add(e.After.Id, DateTime.Now);
                                var z = new Timer(span.TotalMilliseconds);
                                z.Elapsed += (s, e2) => sendMessage((Timer)s, e.After.Id);
                                z.Start();
                            }
                        }
                        catch (Exception excep)
                        {
                            Console.WriteLine(excep);
                        }
                    }
                };

                server.UserLeft += (o, e) =>
                {
                    if (watchlist.ContainsKey(e.User.Id))
                        watchlist.Remove(e.User.Id);
                };
            }
            catch (Exception excep)
            {
                Console.WriteLine(excep);
            }

        }

        public override List<Tuple<string, Command>> getCommands()
        {
            var cmds = base.getCommands();
            cmds.Add(new Tuple<string, Command>("setenclaverole", Command.AdminCommand(server, setEnclaveRole, "setenclaverole <role>")));

            return cmds;
        }

        private void setEnclaveRole(SocketUserMessage e, string m)
        {
            var res = e.Server.Roles.FirstOrDefault(x => x.Name == m);
            string message;
            if (res == null)
                message = "Could not find role: " + m;
            else
            {
                message = "Watched role was set to: " + res.Name;
                role = res;
                server.fileCommand(enclaveRoleFile, x => System.IO.File.WriteAllText(x, res.Id.ToString()));
            }
            server.safeSendMessage(e.Channel, message);
        }

        private void sendMessage(Timer t, ulong m)
        {
            Console.WriteLine("The bell tolls for: " + m);
            if (t != null)
            {
                t.Stop();
                t.Dispose();
            }
            var u = server.getServer().GetUser(m);
            if (u != null && role != null && u.HasRole(role))
            {
                if (channel != null)
                {
                    if(on)
                        server.safeSendMessage(channel, "It has been a while since " + u.Name + " has gotten the enclave role.");
                }
                var z = new Timer(span.TotalMilliseconds);
                z.Elapsed += (s, e2) => sendMessage((Timer)s, u.Id);
                z.Start();
            }
        }
    }
}
