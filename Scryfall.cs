/* Module that queries the scryfall REST API
 * 
 * supports explicit search as well as inline via [[ ]]
 */
using System;
using System.Collections.Generic;
using System.Text;

using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Threading.Tasks;

namespace borkbot
{
    class ScryfallGlobalDB
    {
        List<string> cardnames;
        string filename = "scryfalldb.txt";
        private static ScryfallGlobalDB instance;
        public static ScryfallGlobalDB Instance { get { if (instance == null) instance = new ScryfallGlobalDB(); return instance; } }

        private ScryfallGlobalDB()
        {
            if (!System.IO.File.Exists(filename))
            {
                cardnames = new List<string>();
                var c = new HttpClient();
                Update(c).Wait();
            }
            else
                cardnames = System.IO.File.ReadAllLines(filename).ToList();
        }

        public async Task<bool> Update(HttpClient HttpClient)
        {
            var ret = JObject.Parse(await(await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://api.scryfall.com/catalog/card-names"))).Content.ReadAsStringAsync());
            var data = ret.Value<JArray>("data");
            if (data == null) return false;
            var str = data.Select(x => x.ToString());
            update(str.ToList());
            return true;
        }
        private void update(List<string> newList)
        {
            if(cardnames.Count == newList.Count)
            {
                var c = cardnames.Count;
                bool mismatch = false;
                for(int i = 0; i < c; i++)
                {
                    if(cardnames[i] != newList[i])
                    {
                        mismatch = true;
                        break;
                    }
                }
                if (!mismatch)
                    return;
            }
            cardnames = newList;
            System.IO.File.WriteAllLines(filename, cardnames);
        }

        public List<string> query(string q)
        {
            q = q.ToLower();
            return cardnames.Where(x => x.ToLower().Contains(q)).ToList();
        }
    }
    class Scryfall : CommandHandler
    {
        HttpClient HttpClient;
        public Scryfall(VirtualServer _server) : base(_server)
        {
            HttpClient = new HttpClient();
            server.MessageRecieved += (e, m) =>
            {
                string msg = m.msg.Content;
                var idxStart = msg.IndexOf("[[");
                if (idxStart != -1)
                {
                    var idxEnd = msg.IndexOf("]]",idxStart);
                    if(idxEnd != -1)
                    {
                        var substr = msg.Substring(idxStart + 2, idxEnd - idxStart - 2);
                        lookup(m, substr);
                    }
                }
            };
        }
        public override List<Command> getCommands()
        {
            var ls = new List<Command>();
            ls.Add(new Command(server, "scryfall", lookup, PrivilegeLevel.Everyone, new HelpMsgStrings("it's scryfall search", "<fuzzy name>")));
            ls.Add(Command.AdminCommand(server, "scryupdate", scryupdate, new HelpMsgStrings("updates scryfall db", "")));
            return ls;
        }
        private async Task<bool> checkError<T>(ServerMessage e, T val)
        {
            if(val == null)
            {
                await server.safeSendMessage(e.Channel, "API error");
                return true;
            }
            else
            {
                return false;
            }
        }

        private async void scryupdate(ServerMessage e, string msg)
        {
            var success = await ScryfallGlobalDB.Instance.Update(HttpClient);
            if (!success)
                await checkError<object>(e, null);
            else
                await server.safeSendMessage(e.Channel,"successful db update");
        }

        private async void lookup(ServerMessage e, string msg)
        {
            //todo: sanitize msg
            if(msg == null || msg == "")
            {
                await server.safeSendMessage(e.Channel,"Can not search for empty msg");
                return;
            }
            else
            {
                if(msg[0] == '@')
                {
                    await lookupInternalComplex(e, msg.Substring(1));
                }
                else
                {
                    await lookupInternal(e, msg, false);
                }
            }
        }

        private static DateTime lastSent = DateTime.MinValue;
        private static Object lockObj = new object();

        private void rateLimit()
        {
            //global cd to respect API limits
            lock (lockObj)
            {
                if (lastSent + TimeSpan.FromMilliseconds(100) > DateTime.Now)
                {
                    System.Threading.Thread.Sleep((lastSent + TimeSpan.FromMilliseconds(100) - DateTime.Now).Milliseconds);
                }
                lastSent = DateTime.Now;
            }
        }

        private async Task lookupInternalComplex(ServerMessage e, string msg)
        {
            rateLimit();
            var ret = await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://api.scryfall.com/cards/search?q=" + Uri.EscapeDataString(msg)));
            var json = JObject.Parse(await ret.Content.ReadAsStringAsync());
            var retStatus = json.Value<int?>("status");
            if (retStatus != null)
            {
                var det = json.Value<string>("details");
                if (await checkError(e, det)) return;
                await server.safeSendMessage(e.Channel, det);
                return;
            }
            var count = json.Value<int>("total_cards");
            var data = json.Value<JArray>("data");
            int maxResponse = 10;
            int i = System.Math.Min(maxResponse, count);
            var res = new List<String>();
            foreach(var d in data)
            {
                i--;
                if (i < 0)
                    break;
                res.Add(d.Value<string>("name"));
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("Cards matching your query");
            if(count > maxResponse)
            {
                sb.Append("(showing " + maxResponse + " out of " + count + ")");
            }
            sb.AppendLine(": ");
            foreach (var r in res)
                sb.AppendLine(r);
            await server.safeSendMessage(e.Channel, sb.ToString());
        }

        private async Task lookupInternal(ServerMessage e, string msg, bool lastChance)
        {
            rateLimit();
            var ret = await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://api.scryfall.com/cards/named?fuzzy=" + msg));
            var json = JObject.Parse(await ret.Content.ReadAsStringAsync());
            var retStatus = json.Value<int?>("status");
            if (retStatus != null)
            {
                var type = json.Value<string>("type");
                if (type != null && type == "ambiguous")
                {
                    var s = await fuzzyHandle(e, msg, lastChance);
                    if (!lastChance && s != null)
                    {
                        await Task.Delay(100);
                        await lookupInternal(e, s, true);
                    }
                    return;
                }
                var det = json.Value<string>("details");
                if (await checkError(e, det)) return;
                await server.safeSendMessage(e.Channel, det);
                return;
            }
            var images = json.Value<JObject>("image_uris");
            if (await checkError(e, images)) return;
            var normal = images.Value<string>("normal");
            if (await checkError(e, normal)) return;
            await server.safeSendMessage(e.Channel, normal);
        }

        private async Task<string?> fuzzyHandle(ServerMessage e, string msg, bool lastChance)
        {
            var rets = ScryfallGlobalDB.Instance.query(msg);
            rets = rets.Take(10).ToList();
            if (rets.Count == 1 && !lastChance)
                return rets[0];
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Too many cards match ambiguous name \"" + msg + "\". Did you mean: ");
            foreach (var r in rets)
                sb.AppendLine(r);
            await server.safeSendMessage(e.Channel, sb.ToString());
            return null;
        }
    }
}
