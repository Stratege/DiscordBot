using Discord;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace borkbot
{
    /// <summary>
    /// Represents an anonymous report system, capable of replying to reports and banning users from using them
    /// </summary>
    class ReportModule : EnableableCommandModule
    {
        #region Data Members

        private SocketTextChannel _reportChannel;

        private string _channelFile = "StrikeChannel.txt";

        private PersistantList _bannedUsers;

        private PersistantList _reportUsers;

        private readonly object _lock = new object();

        #endregion

        #region Properties

        public SocketTextChannel ReportChannel
        {
            get
            {
                return _reportChannel;
            }

            set
            {
                _reportChannel = value;
            }
        }

        public PersistantList BannedUsers
        {
            get
            {
                return _bannedUsers;
            }

            set
            {
                _bannedUsers = value;
            }
        }

        public PersistantList ReportUsers
        {
            get
            {
                return _reportUsers;
            }

            set
            {
                _reportUsers = value;
            }
        }


        #endregion

        #region Constructors

        public ReportModule(VirtualServer _server) : base(_server, "report")
        {
            ReportUsers = PersistantList.Create(server, "ReportUsers.txt");
            BannedUsers = PersistantList.Create(server, "ReportBannedUsers.txt");

            // Report channel to send reports to
            var res = server.FileSetup(_channelFile);
            if (res.Count > 0)
            {
                ReportChannel = server.getServer().TextChannels.FirstOrDefault(x => x.Name == res[0]);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Gets all the available commands from this module into the command list
        /// </summary>
        /// <returns></returns>
        public override List<Command> getCommands()
        {
            var commands = base.getCommands();
            commands.Add(makeEnableableCommand("report", Report, PrivilegeLevel.Everyone, new HelpMsgStrings("", "report <text>")));
            commands.Add(makeEnableableAdminCommand("setreportchannel", SetReportChannel, new HelpMsgStrings("", "setreportchannel <channel name>")));
            commands.Add(makeEnableableAdminCommand("reportreply", Reply, new HelpMsgStrings("", "reportreply <num> <text> - replies to the specified report")));
            commands.Add(makeEnableableAdminCommand("reportban", Ban, new HelpMsgStrings("", "reportban <num> - bans the reporting user from using the report system")));
            commands.Add(makeEnableableAdminCommand("reportunban", UnBan, new HelpMsgStrings("", "reportunban <num> - unbans the reporting user from using the report system")));
            return commands;
        }

        /// <summary>
        /// Gets an appropriate DM channel
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        private IMessageChannel GetDMChannel(SocketGuildUser user)
        {
            var channel = user.GetOrCreateDMChannelAsync().Result;

            // GetOrCreateDMChannelAsync returns null the first time always, for some reason.
            // This is why we're waiting a second, and then calling it a second time. Kind of weird.
            // If you can find a way to make it work the first time, that would be great.
            //Task.Delay(2000).ContinueWith((a) =>
            //{
            //    channel = user.GetOrCreateDMChannelAsync().Result;
            //}).Wait();

            return channel;
        }

        /// <summary>
        /// Sets the channel reports are sent to
        /// </summary>
        /// <param name="e"></param>
        /// <param name="m"></param>
        private void SetReportChannel(ServerMessage e, string m)
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
                ReportChannel = res;
                server.fileCommand(_channelFile, x => System.IO.File.WriteAllText(x, res.Name));
            }
            server.safeSendMessage(e.Channel, message);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sends an anonymous report to the set channel
        /// </summary>
        /// <param name="e"></param>
        /// <param name="report"></param>
        private void Report(ServerMessage e, string report)
        {
            // If user was banned, reply with a canned response
            if (BannedUsers.Contains(e.Author.Id.ToString()))
            {
                server.safeSendMessage(GetDMChannel(e.Author), "You have been banned from using the anonymous report system.\nPlease contact the moderators of the channel to dispute it.\nHave a great day!");
            }
            else
            {
                lock (_lock)
                {
                    ReportUsers.Add(e.Author.Id.ToString());
                    server.safeSendMessage(ReportChannel, server.getServer().EveryoneRole + " A user has submitted a report with the given ID - **" + (ReportUsers.Count() - 1) + "**:").Wait();
                    server.safeSendMessage(ReportChannel, report, true).Wait();

                    server.safeSendMessage(GetDMChannel(e.Author), "Your report has been submitted!\nIts ID is **" + (ReportUsers.Count() - 1) + "**.\nSave this in case you have trouble with the report system.").Wait();
                }

            }
        }

        /// <summary>
        /// Reply to a specific report based on the report ID
        /// </summary>
        /// <param name="e"></param>
        /// <param name="reply"></param>
        private void Reply(ServerMessage e, string reply)
        {
            string[] split = reply.Split(new char[] { ' ' }, 2);

            // Parse the command
            int reportID;
            if (split.Length == 2 && int.TryParse(split[0], out reportID))
            {
                lock (_lock)
                {
                    string userID = ReportUsers.ElementAtOrDefault(reportID);

                    // Make sure that report exists
                    if (userID != null)
                    {
                        var user = server.getServer().GetUser(ulong.Parse(userID));
                        var channel = GetDMChannel(user);
                        server.safeSendMessage(channel, "You have received a reply to your report with the ID **" + reportID + "**:").Wait();
                        server.safeSendMessage(channel, split[1]).Wait();
                    }
                    else
                    {
                        server.safeSendMessage(e.Channel, "That report does not exist.");
                    }
                }
            }
            else
            {
                server.safeSendMessage(e.Channel, "Command not in the correct format! Should be !reportreply <num> <text>");
            }
        }

        /// <summary>
        /// Bans the user that made the report from using the report system
        /// </summary>
        /// <param name="e"></param>
        /// <param name="report"></param>
        private void Ban(ServerMessage e, string report)
        {
            // Parse the command
            int reportID;
            if (int.TryParse(report, out reportID))
            {
                lock (_lock)
                {
                    string userID = ReportUsers.ElementAtOrDefault(reportID);

                    // Make sure that report exists
                    if (userID != null)
                    {
                        BannedUsers.Add(userID);
                        server.safeSendMessage(e.Channel, "The user has been banned from the report system.");
                    }
                    else
                    {
                        server.safeSendMessage(e.Channel, "That report does not exist.");
                    }
                }
            }
            else
            {
                server.safeSendMessage(e.Channel, "Command not in the correct format! Should be !reportban <num>");
            }
        }


        /// <summary>
        /// Unban the user with the selected report
        /// </summary>
        /// <param name="e"></param>
        /// <param name="report"></param>
        private void UnBan(ServerMessage e, string report)
        {
            // Parse the command
            int reportID;
            if (int.TryParse(report, out reportID))
            {
                lock (_lock)
                {
                    string userID = ReportUsers.ElementAtOrDefault(reportID);

                    // Make sure that report exists
                    if (userID != null)
                    {
                        BannedUsers.Remove(userID);
                        server.safeSendMessage(e.Channel, "The user has been unbanned from the report system.");
                    }
                    else
                    {
                        server.safeSendMessage(e.Channel, "That report does not exist.");
                    }
                }
            }
            else
            {
                server.safeSendMessage(e.Channel, "Command not in the correct format! Should be !reportunban <num>");
            }
        }

        #endregion
    }
}
