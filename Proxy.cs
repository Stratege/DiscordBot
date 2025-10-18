/* Module which contains the logic for proxying messages
 * 
 * - users set up prefixes that when found in messages have their messages swapped with one by a user defined name+profile
 * - users can also set up auto proxying, replacing all their messages in a particular channel
 * - channels can be set into RP only mode, allowing only RP proxied messages in the channel
 * - RP proxies are not allowed outside of RP channels (use normal proxies for that)
 */

using Discord.Webhook;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Reflection;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace borkbot
{
    [Serializable]
    public class AutoProxies
    {
        public ulong userId;
        public ulong proxyId;
    }

    [Serializable]
    public class ProxyData
    {
        public string prefix;
        public string name;
        public string image;
        public string color; //not yet in use, reserved for later
        public bool isRP;
        public string specialNameInRPMode;
        public ulong proxyId;
    }

    class Proxy : CommandHandler
    {
        PersistantDict<ulong, ulong> chanIdToWebhookId;
        Dictionary<ulong, DiscordWebhookClient> chanIdToWebhook;
        PersistantDict<ulong, List<ProxyData>> userIdToProxyData;
        bool booted = false;
        PersistantValue<bool> isInRPMode;
        PersistantDict<ulong, bool> channelsInRPMode;
        PersistantDict<ulong, List<AutoProxies>> chanIDToAutoProxies;
        PersistantValue<ulong> uniqueProxyId;
        List<Tuple<ulong, ulong>> chanIdWebhookIdToInit;

        public Proxy(VirtualServer _server) : base(_server)
        {
            chanIdToWebhookId = PersistantDict<ulong, ulong>.load(server, "chanidtowebhook");
            chanIdToWebhook = new Dictionary<ulong, DiscordWebhookClient>();
            userIdToProxyData = PersistantDict<ulong, List<ProxyData>>.load(server, "userIdToProxyData");
            isInRPMode = PersistantValue<bool>.load(server, "proxyRPMode");
            channelsInRPMode = PersistantDict<ulong, bool>.load(server, "proxyChannelsToRPModeSetting");
            chanIDToAutoProxies = PersistantDict<ulong, List<AutoProxies>>.load(server, "proxyChanIdToAutoProxies");
            uniqueProxyId = PersistantValue<ulong>.load(server, "proxyUniqueProxyId");
            
            Func<VirtualServer,Tuple<ServerMessage,string>,Task<bool>> f = async (VirtualServer s, Tuple<ServerMessage,string> t) =>
            {
                var e = t.Item1;
                var m = t.Item2;
                if (m == "")
                    return true; //it's something other than a proxyable msg so we ignore it 
                if (e.Author.IsBot)
                    return true; //lets play nice with other bots
                if (e.isProxy)
                    return true; //no more proxy while proxy
                if (chanIdToWebhookId.Count == 0)
                    return true;
                if (m.StartsWith("!ignore"))
                    return true;
                if (!booted)
                {
                    booted = true;
                    var toRemove = new List<ulong>();
                    foreach (var kvp in chanIdToWebhookId)
                    {
                        Discord.Rest.RestWebhook hook;
                        var c = server.getServer().GetTextChannel(kvp.Key);
                        if (c != null)
                        {
                            hook = await c.GetWebhookAsync(kvp.Value);
                        } else {

                            var f = server.getServer().GetForumChannel(kvp.Key);
                            if (f != null)
                            {
                                hook = await f.GetWebhookAsync(kvp.Value);
                            } else {
                                toRemove.Add(kvp.Key);
                                continue;
                            }
                        }                       
                        if(hook == null)
                        {
                            toRemove.Add(kvp.Key);
                            continue;
                        }
                        var dhc = new DiscordWebhookClient(hook);
                        chanIdToWebhook.Add(kvp.Key, dhc);
                    }
                    foreach (var v in toRemove)
                    {
                        chanIdToWebhookId.Remove(v);
                    }
                    if (toRemove.Count > 0)
                        chanIdToWebhookId.persist();
                }
                var settingsChannelId = toRootSettingsChannelId(e.Channel);
                if (!chanIdToWebhook.ContainsKey(settingsChannelId))
                    return true;
                if (!userIdToProxyData.ContainsKey(e.Author.Id) || (channelsInRPMode.ContainsKey(settingsChannelId) && !userIdToProxyData[e.Author.Id].Any(x => x.isRP)))
                {
                    if (channelsInRPMode.ContainsKey(settingsChannelId))
                    {
                        await server.safeSendMessage(e.Channel, e.Author.Mention+" This channel is in RP Mode only. Register a RP Proxy to talk in here (ask your botadmin to ensure proxy registration is in RP Mode).");
                        Console.WriteLine("DeleteAsync1");
                        await e.msg.DeleteAsync();
                        Console.WriteLine("Post DeleteAsync1");
                        return false;
                    } else
                    {
                        return true;
                    }
                }
                var ls = userIdToProxyData[e.Author.Id];
                foreach(var x in ls)
                {
                    if (m.Trim().ToLower().StartsWith(x.prefix))
                    {
                        var sendmsg = m.Substring(x.prefix.Length);
                        if (sendmsg == "")
                            continue;
                        return await replaceMsg(e, x, sendmsg.TrimStart(), true);
                    }
                }
                if (!e.isProxy) {   
                    if (chanIDToAutoProxies.ContainsKey(settingsChannelId))
                    {
                        AutoProxies found = null;
                        foreach (var ap in chanIDToAutoProxies[settingsChannelId])
                        {
                            if (ap.userId == e.Author.Id)
                            {
                                var proxies = userIdToProxyData[e.Author.Id];
                                foreach (var p in proxies)
                                {
                                    if (ap.proxyId == p.proxyId)
                                    {
                                        return await replaceMsg(e, p, m, true);
                                    }
                                }
                                found = ap;
                                await server.safeSendMessage(e.Channel, e.Author.Mention + " autoproxy refers to deleted proxy, autoproxy setting has been removed.");
                                break;
                            }
                        }
                        //we should only be able to get here if we found a autoproxy setup that has no connection anymore
                        if (found != null)
                            chanIDToAutoProxies[settingsChannelId].Remove(found);
                    }
                    if (channelsInRPMode.ContainsKey(settingsChannelId))
                    {
                        var forcedProxy = ls.First(x => x.isRP);
                        return await replaceMsg(e, forcedProxy, m, true);
                    }
                }
                return true;
            };

            server.MessageRecieved += f;

            server.ChannelCreated += async (s, sgc) =>
            {
                if (chanIdToWebhookId.Count > 0)
                {
                    if (sgc.GetType() == typeof(SocketTextChannel))
                    {
                        await makeHook((SocketTextChannel)sgc);
                    }
                }
            };
        }

        ulong toRootSettingsChannelId(ISocketMessageChannel chan)
        {
            var settingsChannel = chan;
            var stc = chan as SocketThreadChannel;
            if (stc != null)
            {
                return stc.ParentChannel.Id;
            }
            return settingsChannel.Id;
        }

        async Task<bool> replaceMsg(ServerMessage e, ProxyData x, string sendmsg, bool triggerCommands)
        {
            Discord.IChannel settingsChannel = e.Channel;
            var stc = e.Channel as SocketThreadChannel;
            if (stc != null)
            {
                settingsChannel = stc.ParentChannel;
            }

            string name = x.name;
            if (x.isRP)
            {
                name += " (";
                if (x.specialNameInRPMode != null)
                {
                    name += x.specialNameInRPMode;
                }
                else
                {
                    name += e.Author.DisplayName;
                }
                name += ")";
            }
            if (x.isRP && (!channelsInRPMode.ContainsKey(settingsChannel.Id) || !channelsInRPMode[settingsChannel.Id]))
            {
                return true;
            }
            else if (!x.isRP && (channelsInRPMode.ContainsKey(settingsChannel.Id) && channelsInRPMode[settingsChannel.Id]))
            {
                await server.safeSendMessage(e.Channel, e.Author.Mention + " Use of non-RP proxy in a RP channel is impossible.");
            }
            else
            {
                ulong? tId = stc?.Id;
                var mId = await chanIdToWebhook[settingsChannel.Id].SendMessageAsync(sendmsg, username: name, avatarUrl: x.image,threadId: tId, embeds: e.msg.Embeds);
                if (triggerCommands)
                {
                    var newMsg = await e.Channel.GetMessageAsync(mId);
                    var eClone = new ServerMessage(e.Server, e.isDM, true, e.Channel, newMsg as Discord.WebSocket.SocketUserMessage, e.Author);
                    await server.messageRecieved(eClone);
                }
            }
            try
            {
                var delReq = new Discord.RequestOptions();
                delReq.RetryMode = Discord.RetryMode.AlwaysRetry;
                await e.msg.DeleteAsync(delReq);
                return false;
            }
            catch {}
            return true;
        }

        public override List<Command> getCommands()
        {
            var ret = new List<Command>();
            ret.Add(Command.OwnerCommand(server, "initProxy", initProxy, new HelpMsgStrings("", "")));
            if(chanIdToWebhookId.Count > 0)
            {
                ret.AddRange(specialCommands());
            }
            return ret;
        }

        private List<Command> specialCommands()
        {
            var ret = new List<Command>();
            ret.Add(new Command(server, "addProxy", addProxy, PrivilegeLevel.Everyone, new HelpMsgStrings("", "prefix replacename")));
            ret.Add(new Command(server, "removeProxy", removeProxy, PrivilegeLevel.Everyone, new HelpMsgStrings("", "prefix/replacename")));
            ret.Add(new Command(server, "setProxyImage", setImage, PrivilegeLevel.Everyone, new HelpMsgStrings("", "prefix/replacename image")));
            ret.Add(new Command(server, "autoproxy", autoproxy, PrivilegeLevel.Everyone, new HelpMsgStrings("used in channel to set an autoproxy, use without proxy name to remove autoproxy", "prefix/replacename")));
            ret.Add(Command.AdminCommand(server, "rpMode", rpMode, new HelpMsgStrings("", "")));
            ret.Add(Command.AdminCommand(server, "channelRPMode", channelRPMode, new HelpMsgStrings("", "")));
            //            ret.Add(new Command(server, "deleteLastProxyMsg", deleteLastMsg, PrivilegeLevel.Everyone, new HelpMsgStrings("Delete the last msg you proxied in this channel. Needs to be within the last 100 msgs.", "prefix/replacename")));
            ret.Add(new Command(server, "listProxies", listProxies, PrivilegeLevel.Everyone, new HelpMsgStrings("list your currently set proxies", " ")));
            return ret;
        }

        private async Task channelRPMode(ServerMessage e, string msg)
        {
            var settingsChannelId = toRootSettingsChannelId(e.Channel);
            if (channelsInRPMode.ContainsKey(settingsChannelId))
            {
                channelsInRPMode.Remove(settingsChannelId);
                await server.safeSendMessage(e.Channel, "Channel is now not in RP Mode anymore");
            }
            else
            {
                channelsInRPMode.Add(settingsChannelId, true);
                await server.safeSendMessage(e.Channel, "Channel is now in RP Mode");
            }
            channelsInRPMode.persist();
        }

        private async Task rpMode(ServerMessage e, string msg)
        {
            var newMode = !isInRPMode.get();
            isInRPMode.set(newMode);
            await server.safeSendMessage(e.Channel, newMode ? "new proxy registrations will now be RP Proxies." : "new proxy registration will now be normal Proxies.");
        }

        private async Task autoproxy(ServerMessage e, string msg)
        {
            var remove = msg.Trim() == "";
            var idx = getEntryIndex(e, msg);
            if(!remove && idx == -1)
            {
                await server.safeSendMessage(e.Channel, "proxy not found");
                return;
            }
            var settingsChannelId = toRootSettingsChannelId(e.Channel);

            if (!chanIDToAutoProxies.ContainsKey(settingsChannelId))
            {
                if(remove)
                {
                    await server.safeSendMessage(e.Channel, "no autoproxy to remove");
                    return;
                }
                chanIDToAutoProxies.Add(settingsChannelId, new List<AutoProxies>());
            }
            var ls = chanIDToAutoProxies[settingsChannelId];
            if(remove)
            {
                AutoProxies pd = null;
                foreach(var x in ls)
                {
                    if(x.userId == e.Author.Id)
                    {
                        pd = x;
                        break;
                    }
                }
                if (pd != null) {
                    ls.Remove(pd);
                    await server.safeSendMessage(e.Channel, "autoproxy removed");
                }
                else
                {
                    await server.safeSendMessage(e.Channel, "no autoproxy to remove");
                }
            }
            else
            {
                AutoProxies pd = null;
                foreach (var x in ls)
                {
                    if (x.userId == e.Author.Id)
                    {
                        pd = x;
                        break;
                    }
                }
                var retMsg = "";
                if (pd != null)
                {
                    ls.Remove(pd);
                    retMsg = "old autoproxy removed\n";
                }
                var pdNew = new AutoProxies();
                pdNew.proxyId = userIdToProxyData[e.Author.Id][idx].proxyId;
                pdNew.userId = e.Author.Id;
                ls.Add(pdNew);
                retMsg += "added new autoproxy";
                await server.safeSendMessage(e.Channel, retMsg);

            }
            chanIDToAutoProxies.persist();
        }

        private async Task setImage(ServerMessage e, string msg)
        {
            var msgSplit = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if(msgSplit.Length != 2)
            {
                await server.safeSendMessage(e.Channel, "need exactly 2 parameters in command");
                return;
            }
            var idx = getEntryIndex(e, msgSplit[0]);
            if (idx == -1)
            {
                await server.safeSendMessage(e.Channel, "could not update image - could not find entry as either prefix or name");
            }
            else if(!msgSplit[1].ToLower().StartsWith("http"))
            {
                await server.safeSendMessage(e.Channel, "could not update image - invalid url");
            } else {
                userIdToProxyData[e.Author.Id][idx].image = msgSplit[1];
                userIdToProxyData.persist();
                await server.safeSendMessage(e.Channel, "updated image for " + userIdToProxyData[e.Author.Id][idx].name);
            }
        }

        private async Task addProxy(ServerMessage e, string msg)
        {
            var msgSplit = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if(msgSplit.Length != 2)
            {
                await server.safeSendMessage(e.Channel,"could not parse into 2 parts");
                return;
            }
            Console.WriteLine(msg);
            var prefix = msgSplit[0];
            var replacename = msgSplit[1];
            if(!userIdToProxyData.ContainsKey(e.Author.Id))
            {
                userIdToProxyData.Add(e.Author.Id, new List<ProxyData>());
            }
            ProxyData pd;
            var prefMod = prefix.Trim().ToLower();
            if(prefMod.StartsWith('!'))
            {
                await server.safeSendMessage(e.Channel, "Proxy replacements can not start with !");
                return;
            }
            var nameMod = replacename.Trim();
            var idx = userIdToProxyData[e.Author.Id].FindIndex(x => x.prefix == prefMod);
            if (idx != -1) {
                pd = userIdToProxyData[e.Author.Id][idx];
                pd.name = nameMod;
            } else
            {
                pd = new ProxyData();
                pd.prefix = prefMod;
                pd.name = nameMod;
                pd.image = null;
                pd.color = null;
                pd.isRP = isInRPMode.get();
                pd.specialNameInRPMode = null; //TODO: ability to set
                pd.proxyId = uniqueProxyId.get();
                uniqueProxyId.set(pd.proxyId + 1);
            }
            userIdToProxyData[e.Author.Id].Add(pd);
            userIdToProxyData.persist();
            await server.safeSendMessage(e.Channel, "Added Proxy: Replacing messages with '"+ prefMod + "' with messages from "+ nameMod);
        }

        private int getEntryIndex(ServerMessage e, string msg)
        {
            var msgMod = msg.Trim().ToLower();
            return userIdToProxyData[e.Author.Id].FindIndex(x => x.prefix == msgMod || x.name.ToLower() == msgMod);
        }

        private async Task removeProxy(ServerMessage e, string msg)
        {
            if (!userIdToProxyData.ContainsKey(e.Author.Id))
            {
                await server.safeSendMessage(e.Channel, "You have no proxy to remove");
                return;
            }
            var idx = getEntryIndex(e, msg);
            if(idx != -1)
            {
                var entry = userIdToProxyData[e.Author.Id][idx];
                var retmsg = "Removing prefix name mapping: '" + entry.prefix + "' => " + entry.name;
                userIdToProxyData[e.Author.Id].RemoveAt(idx);
                userIdToProxyData.persist();
                await server.safeSendMessage(e.Channel, retmsg);
            }
            else
            {
                await server.safeSendMessage(e.Channel, "unable to remove - could not find it as either prefix or name");
            }
        }

        private async Task deleteLastMsg(ServerMessage e, string msg)
        {
            if (!userIdToProxyData.ContainsKey(e.Author.Id))
            {
                await server.safeSendMessage(e.Channel, "You have no proxy to remove last msg from");
                return;
            }

            var idx = getEntryIndex(e, msg);
            if(idx != -1)
            {
                var msgs = new List<Discord.IMessage>();
                var asyncMsgs = e.Channel.GetMessagesAsync();
                await foreach(var msgList in asyncMsgs)
                {
                    msgs.AddRange(msgList.Where(x => x.Source == Discord.MessageSource.Webhook));
                }
                msgs.Sort((l,r) => l.Timestamp == r.Timestamp ? 0 : l.Timestamp > r.Timestamp ? -1 : 1); //sort backwards
                var proxy = userIdToProxyData[e.Author.Id][idx];
                foreach (var msg2 in msgs)
                {
                    if (msg2.Author.IsWebhook && ((!proxy.isRP && msg2.Author.Username == proxy.name) || (proxy.isRP && msg2.Author.Username.StartsWith(proxy.name + " ("))))
                    {
                        try
                        {
                            await msg2.DeleteAsync();
                            if (!chanIDToAutoProxies[e.Channel.Id].Where(x => x.userId == e.Author.Id).Any())
                            {
                                await e.msg.DeleteAsync();
                            }
                        }
                        catch { }
                        break;
                    }
                }
            }
            else
            {
                await server.safeSendMessage(e.Channel, "Unable to find msg to delete");
            }

        }

        private async Task makeHook(SocketGuildChannel c)
        {
            Console.WriteLine("Adding " + c.Name);
            Discord.Rest.RestWebhook hook;
            if (c is SocketTextChannel stc)
            {
                hook = await stc.CreateWebhookAsync("borkbothook");
            }else if(c is SocketForumChannel sfc)
            {
                hook = await sfc.CreateWebhookAsync("borkbothook");
            }
            else
            {
                throw new Exception("type error: " + c);
            }
            chanIdToWebhookId.Add(c.Id, hook.Id);
            if (booted)
            {
                chanIdToWebhook.Add(c.Id, new DiscordWebhookClient(hook));
                chanIdToWebhookId.persist();
            }
        }

        private async Task initProxy(ServerMessage e, string msg)
        {
            var s = server.getServer();
            uint newHooks = 0;
            uint totalHooks = 0;
            foreach (var c_ in s.TextChannels)
            {
                SocketGuildChannel c = c_;
                while (c is SocketThreadChannel stc)
                {
                    if(stc.ParentChannel is SocketTextChannel pstc)
                    {
                        Console.WriteLine("Replacing " + c.Name + " with parent " + pstc.Name);
                        c = pstc;
                    }
                    if(stc.ParentChannel is SocketForumChannel psfc)
                    {
                        Console.WriteLine("Replacing " + c.Name + " with parent forum " + psfc.Name);
                        c = psfc;
                    }
                }
                //todo: remove the bypass for managewebhooks check once discord.net has the permission check fix for forum channels built in
                if (!chanIdToWebhookId.ContainsKey(c.Id) && ((c is SocketForumChannel) || s.CurrentUser.GetPermissions(c).ManageWebhooks))
                {
                    //                    if (c as SocketVoiceChannel != null)
                    //                        continue;
                    try
                    {
                        Console.WriteLine("Checking " + c.Name);
                        IReadOnlyCollection<Discord.Rest.RestWebhook> existingHooks;
                        if (c is SocketTextChannel stc) {
                            existingHooks = await stc.GetWebhooksAsync();
                        }else if(c is SocketForumChannel sfc)
                        {
                            existingHooks = await sfc.GetWebhooksAsync();
                        }
                        else
                        {
                            continue;
                        }
                        bool found = false;
                        foreach (var eh in existingHooks)
                        {
                            if (eh.Creator.Id == server.DC.CurrentUser.Id)
                            {
                                chanIdToWebhookId.Add(c.Id, eh.Id);
                                totalHooks++;
                                found = true;
                                break;
                            }
                        }
                        if (found) continue;
                        await makeHook(c);
                        newHooks++;
                        totalHooks++;
                    } catch (Exception ex)
                    {
                        Console.WriteLine("Unable to make webhook for " + c.Name + " error: " + ex);
                    }
                }else if (chanIdToWebhookId.ContainsKey(c.Id))
                {
                    totalHooks++;
                }
            }
            chanIdToWebhookId.persist();

            var sc = specialCommands();
            foreach(var com in sc)
            {
                if (!server.Commandlist.ContainsKey(com.name.ToLower()))
                {
                    server.Commandlist.Add(com.name.ToLower(), com);
                }
            }


            string proxyusable = totalHooks > 0 ? "Proxy system now usable on this server." : "Bot needs Manage Webhooks permission in at least one channel to use proxy system.";
            await server.safeSendMessage(e.Channel, "Added "+newHooks+" channels to proxy system, for a total of "+totalHooks+" channels integrated. "+ proxyusable);
        }

        private async Task listProxies(ServerMessage e, string msg)
        {
            if (!userIdToProxyData.ContainsKey(e.Author.Id))
            {
                await server.safeSendMessage(e.Channel, "You have no proxies set up, use !äddproxy to add one");
                return;
            }
            var proxies = userIdToProxyData[e.Author.Id];
            var response = proxies.Select(x => "prefix: " + x.prefix + ", name: " + x.name + ", imageurl: " + x.image + ", isRP: " + x.isRP).Aggregate((a,b) => a+"\n"+b);
            await server.safeSendMessage(e.Channel, response);
        }

    }
}
