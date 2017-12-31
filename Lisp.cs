using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace borkbot
{
    class Lisp
    {
    }

}

namespace parser2
{
    class NoParseException : Exception { public static NoParseException except = new NoParseException(); }


    class MonadicParser<T>
    {
        protected Func<string, List<Tuple<T, string>>> content;
        private MonadicParser(Func<string, List<Tuple<T, string>>> _content) { content = _content; }
        public static MonadicParser<T> pure(T x) { return new MonadicParser<T>(str => { var ret = new List<Tuple<T, string>>(); ret.Add(Tuple.Create(x, str)); return ret; }); }
        public MonadicParser<A> bind<A>(Func<T,MonadicParser<A>> mf) {
            Func<string, List<Tuple<A, string>>> f = str =>
              {
                  var xs = this.run(str);
                  var res = new List<Tuple<A, string>>();
                  foreach(var x in xs)
                  {
                      var t = mf(x.Item1).run(x.Item2);
                      if (t != null)
                          res.AddRange(t.Where(elem => elem != null));
                  }
                  return res;
              };
            return new MonadicParser<A>(f);
        }
        public MonadicParser<A> rightright<A>(MonadicParser<A> mx) { return this.bind<A>(x => mx); }
        public List<Tuple<T, string>> run(String x) { return content(x); }
    }

    class ParserState
    {
        List<int> ls;
        int indexPointer = -1;
        public void Push()
        {
            if (indexPointer == -1)
            {
                ls = new List<int>();
                ls.Add(0);
                ls.Add(0);
                indexPointer++;
            }
            else
            {
                ls.Add(ls[indexPointer]);
            }
            indexPointer++;
        }
        public void Pop() { ls.RemoveAt(indexPointer); indexPointer--; }
        public void Squash() { var cur = ls[indexPointer]; Pop(); ls[indexPointer] = cur; }
        public int Peek() { return ls[indexPointer]; }
        public void Set(int x) { ls[indexPointer] = x; }
    }

    class TestParser : MParse2
    {
        public TestParser() : base()
        {

        }

        public AST Run(string str)
        {
            try
            {
                return run(str,Term);
            }
            catch
            {
                return null;
            }
        }

        AST Atom()
        {
            var res = matchAlphanumeric();
            if (res != "")
                return new Atom(res);
            return null;
        }

        AST Pair()
        {
            match("(");
            var a = Term();
            match(".");
            var b = Term();
            match(")");
            return new Pair(a, b);
        }

        AST List()
        {
            return ListSharedCode(() => match("("),() => match(")"));
        }

        AST LRep()
        {
            return ListSharedCode(() => { Whitespace(); }, null);
        }

        AST ListSharedCode(Action before, Action after)
        {
            before?.Invoke();
            var a = Term();
            var b = Maybe(LRep);
            after?.Invoke();
            if (b.Item1)
            {
                return new Pair(a, b.Item2);
            }
            else
            {
                return new Pair(a, parser2.Atom.NIL);
            }
        }

        AST Quote()
        {
            match("'");
            return new Pair(parser2.Atom.QUOTE, Term());
        }

        AST Term() { return OneOf(Pair,Atom,List,Quote);  }

    }
    
    abstract class AST { }
    class Atom : AST { public static Atom NIL = new Atom("NIL"); public static Atom QUOTE = new Atom("QUOTE"); public string val; public Atom(string _val) { val = _val; } }
    class Pair : AST { public AST car; public AST cdr; public Pair(AST a, AST b) { car = a;  cdr = b; } }

    class WMParse2 : MParse2
    {
        public WMParse2() { }

        public override void match(string x)
        {
            Maybe(Whitespace);
            base.match(x);
        }

        public override string matchAlphanumeric()
        {
            return addOptionalWhitespace(base.matchAlphanumeric);
        }

        public override string matchNumber()
        {
            return addOptionalWhitespace(base.matchNumber);
        }

        private T addOptionalWhitespace<T>(ParseRule<T> rule) where T : class
        {
            Maybe(Whitespace);
            var res = rule();
            return res;
        }
    }

    class MParse2
    {
        public delegate T ParseRule<T>() where T : class;
        public ParserState state { get; private set; }
        protected string inputString;

        public MParse2()
        {
        }

        protected T run<T>(String str, ParseRule<T> start) where T : class
        {
            state = new ParserState();
            inputString = str;
            try
            {
                return Parse(start);
            }
            catch
            {
                return null;
            }
        }

        protected void BeginRule()
        {
            state.Push();
        }

        protected void FailRule()
        {
            state.Pop();
        }

        protected void CancelRule()
        {
            state.Pop();
        }

        protected void SucceedRule()
        {
            // Flatten state stack so that we maintain the same values,
            // but remove one level in the stack.
            state.Squash();
        }

