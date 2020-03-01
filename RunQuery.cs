using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord.WebSocket;


using AssocList = borkbot.LinkedList<System.Tuple<borkbot.Var, borkbot.QueryObj>>;

namespace borkbot
{
    class RunQuery : CommandHandler
    {
        CancellationTokenSource tokenSource2 = new CancellationTokenSource();
        static int millisecondMaxQueryLength = 2000;
        int curMaxMillisecondQueryLength = 0;
        QueryParser q = new QueryParser();
        PersistantList storage;
        bool noPriorQueries = true;

        public RunQuery(VirtualServer _server) : base(_server)
        {
            storage = PersistantList.Create(server, "runquerystorage");
        }

        public override List<Command> getCommands()
        {
            var cmds = new List<Command>(2);
            cmds.Add(Command.AdminCommand(server, "getquerysyntax", getQuerySyntax, new HelpMsgStrings("", "getquerysyntax")));
            cmds.Add(Command.AdminCommand(server, "query", query, new HelpMsgStrings("", "query <see getquerysyntax for details. All usage attempts will be logged.>")));
            cmds.Add(Command.AdminCommand(server, "overrideQuery", (e, x) => { int i = int.Parse(x); curMaxMillisecondQueryLength = (i > 0 ? i : 0); }, new HelpMsgStrings("", "")));
            return cmds;
        }

        void getQuerySyntax(ServerMessage e, String m)
        {

        }

        void query(ServerMessage e, String m)
        {
            Interpreter interpreter = new Interpreter(e,server);
            foreach (var x in storage)
            {
                queryHelper(interpreter, x,true);
            }
            noPriorQueries = false;
            Console.WriteLine(e.Author.Username + (e.Server != null ? " on server " + e.Server.Name : "") + " has executed query: \"" + m + "\"");
            var res = "";
            bool kill = false;
            int maxDur = (curMaxMillisecondQueryLength != 0) ? curMaxMillisecondQueryLength : millisecondMaxQueryLength;
            try
            {
                res = Funcs.WithTimeout(() => queryHelper(interpreter, m,false), maxDur);
                
            }
            catch
            {
                server.safeSendMessage(e.Channel, "Query took too long. Current max: " + maxDur + "ms");
                kill = true;
            }
            if (!kill)
            {
                while (res.Length > 2000)
                {
                    
                    server.safeSendMessage(e.Channel, res.Substring(0,2000));
                    res = res.Substring(2000);
                }
                server.safeSendMessage(e.Channel, res);
            }
        }

        String queryHelper(Interpreter interpreter, String m, bool silent)
        {
            var eval = interpreter;
            var res = q.Run(m);
            if(!silent) Console.WriteLine(Stmt.show(res));
            var evalResult = eval.eval(res,storage);
            if (!silent) Console.WriteLine(evalResult);
            return evalResult;
        }

        /*
         * Stmt: Define | Expr
         * Define: <Stratege Only For Now>
         * Expr: Bracket | Let | Func | App | Tuple | PrimObj
         * Bracket: '(' <expr> ')'
         * Let: let <var> = <expr> in <expr>
         * Func: \<var> -> <expr>
         * App: $<func> <var>
         * PrimConst: server | channel | self;
         * OpaqueTypes: Server | Channel | User | Role | Int | String | Id;
         * TransparentTypes: List | Tuple;
         * InbuiltFuncs: *todo*
         * */
    }

    class QueryParser : parser2.WMParse2
    {
        public QueryParser() : base()
        {
        }


        public Stmt Run(string str)
        {
            try
            {
                return run(str, StmtParse);
            }
            catch (Exception e)
            {
                Console.WriteLine("parse error: " + e.Message);
                return null;
            }
        }

        public Stmt StmtParse()
        {
            return OneOf<Stmt>(DefineParse, ExprParse);
        }

        public Define DefineParse()
        {
            match("define");
            var var = Parse(VarParse);
            match("=");
            var boundExpr = Parse(ExprParse);
            return new Define(this.inputString,var,boundExpr);
        }

        public Expr ExprParse()
        {
            return OneOf(BracketParse, LetParse, FuncParse, AppParse, TupleParse, QuoteParse, WrappedPrimObjParse, VariableParse);
        }



        public Expr BracketParse()
        {
            match("(");
            var res = Parse(ExprParse);
            match(")");
            return res; //fuck it, no stupid intermediate Bracket thing for now
        }

        public Expr LetParse()
        {
            match("let");
            var var = Parse(VarParse);
            match("=");
            var boundExpr = Parse(ExprParse);
            match("in");
            var usageExpr = Parse(ExprParse);
            return new Let(var, boundExpr, usageExpr);
        }

        public Expr FuncParse()
        {
            match("\\");
            Func func = new Func();
            func.var = Parse(VarParse);
            match("->");
            func.usageExpr = Parse(ExprParse);
            return func;
        }

        public Expr AppParse()
        {
            match("$");
            var app = new App();
            app.func = Parse(ExprParse);
            app.var = Parse(ExprParse);
            return app;
        }

        public Expr VariableParse()
        {
            var Var = Parse(VarParse);
            return new Variable(Var);
        }

        public Var VarParse()
        {
            var name = matchAlphanumeric();
            return new Var(name);
        }

        public Expr WrappedPrimObjParse()
        {
            return new PrimObj(PrimObjParse());
        }

        public QueryPrimObj PrimObjParse()
        {
            return OneOf(NumberParse, StringParse);
        }

        public QueryPrimObj NumberParse()
        {
            var name = matchNumber();
            //number hack
            UInt64 num;
            if (UInt64.TryParse(name, out num))
                return (new QueryPrimObj(num));
            throw parser2.NoParseException.except;
        }

        public QueryPrimObj StringParse()
        {
            match("\"");
            var str = matchWhile(x => x != '"');
            match("\"");
            return new QueryPrimObj(str);
        }

        public Expr TupleParse()
        {
            match("(");
            var res = Parse(ExprParse);
            match(".");
            var res2 = Parse(ExprParse);
            match(")");
            return new TupleExpr(Tuple.Create(res, res2));
        }

        public Expr QuoteParse()
        {
            match("'");
            return new Quote(Parse(ExprParse));
        }
    }




