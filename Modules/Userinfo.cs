using DiscordBot.Utility;
using System.Collections.Generic;

namespace DiscordBot.Modules
{
    class Userinfo : CommandHandler
    {
        public Userinfo(VirtualServer _server) : base(_server)
        {
        }

        public override List<Command> getCommands()
        {
            var x = new List<Command>();
            x.Add(Command.AdminCommand(server, "userinfo", userinfo, new HelpMsgStrings("Provides all manners of interesting information about a user", "userinfo <usermention>")));
            return x;
        }

        private void userinfo(ServerMessage e, string m)
        {
            var user = Funcs.GetUserByMentionOrName(e.Server.Users, m);
            if(user != null) { 
                var eb = new Discord.EmbedBuilder().WithAuthor("The Overbork", server.DC.CurrentUser.GetAvatarUrl()).WithCurrentTimestamp();
                eb.WithTitle(user.Nickname + "(" + user.Username + ")").WithDescription(user.Mention);
                eb.AddField("Status", user.Status,true).AddField("Joined", user.JoinedAt, true).AddField("Hierarchy Position", user.Hierarchy, true).AddField("Registered", user.CreatedAt, true);
                var embed = eb.Build();
                server.safeSendEmbed(e.Channel, embed);
            }else
            {
                server.safeSendMessage(e.Channel,"Could not find user: " + m);
            }
        }
    }
}
