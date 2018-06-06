using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace borkbot
{
    /// <summary>
    /// Represents a module to determine emote usage, so we could cull the bad ones every once in a while
    /// </summary>
    class EmoteModule : EnableableCommandModule
    {
        #region Data Members

        private PersistantDict<ulong, EmoteStats> _emotes;

        private string _lastResetDate;

        private string _resetDateFile = "EmoteCounterLastReset.txt";

        private readonly object _lock = new object();

        #endregion

        #region Properties

        internal PersistantDict<ulong, EmoteStats> Emotes
        {
            get
            {
                return _emotes;
            }

            set
            {
                _emotes = value;
            }
        }

        public string LastResetDate
        {
            get
            {
                return _lastResetDate;
            }

            set
            {
                _lastResetDate = value;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="_server"></param>
        public EmoteModule(VirtualServer _server) : base(_server, "emote")
        {
            Emotes = PersistantDict<ulong, EmoteStats>.load(_server, "EmoteStats.xml");

            // Get the last reset date
            var res = server.FileSetup(_resetDateFile);
            if (res.Count > 0)
            {
                LastResetDate = res[0];
            }
            else
            {
                _lastResetDate = new DateTimeOffset(DateTime.Now).ToString();
                server.fileCommand(_resetDateFile, x => System.IO.File.WriteAllText(x, LastResetDate));
            }

            Action<Emote> f = (emote) =>
            {
                // Create a new entry if it doesn't have one
                if (!Emotes.ContainsKey(emote.Id))
                {
                    Emotes.Add(emote.Id, new EmoteStats()
                    {
                        Counter = 0,
                        LastPosted = new DateTimeOffset(DateTime.Now).ToString(),
                        Name = emote.Name
                    });
                }

                // Increment the counter
                Emotes[emote.Id].Counter++;
                Emotes[emote.Id].LastPosted = new DateTimeOffset(DateTime.Now).ToString();
                Emotes.persist();
            };

            // Listen to all incoming messages from the server
            server.MessageRecieved += (s, e) =>
            {
                if (!on)
                    return;

                List<Emote> usedEmotes = new List<Emote>();

                if (!e.Author.IsBot && e.msg.Tags.Any((t) => t.Type == TagType.Emoji && server.getServer().Emotes.Contains(t.Value)))
                {
                    lock (_lock)
                    {
                        // Go over all the server emotes that weren't already posted
                        foreach (var tag in e.msg.Tags.Where(t => t.Type == TagType.Emoji && server.getServer().Emotes.Contains(t.Value) && !usedEmotes.Contains(t.Value)))
                        {
                            // Add it to the previously posted list
                            Emote emote = tag.Value as Emote;
                            usedEmotes.Add(emote);

                            if (emote != null)
                                f(emote);
                        }
                    }
                }
            };

            // Count for reactions
            server.ReactionAdded += (s, reaction) =>
            {
                if (!on)
                    return;

                // If a real user reacted wit ha server emote
                if (!reaction.User.Value.IsBot && server.getServer().Emotes.Contains(reaction.Emote))
                {
                    Emote emote = reaction.Emote as Emote;
                    if (emote != null)
                        lock (_lock)
                        {
                            f(emote);
                        }
                }
            };

            server.ReactionRemoved += (s, reaction) =>
            {
                if (!on)
                    return;

                // If a real user removed their server emote reaction
                if (!reaction.User.Value.IsBot && server.getServer().Emotes.Contains(reaction.Emote))
                {
                    Emote emote = reaction.Emote as Emote;
                    if (emote != null)
                        lock (_lock)
                        {
                            // Decrement the counter
                            if (Emotes.ContainsKey(emote.Id))
                            {
                                Emotes[emote.Id].Counter--;
                                Emotes.persist();
                            }
                        }
                }
            };
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Gets all the available commands from this module into the command list
        /// This includes dynamically created commands by role names
        /// </summary>
        /// <returns></returns>
        public override List<Tuple<string, Command>> getCommands()
        {
            var commands = base.getCommands();
            commands.Add(new Tuple<string, Command>("printemotestats", makeEnableableAdminCommand(PrintCounter, "printemotestats - prints the emote stats")));
            commands.Add(new Tuple<string, Command>("resetemotestats", makeEnableableAdminCommand(ResetCounter, "resetemotestats - resets the emote stats")));
            return commands;
        }

        /// <summary>
        /// Resets the counter
        /// </summary>
        /// <param name="e"></param>
        /// <param name="message"></param>
        private void ResetCounter(ServerMessage e, string message)
        {
            lock (_lock)
            {
                Emotes.Clear();
                Emotes.persist();
                LastResetDate = new DateTimeOffset(DateTime.Now).ToString();
                server.fileCommand(_resetDateFile, x => System.IO.File.WriteAllText(x, LastResetDate));
                server.safeSendMessage(e.Channel, "Counter reset successfully!");
            }
        }

        /// <summary>
        /// Prints the counter
        /// </summary>
        /// <param name="e"></param>
        /// <param name="message"></param>
        private void PrintCounter(ServerMessage e, string message)
        {
            string stats = "Here are the emote stats, last resetted on: " + LastResetDate + "\n";

            foreach (var stat in Emotes.Values.OrderBy(x => x.Counter))
            {
                stats += stat + "\n";
            }

            server.safeSendMessage(e.Channel, stats, true);
        }

        #endregion
    }

    /// <summary>
    /// Represents emote statistics we're saving
    /// </summary>
    [Serializable]
    public class EmoteStats
    {
        #region Data Members

        private DateTimeOffset _lastPosted;

        private int _counter;

        private string _name;

        #endregion

        #region Properties

        // This is a string because XmlSerializer doesn't know how to serialize DateTimeOffset objects
        public string LastPosted
        {
            get
            {
                return _lastPosted.ToString();
            }

            set
            {
                _lastPosted = DateTimeOffset.Parse(value);
            }
        }

        public int Counter
        {
            get
            {
                return _counter;
            }

            set
            {
                _counter = value;
            }
        }

        public string Name
        {
            get
            {
                return _name;
            }

            set
            {
                _name = value;
            }
        }

        #endregion

        #region Override Methods

        public override string ToString()
        {
            return "Emote: :" + Name + ": - Counter: " + Counter + ", Last posted/reacted on: " + LastPosted;
        }

        #endregion
    }
}