    class LinkedList<T> : IEnumerable<T>
    {
        public T obj;
        public LinkedList<T> next;
        public LinkedList(T _obj, LinkedList<T> _next) { obj = _obj; next = _next; }
        public void Add(T _obj)
        {
            if (obj == null) obj = _obj;
            else
            {
                if (next == null) next = new LinkedList<T>(_obj, null);
                else next.Add(_obj);
            }
        }

        public static LinkedList<T> Create(T _obj, LinkedList<T> _next) { return new LinkedList<T>(_obj, _next); }

        public LinkedList<T> prepend(T _obj)
        {
            return new LinkedList<T>(_obj, this);
        }

        public LinkedList<J> map<J>(Func<T, J> f)
        {
            return new LinkedList<J>(f(obj), (next != null ? next.map(f) : null));
        }

        public int length()
        {
            int length = 0;
            var ls = this;
            while (ls != null)
            {
                if (ls.obj != null)
                    length++;
                ls = ls.next;
            }
            return length;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new LLEnum<T>(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new LLEnum<T>(this);
        }

        private class LLEnum<J> : IEnumerator<J>
        {
            public LLEnum(LinkedList<J> list)
            {
                this.first = list;
                this.coll = null;
            }
            LinkedList<J> first;
            LinkedList<J> coll;
            public J Current
            {
                get
                {
                    return coll.obj;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return coll.obj;
                }
            }

            public void Dispose() {}

            public bool MoveNext()
            {
                if(coll == null)
                {
                    coll = first;
                    return true;
                }
                if(coll.next != null)
                {
                    coll = coll.next;
                    return true;
                }
                return false;
            }

            public void Reset()
            {
                coll = null;
            }
        }

    }


    class Interpreter
    {
        LinkedList<Tuple<String, QueryObj>> builtinList;

        private static class InternalFuncs
        {
            public static QueryPrimObj Name(QueryPrimObj prim)
            {
                string var = null;
                if (prim.message != null)
                    var = prim.message.Author.Username;
                if (prim.server != null)
                    var = prim.server.Name;
                if (prim.channel != null)
                    var = prim.channel.Name;
                if (prim.user != null)
                {
                    var = prim.user.Nickname;
                    if (var == null)
                        var = prim.user.Username;
                }
                if (prim.role != null)
                    var = prim.role.Name;
                if (var == null)
                    throw new Exception("type error in 'Name'");
                return new QueryPrimObj(var);
            }

            public static QueryPrimObj Nickname(QueryPrimObj prim)
            {
                string var = null;
                if (prim.message != null)
                    var = ((SocketGuildUser)prim.message.Author).Nickname;
                if (prim.user != null)
                    var = prim.user.Nickname;
                if (var == null)
                    throw new Exception("type error in 'Nickname'");
                return new QueryPrimObj(var);
            }

            public static QueryPrimObj Id(QueryPrimObj prim)
            {
                ulong var = 0;
                if (prim.message != null)
                    var = prim.message.Id;
                if (prim.server != null)
                    var = prim.server.Id;
                if (prim.channel != null)
                    var = prim.channel.Id;
                if (prim.user != null)
                    var = prim.user.Id;
                if (prim.role != null)
                    var = prim.role.Id;
                if (var == 0)
                    throw new Exception("type error in 'Name'");
                return new QueryPrimObj(var);
            }


            private static Func<QueryPrimObj, QueryPrimObj> ListOpHelper(Func<IEnumerable<QueryPrimObj>, Func<QueryPrimObj, QueryPrimObj>, IEnumerable<QueryPrimObj>> f, Func<QueryPrimObj, QueryPrimObj> g)
            {
                return x =>
                {
                    if (x.ls == null && x.str == null)
                        throw new Exception("type error in Map/Filter: can only call on lists or strings");
                    if (x.ls != null) return new QueryPrimObj(f(x.ls, g));
                    else return new QueryPrimObj(f(x.str.Select(y => new QueryPrimObj(""+y)), g));
                };
            }

            private static Func<QueryPrimObj, QueryPrimObj> MapInternal(Func<QueryPrimObj, QueryPrimObj> f)
            {
                return ListOpHelper(System.Linq.Enumerable.Select, f);
            }


            private static Func<QueryPrimObj, QueryPrimObj> FilterInternal(Func<QueryPrimObj, QueryPrimObj> f)
            {
                return ListOpHelper((ls, x) => System.Linq.Enumerable.Where(ls, (y) => { var res = x(y); return (bool)res.boolean; }), f);
            }

            public static QueryPrimObj HOFHelper(Func<Func<QueryPrimObj, QueryPrimObj>, Func<QueryPrimObj, QueryPrimObj>> op, QueryPrimObj f, AssocList ls)
            {
                if (f.builtInFuncObj != null)
                {
                    var temp = new BuiltInFuncObj();
                    temp.qq = (y, ls2) => op(x => f.builtInFuncObj.qq(x, fuseAssocLists(ls, ls2)))(y);
                    return new QueryPrimObj(temp);
                }
                if (f.funcObj != null)
                {
                    return HOFHelper(op, new QueryPrimObj(externalToBuiltIn(f.funcObj)), ls);
                }
                throw new NotImplementedException();
            }

            public static QueryPrimObj Map(QueryPrimObj f, AssocList ls)
            {
                return HOFHelper(MapInternal, f, ls);
            }

            public static QueryPrimObj Filter(QueryPrimObj f, AssocList ls)
            {
                return HOFHelper(FilterInternal, f, ls);
            }

            public static BuiltInFuncObj externalToBuiltIn(FuncObj f)
            {
                var res = new BuiltInFuncObj();
                res.qq = (x, ls) =>
                {
                    var app = new App();
                    var tempFunc = new Func();
                    tempFunc.var = f.var;
                    tempFunc.usageExpr = f.code;
                    app.func = tempFunc;
                    app.var = new Variable(f.var);
                    var ls2 = fuseAssocLists(ls, f.assocList).prepend(Tuple.Create<Var, QueryObj>(f.var, new QueryObj(x)));
                    var resOfEval = Interpreter.evalExpr(app, ls2);
                    if (resOfEval.prim == null)
                        throw new Exception("type error in func wrapping");
                    return resOfEval.prim;
                };
                return res;
            }

