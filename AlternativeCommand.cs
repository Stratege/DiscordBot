using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace borkbot
{
    class AlternativeCommand : EnableableCommandModule
    {
        public string alternativeSyntax = "!";
        public AlternativeCommand(VirtualServer _server) : base(_server, "altcommand")
        {
        }
        public bool isOn { get { return on; } }
    }
}
