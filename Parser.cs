/* Generalized version of the precedence parser used by the Math module
 * 
 * TODO: extract or make the math module use this
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace parser2
{
    //so the idea behind this tokenization is that we never have to move backwards after accepting a token.
    //thus what is a valid token can only depend on the past.
    //further, when one rule clicks for making it a token, then the token is that token. 
    //Order guaranteed to try the ones first in the list first and stop when found.
    class Tokenizer<Token> where Token : class
    {
        string input;
        int lastAcceptedIndex = 0;
        int currentIndex = 0;
        List<Token> tokens;
        List<Func<String, Token>> createFuncs;
        public Tokenizer(List<Func<String, Token>> _createFuncs, String _input)
        {
            createFuncs = _createFuncs;
            input = _input;
        }

        private void Tokenize()
        {
            int oldLastAcceptedIndex = 0;
            while (lastAcceptedIndex < input.Length && lastAcceptedIndex > oldLastAcceptedIndex)
            {
                oldLastAcceptedIndex = lastAcceptedIndex;
                foreach (var f in createFuncs)
                {
                    var res = f(input.Substring(lastAcceptedIndex));
                    if (res != null)
                    {
                        tokens.Add(res);
                        break;
                    }
                }
            }
        }

        public static Func<string, Token> substringEqual(String x, Func<String, Token> createToken)
        {
            return (input) =>
            {
                if (input.Length > x.Length && x.StartsWith(x))
                    return createToken(x);
                return null;
            };
        }

        public static Func<string, Token> whileCond(Func<Char, bool> condition, Func<String, Token> createToken)
        {
            return (input) =>
            {
                int currentIndex = 0;
                while (currentIndex < input.Length && condition(input[currentIndex]))
                    currentIndex++;
                if (currentIndex > 0)
                {
                    return createToken(input.Substring(0, currentIndex + 1));
                }
                else
                {
                    return null;
                }
            };
        }

        public static Func<string, Token> nameToken(Func<String, Token> createNameToken)
        {
            return whileCond(Char.IsLetter, createNameToken);
        }

        public static Func<string, Token> numberToken(Func<String, Token> createNumberToken)
        {
            return whileCond(Char.IsDigit, createNumberToken);
        }


    }
    class BaseToken<TokenType>
    {
        public TokenType type;
        public string content;
        public BaseToken(TokenType _type, string _content) { type = _type; content = _content; }
    }

    class ParseState<TokenType>
    {
        List<BaseToken<TokenType>> content;
        private int index = 0;
        public BaseToken<TokenType> current { get { return (index >= content.Count ? null : content[index]); } }
        public ParseState(List<BaseToken<TokenType>> tokens)
        {
            content = tokens;
        }

        public Boolean accept(string s)
        {
            var cur = current;
            if (cur.content == s)
            {
                index++;
                return true;
            }
            return false;
        }
        public Boolean expect(string s)
        {
            var x = accept(s);
            if (x)
                return true;
            //technically? error.
            throw new Exception("invalid parse");
            //return Tuple.Create<String, Boolean>(null,false);
        }

        public BaseToken<TokenType> lookahead(UInt16 i)
        {
            if (index + i >= content.Count)
                return null;
            return content[index + i];
        }

    }


    class Parser<TokenType, AST> where AST : class
    {
        ParseState<TokenType> ps;
        Dictionary<TokenType, Func<Parser<TokenType, AST>, BaseToken<TokenType>, AST>> prefixMap = new Dictionary<TokenType, Func<Parser<TokenType, AST>, BaseToken<TokenType>, AST>>();
        Dictionary<TokenType, InfixOPParser<TokenType, AST>> infixMap = new Dictionary<TokenType, InfixOPParser<TokenType, AST>>();
        static String[] delimiters = { "+", "-", "*", "\\*", "/", "d", "D", "(", ")", "<=", ">=", "==", "<", ">", "{", "}", "&&", "||", "=>", "!", "^" };

        public Parser()
        {
            /*
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
            registerInfix("{", new InfixOPParser((parser, op, token) => { OP expression = parser.parseExpression(0); parser.consume("}"); return new LazyBinaryOp(op, "{", expression); }, Precedence.CONDITIONAL));
            registerInfix("^", new BinaryOperatorParser(Precedence.COMPARATOR));

            registerPrefix("(", (parser, token) => { OP expression = parser.parseExpression(0); parser.consume(")"); return expression; });
            Func<Parser, string, OP> prefixParse = (parser, token) => { OP right = parser.parseExpression(Precedence.PREFIX); return new PrefixOp(token, right); };
            registerPrefix("-", prefixParse);
            registerPrefix("!", prefixParse);

            registerInfix("&&", new BinaryOperatorParser(Precedence.COMPARATOR));
            registerInfix("||", new BinaryOperatorParser(Precedence.COMPARATOR));
            registerInfix("=>", new BinaryOperatorParser(Precedence.COMPARATOR));
            */
        }
        
        public AST parse(List<BaseToken<TokenType>> tokens)
        {
            try
            {
                ps = new ParseState<TokenType>(tokens);
                return parseExpression(0);
            }
            catch (Exception e)
            {
                Console.WriteLine(tokens + " - failed with: " + e);
            }
            return null;
        }

        private BaseToken<TokenType> consume()
        {
            var token = ps.current;
            ps.accept(token.content);
            return token;
        }

        public BaseToken<TokenType> consume(string x)
        {
            var token = ps.current;
            ps.expect(x);
            return token;
        }

        private BaseToken<TokenType> lookAhead(ushort i)
        {
            return ps.lookahead(i);
        }

        private Func<Parser<TokenType, AST>, BaseToken<TokenType>, AST> getPrefix(TokenType tokenType)
        {
            /*            int val;
                        if (Int32.TryParse(token.content, out val))
                        {
                            return (x, y) => new Number(val);
                        }*/
            Func<Parser<TokenType, AST>, BaseToken<TokenType>, AST> prefix;
            if (prefixMap.TryGetValue(tokenType, out prefix))
                return prefix;
            else
                throw new Exception("Could not parse token of type \"" + tokenType + "\".");
        }

        public AST parseExpression(int precedence)
        {
            var token = consume();
            var prefix = getPrefix(token.type);

            AST left = prefix(this, token);


            InfixOPParser<TokenType, AST> infix;
            while (precedence < getPrecedence())
            {
                if (ps.current == null)
                    return left;
                token = consume();

                infix = getInfix(token.type);
                left = infix.op(this, left, token);
            }

            return left;
        }

        private InfixOPParser<TokenType, AST> getInfix(TokenType token)
        {
            InfixOPParser<TokenType, AST> infix;
            if (!infixMap.TryGetValue(token, out infix)) return null;
            return infix;
        }

        private int getPrecedence()
        {
            var tok = lookAhead(0);
            if (tok == null) return 0;
            InfixOPParser<TokenType, AST> parser = getInfix(tok.type);
            if (parser != null) return parser.precedence;
            return 0;
        }


        protected void registerPrefix(TokenType token, Func<Parser<TokenType, AST>, BaseToken<TokenType>, AST> prefixParse)
        {
            prefixMap.Add(token, prefixParse);
        }

        protected void registerInfix(TokenType token, InfixOPParser<TokenType, AST> infixParse)
        {
            infixMap.Add(token, infixParse);
        }

    }


    class InfixOPParser<TokenType, AST> where AST : class
    {
        public InfixOPParser(Func<Parser<TokenType, AST>, AST, BaseToken<TokenType>, AST> _op, int _precedence) { op = _op; precedence = _precedence; }
        public Func<Parser<TokenType, AST>, AST, BaseToken<TokenType>, AST> op;
        public int precedence;
    }

    class BinaryOperatorParser<TokenType, AST> : InfixOPParser<TokenType, AST> where AST : class
    {
        public BinaryOperatorParser(Func<AST, BaseToken<TokenType>, AST, AST> generator, int _precedence, bool rightAssoc = false) : base((x, y, z) => parse(generator, rightAssoc, _precedence, x, y, z), _precedence) { }
        public static AST parse(Func<AST, BaseToken<TokenType>, AST, AST> gen, bool rightAssoc, int precedence, Parser<TokenType, AST> parser, AST left, BaseToken<TokenType> token)
        {
            AST right = parser.parseExpression(precedence - (rightAssoc ? 1 : 0));
            return gen(left, token, right);
        }
    }
}