            public static QueryPrimObj Roles(QueryPrimObj prim)
            {
                IEnumerable<SocketRole> var = null;
                if (prim.message != null)
                    var = prim.message.MentionedRoles;
                if (prim.server != null)
                    var = prim.server.Roles;
                if (prim.user != null)
                    var = prim.user.Roles;
                if (var == null)
                    throw new Exception("type error in 'Roles'");
                return new QueryPrimObj(var.Select(x => package(x)));
            }

            public static Func<QueryPrimObj, QueryPrimObj, QueryPrimObj> PrimObjBinOpHelper<J>(Func<Nullable<bool>, Nullable<bool>, J> f1, Func<BuiltInFuncObj, BuiltInFuncObj, J> f2, Func<SocketGuildChannel, SocketGuildChannel, J> f3, Func<FuncObj, FuncObj, J> f4, Func<Nullable<ulong>, Nullable<ulong>, J> f5, Func<IEnumerable<QueryPrimObj>, IEnumerable<QueryPrimObj>, J> f6, Func<SocketRole, SocketRole, J> f7, Func<SocketGuild, SocketGuild, J> f8, Func<String, String, J> f9, Func<Tuple<QueryPrimObj, QueryPrimObj>, Tuple<QueryPrimObj, QueryPrimObj>, J> f10, Func<SocketGuildUser, SocketGuildUser, J> f11, Func<SocketMessage, SocketMessage, J> f12)
            {
                return (x, y) =>
                {
                    J res = default(J);
                    if (x.boolean != null)
                        res = f1(x.boolean, y.boolean);
                    if (x.builtInFuncObj != null)
                        res = f2(x.builtInFuncObj, y.builtInFuncObj);
                    if (x.channel != null)
                        res = f3(x.channel, y.channel);
                    if (x.funcObj != null)
                        res = f4(x.funcObj, y.funcObj);
                    if (x.id != null)
                        res = f5(x.id, y.id);
                    if (x.ls != null)
                        res = f6(x.ls, y.ls);
                    if (x.role != null)
                        res = f7(x.role, y.role);
                    if (x.server != null)
                        res = f8(x.server, y.server);
                    if (x.str != null)
                        res = f9(x.str, y.str);
                    if (x.tup != null)
                        res = f10(x.tup, y.tup);
                    if (x.user != null)
                        res = f11(x.user, y.user);
                    if (x.message != null)
                        res = f12(x.message, y.message);
                    return package<J>(res);
                };
            }

            public static QueryPrimObj PrimEq(QueryPrimObj x, QueryPrimObj y)
            {
                return PrimObjBinOpHelper((a, b) => (a.Equals(b)), BuiltInFuncObj.Equals, SocketGuildChannel.Equals, FuncObj.Equals, (a, b) => (a.Equals(b)), IEnumerable<QueryPrimObj>.Equals, SocketRole.Equals, SocketGuild.Equals, string.Equals, Tuple<QueryPrimObj, QueryPrimObj>.Equals, SocketGuildUser.Equals, SocketMessage.Equals)(x, y);
            }

            public static QueryPrimObj PrimNEq(QueryPrimObj x, QueryPrimObj y)
            {
                return package<bool>(!extract<bool>(PrimEq(x, y)));
            }

            public static QueryPrimObj Head(QueryPrimObj x)
            {
               return enhance<IEnumerable<QueryPrimObj>,QueryPrimObj>(Enumerable.First<QueryPrimObj>,x);
            }

            public static IEnumerable<QueryPrimObj> Drop(ulong amount, IEnumerable<QueryPrimObj> x)
            {
                return x.Skip((int)amount);
            }

            public static QueryPrimObj enhance<T,J>(Func<T,J> f, QueryPrimObj x)
            {
                return package<J>(f(extract<T>(x)));
            }

            public static IEnumerable<SocketGuildUser> Users(QueryPrimObj x)
            {
                IEnumerable<SocketGuildUser> res = null;
                if (x.server != null)
                    res = x.server.Users;
                else if (x.channel != null)
                {
                    SocketGuildChannel sgc = x.channel as SocketGuildChannel;
                    if (sgc != null)
                    {
                        res = sgc.Users;
//                        res = x.channel.Users;
                    }
                }
                else if (x.role != null)
                    res = x.role.Members;
                else if (x.message != null)
                    res = (IEnumerable<SocketGuildUser>)x.message.MentionedUsers;
                return res;
            }

            public static QueryPrimObj Sum(QueryPrimObj x)
            {
                if (x.ls == null)
                    throw new Exception("type error: ls null in Sum");
                var typeIndicator = x.ls.FirstOrDefault();
                if (typeIndicator == null)
                {
                    //hacks
                    var res = new QueryPrimObj((ulong)0);
                    res.str = "";
                    res.boolean = false;
                    return res;
                }
                if (typeIndicator.boolean != null)
                {
                    return package<bool>(x.ls.Aggregate(false, (a, y) => (a || (bool)y.boolean), z => z));
                }
                if(typeIndicator.id != null)
                {
                    return package<ulong>(x.ls.Aggregate((ulong)0, (a, y) => (a + (ulong)y.id), z => z));
                }
                if(typeIndicator.str != null)
                {
                    return package<string>(x.ls.Aggregate("", (a, y) => a + y.str, z => z));
                }
                throw new Exception("wrong type in list");
            }

            public static IEnumerable<SocketGuildChannel> Channels(QueryPrimObj x)
            {
                IEnumerable<SocketGuildChannel> res = null;
                if (x.server != null)
                    res = x.server.Channels;
                else if (x.user != null)
                {
                    var server = x.user.Guild;
                    res = server.Channels.Where(chn => chn.GetUser(x.user.Id) != null);
                }
                else if (x.role != null)
                {
                    res = x.role.Guild.Channels.Where(y => {
                        bool access = false;
                        if (x.role.Permissions.Administrator)
                            return true;
                        var pon = y.GetPermissionOverwrite(x.role);
                        if (pon.HasValue) {
                            var po = pon.Value;
                            access = access || po.ReadMessages == Discord.PermValue.Allow;
                            var everyone = y.GetPermissionOverwrite(x.role.Guild.EveryoneRole).Value;
                            access = access || (everyone.ReadMessages != Discord.PermValue.Deny && po.ReadMessages == Discord.PermValue.Inherit);
                        }
                        return access;
                    });
                }
                else if (x.message != null)
                {
                    res = x.message.MentionedChannels;
                }
                return res;
            }

