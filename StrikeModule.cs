using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;


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

        private uint daysTillAutoresolve;
        private string daysTillAutoresolveFile = "StrikeAutoresolve.txt";

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
            var time = server.FileSetup(daysTillAutoresolveFile);
            if(time.Count == 1 && UInt32.TryParse(time[0],out daysTillAutoresolve))
            {
                cleanupStrikes();
            }else
            {
                daysTillAutoresolve = 0;
            }
        }

        #endregion

        /// <summary>
        /// Gets all the available commands from this module into the command list
        /// This includes dynamically created commands by role names
        /// </summary>
        /// <returns></returns>
        public override List<Command> getCommands()
        {
            var commands = base.getCommands();
            commands.Add(makeEnableableAdminCommand("strike", StrikeUser, new HelpMsgStrings("", "strike <user tag> <reason>")));
            commands.Add(makeEnableableAdminCommand("warning", StrikeUser, new HelpMsgStrings("", "warning <user tag> <reason>")));
            commands.Add(makeEnableableAdminCommand("setstrikechannel", SetStrikeChannel, new HelpMsgStrings("", "setstrikechannel <channel name>")));
            commands.Add(makeEnableableAdminCommand("printallstrikes", PrintAllStrikes, new HelpMsgStrings("lists all unresolved strikes by default, if one adds \"all\" as a param it lists even the resolved ones", "printallstrikes <optional:\"all\">")));
            commands.Add(makeEnableableAdminCommand("resolveallstrikes", ResolveAllStrikes, new HelpMsgStrings("", "resolveallstrikes")));
            commands.Add(makeEnableableAdminCommand("resolvestrike", ResolveStrike, new HelpMsgStrings("", "resolvestrike <user tag> <strike number> <reason>")));
            commands.Add(makeEnableableAdminCommand("setautoresolvetime", SetAutoResolveTime, new HelpMsgStrings("sets the time after which a strike gets automatically marked as resolved", "setautoresolvetime <number as days>")));
            commands.Add(makeEnableableCommand("printstrikes", PrintStrikes, PrivilegeLevel.Everyone, new HelpMsgStrings("", "printstrikes")));
            return commands;
        }

        private void cleanupStrikes()
        {
            if (daysTillAutoresolve == 0)
                return;
            var resolveTime = DateTimeOffset.Now - TimeSpan.FromDays(daysTillAutoresolve);
            var resolveString = "Autoresolve after " + daysTillAutoresolve.ToString() + " days.";
            foreach (var userId in UserStrikes.Keys)
            {
                foreach (var strike in UserStrikes[userId].Where((s) => !s.Resolved && s._strikeDate < resolveTime))
                {
                    strike.Resolved = true;
                    strike.ResolveReason = resolveString;
                }
            }
            UserStrikes.persist();
            Timer t = new Timer((TimeSpan.FromDays(daysTillAutoresolve)).TotalMilliseconds);
            var daysOld = daysTillAutoresolve;
            t.Elapsed += (a, b) =>
            {
                if (t != null)
                {
                    t.Stop();
                    t.Dispose();
                }
                if(daysOld == daysTillAutoresolve) //only restarts timer if nothing else has been messing with this
                    cleanupStrikes();
            };
            t.AutoReset = false;
            t.Start();

        }

        #region Admin Commands

        private void SetAutoResolveTime(ServerMessage e, string msg)
        {
            uint days;
            if(UInt32.TryParse(msg, out days))
            {
                if (days == daysTillAutoresolve)
                {
                    server.safeSendMessage(e.Channel, "period was already at " + daysTillAutoresolve + " days");
                }
                else
                {
                    daysTillAutoresolve = days;
                    server.fileCommand(_channelFile, x => System.IO.File.WriteAllText(x, daysTillAutoresolve.ToString()));
                    if (daysTillAutoresolve > 0)
                        cleanupStrikes();
                    server.safeSendMessage(e.Channel, "successfully set period to " + daysTillAutoresolve + " days");
                }
            }else
            {
                server.safeSendMessage(e.Channel, "unable to parse daycount");
            }
        }

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

            if (split.Length == 3 && Funcs.GetUserByMentionOrName(server.getServer().Users, split[0]) != null)
            {
                var user = Funcs.GetUserByMentionOrName(server.getServer().Users, split[0]);
                var userName = user.Nickname ?? user.Username;

                int strikeID;
                // Print an appropriate response to each scenario
                if (!UserStrikes.ContainsKey(user.Id))
                {
                    server.safeSendMessage(e.Channel, userName + " does not have any strikes to resolve.");
                }
                else if (!int.TryParse(split[1], out strikeID))
                {
                    server.safeSendMessage(e.Channel, "Could not parse the strike number to resolve.");
                }
                else if (strikeID < 1 || strikeID > UserStrikes[user.Id].Count)
                {
                    server.safeSendMessage(e.Channel, "There are no strikes that correspond to that number.");
                }
                else if (UserStrikes[user.Id][strikeID - 1].Resolved)
                {
                    server.safeSendMessage(e.Channel, "That specific strike has been resolved already.");
                }
                // Resolve the strike
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

            if (split.Length == 2 && Funcs.GetUserByMentionOrName(server.getServer().Users, split[0]) != null)
            {
                var user = Funcs.GetUserByMentionOrName(server.getServer().Users, split[0]);
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
            var res = server.getServer().TextChannels.FirstOrDefault(x => x.Name == m);
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

            if (split.Length == 2 && Funcs.GetUserByMentionOrName(server.getServer().Users, split[0]) != null)
            {
                var user = Funcs.GetUserByMentionOrName(server.getServer().Users, split[0]);
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
                    if (UserStrikes[user.Id].Count((s) => !s.Resolved) >= 4)
                    {
                        server.safeSendMessage(StrikeChannel, server.getServer().EveryoneRole + " " + userName + " has been given their third (or more) strike in " + (e.Channel as SocketTextChannel).Mention + ".\n Reason: " + strike.Reason);
                    }
                }
                if (UserStrikes[user.Id].Count((s) => !s.Resolved) >= 2)
                {
                    server.safeSendMessage(e.Channel, "The user " + userName + " has been given a strike for the following reason: " + split[1]);
                }else
                {
                    server.safeSendMessage(e.Channel, "The user " + userName + "has been given a warning for the following reason: " + split[1]);
                }
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

            bool listResolved = message == "all";
            string allStrikes = "Here are the following strikes for the following users:\n";

            // Go over all the users with strikes
            foreach (var userID in UserStrikes.Keys)
            {
                var strikes = UserStrikes[userID];
                if(!listResolved)
                {
                    strikes = strikes.Where(x => !x.Resolved).ToList();
                    if (strikes.Count() == 0)
                        continue;
                }
                var user = server.getServer().GetUser(userID);
                var userName = user?.Nickname ?? user?.Username ?? userID.ToString();

                allStrikes += "**__" + userName + ":__**\n";

                // Go over all the strikes for each user
                allStrikes += PrintStrikesNicely(strikes);
            }

            server.safeSendMessage(e.Channel, allStrikes, true);
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
                if (!UserStrikes.ContainsKey(e.Author.Id) || UserStrikes[e.Author.Id].Count((s) => !s.Resolved) == 0)
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

            printedStrikes += "Unresolved strikes: " + strikes.Count((s) => !s.Resolved) + "\n";

            return printedStrikes;
        }

        #endregion

    }

    [Serializable]
    public class StrikeDetails
    {
        #region Data Members

        public DateTimeOffset _strikeDate;

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
            string strike = Reason + "\n    Date: " + StrikeDate + "\n    Resolved? " + Resolved + "\n    Resolve Reason: " + ResolveReason + "\n";

            // Bold the unresolved strikes
            if (!Resolved)
            {
                strike = "**" + strike + " **";
            }

            return strike;
        }

        #endregion
    }
}
