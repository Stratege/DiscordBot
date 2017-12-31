using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using System.Text.RegularExpressions;

namespace borkbot
{
    class WelcomeMessage
    {
        private List<WelcomeMessageContent> content;
        private static string[] delimiters = { WelcomeMessageId.raw, WelcomeMessageName.raw };

        public WelcomeMessage(String config)
        {
            
            var res = Regex.Split(config, string.Join("|",delimiters.Select(x => "(" + x + ")")));
            content = new List<WelcomeMessageContent>(res.Length * 2);
            foreach(var x in res)
            {
                if (x == WelcomeMessageId.raw)
                    content.Add(new WelcomeMessageId());
                else if (x == WelcomeMessageName.raw)
                    content.Add(new WelcomeMessageName());
                else
                    content.Add(new WelcomeMessageString(x));
            }
        }

        public string Response(SocketGuildUser u)
        {
            return String.Join(String.Empty,content.Select(x => x.getString(u)));
        }

        public string RawMessage()
        {
            return String.Join(String.Empty, content.Select(X => X.getRawString()));
        }
    }

    interface WelcomeMessageContent
    {
        string getString(SocketGuildUser u);
        string getRawString();
    }

    class WelcomeMessageId : WelcomeMessageContent
    {
        public static string raw = "{user}";
        public string getString(SocketGuildUser u) { return u.Mention; }
        public string getRawString() { return raw; }
    }

    class WelcomeMessageString : WelcomeMessageContent
    {
        private String str;
        public WelcomeMessageString(String _str) { str = _str; }
        public string getString(SocketGuildUser u) { return str;  }
        public string getRawString() { return str; }
    }

    class WelcomeMessageName : WelcomeMessageContent
    {
        public static string raw = "{username}";
        public string getString(SocketGuildUser u) { return u.Username; }
        public string getRawString() { return raw; }
    }

}