            public static QueryPrimObj Negate(QueryPrimObj x)
            {
                return package<bool>(!extract<bool>(x));
            }

            public static QueryPrimObj And(QueryPrimObj x, QueryPrimObj y)
            {
                return package<bool>(extract<bool>(x) && extract<bool>(y));
            }

            public static QueryPrimObj GetPermissions(SocketGuildChannel c, SocketGuildUser u)
            {
                if (u == null || c == null)
                    throw new Exception("type error");
                var perm = u.GetPermissions(c);
                Func<String, bool?, QueryPrimObj> f = (x, y) => new QueryPrimObj(Tuple.Create(new QueryPrimObj(x), (y != null) ? new QueryPrimObj((bool)y) : new QueryPrimObj(false)));
                Func<String, ulong, QueryPrimObj> g = (x, y) => new QueryPrimObj(Tuple.Create(new QueryPrimObj(x), new QueryPrimObj(y)));
                LinkedList<QueryPrimObj> ret = new LinkedList<QueryPrimObj>(null,null);
                ret.Add(f("AttachFiles", perm.AttachFiles));
                ret.Add(f("Connect", perm.Connect));
                ret.Add(f("CreateInstantInvite", perm.CreateInstantInvite));
                ret.Add(f("DeafenMembers", perm.DeafenMembers));
                ret.Add(f("EmbedLinks", perm.EmbedLinks));
                ret.Add(f("ManageChannel", perm.ManageChannel));
                ret.Add(f("ManageMessages", perm.ManageMessages));
                ret.Add(f("ManagePermissions", perm.ManagePermissions));
                ret.Add(f("MentionEveryone", perm.MentionEveryone));
                ret.Add(f("MoveMembers", perm.MoveMembers));
                ret.Add(f("MuteMembers", perm.MuteMembers));
                ret.Add(g("RawValue", perm.RawValue));
                ret.Add(f("ReadMessageHistory", perm.ReadMessageHistory));
                ret.Add(f("ReadMessages", perm.ReadMessages));
                ret.Add(f("SendMessages", perm.SendMessages));
                ret.Add(f("SendTTSMessages", perm.SendTTSMessages));
                ret.Add(f("Speak", perm.Speak));
                ret.Add(f("UseVoiceActivation", perm.UseVAD));
                return new QueryPrimObj(ret);
            }
/* this is not supported anymore, todo: consider what to do with it?
            public static QueryPrimObj lastActivity(QueryPrimObj x)
            {
                DateTime? dt;
                if (x.user != null)
                    dt = x.user.LastActivityAt;
                else
                    throw new Exception("type error: user was null");
                if (!dt.HasValue)
                    return new QueryPrimObj("never");
                else
                    return new QueryPrimObj(DateTime.Now.Subtract(dt.Value).ToString());
            }*/
        }

        public Interpreter(ServerMessage e, VirtualServer s)
        {
            builtinList = new LinkedList<Tuple<string, QueryObj>>(null, null);
            builtinList.Add(new Tuple<string, QueryObj>("newline", new QueryObj(new QueryPrimObj("\n"))));
            builtinList.Add(new Tuple<string, QueryObj>("true", new QueryObj(new QueryPrimObj(true))));
            builtinList.Add(new Tuple<string, QueryObj>("false", new QueryObj(new QueryPrimObj(false))));
            addBinaryFunc<ulong, ulong, ulong>("add", (x, y) => x + y);
            addBinaryFunc<ulong, ulong, ulong>("sub", (x, y) => x - y);
            addBinaryFunc<bool,bool,bool>("and",(x,y) => x && y);
            addBinaryFunc<bool, bool, bool>("or", (x, y) => x || y);
            addAssocIgnoringFunc("neg", InternalFuncs.Negate);
            addAssocIgnoringFunc("negate", (FuncMaker<int, int>(x => -x)));
            builtinList.Add(new Tuple<string, QueryObj>("server", new QueryObj(new QueryPrimObj(s.getServer()))));
            builtinList.Add(new Tuple<string, QueryObj>("channel", new QueryObj(new QueryPrimObj(e.Channel as SocketGuildChannel))));
            builtinList.Add(new Tuple<string, QueryObj>("self", new QueryObj(new QueryPrimObj(s.getServer().GetUser(e.Author.Id)))));
            builtinList.Add(new Tuple<string, QueryObj>("this", new QueryObj(new QueryPrimObj(e.msg))));
            addAssocIgnoringFunc("name", InternalFuncs.Name);
            addAssocIgnoringFunc("nickname", InternalFuncs.Nickname);
            addAssocIgnoringFunc("id", InternalFuncs.Id);
            addFunc("map", InternalFuncs.Map);
            addFunc("filter", InternalFuncs.Filter);
            addAssocIgnoringFunc("roles", InternalFuncs.Roles);
            addBinaryFunc<QueryPrimObj, QueryPrimObj, QueryPrimObj>("eq", InternalFuncs.PrimEq);
            addBinaryFunc<QueryPrimObj, QueryPrimObj, QueryPrimObj>("neq", InternalFuncs.PrimNEq);
            addAssocIgnoringFunc("head", InternalFuncs.Head);
            addBinaryFunc<ulong,IEnumerable<QueryPrimObj>,IEnumerable<QueryPrimObj>>("drop", InternalFuncs.Drop);
            addBinaryFunc<ulong, IEnumerable<QueryPrimObj>, IEnumerable<QueryPrimObj>>("take", (x,y) => y.Take((int)x));
            addAssocIgnoringFunc("users", FuncMaker(FuncMaker3<QueryPrimObj,SocketGuildUser>(InternalFuncs.Users)));
            addAssocIgnoringFunc("sum", InternalFuncs.Sum);
            addAssocIgnoringFunc("channels", FuncMaker(FuncMaker3<QueryPrimObj, SocketGuildChannel>(InternalFuncs.Channels)));
            Func<AssocList, LinkedList<QueryPrimObj>> assocListTransformed = x => x.map(a => new QueryPrimObj(Tuple.Create(new QueryPrimObj(a.Item1.Name), (a.Item2.prim != null ? a.Item2.prim : new QueryPrimObj("non-prim-obj")))));
            addFunc("assocList", (x, y) => new QueryPrimObj(assocListTransformed(y)));
            addAssocIgnoringFunc("car", x => (x.tup != null ? x.tup.Item1 : InternalFuncs.Head(x)));
            addAssocIgnoringFunc("cdr", x => (x.tup != null ? x.tup.Item2 : new QueryPrimObj(InternalFuncs.Drop(1, x.ls))));
            addAssocIgnoringFunc("boolToInt", x => new QueryPrimObj(x.boolean != null ? (x.boolean == true ? (ulong)1 : (ulong)0) : (ulong)x.id));
            addBinaryFunc<QueryPrimObj, QueryPrimObj, IEnumerable<QueryPrimObj>>("cons", (x, y) => (y.ls != null ? LinkedList<QueryPrimObj>.Create(x,null).Concat(y.ls) : LinkedList<QueryPrimObj>.Create(x,(LinkedList<QueryPrimObj>.Create(y, null)))));
            addBinaryFunc<ulong, ulong, bool>("gt", (x, y) => x > y); //todo: make polymorphic;
            addBinaryFunc<SocketGuildChannel, SocketGuildUser, QueryPrimObj>("getPermissions", InternalFuncs.GetPermissions);
//            addAssocIgnoringFunc("alstActivity", InternalFuncs.lastActivity);
            addAssocIgnoringFunc("show", x => new QueryPrimObj(x.show()));
            addAssocIgnoringFunc("messages", x => new QueryPrimObj((x.channel as ISocketMessageChannel).CachedMessages.OrderBy(z => z.Timestamp).Select(y => new QueryPrimObj(y))));
            addAssocIgnoringFunc("user", x => new QueryPrimObj((SocketGuildUser)x.message.Author));
            //addAssocIgnoringFunc("channel", x => new QueryPrimObj(x.message.Channel));
            addAssocIgnoringFunc("timestamp", x => new QueryPrimObj((ulong)x.message.Timestamp.Ticks));
            addAssocIgnoringFunc("text", x => new QueryPrimObj(x.message.Content));
            addFunc("unquote", (obj, ass) => evalExpr(obj.exprObj.expr, fuseAssocLists(obj.exprObj.assoc, ass)).prim);
            addBinaryFunc<string, string, bool>("strContains", (x, y) => x.Contains(y));
            //            builtinList.Add(new Tuple<string, QueryObj>())
        }

