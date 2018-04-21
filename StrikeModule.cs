using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace borkbot
{
    /// <summary>
    /// Represents a the strike command module, where admins can give users strikes, resolve strikes, and users can print strikes
    /// If there are 3 or more unresolved strikes, Borkbot should notify the admins in the set channel of that particular user.
    /// </summary>
    class StrikeModule : EnableableCommandModule
    {
        #region Data Members

        private PersistantDict<ulong, List<StrikeDetails>> _userStrikes;

        private SocketTextChannel _strikeChannel;

        private string _channelFile = "StrikeChannel.txt";

        #endregion

        #region Properties

        internal PersistantDict<ulong, List<StrikeDetails>> UserStrikes
        {
            get
            {
                return _userStrikes;
            }

            set
            {
                _userStrikes = value;
            }
        }

        protected SocketTextChannel StrikeChannel
        {
            get
            {
                return _strikeChannel;
            }

            set
            {
                _strikeChannel = value;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="_server"></param>
        public StrikeModule(VirtualServer _server) : base(_server, "strike")
        {
            UserStrikes = PersistantDict<ulong, List<StrikeDetails>>.load(_server, "UserStrikes.xml");

            // Get the strike channel to print admin alerts in
            var res = server.FileSetup(_channelFile);
            if (res.Count > 0)
            {
                StrikeChannel = server.getServer().TextChannels.FirstOrDefault(x => x.Name == res[0]);
            }
        }

        #endregion

        /// <summary>
        /// Gets all the available commands from this module into the command list
        /// This includes dynamically created commands by role names
        /// </summary>
        /// <returns></returns>
        public override List<Tuple<string, Command>> getCommands()
        {
            var commands = base.getCommands();
            commands.Add(new Tuple<string, Command>("strike", new Command(server, StrikeUser, PrivilegeLevel.BotAdmin, "strike <user tag> <reason>")));
            commands.Add(new Tuple<string, Command>("setstrikechannel", new Command(server, SetStrikeChannel, PrivilegeLevel.BotAdmin, "setstrikechannel <channel name>")));
            commands.Add(new Tuple<string, Command>("printallstrikes", new Command(server, PrintAllStrikes, PrivilegeLevel.BotAdmin, "printallstrikes")));
            commands.Add(new Tuple<string, Command>("resolveallstrikes", new Command(server, ResolveAllStrikes, PrivilegeLevel.BotAdmin, "resolveallstrikes")));
            commands.Add(new Tuple<string, Command>("resolvestrike", new Command(server, ResolveStrike, PrivilegeLevel.BotAdmin, "resovlestrike <user tag> <strike number> <reason>")));
            commands.Add(new Tuple<string, Command>("printstrikes", new Command(server, PrintStrikes, PrivilegeLevel.Everyone, "printstrikes")));
            return commands;
        }

        #region Admin Commands

        /// <summary>
        /// Resolves a specific strike
        /// </summary>
        /// <param name="e"></param>
        /// <param name="message"></param>
        private void ResolveStrike(ServerMessage e, string message)
        {
            if (!on)
                return;

            string[] split = message.Split(new char[] { ' ' }, 3);

            if (split.Length == 3 && Funcs.GetUserByMentionOrName(e.Server.Users, split[0]) != null)
            {
                var user = Funcs.GetUserByMentionOrName(e.Server.Users, split[0]);
                var userName = user.Nickname ?? user.Username;


                // If the user doesn't have strikes to resolve, print a response
                int strikeID;
                if (!UserStrikes.ContainsKey(user.Id) || !int.TryParse(split[1], out strikeID) || UserStrikes[user.Id][strikeID - 1].Resolved)
                {
                    server.safeSendMessage(e.Channel, userName + " does not have any strikes to resolve.");
                }
                else
                {
                    UserStrikes[user.Id][strikeID - 1].Resolved = true;
                    UserStrikes[user.Id][strikeID - 1].ResolveReason = split[2];
                    UserStrikes.persist();
                    server.safeSendMessage(e.Channel, userName + "'s strike has been resolved.");
                }
            }
        }

        /// <summary>
        /// Resolves all strikes a specific user has, with the same reason
        /// </summary>
        /// <param name="e"></param>
        /// <param name="m"></param>
        private void ResolveAllStrikes(ServerMessage e, string message)
        {
            if (!on)
                return;

            string[] split = message.Split(new char[] { ' ' }, 2);

            if (split.Length == 2 && Funcs.GetUserByMentionOrName(e.Server.Users, split[0]) != null)
            {
                var user = Funcs.GetUserByMentionOrName(e.Server.Users, split[0]);
                var userName = user.Nickname ?? user.Username;

                // If the user doesn't have strikes to resolve, print a response
                if (!UserStrikes.ContainsKey(user.Id) || UserStrikes[user.Id].All((s) => s.Resolved))
                {
                    server.safeSendMessage(e.Channel, userName + " does not have any strikes to resolve.");
                }
                else
                {
                    foreach (var strike in UserStrikes[user.Id].Where((s) => !s.Resolved))
                    {
                        strike.Resolved = true;
                        strike.ResolveReason = split[1];
                    }

                    UserStrikes.persist();

                    server.safeSendMessage(e.Channel, "All of " + userName + "'s unresolved strikes have been resolved.");
                }
            }
        }

        /// <summary>
        /// Sets the strike alert channel when a user receives more than 3 strikes
        /// </summary>
        /// <param name="e"></param>
        /// <param name="m"></param>
        private void SetStrikeChannel(ServerMessage e, string m)
        {
            if (m.Length > 0 && m[0] == '#')
                m = m.Substring(1);
            var res = e.Server.TextChannels.FirstOrDefault(x => x.Name == m);
            string message;
            if (res == null)
                message = "Could not find channel: " + m;
            else
            {
                message = "Channel was set to: " + res.Name;
                StrikeChannel = res;
                server.fileCommand(_channelFile, x => System.IO.File.WriteAllText(x, res.Name));
            }
            server.safeSendMessage(e.Channel, message);
        }

        /// <summary>
        /// Gives a strike to a mentioned user with a specific reason
        /// </summary>
        /// <param name="e"></param>
        /// <param name="message"></param>
        private void StrikeUser(ServerMessage e, string message)
        {
            if (!on)
                return;

            string[] split = message.Split(new char[] { ' ' }, 2);

            if (split.Length == 2 && Funcs.GetUserByMentionOrName(e.Server.Users, split[0]) != null)
            {
                var user = Funcs.GetUserByMentionOrName(e.Server.Users, split[0]);
                var userName = user.Nickname ?? user.Username;

                var strike = new StrikeDetails()
                {
                    StrikeDate = new DateTimeOffset(DateTime.Now).ToString(),
                    Reason = split[1]
                };

                // If the user didn't have any previous strikes, create an entry for them
                if (!UserStrikes.ContainsKey(user.Id))
                {
                    var strikes = new List<StrikeDetails>() { strike };

                    UserStrikes.Add(user.Id, strikes);
                    UserStrikes.persist();
                }
                // The user already has an entry, add the strike to the others
                else
                {
                    UserStrikes[user.Id].Add(strike);
                    UserStrikes.persist();

                    // Alert for 3 or more unresolved strikes
                    if (UserStrikes[user.Id].Count((s) => !s.Resolved) >= 3)
                    {
                        server.safeSendMessage(StrikeChannel, e.Server.EveryoneRole + " " + userName + " has been given their third (or more) strike in " + (e.Channel as SocketTextChannel).Mention + ".\n Reason: " + strike.Reason);
                    }
                }

                server.safeSendMessage(e.Channel, "The user " + userName + " has been given a strike for the following reason: " + split[1]);
            }
            else
            {
                server.safeSendMessage(e.Channel, "Invalid format! Please use ``!strike <user mention> <reason>");
            }
        }

        /// <summary>
        /// Prints all the strikes everyone has 
        /// </summary>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        private void PrintAllStrikes(ServerMessage e, string message)
        {
            if (!on)
                return;

            string allStrikes = "Here are the following strikes for the following users:\n";

            // Go over all the users with strikes
            foreach (var userID in UserStrikes.Keys)
            {
                var user = server.getServer().GetUser(userID);
                var userName = user?.Nickname ?? user?.Username ?? userID.ToString();

                allStrikes += "**__" + userName + ":__**\n";

                // Go over all the strikes for each user
                allStrikes += PrintStrikesNicely(UserStrikes[userID]);
            }

            server.safeSendMessage(e.Channel, allStrikes);
        }

        #endregion

        #region User Commands

        /// <summary>
        /// Print the user's strikes
        /// </summary>
        /// <param name="e"></param>
        /// <param name="message"></param>
        private void PrintStrikes(ServerMessage e, string message)
        {
            if (!on)
                return;

            var channel = e.Author.GetOrCreateDMChannelAsync().Result as SocketDMChannel;

            // GetOrCreateDMChannelAsync returns null the first time always, for some reason.
            // This is why we're waiting a second, and then calling it a second time. Kind of weird.
            // If you can find a way to make it work the first time, that would be great.
            Task.Delay(1000).ContinueWith((a) =>
            {
                channel = e.Author.GetOrCreateDMChannelAsync().Result as SocketDMChannel;

                // Print the appropriate response
                if (!UserStrikes.ContainsKey(e.Author.Id))
                {

                    server.safeSendMessage(channel, "You do not have any strikes at the present moment.\nGood job! Please continue to be kind, unwavering, and awesome!");
                }
                else
                {
                    var strikes = "Here is a list of all your strikes:\n";
                    strikes += PrintStrikesNicely(UserStrikes[e.Author.Id]);
                    server.safeSendMessage(channel, strikes);
                }
            });
        }

        #endregion

        #region Other Methods

        /// <summary>
        /// Beautify strikes for printing
        /// Pretty sure I can do this with just LINQ, but eh
        /// </summary>
        /// <param name="strikes"></param>
        /// <returns></returns>
        private string PrintStrikesNicely(List<StrikeDetails> strikes)
        {
            var printedStrikes = string.Empty;
            var counter = 0;

            foreach (var strike in strikes)
            {
                counter++;
                printedStrikes += "**" + counter + ".** " + strike + "\n";
            }

            return printedStrikes;
        }

        #endregion

    }

    [Serializable]
    public class StrikeDetails
    {
        #region Data Members

        private DateTimeOffset _strikeDate;

        private string _reason;

        private bool _resolved;

        private string _resolveReason;

        #endregion

        #region Properties

        // This is a string because XmlSerializer doesn't know how to serialize DateTimeOffset objects
        public string StrikeDate
        {
            get
            {
                return _strikeDate.ToString();
            }

            set
            {
                _strikeDate = DateTimeOffset.Parse(value);
            }
        }

        public string Reason
        {
            get
            {
                return _reason;
            }

            set
            {
                _reason = value;
            }
        }

        public bool Resolved
        {
            get
            {
                return _resolved;
            }

            set
            {
                _resolved = value;
            }
        }

        public string ResolveReason
        {
            get
            {
                return _resolveReason;
            }

            set
            {
                _resolveReason = value;
            }
        }

        #endregion

        #region Override Methods

        public override string ToString()
        {
            return Reason + "\n    Date: " + StrikeDate + "\n    Resolved? " + Resolved + "\n    Resolve Reason: " + ResolveReason + "\n";
        }

        #endregion
    }
}
