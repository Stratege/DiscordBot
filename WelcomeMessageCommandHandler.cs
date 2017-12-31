using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace borkbot
{
    class WelcomeMessageCommandHandler : CommandHandler
    {
        protected WelcomeMessage wmls;
        protected bool on = false;
        private string command;
        protected string syntaxmessage;

        public WelcomeMessageCommandHandler(VirtualServer _server, string _command, string _syntaxmessage) : base(_server)
        {
            command = _command;
            syntaxmessage = _syntaxmessage;
            string message = String.Join("\n",server.FileSetup(command + ".txt"));
            if (message == String.Empty)
                wmls = new WelcomeMessage("Welcome " + WelcomeMessageId.raw);
            else
            {
                var split = message.Split("\n".ToArray(), 2);
                if (split.Length == 2)
                {
                    if(split[0] == "on")
                    {
                        on = true;
                    }
                    else if(split[0] == "off")
                    {
                        on = false;
                    }
                    else
                    {
                        Console.WriteLine("Welcome message in corrupted format: " + message);
                    }

                    wmls = new WelcomeMessage(split[1]);
                }
                else
                {
                    wmls = new WelcomeMessage("Welcome " + WelcomeMessageId.raw);
                }
            }
        }

        public override List<Tuple<string, Command>> getCommands()
        {
            
            var commands = new List<Tuple<string, Command>>(1);
            commands.Add(new Tuple<string, Command>(command, Command.AdminCommand(server,cmd,syntaxmessage)));
            return commands;
        }

        protected virtual void cmd(SocketUserMessage e, string m)
        {
            var split = m.Split(" ".ToCharArray(),2);
            string message = "";
            if (split[0] == "off")
            {
                if (!on)
                    message = command + " was already off.";
                else
                {
                    message = "Understood, shutting off " + command;
                    on = false;
                    persistState();
                }
            }
            else if(split[0] == "on")
            {
                if(split.Length == 1)
                {
                    if (on)
                    {
                        message = "Already on, did you mean to set a new message?";
                    }
                    else
                    {
                        on = true;
                        message = "Understood, turning " + command + " on without changing the message.\n\nLast message was:\n" + wmls.RawMessage();
                        persistState();
                    }
                }
                else
                {
                    wmls = new WelcomeMessage(split[1]);
                    if (!on)
                    {
                        message = "Understood, turning " + command + "on.\n\nNew message:\n" + wmls.RawMessage();
                    }
                    else
                    {
                        message = "Understood, changed message of " + command + " to:\n" + wmls.RawMessage();
                    }
                    on = true;
                    persistState();
                }
            }
            else
            {
                message = command + " <on/off> <message>";
            }
            server.safeSendMessage(e.Channel,message);
        }

        private void persistState()
        {
            server.fileCommand(command + ".txt",x => System.IO.File.WriteAllText(x, (on ? "on" : "off") + "\n" + wmls.RawMessage()));
        }

    }
}