        private void addBinaryFunc<T, J, K>(String name, Func<T, J, K> f)
        {
            var temp = makeBinaryFunc<T, J, K>(f, name);
            builtinList.Add(temp.Item1);
            builtinList.Add(temp.Item2);
        }

        private void addFunc(String name, Func<QueryPrimObj,AssocList,QueryPrimObj> qq)
        {
            builtinList.Add(new Tuple<string, QueryObj>(name, makeWrappedFunc(qq)));
        }

        private void addAssocIgnoringFunc(String name, Func<QueryPrimObj, QueryPrimObj> qq)
        {
            builtinList.Add(new Tuple<string, QueryObj>(name, makeWrappedFunc((x,ls) => qq(x))));
        }


        public static Tuple<Tuple<string, QueryObj>, Tuple<string, QueryObj>> makeBinaryFunc<T, J, K>(Func<T, J, K> f, String name)
        {
            Func<Tuple<T, J>, K> f2 = x => f(x.Item1, x.Item2);
            //todo: stop ignoring the assoc list
            var f3 = makeWrappedFunc((x,ls) => FuncMaker(FuncMaker5(f2))(x));
            //\_a1 -> \_a2 -> let _a3 = _a1 in let _a4 = _a2 in f3 (_a3._a4)
            var tempf1 = new FuncObj();
            tempf1.var = new Var("_a1");
            var tempf2 = new Func();
            tempf2.var = new Var("_a2");
            App code = new App();
            Variable temp = new Variable(new Var("_" + name));
            code.func = temp;
            //let's not introduce a let because lets have strictness as a sideeffect
            const bool useLet = false;
            if (useLet)
            {
                code.var = new TupleExpr(new Tuple<Expr, Expr>(new Variable(new Var("_a3")), new Variable(new Var("_a4"))));
                var letHelper = new Let(new Var("_a3"), new Variable(new Var("_a1")), new Let(new Var("_a4"), new Variable(new Var("_a2")), code));
                tempf2.usageExpr = letHelper;
            }
            else
            {
                code.var = new TupleExpr(new Tuple<Expr, Expr>(new Variable(tempf1.var), new Variable(tempf2.var)));
                tempf2.usageExpr = code;
            }
            tempf1.code = tempf2;
            return Tuple.Create(Tuple.Create(name, new QueryObj(new QueryPrimObj(tempf1))), Tuple.Create("_" + name, f3));
        }



        public static QueryObj makeWrappedFunc(Func<QueryPrimObj, AssocList, QueryPrimObj> f)
        {
            var temp = new BuiltInFuncObj();
            temp.qq = f;
            return new QueryObj(new QueryPrimObj(temp));
        }

        public static Func<Tuple<QueryPrimObj, QueryPrimObj>, J> FuncMaker5<A, B, J>(Func<Tuple<A, B>, J> f)
        {
            return x => f(Tuple.Create(extract<A>(x.Item1), extract<B>(x.Item2)));
        }

        public static Func<IEnumerable<QueryPrimObj>, J> FuncMaker2<T, J>(Func<IEnumerable<T>, J> f)
        {
            return x => f(x.Select(y => extract<T>(y)));
        }

        public static Func<T, IEnumerable<QueryPrimObj>> FuncMaker3<T, J>(Func<T, IEnumerable<J>> f)
        {
            return x =>
            {
                var res = f(x);
                return res.Select(y => package(y));
            };
        }

