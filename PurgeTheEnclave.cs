using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using System.Timers;

namespace borkbot
{
    class PurgeTheEnclave : EnableableCommandModule
    {
        SocketRole role;
        private static string enclaveRoleFile = "Enclaverole.txt";
        private static string adminChan = "Adminchannel.txt";
        Dictionary<ulong, DateTime> watchlist;
        String watchlistPath = "enclavewatch.xml";
        SocketTextChannel channel;
        TimeSpan span = new TimeSpan(3, 0, 0);

        public PurgeTheEnclave(VirtualServer _server) : base(_server, "enclavepurge")
        {
            try
            {

                var chan = server.FileSetup(adminChan);
                if (chan.Count > 0)
                {
                    channel = server.getServer().TextChannels.FirstOrDefault(x => x.Name == chan[0]);
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
                    var before = e.Item1;
                    var after = e.Item2;
                    if (role != null)
                    {
                        try
                        {
                            bool wasEnclave = before.Roles.Contains(role);
                            bool isEnclave = after.Roles.Contains(role);
                            if (wasEnclave && !isEnclave && watchlist.ContainsKey(after.Id))
                            {
                                Console.WriteLine("no more Enclave for: " + after.Username);
                                watchlist.Remove(after.Id);
                            }
                            else if (!wasEnclave && isEnclave && !watchlist.ContainsKey(after.Id))
                            {
                                Console.WriteLine("now Enclave: " + after.Username);
                                watchlist.Add(after.Id, DateTime.Now);
                                var z = new Timer(span.TotalMilliseconds);
                                z.Elapsed += (s, e2) => sendMessage((Timer)s, after.Id);
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
                    if (watchlist.ContainsKey(e.Id))
                        watchlist.Remove(e.Id);
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

        private void setEnclaveRole(ServerMessage e, string m)
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
            if (u != null && role != null && u.Roles.Contains(role))
            {
                if (channel != null)
                {
                    if(on)
                        server.safeSendMessage(channel, "It has been a while since " + u.Username + " has gotten the enclave role.");
                }
                var z = new Timer(span.TotalMilliseconds);
                z.Elapsed += (s, e2) => sendMessage((Timer)s, u.Id);
                z.Start();
            }
        }
    }
}
