/*
 * Collection of helper functions for serializing/deserializing as well as other small functions
 * 
 * */

using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace borkbot
{
    public class DictSerHelper<T, K>
    {
        public T car;
        public K cdr;
        public DictSerHelper(T _car, K _cdr) { car = _car; cdr = _cdr; }
        public DictSerHelper() { }
    }

    public static class Funcs
    {


        public static T XMLSetup<T>(String filepath)
        {
            using (var filestream = File.Open(filepath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                if (filestream.Length > 0)
                    return (T)new XmlSerializer(typeof(T)).Deserialize(filestream);
                else
                    return default(T);
            }
        }

        public static Dictionary<T, K> XMlDictSetup<T, K>(String filepath)
        {
            var xs = XMLSetup<List<DictSerHelper<T, K>>>(filepath);
            if (xs == null)
                return new Dictionary<T, K>();
            var dict = new Dictionary<T, K>(xs.Count);
            foreach (var x in xs)
            {
                dict.Add(x.car, x.cdr);
            }
            return dict;
        }

        public static void XMLSerialization<T>(String filepath, T obj)
        {
            using (var filestream = File.Open(filepath, FileMode.Create, FileAccess.Write))
            {
                new XmlSerializer(typeof(T)).Serialize(filestream, obj);
            }
        }

        public static void XMlDictSerialization<T, K>(String filepath, Dictionary<T, K> dict)
        {
            var xs = new List<DictSerHelper<T, K>>(dict.Count);
            foreach (var x in dict)
            {
                xs.Add(new DictSerHelper<T, K>(x.Key, x.Value));
            }
            XMLSerialization<List<DictSerHelper<T, K>>>(filepath, xs);
        }


        public static bool validateMentionTarget(ServerMessage e, String m)
        {
            var users = e.msg.MentionedUsers.Where(x => !x.IsBot).Select(x => Tuple.Create(x.Mention, x.Id, x)).ToList();
            //turning this lax... let's hope it works
            //return (users.Count != 0 && m == users[0].Item1);
            return (users.Count != 0);
        }

        public static List<string> splitKeepDelimiters(string input, string[] delimiters)
        {
            int[] nextPosition = delimiters.Select(d => input.IndexOf(d)).ToArray();
            List<string> result = new List<string>();
            int pos = 0;
            while (true)
            {
                int firstPos = int.MaxValue;
                string delimiter = null;
                for (int i = 0; i < nextPosition.Length; i++)
                {
                    if (nextPosition[i] != -1 && nextPosition[i] < firstPos)
                    {
                        firstPos = nextPosition[i];
                        delimiter = delimiters[i];
                    }
                }
                if (firstPos != int.MaxValue)
                {
                    result.Add(input.Substring(pos, firstPos - pos));
                    result.Add(delimiter);
                    pos = firstPos + delimiter.Length;
                    for (int i = 0; i < nextPosition.Length; i++)
                    {
                        if (nextPosition[i] != -1 && nextPosition[i] < pos)
                        {
                            nextPosition[i] = input.IndexOf(delimiters[i], pos);
                        }
                    }
                }
                else
                {
                    result.Add(input.Substring(pos));
                    break;
                }
            }
            return result;
        }


        //taken straight from: http://stackoverflow.com/a/9461311
        public static R WithTimeout<R>(Func<R> proc, int millisecondsDuration)
        {
            var reset = new System.Threading.AutoResetEvent(false);
            var r = default(R);
            Exception ex = null;

            var t = new System.Threading.Thread(() =>
            {
                try
                {
                    r = proc();
                }
                catch (Exception e)
                {
                    ex = e;
                }
                reset.Set();
            });

            t.Start();

            // not sure if this is really needed in general
            while (t.ThreadState != System.Threading.ThreadState.Running)
            {
                System.Threading.Thread.Sleep(0);
            }

            if (!reset.WaitOne(millisecondsDuration))
            {
                t.Abort();
                throw new TimeoutException();
            }

            if (ex != null)
            {
                throw ex;
            }

            return r;
        }

        static public SocketGuildUser GetUserByMentionOrName(IEnumerable<SocketGuildUser> users, String str)
        {
            String mentionString;
            if (str[0] == '<' && str[1] == '@' && str[2] != '!')
            {
                mentionString = "<@!" + str.Substring(2);
            }
            else
            {
                mentionString = str;
            }
            return users.FirstOrDefault(x => x.Mention == mentionString /*|| x.NicknameMention == split[0]*/ || x.Username == str || x.Nickname == str);
        }

    }
}