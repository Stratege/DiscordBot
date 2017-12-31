using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

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
                    Channel x = await u.User.CreatePMChannel();
                    server.safeSendMessage(x,wmls.Response(u));
                }
            };
        }
    }
}
