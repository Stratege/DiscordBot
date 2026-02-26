using System;
using System.Collections.Generic;
using System.Linq;

namespace DiscordBot.Utility
{
    internal class MkCommand : EnableableCommandModule
    {
        static
        string pw = null;
        public MkCommand(VirtualServer _server) : base(_server,"mkCommand") { 
            
        }

        public override List<Command> getCommands()
        {
            var ls = new List<Command>
            {
                makeEnableableAdminCommand("mkCommand", mkCommand, new HelpMsgStrings("", "")),
                Command.AdminCommand(server,"enableCommandCreation",enableCommand,new HelpMsgStrings("enables the creation of custom commands. Requires the proper password to use. Inquire with the botowner.","enableCommandCreation <pw>"))
            };
            return ls;
        }

        private void enableCommand(ServerMessage arg1, string arg2)
        {
            bool pwCorrect = arg2 == pw;
            if(!arg1.isDM)
            {
                server.safeSendMessage(arg1.Channel, "command can only be used in DMs");
                arg1.msg.DeleteAsync();
            }else if (!pwCorrect)
            {
                server.safeSendMessage(arg1.Channel, "Wrong password");
            }
            else
            {
                on = true;
                _EnableCommandPersistState();
                server.safeSendMessage(arg1.Channel, "Enabled command creation");
            }
        }

        T frame<T>(string str, string start, Func<string,T> f) where T : class
        {
            if (!str.ToLower().StartsWith(start))
                return null;
            return f(str);
        }
        abstract class Arg { }
        class NamedArg : Arg { public string name; public NamedArg(string _name) { name = _name; } }
        class UnnamedArg : Arg { }

        Arg[] getArgsName(string argsstring)
        {
            if (!argsstring.ToLower().StartsWith("args:"))
                return null; 
            var args = argsstring.Substring(5).Split(new[] { " ", "\t" }, StringSplitOptions.RemoveEmptyEntries);
            if(args.Length > 0 && int.TryParse(args[0],out int count))
            {
                if (count > 20)
                    return null; //no eating up all my resources thx
                return args.Skip(1).Select<string, Arg>(x => new NamedArg(x)).Concat(Enumerable.Repeat(new UnnamedArg(), count)).Take(count).ToArray();
            }
            return args.Select<string,Arg>(x => new NamedArg(x)).ToArray();
        }

        HelpMsgStrings getHelpMsg(string helpMsg)
        {
            if (!helpMsg.ToLower().StartsWith("help:"))
                return null;
            return new HelpMsgStrings(helpMsg.Trim(), "");
        }

        PrivilegeLevel? getSecurity(string secMsg)
        {
            if (!secMsg.ToLower().StartsWith("security:"))
                return null;
            switch (secMsg.ToLower().Trim())
            {
                case "everyone":
                    return PrivilegeLevel.Everyone;
                case "admin":
                case "botadmin":
                    return PrivilegeLevel.BotAdmin;
                default:
                    return null;
             }
        }

        bool isSpecial(string str)
        {
            var strlow = str.ToLower();
            return strlow.StartsWith("help:") || strlow.StartsWith("security:") || strlow.StartsWith("args:");
        }

        private void mkCommand(ServerMessage e, string args)
        {
            var argsSplit = args.Split(
                new[] { "\r\n", "\r", "\n" },
                StringSplitOptions.RemoveEmptyEntries
            );
            if (args.Length == 0)
                return; //todo
            var name = args[0];
            var cmdArgs = argsSplit.Select(getArgsName).FirstOrDefault(x => x != null);
            var cmdHelp = argsSplit.Select(getHelpMsg).FirstOrDefault(x => x != null);
            var security = argsSplit.Select(getSecurity).FirstOrDefault(x => x != null);
            if (cmdHelp == null || security == null)
                return; //todo
            var prog = argsSplit.Skip(1).SkipWhile(isSpecial);
            /*
             * Syntax:
             *      <name> = <expr>         -- assignment, mutating
             *      1-2
             *      This "is" "a" "call" -- calls This with 3 args, no partial func application
             *      x => x                  -- lambda
             *      (<expr>)
             *      $1                      -- first arg, only way to refer to unnamed args
             *      return <expr>           -- terminate execution, returning x (prints if direct call)
             *      <lastline>              -- terminates execution, returning x (prints if direct call)
             */

            /*
             * maybe ability to type args? Like "$1:User"? Global Type Inference?
             *      => user args are parsed and converted, if not able to convert complain happens
             *      => need to improve args parser
             * "" for calling in a way that makes spaces work? ie. foo "hi world" on cmdline is calling foo with 1 arg: "hi world"
             */


            /* functions:
             * Random <num> <num>  -- generates random integer in range, inclusive
             * Add num num         -- adds 2 numbers
             * Mul num num         -- muls 2 numbers
             * Sub num num         -- subtracts 2 numbers
             * DivFloor num num    -- divides 2 numbers, flooring result
             * DivCeil num num     -- divides 2 numbers, cleiling result
             * DivRound num num    -- divides 2 numbers, rounding result
             * Rem num num         -- gives reminder of integer divison
             * 
             */
        }
    }
}
