using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace borkbot
{
    class Math : EnableableCommandModule
    {
        Parser parser = new Parser();
        public Math(VirtualServer _server) : base(_server, "math")
        {
        }

        public override List<Tuple<string, Command>> getCommands()
        {
            var cmds = base.getCommands();
            cmds.Add(new Tuple<string, Command>("math", makeEnableableCommand(math, PrivilegeLevel.Everyone, "math <just play with it, not writing the full grammar in the hint>")));
            return cmds;
        }

        private void math(ServerMessage e, string m)
        {
            var res = parser.parse(m);
            if (res != null)
            {
                var res2 = res.eval();
                server.safeSendMessage(e.Channel, "Your result: **" + res2.Item1 + "** ["+res2.Item2+"]");
            }
        }
    }

    class ParseState
    {
        List<String> content;
        private int index = 0;
        public string current { get { return (index >= content.Count ? "" : content[index]); } }
        public ParseState(List<String> tokens)
        {
            content = tokens.Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        }

        public Tuple<String,Boolean> accept(string s)
        {
            string cur = current;
            if (cur == s)
            {
                index++;
                return Tuple.Create(cur,true);
            }
            return Tuple.Create<String,Boolean>(null,false);
        }
        public Tuple<String,Boolean> expect(string s)
        {
            var x = accept(s);
            if(x.Item2)
                return Tuple.Create(x.Item1,x.Item2);
            //technically? error.
            throw new Exception("invalid parse");
            //return Tuple.Create<String, Boolean>(null,false);
        }

        public String lookahead(UInt16 i)
        {
            if (index + i >= content.Count)
                return "";
            return content[index+i];
        }

    }

    public class Precedence
    {
        public static int ASSIGNMENT = 1;
        public static int CONDITIONAL = 2;
        public static int COMPARATOR = 3;
        public static int SUM = 4;
        public static int PRODUCT = 5;
        public static int EXPONENT = 6;
        public static int DICE = 7;
        public static int PREFIX = 8;
        public static int POSTFIX = 9;
        public static int CALL = 10;
    }

    class Parser
    {
        ParseState ps;
        Dictionary<string, Func<Parser,string, OP>> prefixMap = new Dictionary<string, Func<Parser, string, OP>>();
        Dictionary<string, InfixOPParser> infixMap = new Dictionary<string, InfixOPParser>();
        static String[] delimiters = { "+", "-", "*", "\\*", "/", "d", "D", "(", ")", "<=", ">=", "==", "<", ">", "{", "}", "&&", "||", "=>", "!","^"};

        public Parser()
        {
            registerInfix("+", new BinaryOperatorParser(Precedence.SUM));
            registerInfix("-", new BinaryOperatorParser(Precedence.SUM));
            registerInfix("*", new BinaryOperatorParser(Precedence.PRODUCT));
            registerInfix("\\*", new BinaryOperatorParser(Precedence.PRODUCT));
            registerInfix("/", new BinaryOperatorParser(Precedence.PRODUCT));
            registerInfix("d", new BinaryOperatorParser(Precedence.DICE));
            registerInfix("D", new BinaryOperatorParser(Precedence.DICE));
            registerInfix("<", new BinaryOperatorParser(Precedence.COMPARATOR));
            registerInfix(">", new BinaryOperatorParser(Precedence.COMPARATOR));
            registerInfix("<=", new BinaryOperatorParser(Precedence.COMPARATOR));
            registerInfix(">=", new BinaryOperatorParser(Precedence.COMPARATOR));
            registerInfix("==", new BinaryOperatorParser(Precedence.COMPARATOR));
            registerInfix("{", new InfixOPParser((parser, op, token) => { OP expression = parser.parseExpression(0); parser.consume("}"); return new LazyBinaryOp(op,"{",expression); },Precedence.CONDITIONAL));
            registerInfix("^", new BinaryOperatorParser(Precedence.COMPARATOR));

            registerPrefix("(", (parser, token) => { OP expression = parser.parseExpression(0); parser.consume(")"); return expression; });
            Func<Parser,string,OP> prefixParse = (parser, token) => { OP right = parser.parseExpression(Precedence.PREFIX); return new PrefixOp(token, right); };
            registerPrefix("-", prefixParse);
            registerPrefix("!", prefixParse);

            registerInfix("&&", new BinaryOperatorParser(Precedence.COMPARATOR));
            registerInfix("||", new BinaryOperatorParser(Precedence.COMPARATOR));
            registerInfix("=>", new BinaryOperatorParser(Precedence.COMPARATOR));
        }

        public OP parse(String m)
        {
            try
            {
                ps = new ParseState(Funcs.splitKeepDelimiters(m, delimiters));
                return parseExpression(0);
            }
            catch (Exception e)
            {
                Console.WriteLine(m + " - failed with: " + e);
            }
            return null;
        }

        private string consume()
        {
            var token = ps.current;
            ps.accept(token);
            return token;
        }

        private string consume(string x)
        {
            ps.expect(x);
            return x;
        }

        private string lookAhead(ushort i)
        {
            return ps.lookahead(i);
        }

        private Func<Parser,string,OP> getPrefix(String token)
        {
            int val;
            if(Int32.TryParse(token,out val))
            {
                return (x, y) => new Number(val);
            }
            Func<Parser, string, OP> prefix;
            if (prefixMap.TryGetValue(token, out prefix))
                return prefix;
            else
                throw new Exception("Could not parse \"" + token + "\".");
        }

        public OP parseExpression(int precedence)
        {
            var token = consume();
            var prefix = getPrefix(token);

            OP left = prefix(this, token);


            InfixOPParser infix;
            while (precedence < getPrecedence())
            {
                if (ps.current == "")
                    return left;
                token = consume();

                infix = getInfix(token);
                left = infix.op(this, left, token);
            }

            return left;
        }

        private InfixOPParser getInfix(String token)
        {
            InfixOPParser infix;
            if (!infixMap.TryGetValue(token, out infix)) return null;
            return infix;
        }

        private int getPrecedence()
        {
            InfixOPParser parser = getInfix(lookAhead(0));
            if (parser != null) return parser.precedence;
            return 0;
        }


        private void registerPrefix(string token, Func<Parser, string, OP> prefixParse)
        {
            prefixMap.Add(token, prefixParse);
        }

        private void registerInfix(string token, InfixOPParser infixParse)
        {
            infixMap.Add(token, infixParse);
        }

    }


    class InfixOPParser
    {
        public InfixOPParser(Func<Parser, OP, String, OP> _op, int _precedence) { op = _op; precedence = _precedence; }
        public Func<Parser,OP,String,OP> op;
        public int precedence;
    }
    
    class BinaryOperatorParser : InfixOPParser
    {
        public BinaryOperatorParser(int _precedence, bool rightAssoc = false) : base((x, y, z) => parse(rightAssoc, _precedence, x, y, z), _precedence) { }
        public static OP parse(bool rightAssoc, int precedence, Parser parser,OP left, String token)
        {
            OP right = parser.parseExpression(precedence - (rightAssoc ? 1 : 0));
            return new BinaryOp(left, token, right);
        }
    }

    abstract class OP
    {
        public abstract Tuple<int,string> eval();
    }

    class Number : OP
    {
        int x;
        public Number(int _x) { x = _x; }
        public override Tuple<int, string> eval() { return Tuple.Create(x,x.ToString()); }
    }


    class LazyBinaryOp : OP
    {
        static Dictionary<String, Func<OP, OP, Tuple<int, string>>> funcs;

        OP x;
        OP y;
        String Op;

        public LazyBinaryOp(OP _x, String _Op, OP _y)
        {
            x = _x;
            Op = _Op;
            y = _y;
            if (funcs == null)
            {
                funcs = new Dictionary<String, Func<OP, OP, Tuple<int, string>>>();
                funcs.Add("{", (x, y) =>
                 {
                     var count = x.eval();
                     var acc = 0;
                     var msg = count.Item2 + "{";
                     for (int i = 0; i < count.Item1; i++)
                     {
                         var res = y.eval();
                         acc += res.Item1;
                         if (i > 0)
                             msg += " + " + res.Item2;
                         else
                             msg += res.Item2;
                     }
                     msg += "}";
                     return Tuple.Create(acc, msg);
                 }
                    );
            }
        }

        public override Tuple<int, string> eval()
        {
            return funcs[Op](x, y);
        }
    }

    class BinaryOp : OP
    {
        static Random rnd = new Random();
        static Dictionary<String, Func<Tuple<int, string>, Tuple<int, string>, Tuple<int, string>>> funcs;
        OP x;
        OP y;
        String Op;

        private Tuple<T1,T2> t<T1,T2>(T1 t1, T2 t2)
        {
            return Tuple.Create(t1, t2);
        }

        private void addFunc(string symbol, Func<int,int,int> op)
        {
            funcs.Add(symbol, (x, y) => t(op(x.Item1,y.Item1), "(" + x.Item2 + symbol + y.Item2 + ")"));
        }

        private void addLogicFunc(string symbol, Func<int,int,bool> op)
        {
            addFunc(symbol, (x, y) => op(x, y) ? 1 : 0);
        }

        public BinaryOp(OP _x, String _Op, OP _y)
        {
            x = _x;
            Op = _Op;
            y = _y;
            if (funcs == null)
            {
                funcs = new Dictionary<string, Func<Tuple<int, string>, Tuple<int, string>, Tuple<int,string>>>();
                addFunc("+", (x, y) => (x + y));
                addFunc("-", (x, y) => (x - y));
                addFunc("*", (x, y) => (x * y));
                addFunc("\\*", (x, y) => (x * y));
                addFunc("/", (x, y) => (x / y));
                addFunc("^", (x, y) => (y > 99 ? 0 : (y < -99 ? 0 : (int)System.Math.Pow(x, y))));
                Func<Tuple<int, string>, Tuple<int, string>, Tuple<int, string>> diceroll = (x, y) => { var res = Dice.roll(rnd, x.Item1, y.Item1); return (res != null ? t(res.Item1, "[rolled: **" + res.Item1 + "** = " + res.Item2 + " with " + x.Item2 + "d" + y.Item2 + "]") : t(0, "[invalid: " + x.Item2 + "d" + y.Item2 + "]")); };
                funcs.Add("d", diceroll);
                funcs.Add("D", diceroll);
                addLogicFunc("<", (x, y) => (x < y));
                addLogicFunc(">", (x, y) => (x > y));
                addLogicFunc("<=", (x, y) => (x <= y));
                addLogicFunc(">=", (x, y) => (x >= y));
                addLogicFunc("==", (x, y) => (x == y));
                addLogicFunc("&&", (x, y) => (intToBool(x) && intToBool(y)));
                addLogicFunc("||", (x, y) => (intToBool(x) || intToBool(y)));
                addLogicFunc("=>", (x, y) => (!intToBool(x) || intToBool(y)));
            }

        }
        bool intToBool(int x)
        {
            return x == 0 ? false : true;
        }

        public override Tuple<int, string> eval()
        {
            return funcs[Op](x.eval(), y.eval());
        }
        
    }

    class PrefixOp : OP
    {
        String Op;
        OP x;
        static Dictionary<String, Func<Tuple<int, string>, Tuple<int, string>>> funcs;

        private Tuple<T1, T2> t<T1, T2>(T1 t1, T2 t2)
        {
            return Tuple.Create(t1, t2);
        }

        public PrefixOp(String _Op, OP _x)
        {
            x = _x;
            Op = _Op;
            if (funcs == null)
            {
                funcs = new Dictionary<string, Func<Tuple<int, string>, Tuple<int, string>>>();
                funcs.Add("-", x => t(-x.Item1,"(-"+x.Item2+")"));
                funcs.Add("!", x => t((x.Item1 == 0) ? 1 : 0, "(!" + x.Item2 + ")"));
            }
        }

        public override Tuple<int, string> eval()
        {
            return funcs[Op](x.eval());
        }
    }


}
