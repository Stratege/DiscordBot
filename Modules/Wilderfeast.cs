using DiscordBot.Persistance;
using DiscordBot.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    internal class Wilderfeast : EnableableCommandModule
    {
        PersistantDict<ulong, Dictionary<ulong, ulong>> gamesWithUserForUser;
        public Wilderfeast(VirtualServer _server) : base(_server, "Wilderfeast")
        {
            gamesWithUserForUser = PersistantDict<ulong, Dictionary<ulong, ulong>>.load(server, "wfGamesWithUserForUser");
        }

        public override List<Command> getCommands()
        {
            var ls = base.getCommands();
            ls.Add(new Command(server, "registerGame", registerGame, PrivilegeLevel.Everyone, new HelpMsgStrings("", "")));
            ls.Add(new Command(server, "checkGames", checkGames, PrivilegeLevel.Everyone, new HelpMsgStrings("", "")));
            return ls;
        }

        private async Task checkGames(ServerMessage message, string arg2)
        {
            var id = message.Author.Id;
            if (!gamesWithUserForUser.ContainsKey(id))
            {
                await server.safeSendMessage(message.Channel, "You have not yet played a game");
            } else
            {
                var d = gamesWithUserForUser[id];
                var s = server.getServer();
                var users = s.Users.Select(x => new KeyValuePair<ulong,string>(x.Id, x.Username)).ToDictionary();
                var ls = d.Select(x => (users.ContainsKey(x.Key) ? users[x.Key] : x.Key.ToString()) + " (" + x.Value + ")");
                await server.safeSendMessage(message.Channel, "You have played with the following people (times played): \n" + String.Join("\n", ls));
            }
        }

        private async Task registerGame(ServerMessage message, string arg2)
        {
            var users = message.msg.MentionedUsers().Where(x => !x.IsBot && !x.IsWebhook).ToList();
            List<(Discord.IUser,ulong)> lvlups = [];
            foreach(var user in users)
            {
                if(!gamesWithUserForUser.ContainsKey(user.Id))
                {
                    gamesWithUserForUser[user.Id] = [];
                }
                var dict = gamesWithUserForUser[user.Id];
                var countPre = dict.Count;
                foreach(var otherUser in users)
                {
                    if (user.Id == otherUser.Id) continue;
                    if(!dict.ContainsKey(otherUser.Id))
                    {
                        dict[otherUser.Id] = 1;
                    } else
                    {
                        dict[otherUser.Id]++;
                    }
                }
                var countPost = dict.Count;
                if (countPre < 2 && countPost >= 2) lvlups.Add((user, 2));
            }
            await server.safeSendMessage(message.Channel, "Game registered with players "+String.Join(", ",users.Select(x => x.Mention)), true);
            gamesWithUserForUser.persist();
            foreach(var lvlup in lvlups)
            {
                await server.safeSendMessage(message.Channel, lvlup.Item1.Mention + " has leveled up their breadth! Milestone hit: " + lvlup.Item2 + " players played with");
            }
        }
    }
}