        public static T extract<T>(QueryPrimObj x)
        {
            T res;
            if (typeof(T) == typeof(QueryPrimObj))
            {
                res = ((T)(object)x);
            }
            else if (typeof(T) == typeof(SocketGuild))
            {
                res = ((T)(object)x.server);
            }
            else if (typeof(T) == typeof(SocketGuildChannel))
            {
                res = ((T)(object)x.channel);
            }
            else if (typeof(T) == typeof(SocketGuildUser))
            {
                res = ((T)(object)x.user);
            }
            else if (typeof(T) == typeof(SocketRole))
            {
                res = ((T)(object)x.role);
            }
/*            else if (typeof(T) == typeof(int))
            {
                res = ((T)(object)x.integer);
            }*/
            else if (typeof(T) == typeof(String))
            {
                res = ((T)(object)x.str);
            }
            else if (typeof(T) == typeof(ulong))
            {
                res = ((T)(object)x.id);
            }
            else if (typeof(T) == typeof(IEnumerable<QueryPrimObj>))
            {
                res = ((T)(object)x.ls);
            }
            else if (typeof(T) == typeof(bool))
            {
                res = ((T)(object)x.boolean);
            }
            else if (typeof(T) == typeof(Tuple<QueryPrimObj, QueryPrimObj>))
            {
                res = ((T)(object)x.tup);
            }
            else if (typeof(T) == typeof(BuiltInFuncObj))
            {
                res = ((T)(object)x.builtInFuncObj);
            }
            else
            {
                throw new Exception("Invalid type for extraction: " + typeof(T).ToString());
            }
            return res;
        }

        public static QueryPrimObj package<J>(J res)
        {
            QueryPrimObj y;
            if (typeof(J) == typeof(QueryPrimObj))
            {
                y = (QueryPrimObj)(object)res;
            }
            else if (typeof(J) == typeof(SocketGuild))
            {
                y = new QueryPrimObj((SocketGuild)(object)res);
            }
            else if (typeof(J) == typeof(SocketGuildChannel))
            {
                y = new QueryPrimObj((SocketGuildChannel)(object)res);
            }
            else if (typeof(J) == typeof(SocketGuildUser))
            {
                y = new QueryPrimObj((SocketGuildUser)(object)res);
            }
            else if (typeof(J) == typeof(SocketRole))
            {
                y = new QueryPrimObj((SocketRole)(object)res);
            }
/*            else if (typeof(J) == typeof(int))
            {
                y = new QueryPrimObj((int)(object)res);
            }*/
            else if (typeof(J) == typeof(String))
            {
                y = new QueryPrimObj((String)(object)res);
            }
            else if (typeof(J) == typeof(ulong))
            {
                y = new QueryPrimObj((ulong)(object)res);
            }
            else if (typeof(J) == typeof(IEnumerable<QueryPrimObj>))
            {
                y = new QueryPrimObj((IEnumerable<QueryPrimObj>)(object)res);
            }
            else if (typeof(J) == typeof(bool))
            {
                y = new QueryPrimObj((bool)(object)res);
            }
            else if (typeof(J) == typeof(Tuple<QueryPrimObj, QueryPrimObj>))
            {
                y = new QueryPrimObj((Tuple<QueryPrimObj, QueryPrimObj>)(object)res);
            }
            else if (typeof(J) == typeof(BuiltInFuncObj))
            {
                y = new QueryPrimObj((BuiltInFuncObj)(object)res);
            }
            else
            {
                throw new Exception("Invalid packaging type: " + typeof(J).ToString());
            }
            return y;
        }

        public static Func<QueryPrimObj, QueryPrimObj> FuncMaker<T, J>(Func<T, J> f)
        {
            return x =>
            {
                return package(f(extract<T>(x)));
            };

        }

        static string showList(LinkedList<Tuple<Var, QueryObj>> ls)
        {
            string ret = "[";
            while (ls != null)
            {
                if (ls.obj == null)
                    ret += "Nil";
                else
                    ret += "(" + ls.obj.Item1.show() + "." + ls.obj.Item2.show() + ")";
                ls = ls.next;
            }
            ret += "]";
            return ret;
        }
        /*
        static LinkedList<Tuple<Var, QueryObj>> fuseAssocLists(LinkedList<Tuple<Var, QueryObj>> ls1, LinkedList<Tuple<Var, QueryObj>> ls2)
        {
            if (ls1 == null)
                return ls2;
            if (ls2 == null)
                return ls1;
            if (ls1 == ls2)
                return ls1;
            if (!any(ls1, x => x == ls2))
                return new LinkedList<Tuple<Var, QueryObj>>(ls2.obj, fuseAssocLists(ls1, ls2.next));
            return fuseAssocLists(ls2, ls1);
        }
        */
        static LinkedList<Tuple<Var, QueryObj>> fuseAssocLists(LinkedList<Tuple<Var, QueryObj>> ls1, LinkedList<Tuple<Var, QueryObj>> ls2)
        {
            LinkedList<Tuple<Var, QueryObj>> fst = null;
            LinkedList<Tuple<Var, QueryObj>> ret = null;
            while (true)
            {

                if (ls1 == null)
                {
                    if (fst == null)
                        return ls2;
                    ret.next = ls2;
                    return fst;
                }
                if (ls2 == null)
                {
                    if (fst == null)
                        return ls1;
                    ret.next = ls1;
                    return fst;
                }
                if (ls1 == ls2)
                {
                    if (fst == null)
                        return ls1;
                    ret.next = ls1;
                    return fst;
                }
                if (!any(ls1, x => x == ls2))
                {
                    LinkedList<Tuple<Var, QueryObj>> last = new LinkedList<Tuple<Var, QueryObj>>(ls2.obj, null);
                    if (fst == null)
                        fst = last;
                    if (ret != null)
                    {
                        ret.next = last;
                    }
                    ret = last;
                    ls2 = ls2.next;
                }
                else
                {
                    var lsTemp = ls2;
                    ls2 = ls1;
                    ls1 = lsTemp;
                }
            }

        }

