/* Module for setting up regular reminders
 * 
 * also contains the various things needed for changing it
 * 
 * TODO: investigate if it has persistance?
 * TODO: improve parsing and reporting, currently does not handle #<channelname> and reminder list is not helpful
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using Discord.WebSocket;

namespace borkbot
{
    class Reminder : CommandHandler
    {
        LinkedList<ReminderObj> reminderlist = null;
        bool running = false;

        class ReminderObj
        {
            public DateTime nextSchedule;
            public String name;
            public ulong channelid;
            public Int32 freq;
            public String msg;
        }

        public Reminder(VirtualServer _server) : base(_server)
        {
        }

        public override List<Command> getCommands()
        {
            var coms = new List<Command>(1);
            coms.Add(Command.AdminCommand(this.server, "reminder", reminderfunc, new HelpMsgStrings("",
                       @"reminder add <name> <channel> <repetition-frequency (in min)> <msg> 
reminder list
reminder delete <name>
reminder change channel <name> <channel>
reminder change frequency <name> <frequency>
reminder change msg <name> <msg>")));
            return coms;
        }

        private void emitMsg(ReminderObj obj)
        {
            var chan = server.getServer().GetChannel(obj.channelid);
            if (chan == null)
                return;
            SocketTextChannel stc = chan as SocketTextChannel;
            if (stc != null)
                return;
            var msg = stc.CachedMessages.FirstOrDefault();
            Console.WriteLine(msg.Author+" with "+ msg.Content);
            if (msg != null && msg.Source == Discord.MessageSource.Bot)
                return;
            server.safeSendMessage(stc, obj.msg);
        }

        private void reschedule(ReminderObj obj)
        {
            obj.nextSchedule = obj.nextSchedule + new TimeSpan(0, obj.freq, 0);
            var ls = reminderlist;
            LinkedList<ReminderObj> prev = null;
            while(ls != null && ls.obj.nextSchedule < obj.nextSchedule)
            {
                prev = ls;
                ls = ls.next;
            }
            if(prev == null)
            {
                reminderlist = new LinkedList<ReminderObj>(obj, ls);
            }
            else
            {
                var wrappedobj = new LinkedList<ReminderObj>(obj, ls);
                prev.next = wrappedobj;
            }
            if(!running)
            {
                running = true;
                var z = new Timer((reminderlist.obj.nextSchedule - DateTime.Now).TotalMilliseconds);
                z.Elapsed += (s, e2) => sendMessage((Timer)s);
                z.Start();
            }
        }

        private void addfunc(ServerMessage arg1, String name, String channelname, String frequency, String msg)
        {
            var channel = server.getServer().TextChannels.Where(chn => chn.Name == channelname).FirstOrDefault();
            if (reminderlist != null && reminderlist.Any(x => x.name == name))
            {
                server.safeSendMessage(arg1.Channel, "error: name already exists");
                return;
            }
            if(channel == null)
            {
                server.safeSendMessage(arg1.Channel,"error: no such channel");
                return;
            }
            Int32 freq;
            if(!Int32.TryParse(frequency,out freq) || freq <= 0)
            {
                server.safeSendMessage(arg1.Channel, "error: frequency needs to be a whole number greater 0");
                return;
            }
            ReminderObj k = new ReminderObj();
            k.freq = freq;
            k.nextSchedule = DateTime.Now;
            k.name = name;
            k.channelid = channel.Id;
            k.msg = msg;
            emitMsg(k);
            reschedule(k);
            server.safeSendMessage(arg1.Channel, "successfully added " + name + " at an interval of " + k.freq + " minutes.");
        }

        private void sendMessage(Timer t)
        {
            if (t != null)
            {
                t.Stop();
                t.Dispose();
            }
            var ls = reminderlist;
            if(ls != null)
            {
                while (reminderlist.obj.nextSchedule < DateTime.Now)
                {
                    ls = reminderlist;
                    reminderlist = reminderlist.next;
                    emitMsg(ls.obj);
                    reschedule(ls.obj);
                }
                var z = new Timer((reminderlist.obj.nextSchedule - DateTime.Now).TotalMilliseconds);
                z.Elapsed += (s, e2) => sendMessage((Timer)s);
                z.Start();
            }else
            {
                running = false;
            }
        }
        

        private void deletefunc(ServerMessage arg1, String name)
        {
            var ls = reminderlist;
            LinkedList<ReminderObj> prev = null;
            while(ls != null && ls.obj.name != name)
            {
                prev = ls;
                ls = ls.next;
            }
            if (ls == null)
            {
                server.safeSendMessage(arg1.Channel, "could not find " + name);
                return;
            }
            if (prev == null)
            {
                reminderlist = reminderlist.next;
            }else
            {
                prev.next = ls.next;
            }
            server.safeSendMessage(arg1.Channel, "deleted " + ls.obj.name + " with interval of " + ls.obj.freq + " minutes and message: \n" + ls.obj.msg);
        }

        private void changefunc(ServerMessage arg1, string type, string name, string content)
        {
            if(type != "channel" && type != "frequency" && type != "msg")
            {
                server.safeSendMessage(arg1.Channel, "unknown change type: " + type);
                return;
            }
            var ls = reminderlist;
            LinkedList<ReminderObj> prev = null;
            while (ls != null && ls.obj.name != name)
            {
                prev = ls;
                ls = ls.next;
            }
            if (ls == null)
            {
                server.safeSendMessage(arg1.Channel, "could not find "+name);
                return;
            }
            var obj = ls.obj;
            if (type == "channel")
            {
                var channel = server.getServer().TextChannels.Where(chn => chn.Name == content).FirstOrDefault();
                if (channel == null)
                {
                    server.safeSendMessage(arg1.Channel, "error: no such channel");
                    return;
                }
                obj.channelid = channel.Id;
            }
            else if (type == "frequency")
            {
                Int32 freq;
                if (!Int32.TryParse(content, out freq) || freq <= 0)
                {
                    server.safeSendMessage(arg1.Channel, "error: frequency needs to be a whole number greater 0");
                    return;
                }
                obj.freq = freq;
                obj.nextSchedule = DateTime.Now;
                if (prev == null)
                {
                    reminderlist = reminderlist.next;
                }
                else
                {
                    prev.next = ls.next;
                }
                reschedule(obj);
            }
            else if (type == "msg")
            {
                obj.msg = content;
            }
            server.safeSendMessage(arg1.Channel,"Successfully changed " + obj.name);
        }

        private void reminderfunc(ServerMessage arg1, string msg)
        {
            var xs = msg.Split(" ".ToCharArray(), 5, StringSplitOptions.RemoveEmptyEntries);
            if(xs.Length == 0)
            {
                server.safeSendMessage(arg1.Channel, "error: no args");
            }
            else if (xs[0] == "list")
            {
                String outmsg = "Listing currently scheduled things: ";
                var ls = reminderlist;
                while (ls != null)
                {
                    outmsg += "\n"+ls.obj.name;
                    ls = ls.next;
                }
                server.safeSendMessage(arg1.Channel, outmsg);
            }
            else if (xs[0] == "add")
            {
                if(xs.Length != 5)
                {
                    server.safeSendMessage(arg1.Channel, "error: not enough args for add");
                }else
                {
                    addfunc(arg1, xs[1], xs[2], xs[3], xs[4]);
                }
            }
            else if(xs[0] == "delete")
            {
                if(xs.Length != 2)
                {
                    server.safeSendMessage(arg1.Channel, "error: wrong arg count for delete");
                }
                else
                {
                    deletefunc(arg1, xs[1]);
                }
            }
            else if(xs[0] == "change")
            {
                if (xs.Length != 4)
                {
                    server.safeSendMessage(arg1.Channel, "error: wrong arg count for change");
                }
                else
                {
                    changefunc(arg1, xs[1], xs[2], xs[3]);
                }
            }
            else
            {
                server.safeSendMessage(arg1.Channel, "error: unknown subcommand " + xs[0]);
            }
        }
    }
}
