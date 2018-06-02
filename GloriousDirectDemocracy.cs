using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace borkbot
{
    class LazyDiscLoaded<T>
    {
        T obj;
        bool loaded;
        String FileName;
        Func<String, T> loader;
        public LazyDiscLoaded(String FileName, Func<String,T> loader)
        {
            //todo: Validate Filename
            this.FileName = FileName;
            this.loader = loader;
            loaded = false;
            obj = default(T);
        }

        public LazyDiscLoaded(T value) //we can also create it already initialized
        {
            loaded = true;
            obj = value;
        }

        public T get()
        {
            if (loaded)
                return obj;
            try
            {
                obj = loader(FileName);
                loaded = true;
                return obj;
            }
            catch
            {
                return default(T);
            }
        }
    }
    /* interface 
     * !Proposal New <NewName> <MinutesItLasts>
     *           Amend <ExistingName> Text <ReplacementText>
     *           Amend <ExistingName> AddOption <VoteKeyword> <OptionDescription>
     *           Amend <ExistingName> RemoveOption <VoteKeyword>
     *           Start <ExistingName>
     *           Delay <ExistingName>
     *           Abort <ExistingName>
     *           Lookup <ExistingName>
     *           List <Opt:Begin> <Opt:End>
     * !Vote     <ProposalName>
     * !Vote     <ProposalName> <Option>
     * */
    [Serializable]
    public class Proposal
    {
        public String Name; //Also the identifier
        public String Text;
        public List<Option> Options;
        [XmlIgnore]
        public TimeSpan TotalDuration { get; set; }
        [Browsable(false)]
        [XmlElement(DataType = "duration", ElementName = "TotalDuration")]
        public string TimeSinceLastEventString
        {
            get
            {
                return XmlConvert.ToString(TotalDuration);
            }
            set
            {
                TotalDuration = string.IsNullOrEmpty(value) ?
                TimeSpan.Zero : XmlConvert.ToTimeSpan(value);
            }
        }
        public DateTime TimeOfCompletion;
        public bool Started;
        public String reasonForEnding;
        public List<Vote> votes;
        public ulong msgId;
        public ulong channelId;
        public Proposal()
        {

        }
        public Proposal(string name, TimeSpan span)
        {
            this.Name = name;
            this.TotalDuration = span;
            this.Started = false;
            this.votes = new List<Vote>();
            this.Options = new List<Option>();
            this.reasonForEnding = "";
        }


        async public Task<bool> start(Action<Proposal> store, VirtualServer serv, ServerMessage msg, ulong propId)
        {
            if (Started)
                return false;
            var ownMsg = await serv.safeSendMessage(msg.Channel, PropPrint(false, propId));
            if (ownMsg == null)
                return false;
            Started = true;
            msgId = ownMsg.Id;
            channelId = ownMsg.Channel.Id;
            this.TimeOfCompletion = DateTime.Now + this.TotalDuration;
            store(this);

            return true;
        }

        public string PropPrint(bool isArchived, ulong id)
        {
            string msg;
            int endLength;
            if(isArchived)
            {
                msg = "=== ARCHIVED PROPOSAL #" + id + " - '" + this.Name + "' ===";
            }
            else
            {
                msg = "=== PROPOSAL #" + id + " - '" + this.Name + "' ===";
            }
            endLength = msg.Length;
            msg += "\n\n Duration: " + this.TotalDuration;
            if (this.Started)
            {
                msg += "\n Started: " + this.TimeOfCompletion.Subtract(this.TotalDuration);
                if (isArchived)
                {
                    msg += "\n Ended: " + this.TimeOfCompletion;
                    msg += "\n Reason: " + this.reasonForEnding;
                }
                else
                {
                    msg += "\n Time Remaining: " + this.TimeOfCompletion.Subtract(DateTime.Now);
                }
            }
            msg += "\n\n" + this.Text;
//            if (this.Started)
//            {
                msg += "\n\n== OPTIONS ==";
                if (this.Options.Count > 0)
                {
                    List<Tuple<int, String>> optsList = new List<Tuple<int, String>>(this.Options.Count);
                    //todo: figure out if this can be done faster?
                    foreach (var x in this.Options)
                    {
                        var votes = 0;
                        foreach (var y in this.votes)
                        {
                            if (y.VoteKeywordVotedFor == x.VoteKeyword)
                            {
                                votes++;
                            }
                        }
                        var tempstr = "\n" + votes + " votes for " + x.VoteKeyword + " - " + x.Description;
                        optsList.Add(Tuple.Create(votes, tempstr));
                    }
                    Console.WriteLine("Opts: " + optsList.Count);
                    optsList.Sort((x, y) => (x.Item1 > y.Item1) ? -1 : (x.Item1 < y.Item1) ? 1 : 0);
                    foreach (var z in optsList)
                        msg += z.Item2;
                }
//            }
            msg += "\n" + new string('=', endLength);
            return msg;
        }

        /*           static public Proposal FromString(String t)
                    {
                        return null;
                    }
                    public string SerializeProposal()
                    {
                        return null;
                    }*/
    }

    public class Option
    {
        public String VoteKeyword;
        public String Description;
    }

    public class Vote
    {
        public ulong userid;
        public DateTime timeOfVoteCast;
        public String VoteKeywordVotedFor;
        public Vote() { }
        public Vote(ulong _userid, DateTime _timeOfVoteCast, String _VoteKeywordVotedFor)
        {
            userid = _userid;
            timeOfVoteCast = _timeOfVoteCast;
            VoteKeywordVotedFor = _VoteKeywordVotedFor;
        }
    }

    class GloriousDirectDemocracy : EnableableCommandModule
    {
        
        Dictionary<String, ulong> NameOfAllProposals;
        Dictionary<ulong,Proposal> Proposals;
        Dictionary<ulong,LazyDiscLoaded<Proposal>> pastProposals;
        ulong lowestUnusedId;
        const string folder = "GloriousDirectDemocracy/";
        const string LUIFileName = "LowestUnusedId.txt";
        const string MasterTableFileName = "MasterTable.xml";
        const string CurrentProposalNamesFileName = "CurrentProposals.txt";

        public GloriousDirectDemocracy(VirtualServer _server) : base(_server, "GloriousDirectDemocracy")
        {
            server.fileCommand(folder, x => System.IO.Directory.CreateDirectory(x));
            List<string> arr = server.FileSetup(folder+LUIFileName);
            string LowestUnusedIdStr = "0";
            if (arr.Count > 0)
                LowestUnusedIdStr = arr[0];
            if (!ulong.TryParse(LowestUnusedIdStr, out lowestUnusedId))
                lowestUnusedId = 0;
            NameOfAllProposals = server.XMlDictSetup<String, ulong>(folder+MasterTableFileName);
            List<String> currentProposals = server.FileSetup(folder+ CurrentProposalNamesFileName);
            Proposals = new Dictionary<ulong, Proposal>(currentProposals.Count + 10);
            pastProposals = new Dictionary<ulong, LazyDiscLoaded<Proposal>>();
            foreach (var entry in NameOfAllProposals)
            {
                string filename = folder + entry.Value + ".prop";
                if (currentProposals.FindIndex(x => x == entry.Key) != -1)
                {
                    //currentProposal
                    try
                    {
                        /*                        string outPut = server.fileCommand(filename, x => System.IO.File.ReadAllText(x));
                                                var prop =  //Proposal.FromString(outPut);
                        */
                        var prop = server.fileCommand(filename, x => Funcs.XMLSetup<Proposal>(x));
                        Proposals.Add(entry.Value, prop);
                        if(prop.Started)
                            queuePropEnd(prop, entry.Value);
                    }
                    catch
                    {
                        Console.WriteLine("Unable to Get Proposal " + entry.Value + " - '" + entry.Key + "'");
                    }
                }else
                {
                    //pastProposal
                    pastProposals.Add(entry.Value, new LazyDiscLoaded<Proposal>(filename, y => server.fileCommand(y, x => Funcs.XMLSetup<Proposal>(x))));
                }
            }
        }

        public override List<Tuple<string, Command>> getCommands()
        {
            var cmds = base.getCommands();
            cmds.Add(Tuple.Create("proposal", makeEnableableAdminCommand(proposal, "proposal - see proposalsyntax command")));
            cmds.Add(Tuple.Create("proposalSyntax", makeEnableableAdminCommand(proposalsyntax, "proposalsyntax")));
            cmds.Add(Tuple.Create("vote", makeEnableableCommand(vote, PrivilegeLevel.Everyone, "vote <ProposalName> <VoteOption>")));
            return cmds;
        }

        private void vote(ServerMessage arg1, string arg2)
        {
            var split = arg2.Split(" ".ToCharArray(),2);
            ulong propid;
            string msg;
            if(NameOfAllProposals.TryGetValue(split[0],out propid))
            {
                Proposal prop;
                if(Proposals.TryGetValue(propid,out prop))
                {
                    if(prop.Started)
                    {
                        var pastVote = prop.votes.FirstOrDefault(x => x.userid == arg1.Author.Id);
                        var Option = prop.Options.FirstOrDefault(x => x.VoteKeyword == split[1]);
                        if (Option != null)
                        {
                            if(pastVote != null)
                            {
                                msg = "Changing your vote from " + pastVote.VoteKeywordVotedFor + " to " + Option.VoteKeyword;
                                pastVote.VoteKeywordVotedFor = Option.VoteKeyword;
                                pastVote.timeOfVoteCast = DateTime.Now;
                            }else
                            {
                                prop.votes.Add(new Vote(arg1.Author.Id,DateTime.Now,Option.VoteKeyword));
                                msg = "Casting your vote as " + Option.VoteKeyword;
                            }
                            savePropToDisc(propid, prop);
                            proposalMsgUpdate(prop, false, propid);
                        }
                        else
                        {
                            msg = "Your Vote could not be cast as Option " + split[1] + " could not be found.";
                            if(pastVote != null)
                            {
                                msg = msg + " Keeping your previous vote of " + pastVote.VoteKeywordVotedFor;
                            }
                        }

                    }
                    else
                    {
                        msg = "Proposal #" + propid + " - '" + split[0] + "' has not been started yet";
                    }
                }
                else
                {
                    msg = "Proposal #"+propid+" - '" + split[0] + "' has already ended";
                }
            }
            else
            {
                msg = "Could not find Proposal '"+split[0]+"'";
            }
            server.safeSendMessage(arg1.Channel, msg);
        }

        private void proposalsyntax(ServerMessage arg1, string arg2)
        {
            const string propsynmsg =
@"!Proposal New <NewName> <MinutesItLasts>
          Amend <ExistingName> Text <ReplacementText>
          Amend <ExistingName> AddOption <VoteKeyword> <OptionDescription>
          Amend <ExistingName> RemoveOption <VoteKeyword>
          Start <ExistingName>
          Abort <ExistingName>
          Lookup <ExistingName>
          LookupById <ExistingName>
          List <Opt:Begin> <Opt:End>";
            //          Delay < ExistingName >

            server.safeSendMessage(arg1.Channel, propsynmsg);
        }
        private void proposal(ServerMessage arg1, string arg2)
        {
            var split = arg2.Split(" ".ToCharArray(), 3);
            //todo: Validate proposal name;
            string msg;
            if (split.Length == 2)
            {
                if (split[0].Equals("Start", StringComparison.InvariantCultureIgnoreCase))
                {
                    msg = proposalStart(arg1, split[1]);
                }
/*                else if (split[0].Equals("Delay", StringComparison.InvariantCultureIgnoreCase))
                {
                    msg = proposalDelay(arg1, split[1]);
                }
*/                else if (split[0].Equals("Abort", StringComparison.InvariantCultureIgnoreCase))
                {
                    msg = proposalAbort(arg1, split[1]);
                }
                else if (split[0].Equals("Lookup", StringComparison.InvariantCultureIgnoreCase))
                {
                    msg = proposalLookup(arg1, split[1]);
                }
                else if (split[0].Equals("LookupById", StringComparison.InvariantCultureIgnoreCase))
                {
                    msg = proposalLookupById(arg1, split[1]);
                }
                else
                {
                    msg = "Invalid";
                }
            }
            else if (split.Length == 3)
            {
                if (split[0].Equals("New", StringComparison.InvariantCultureIgnoreCase))
                {
                    msg = proposalNew(arg1, split[1], split[2]);
                }
                else if (split[0].Equals("Amend", StringComparison.InvariantCultureIgnoreCase))
                {
                    msg = proposalAmend(arg1, split[1], split[2]);
                }
                else if (split[0].Equals("List", StringComparison.InvariantCultureIgnoreCase))
                {
                    msg = proposalList(arg1, split[1], split[2]);
                }
                else
                {
                    msg = "Invalid";
                }
            }
            else
            {
                msg = "Invalid";
            }
            server.safeSendMessage(arg1.Channel, msg);
        }

        private Tuple<Proposal,ulong,String> getProposalForName(string name)
        {
            ulong id;
            if(!NameOfAllProposals.TryGetValue(name,out id)) {
                return Tuple.Create<Proposal, ulong,String>(null, 0, "Could not find Proposal '" + name + "'");
            }
            Proposal ret;
            if(!Proposals.TryGetValue(id,out ret)) {
                return Tuple.Create<Proposal, ulong, String>(null, id, "Proposal #" + id + " - '" + name + "' has already ended");
            }
            return Tuple.Create(ret, id, "");
        }

        private string proposalStart(ServerMessage arg1, string name)
        {
            var ret = getProposalForName(name);
            if (ret.Item1 == null)
                return ret.Item3;
            var prop = ret.Item1;
            if(prop.Started)
            {
                return "Proposal #"+ ret.Item2 +" - '" + name + "' has already been started";
            }
            var task = prop.start(x => savePropToDisc(ret.Item2, x), server, arg1, ret.Item2);
            task.Wait();
            if (task.Result)
            {
                queuePropEnd(prop, ret.Item2);
                return "Proposal #" + ret.Item2 + " - '" + name + "' has been started";
            }else
            {
                return "Failed to start Proposal #" + ret.Item2 + " - this is a bug and should be reported";
            }
        }

        private void queuePropEnd(Proposal prop, ulong propId)
        {
            var end = prop.TotalDuration;
            if (prop.TimeOfCompletion != default(DateTime))
                end = prop.TimeOfCompletion - DateTime.Now;
            //already over
            if (prop.TimeOfCompletion <= DateTime.Now)
            {
                proposalComplete(prop, propId);
            }
            else
            {
                Task.Delay(end).ContinueWith((x) => proposalComplete(prop, propId));
            }
        }

        private async void proposalComplete(Proposal prop, ulong propId)
        {
            if (this.Proposals.Remove(propId))
            {
                IEnumerable<String> currentProposalNames = Proposals.Values.Select(x => x.Name);
                server.fileCommand(folder + CurrentProposalNamesFileName, x => System.IO.File.WriteAllLines(x, currentProposalNames));
                this.pastProposals.Add(propId, new LazyDiscLoaded<Proposal>(prop));
                var chan = server.getServer().GetTextChannel(prop.channelId);
                await server.safeSendMessage(chan, prop.PropPrint(false, propId));
                await server.safeSendMessage(chan, "Voting ended.");
            }else
            {
                Console.WriteLine("Could not complete proposal #" + propId + " - " + prop.Name + " because it was not in list of proposals");
            }
        }

        /*
       private string proposalDelay(ServerMessage arg1, string name)
       {
           var ret = getProposalForName(name);
           if (ret.Item1 == null)
               return ret.Item3;
           var prop = ret.Item1;
           if (!prop.Started)
           {
               return "Proposal #" + ret.Item2 + " - '" + name + "' has not been started yet";
           }
           prop.Started = false;
           prop.TimeOfCompletion =
           throw new NotImplementedException();
       }
       */
        private string proposalAbort(ServerMessage arg1, string name)
        {
            var ret = getProposalForName(name);
            if (ret.Item1 == null)
                return ret.Item3;
            var prop = ret.Item1;
//            moveToPastProposals(prop);
            return "Proposal #" + ret.Item2 + " - '" + name + "' has been aborted (except not really yet, sorry TODO)";
        }

        private string proposalLookup(ServerMessage arg1, string name)
        {
            ulong id;
            String msg;
            if (!NameOfAllProposals.TryGetValue(name, out id))
            {
                return "Could not find Proposal '" + name + "'";
            }
            Proposal ret;
            bool completed = false;
            if (!Proposals.TryGetValue(id, out ret))
            {
                LazyDiscLoaded<Proposal> ret2;
                if (!pastProposals.TryGetValue(id, out ret2))
                {
                    return "While we found the Proposal Id #" + id + " the actual proposal has been swallowed by the void. This is a bug and should be reported to the maintainer.";
                }else
                {
                    ret = ret2.get();
                    completed = true;
                }
            }else
            {
                completed = false;
            }
            return ret.PropPrint(completed,id);
        }

        public void savePropToDisc(ulong id, Proposal prop)
        {
//            server.fileCommand(folder + voteId + ".prop", x => System.IO.File.WriteAllText(x, prop.SerializeProposal()));
            server.XMLSerialization(folder + id + ".prop", prop);
        }

        private string proposalLookupById(ServerMessage arg1, string idString)
        {
            ulong id;
            if(!ulong.TryParse(idString,out id))
            {
                return "Could not parse " + idString + " into an Integer Number";
            }
            String msg;
            Proposal ret;
            bool completed = false;
            int endLength;
            if (!Proposals.TryGetValue(id, out ret))
            {
                LazyDiscLoaded<Proposal> ret2;
                if (!pastProposals.TryGetValue(id, out ret2))
                {
                    return "Could not find Proposal with #" + id;
                }
                else
                {
                    ret = ret2.get();
                    msg = "=== ARCHIVED PROPOSAL #" + id + " - '" + ret.Name + "' ===";
                    endLength = msg.Length;
                    completed = true;
                }
            }
            else
            {
                msg = "=== PROPOSAL #" + id + " - '" + ret.Name + "' ===";
                endLength = msg.Length;
            }
            msg += "\n\n Duration: " + ret.TotalDuration;
            if (ret.Started)
            {
                msg += "\n Started: " + ret.TimeOfCompletion.Subtract(ret.TotalDuration);
                if (completed)
                {
                    msg += "\n Ended: " + ret.TimeOfCompletion;
                    msg += "\n Reason: " + ret.reasonForEnding;
                }
                else
                {
                    msg += "\n Time Remaining: " + ret.TimeOfCompletion.Subtract(DateTime.Now);
                }
            }
            msg += "\n\n" + ret.Text;
            if (ret.Started)
            {
                msg += "\n\n== OPTIONS ==";
                foreach (var x in ret.Options)
                    msg += "\n" + x.VoteKeyword + " - " + x.Description;
            }
            msg += "\n" + new string('=', endLength);
            return msg;
        }
        private string proposalNew(ServerMessage arg1, string name, string args)
        {
            string msg;
            ulong unusedid;
            if(!NameOfAllProposals.TryGetValue(name,out unusedid))
            {
                int timeInMinutes;
                if(int.TryParse(args,out timeInMinutes))
                {
                    var span = new TimeSpan(0, timeInMinutes, 0);
                    var prop = new Proposal(name,span);
                    ulong voteId = lowestUnusedId;
                    lowestUnusedId++;
                    server.fileCommand(folder + LUIFileName, x => System.IO.File.WriteAllText(x, lowestUnusedId.ToString()));
                    savePropToDisc(voteId, prop);
                    Proposals.Add(voteId, prop);
                    IEnumerable<String> currentProposalNames = Proposals.Values.Select(x => x.Name);
                    server.fileCommand(folder + CurrentProposalNamesFileName, x => System.IO.File.WriteAllLines(x, currentProposalNames));
                    NameOfAllProposals.Add(prop.Name, voteId);
                    server.XMlDictSerialization(folder + MasterTableFileName, NameOfAllProposals);
                    msg = "Added your Proposal '" + name + "' as Proposal #" + voteId + " and once started it will run for "+span.ToString();
                }
                else
                {
                    msg = "Could not create Proposal: " + args + " is not a valid Integer Number";
                }
            }
            else
            {
                msg = "Could not create Proposal: Proposal with this name already existed - Proposal #" + unusedid + " - " + name;
            }
            return msg;
        }


        private string proposalAmendAddOption(ServerMessage arg1, ulong id, Proposal prop, string votekeyword, string description)
        {
            var opt = prop.Options.FirstOrDefault(x => x.VoteKeyword == votekeyword);
            if (opt != null)
                return "Voting Option '" + votekeyword + "' already exists.";
            else
            {
                var opt2 = new Option();
                opt2.VoteKeyword = votekeyword;
                opt2.Description = description;
                prop.Options.Add(opt2);
                savePropToDisc(id, prop);
                return "Voting Option '" + votekeyword + "' added.";
            }
        }

        private string proposalAmendRemoveOption(ServerMessage arg1, ulong id, Proposal prop, string votekeyword)
        {
            var opt = prop.Options.FirstOrDefault(x => x.VoteKeyword == votekeyword);
            if (opt == null)
                return "No such Voting Option: " + votekeyword;
            else
            {
                prop.Options.Remove(opt);
                savePropToDisc(id, prop);
                return "Successfully removed '" + votekeyword + "' Option";
            }
        }

        private string proposalAmend(ServerMessage arg1, string name, string args)
        {
            var ret = getProposalForName(name);
            if (ret.Item1 == null)
                return ret.Item3;
            var prop = ret.Item1;
            string msg;
            var split = args.Split(" ".ToCharArray(), 3);
            /*            Amend<ExistingName> Text < ReplacementText >
            Amend < ExistingName > AddOption < VoteKeyword > < OptionDescription >
            Amend < ExistingName > RemoveOption < VoteKeyword >
            */
            if (split[0].Equals("Text", StringComparison.InvariantCultureIgnoreCase))
            {
                prop.Text = split[1] + (split.Length > 2 ? " " + split[2] : "");
                msg = "Proposal text has been replaced.";
                savePropToDisc(ret.Item2, prop);
            }
            else if (split.Length == 2)
            {
                if (split[0].Equals("RemoveOption", StringComparison.InvariantCultureIgnoreCase))
                {
                    msg = proposalAmendRemoveOption(arg1, ret.Item2, prop, split[1]);
                }
                else
                {
                    msg = "Invalid";
                }
            }
            else if (split.Length > 2)
            {
                if (split[0].Equals("AddOption", StringComparison.InvariantCultureIgnoreCase))
                {
                    msg = proposalAmendAddOption(arg1, ret.Item2, prop, split[1], split[2]);
                }else
                {
                    msg = "Invalid";
                }
            }else
            {
                msg = "Invalid";
            }

            return msg;
        }

        private string proposalList(ServerMessage arg1, string name, string args)
        {
//            string msg;
            return "Sorry, not yet";
        }


        private async void proposalMsgUpdate(Proposal prop, bool isArchived, ulong Id)
        {
            //todo: rate limiting
            if (prop.Started) //safety
            {
                var s = server.getServer();
                var c = s.GetTextChannel(prop.channelId);
                if(c != null)
                {
                    var msg = await c.GetMessageAsync(prop.msgId) as Discord.Rest.RestUserMessage;
                    if (msg != null && msg.Author.Id == s.CurrentUser.Id)
                    {
                        await msg.ModifyAsync((x) => x.Content = prop.PropPrint(isArchived, Id));
                        return;
                    }
                }
            }
            Console.WriteLine("msg update failed for prop with id " + Id);
        }

    }
}