        public T Parse<T>(ParseRule<T> rule) where T : class
        {
            BeginRule();
            try
            {
                var result = rule();
                SucceedRule();
                return result;
            }
            catch (Exception e)
            {
                FailRule();
                throw e;
            }
        }

        public T OneOf<T>(params ParseRule<T>[] array) where T : class
        {
            foreach (ParseRule<T> rule in array)
            {
                try
                {
                    return Parse<T>(rule);
                }
                catch
                {

                }
            }
            throw NoParseException.except;
        }

        public virtual void match(string elm)
        {
            var start = state.Peek();
            if (inputString.Substring(start).StartsWith(elm))
            {
                state.Set(start + elm.Length);
            }
            else
            {
                throw NoParseException.except;
            }
        }

        public virtual string matchWhile(Func<char, bool> f)
        {
            var start = state.Peek();
            var str2 = inputString.Substring(start);
            int i = 0;
            while (str2.Length > i && f(str2[i]))
                i++;
            if (i == 0)
                throw NoParseException.except;
            state.Set(start + i);
            return str2.Substring(0, i);
        }

        public virtual string matchAlphanumeric()
        {
            var start = state.Peek();
            var str2 = inputString.Substring(start);
            int i = 0;
            while (str2.Length > i && Char.IsLetterOrDigit(str2[i]))
                i++;
            if (i == 0)
                throw NoParseException.except;
            state.Set(start + i);
            return str2.Substring(0, i);
        }

        public virtual string matchNumber()
        {
            var start = state.Peek();
            var str2 = inputString.Substring(start);
            int i = 0;
            while (str2.Length > i && Char.IsDigit(str2[i]))
                i++;
            if (i == 0)
                throw NoParseException.except;
            state.Set(start + i);
            return str2.Substring(0, i);
        }

        public virtual void EndOfInput()
        {
            var start = state.Peek();
            if (start < inputString.Length)
                throw NoParseException.except;
        }

        public string Whitespace()
        {
            var start = state.Peek();
            var str2 = inputString.Substring(start);
            int i = 0;
            while (str2.Length > i && Char.IsWhiteSpace(str2[i]))
                i++;
            if (i == 0)
                throw NoParseException.except;
            state.Set(start + i);
            return str2.Substring(0, i);
        }

        public Tuple<bool, T> Maybe<T>(ParseRule<T> rule) where T : class
        {
            try
            {
                return Tuple.Create(true, Parse(rule));
            }
            catch
            {
                return Tuple.Create<bool, T>(false, null);
            }
        }
    }

