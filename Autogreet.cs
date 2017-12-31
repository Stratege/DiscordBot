using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace borkbot
{
    class Autogreet : WelcomeMessageCommandHandler
    {
        public Autogreet(VirtualServer _server) : base(_server, "autogreet", "autogreet <on/off> <autogreet message>")
        {
            server.UserJoined += async (s, u) =>
            {
                if (on)
                {
                    var x = await u.GetOrCreateDMChannelAsync();
                    var chn = x as SocketDMChannel;
                    if (chn != null)
                        server.safeSendMessage(chn, wmls.Response(u));
                    else
                        Console.WriteLine("Could not create DM channel with: " + u.Username);
                }
            };
        }
    }
}