        static bool any(LinkedList<Tuple<Var, QueryObj>> ls, Func<LinkedList<Tuple<Var, QueryObj>>, bool> f)
        {
            while (ls != null)
            {
                if (f(ls))
                    return true;
                ls = ls.next;
            }
            return false;
        }

        static QueryObj lookup(Var var, LinkedList<Tuple<Var, QueryObj>> ls)
        {


            while (ls != null)
            {
                if (ls.obj != null && ls.obj.Item1.Name == var.Name)
                    return ls.obj.Item2;
                ls = ls.next;
            }
            throw new Exception("could not lookup: " + var.Name);
        }

        public string eval(Stmt stmt, PersistantList storage)
        {
            string ret;
            try
            {
                var res = evalStmt(stmt, storage);
                ret = res.show();
            }
            catch (Exception e)
            {
                ret = e.Message + " in " + e.TargetSite.Name;
            }
            return ret;
        }

        QueryObj evalStmt(Stmt stmt, PersistantList storage)
        {
            Func<Tuple<String, QueryObj>, Tuple<Var, QueryObj>> tupleProject = x =>
            {
                var Var = new Var(x.Item1);
                return new Tuple<Var, QueryObj>(Var, x.Item2);
            };
            AssocList assocList = builtinList.map(tupleProject);
            if (stmt is Expr)
            {
                return evalExpr((Expr)stmt, assocList);
            }
            if (stmt is Define)
            {
                Define def = (Define)stmt;
                if (!storage.Contains(def.inputString))
                    storage.Add(def.inputString);
                var evalRes = evalExpr(def.boundExpr, assocList);
                builtinList.Add(Tuple.Create(def.var.Name, evalRes));
                return evalRes;
            }
            throw new Exception("evalStmt is partial or arg is null");
        }

        static QueryObj evalExpr(Expr expr, LinkedList<Tuple<Var, QueryObj>> assocList)
        {

            if (expr is Let)
            {
                Let let = (Let)expr;

                var res = new Tuple<Var, QueryObj>(let.var, evalExpr(let.boundExpr, assocList));
                return evalExpr(let.usageExpr, new LinkedList<Tuple<Var, QueryObj>>(res, assocList));
            }
            else if (expr is Func)
            {
                Func f = (Func)expr;
                var fObj = new FuncObj();
                fObj.var = f.var;
                fObj.code = f.usageExpr;
                fObj.assocList = assocList;
                return new QueryObj(new QueryPrimObj(fObj));
            }
            else if (expr is App)
            {
                App app = (App)expr;
                var res = evalExpr(app.func, assocList);
                if (res.prim == null || (res.prim.funcObj == null && res.prim.builtInFuncObj == null))
                    throw new Exception("not a function on the left side of App: "+res.show());
                if (res.prim.builtInFuncObj != null)
                    return resolveBuiltInCall(res.prim.builtInFuncObj, evalExpr(app.var, assocList), assocList);
                var assocList2 = fuseAssocLists(assocList, res.prim.funcObj.assocList);
                //                Console.WriteLine(Program.show(expr) + " - " + showList(assocList2));
                var addedVar = new Tuple<Var, QueryObj>(res.prim.funcObj.var, new QueryObj(new QueryPrimObj(new ExprObj(app.var, assocList2))));
                return evalExpr(res.prim.funcObj.code, new LinkedList<Tuple<Var, QueryObj>>(addedVar, assocList2));
            }
            else if (expr is Variable)
            {
                Variable v = (Variable)expr;
                var res = lookup(v.var, assocList);
                while (res.prim.exprObj != null)
                {
                    if (res.prim.exprObj.expr is Variable && ((Variable)res.prim.exprObj.expr).var.Name == v.var.Name)
                    {
                        res = assocList.Where(x => x.Item1.Name == v.var.Name && !(x.Item2.prim != null && x.Item2.prim.exprObj != null && x.Item2.prim.exprObj.expr is Variable && ((Variable)res.prim.exprObj.expr).var.Name == v.var.Name)).First().Item2;
                        continue;
                    }
                    return evalExpr(res.prim.exprObj.expr, fuseAssocLists(assocList, res.prim.exprObj.assoc));
                }
                return res;
            }
            else if (expr is PrimObj)
            {
                return new QueryObj(((PrimObj)expr).prim);
            }
            else if (expr is TupleExpr)
            {
                TupleExpr tup = (TupleExpr)expr;
                var e1 = evalExpr(tup.tuple.Item1, assocList);
                var e2 = evalExpr(tup.tuple.Item2, assocList);
                if (e1.prim == null || e2.prim == null)
                    throw new Exception("type error in " + Expr.show(tup) + " - not containing primitives when required to.");
                return new QueryObj(new QueryPrimObj(Tuple.Create(e1.prim, e2.prim)));
            }
            else if (expr is Quote)
            {
                Quote quote = (Quote)expr;
                return new QueryObj(new QueryPrimObj(new ExprObj(quote.content, assocList)));
            }
            else { throw new Exception("partial pattern matching algo"); }
        }

        static QueryObj resolveBuiltInCall(BuiltInFuncObj bifo, QueryObj q, LinkedList<Tuple<Var,QueryObj>> ls)
        {
            QueryPrimObj qp = q.prim;
            if (qp == null)
                throw new Exception("called prim func with non-prim type");
            return new QueryObj(bifo.qq(qp,ls));
        }
    }

    class Var
    {
        public string Name; string Type;

        public Var(string name)
        {
            Name = name;
        }

        public string show()
        {
            return Name;
        }
    }

    abstract class Stmt {
        public static string show(Stmt i)
        {
            if (i == null)
                return "error: can not show null";
            if (i is Expr)
                return Expr.show((Expr)i);
            if (i is Define)
                return ((Define)i).inputString;
            return "error: stmt.show on type "+i.GetType().ToString();
        }
    }

    class Define : Stmt
    {
        public string inputString;
        public Var var;
        public Expr boundExpr;

        public Define(string inputString, Var var, Expr boundExpr)
        {
            this.inputString = inputString;
            this.var = var;
            this.boundExpr = boundExpr;
        }
    }

