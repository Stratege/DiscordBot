using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;


namespace borkbot
{
    enum PrivilegeLevel { BotOwner, BotAdmin, Everyone };

    class HelpMsgStrings
    {
        string description;
        string format;
        List<string> arguments;

        public HelpMsgStrings(string _description, string _format)
        {
            description = _description;
            format = _format;
            arguments = new List<string>();
        }

        public HelpMsgStrings addArg(string arg)
        {
            arguments.Add(arg);
            return this;
        }

        public string getFormat()
        {
            return format;
        }

        public string getDescription()
        {
            return description;
        }

        public List<string> getArguments()
        {
            return arguments;
        }

   }

    class Command
    {
        static ulong botOwnerId = 94642692868288512; //todo: turn into a configurable config file option
        VirtualServer server;
        public string name;
        PrivilegeLevel priv;
        Action<ServerMessage, string> cmd;
        public HelpMsgStrings helpmessage;

        public Command(VirtualServer _server, string _name, Action<ServerMessage, string> _cmd, PrivilegeLevel _priv, HelpMsgStrings _helpmessage)
        {
            server = _server;
            name = _name;
            helpmessage = _helpmessage;
            priv = _priv;
            cmd = _cmd;
        }

        public void invoke(ServerMessage e, string m)
        {
            if (checkPrivilege(e.Author, e.Channel))
            {
                Console.WriteLine("Invoking " + m);
                cmd(e, m);
            }
            else
                Console.WriteLine(e.Author.Username + " has insufficient privileges for " + m);

        }

        public bool checkPrivilege(SocketUser u, ISocketMessageChannel c)
        {
            return (priv == PrivilegeLevel.BotOwner && u.Id == botOwnerId) || (priv == PrivilegeLevel.BotAdmin && server.isAdmin(u,c)) || priv == PrivilegeLevel.Everyone;
        }

        public static Command AdminCommand(VirtualServer _server, string _name, Action<ServerMessage, String> _cmd, HelpMsgStrings _helpmessage)
        {
            return new Command(_server,_name,_cmd, PrivilegeLevel.BotAdmin, _helpmessage);
        }

        public static Command OwnerCommand(VirtualServer _server, string _name, Action<ServerMessage, String> _cmd, HelpMsgStrings _helpmessage)
        {
            return new Command(_server, _name, _cmd, PrivilegeLevel.BotOwner, _helpmessage);
        }

        public Discord.Embed getHelpMessageEmbed()
        {
            var eb = new Discord.EmbedBuilder().WithAuthor("The Overbork", server.DC.CurrentUser.GetAvatarUrl()).WithCurrentTimestamp();
            eb.WithTitle(name).WithDescription(helpmessage.getDescription()).AddField("Format", helpmessage.getFormat(),true).AddField("Permission Required", priv == PrivilegeLevel.Everyone ? "None" : "Bot Admin",true);
            var args = helpmessage.getArguments().SelectMany(x => x + "\n").ToArray();
            eb.AddField("Arguments", args.Length == 0 ? "None" : new String(args));
            /*            const int maxFieldSize = 1024;
                        for (int i = 0; i < ((botInfo.Length + (maxFieldSize - 1)) / maxFieldSize); i++)
                        {
                            int remLen = botInfo.Length - i * maxFieldSize;
                            int len = remLen < maxFieldSize ? remLen : maxFieldSize;
                            eb = eb.AddField("help" + i, botInfo.Substring(i * maxFieldSize, len));
                        }
                        safeSendEmbed(e.Channel, eb.Build());*/
            return eb.Build();
        }
    }
}
