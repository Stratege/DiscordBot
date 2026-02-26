/* Module for automated sending of a PM to newly joined user
 * */

using System;
using Discord.WebSocket;
using DiscordBot.Utility;

namespace DiscordBot.Modules
{
    class Autogreet : WelcomeMessageCommandHandler
    {
        public Autogreet(VirtualServer _server) : base(_server, "autogreet", new HelpMsgStrings("", "autogreet <on/off> <autogreet message>"))
        {
            server.UserJoined += async (s, u) =>
            {
                if (on)
                {
                    var x = await u.CreateDMChannelAsync();
                    var chn = x as SocketDMChannel;
                    if (chn != null)
                        await server.safeSendMessage(chn, wmls.Response(u));
                    else
                        Console.WriteLine("Could not create DM channel with: " + u.Username);
                }
            };
        }
    }
}