    abstract class Expr : Stmt
    {
        public static string show(Expr i)
        {
            if (i == null)
                return "error: can not show null";
            if (i is Let)
            {
                Let let = (Let)i;
                return ("(let " + let.var.show() + " = " + show(let.boundExpr) + " in " + show(let.usageExpr)) + ")";
            }
            if (i is Func)
            {
                Func f = (Func)i;
                return "(\\" + f.var.show() + " -> " + show(f.usageExpr) + ")";
            }
            if (i is App)
            {
                App app = (App)i;
                return "($" + show(app.func) + " " + show(app.var) + ")";
            }
            if (i is Variable)
            {
                Variable var = (Variable)i;
                return var.var.show();
            }
            if (i is PrimObj)
            {
                PrimObj prim = (PrimObj)i;
                return prim.prim.show();
            }
            if (i is TupleExpr)
            {
                TupleExpr tup = (TupleExpr)i;
                return "(" + show(tup.tuple.Item1) + "." + show(tup.tuple.Item2) + ")";
            }
            if (i is Quote)
            {
                Quote quote = (Quote)i;
                return "'" + show(quote.content);
            }
            return "error";
        }
    }

    class Bracket : Expr { public Expr expr; }
    class Let : Expr { public Var var; public Expr boundExpr; public Expr usageExpr; public Let(Var _var, Expr _boundExpr, Expr _usageExpr) { var = _var; boundExpr = _boundExpr; usageExpr = _usageExpr; } }
    class Func : Expr { public Var var; public Expr usageExpr; }
    class App : Expr { public Expr func; public Expr var; }
    class Quote : Expr { public Expr content; public Quote(Expr expr) { content = expr; } }
    class TupleExpr : Expr
    {
        public Tuple<Expr, Expr> tuple;

        public TupleExpr(Tuple<Expr, Expr> tuple)
        {
            this.tuple = tuple;
        }
    }
    class Variable : Expr
    {
        public Var var;

        public Variable(Var var)
        {
            this.var = var;
        }
    }
    class PrimObj : Expr { public QueryPrimObj prim; public PrimObj(QueryPrimObj _prim) { prim = _prim; } }
    abstract class Type { public String name; }
    class BuiltInType : Type { }
    class UserType : Type { public List<TypeProd> Sum; }
    class TypeProd { public String name; public List<Type> vals; }
    class FuncType : Type { Type src; Type tgt; }

    class ExprObj
    {
        public Expr expr;
        public AssocList assoc;
        public ExprObj(Expr _expr, AssocList _assoc) { expr = _expr;  assoc = _assoc; }
        public String show() { return Expr.show(expr); }
    }

    class QueryPrimObj
    {
        public SocketMessage message;
        public SocketGuild server;
        public SocketGuildChannel channel;
        public SocketGuildUser user;
        public SocketRole role;
        public String str;
        public ulong? id;
        public IEnumerable<QueryPrimObj> ls;
        public bool? boolean;
        public FuncObj funcObj;
        public Tuple<QueryPrimObj, QueryPrimObj> tup;
        public BuiltInFuncObj builtInFuncObj;
        public ExprObj exprObj;

        public QueryPrimObj(SocketGuild _server) { server = _server; }
        public QueryPrimObj(SocketGuildChannel channel) { this.channel = channel; }
        public QueryPrimObj(SocketGuildUser user) { this.user = user; }
        public QueryPrimObj(SocketRole role) { this.role = role; }
        public QueryPrimObj(string str) { this.str = str; }
        public QueryPrimObj(ulong id) { this.id = id; }
        public QueryPrimObj(IEnumerable<QueryPrimObj> ls) { this.ls = ls; }
        public QueryPrimObj(bool boolean) { this.boolean = boolean; }
        public QueryPrimObj(FuncObj _funcObj) { funcObj = _funcObj; }
        public QueryPrimObj(BuiltInFuncObj _builtInFuncObj) { builtInFuncObj = _builtInFuncObj; }
        public QueryPrimObj(Tuple<QueryPrimObj, QueryPrimObj> _tup) { tup = _tup; }
        public QueryPrimObj(SocketMessage message) { this.message = message; }
        public QueryPrimObj(ExprObj exprObj) { this.exprObj = exprObj; }

        public string show()
        {
            if (message != null)
                return "<message:" + message.Timestamp.ToString() + " " + message.Author.Username + ": " + message.Content + ">";
            if (server != null)
                return "<server:" + server.Name + ">";
            if (channel != null)
                return "<channel:" + channel.Name + ">";
            if (user != null)
                return "<user:" + user.Username + ">";
            if (role != null)
                return "<role:" + role.Name + ">";
            if (str != null)
                return "\"" + str + "\"";
            if (id != null)
                return "<id:" + id + ">";
            if (ls != null)
            {
                var res = ls.SelectMany(x => x.show() + ",");
                return "<list:[" + new String(res.ToArray()) +"]>";
            }
            if (boolean != null)
                return "<bool:" + boolean.ToString() + ">";
            if (funcObj != null)
                return funcObj.show();
            if (tup != null)
                return "(" + tup.Item1.show() + "." + tup.Item2.show() + ")";
            if (builtInFuncObj != null)
                return builtInFuncObj.show();
            if (exprObj != null)
                return "'" + exprObj.show();
            return "impossible";
        }
    }

    class UserObj { String type; String elem; List<QueryObj> vals; public string show() { return type + "-" + elem + "{" + vals.Select(x => x.show()).SelectMany(x => x + ",") + "}"; } }

    class FuncObj { public Var var; public Expr code; public AssocList assocList; /* assocList is optional and intended for closures */ public string show() { return "(\\" + var.show() + " -> " + Expr.show(code) + ")"; } }

    class QueryObj
    {
        public QueryPrimObj prim;
        public UserObj userObj;
        public QueryObj(QueryPrimObj _prim) { prim = _prim; }
        public QueryObj(UserObj _userObj) { userObj = _userObj; }
        public string show()
        {
            if (prim != null)
                return prim.show();
            if (userObj != null)
                return userObj.show();
            return "impossible";
        }
    }

    class BuiltInFuncObj
    {
        public Func<QueryPrimObj, AssocList, QueryPrimObj> qq;
        public string show()
        {
            return "<Builtin Function>";
        }
    }

}

