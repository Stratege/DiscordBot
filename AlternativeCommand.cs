/* Pseudo-module for creating the alternative command invocation syntax
 * 
 * TODO: Rework
 *      - Integrate with new discord bot command options
 *      - make alternative syntax configurable
 *      - give a hint one time per server if they try to use it and it hasn't been enabled yet
 * */

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