    /*
    public enum LispTokenTypes { Name, Number, BracketLeft, BracketRight, Quote, Space, Dot }


    class LispTokens : BaseToken<LispTokenTypes>
    {
        public LispTokens(LispTokenTypes _type, string _content) : base(_type, _content)
        {
        }
    }
    
    class LispTokenizer : Tokenizer<LispTokens>
    {
        private static List<Func<String, LispTokens>> _createFuncs;

        public LispTokenizer(String _input) : base(_createFuncs, _input)
        {
            if(_createFuncs == null)
            {
                _createFuncs = new List<Func<String, LispTokens>>();
                _createFuncs.Add(Tokenizer<LispTokens>.nameToken(x => new LispTokens(LispTokenTypes.Name, x)));
                _createFuncs.Add(Tokenizer<LispTokens>.numberToken(x => new LispTokens(LispTokenTypes.Number, x)));
                SubstringEqual("'", LispTokenTypes.Quote);
                SubstringEqual("(", LispTokenTypes.BracketLeft);
                SubstringEqual(")", LispTokenTypes.BracketRight);
                SubstringEqual(" ", LispTokenTypes.Space);
                SubstringEqual(".", LispTokenTypes.Dot);
            }
        }

        private void SubstringEqual(string x, LispTokenTypes type)
        {
            _createFuncs.Add(Tokenizer<LispTokens>.substringEqual(x, y => new LispTokens(type, y)));

        }
    }

    public class LispPrecedence
    {
        public static int DOT = 1;
        public static int PREFIX = 2;
    }

    class LispParser : Parser<LispTokenTypes,LispLazyAST>
    {
        public LispParser()
        {
            registerPrefix(LispTokenTypes.BracketLeft, (parser, token) => { LispLazyAST expr = parser.parseExpression(0); parser.consume(")"); return expr; );
            registerPrefix(LispTokenTypes.Space, (parser, token) => { return parser.parseExpression(0); });

            Func<Parser<LispTokenTypes, LispLazyAST>, BaseToken<LispTokenTypes>, LispLazyAST> prefixParse = (parser, token) => { LispLazyAST right = parser.parseExpression(LispPrecedence.PREFIX); return new Pair(new Atom(token), right); };
            //            registerPrefix("-", prefixParse);
            //            registerPrefix("!", prefixParse);
            registerPrefix(LispTokenTypes.Quote, prefixParse);
            Func<Parser<LispTokenTypes, LispLazyAST>, BaseToken<LispTokenTypes>, LispLazyAST> atomParse = (parser, token) => { return new LazyAtom(token); };
            registerPrefix(LispTokenTypes.Name, atomParse);
            registerPrefix(LispTokenTypes.Number, atomParse);

            registerInfix(LispTokenTypes.Dot, new BinaryOperatorParser<LispTokenTypes, LispLazyAST>((left, token, right) => new Pair(left, right), LispPrecedence.DOT, true));
        }
    }
    // data AST = Atom X | Pair AST AST
    // data LazyAST = LAtom X | LPair (Either AST LazyAST) (Either AST LazyAST) | LSpace (Either AST LazyAST) | LBracket (Either AST LazyAST)
    //force (LAtom x) = Left (Atom x)
    //force (LPair (Left x) (Left y)) = Left (Pair x y)
    //force (LPair (Left x) (Right y)) = LPair (Left x) (force y)
    //force (LPair (Right x) y = LPair (force x) y
    //force (LSpace (Left x)) = Left x
    //force (Lspace (Right x)) = Right (LSpace (force x))
    //force (LBracket (Left x)) = 

    abstract class LispLazyAST
    {
        public BaseToken<LispTokenTypes> token;
        public abstract LispAST force();
        public LispLazyAST(BaseToken<LispTokenTypes> _token) { token = _token; }
    }

    class LSpace : LispLazyAST
    {
        LispLazyAST 
        public LSpace(BaseToken<LispTokenTypes> _token, LispLazyAST _next) : base(_token)
        {
        }

        public override LispAST force()
        {
            throw new NotImplementedException();
        }
    }

    class LazyAtom : LispLazyAST
    {
        public LazyAtom(BaseToken<LispTokenTypes> _token) : base(_token)
        public override LispAST force() { return new Atom(token);}
    }

    class LazyPair : LispLazyAST
    {
        public 

        public LazyPair(LispLazyAST _left, LispLazyAST _right) { left = _left; right = _right; }
        public force() { }
    }

    abstract class LispAST
    {

    }

    class Pair : LispAST
    {
        public LispAST left;
        public LispAST right;
        public Pair(LispAST _left, LispAST _right) { left = _left; right = _right; }
    }

    class Atom : LispAST
    {
        public BaseToken<LispTokenTypes> token;
        public Atom(BaseToken<LispTokenTypes> _token) { token = _token; }
    }

    /*

    abstract class OP
    {
        public abstract Tuple<int, string> eval();
    }

    class Number : OP
    {
        int x;
        public Number(int _x) { x = _x; }
        public override Tuple<int, string> eval() { return Tuple.Create(x, x.ToString()); }
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

        private Tuple<T1, T2> t<T1, T2>(T1 t1, T2 t2)
        {
            return Tuple.Create(t1, t2);
        }

        private void addFunc(string symbol, Func<int, int, int> op)
        {
            funcs.Add(symbol, (x, y) => t(op(x.Item1, y.Item1), "(" + x.Item2 + symbol + y.Item2 + ")"));
        }

        private void addLogicFunc(string symbol, Func<int, int, bool> op)
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
                funcs = new Dictionary<string, Func<Tuple<int, string>, Tuple<int, string>, Tuple<int, string>>>();
                addFunc("+", (x, y) => (x + y));
                addFunc("-", (x, y) => (x - y));
                addFunc("*", (x, y) => (x * y));
                addFunc("\\*", (x, y) => (x * y));
                addFunc("/", (x, y) => (x / y));
                addFunc("^", (x, y) => (y > 20 ? 0 : (y < -20 ? 0 : (int)System.Math.Pow(x, y))));
                Func<Tuple<int, string>, Tuple<int, string>, Tuple<int, string>> diceroll = (x, y) => { var res = borkbot.Dice.roll(rnd, x.Item1, y.Item1); return (res != null ? t(res.Item1, "[rolled: **" + res.Item1 + "** = " + res.Item2 + " with " + x.Item2 + "d" + y.Item2 + "]") : t(0, "[invalid: " + x.Item2 + "d" + y.Item2 + "]")); };
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
                funcs.Add("-", x => t(-x.Item1, "(-" + x.Item2 + ")"));
                funcs.Add("!", x => t((x.Item1 == 0) ? 1 : 0, "(!" + x.Item2 + ")"));
            }
        }

        public override Tuple<int, string> eval()
        {
            return funcs[Op](x.eval());
        }
    }*/
}
