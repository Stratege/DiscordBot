using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace borkbot
{
    /// <summary>
    /// Represents the role modules, made to allow users to grant/remove their roles
    /// </summary>
    class RoleCommand : EnableableCommandModule
    {

        #region Data Members

        // Represents the whitelisted roles, saved in the file
        private PersistantList whitelistedRoles;

        private string _timeoutFile = "TimeoutRole.txt";

        private SocketRole _timeoutRole;

        #endregion

        #region Properties

        public PersistantList WhitelistedRoles
        {
            get
            {
                return whitelistedRoles;
            }
            set
            {
                whitelistedRoles = value;
            }
        }

        public SocketRole TimeoutRole
        {
            get
            {
                return _timeoutRole;
            }

            set
            {
                _timeoutRole = value;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor
        /// Initializes the whitelisted roles from the file
        /// </summary>
        /// <param name="_server"></param>
        public RoleCommand(VirtualServer _server, DiscordSocketClient _dc) : base(_server, "role")
        {
            WhitelistedRoles = PersistantList.Create(server, "WhitelistedRoles.txt");
            _dc.RoleUpdated += _dc_RoleUpdated;
            _dc.RoleDeleted += _dc_RoleDeleted;

            // Get the strike channel to print admin alerts in
            var res = server.FileSetup(_timeoutFile);
            if (res.Count > 0)
            {
                TimeoutRole = server.getServer().Roles.FirstOrDefault(x => x.Name == res[0]);
            }
        }

        #endregion

        /// <summary>
        /// Gets all the available commands from this module into the command list
        /// This includes dynamically created commands by role names
        /// </summary>
        /// <returns></returns>
        public override List<Tuple<string, Command>> getCommands()
        {
            var commands = base.getCommands();
            commands.Add(new Tuple<string, Command>("role", makeEnableableCommand(ModifyRoles, PrivilegeLevel.Everyone, "role <role name>")));
            commands.Add(new Tuple<string, Command>("fullaccess", makeEnableableCommand(GrantFullAccess, PrivilegeLevel.Everyone, "fullaccess")));
            commands.Add(new Tuple<string, Command>("modifywhitelistedrole", makeEnableableAdminCommand(ModifyWhitelistedRole, "modifywhitelistedrole <role name>")));
            commands.Add(new Tuple<string, Command>("printwhitelistedroles", makeEnableableAdminCommand(PrintWhitelistedRoles, "printwhitelistedroles <role name>")));
            commands.Add(new Tuple<string, Command>("settimeoutrole", makeEnableableAdminCommand(SetTimeoutRole, "settimeoutrole <role name>")));

            // Go over all the whitelisted roles to create their own !<role name> commands, to toggle them individually
            foreach (var roleID in WhitelistedRoles)
            {
                var role = GetRoleByID(roleID);
                if (role != null)
                {
                    // We might be trying to add the same command twice, because of roles that share the same first word
                    // For example: Stable Dweller exists, now trying to add Stable Pony.
                    if (!commands.Any((t) => t.Item1 == GetRoleNameCommand(role.Name)))
                    {
                        commands.Add(new Tuple<string, Command>(GetRoleNameCommand(role.Name), new Command(server, ModifyRolesByCommandName, PrivilegeLevel.Everyone, role.Name + " (role command)")));
                    }
                }
                else
                {
                    // Error handling
                    Console.WriteLine("The following ID does not match any role on the server: " + roleID);
                }
            }

            return commands;
        }

        /// <summary>
        /// Because commands can only be one word, it makes it a bit difficult when we want to be able to toggle roles using !<role name> if roles can be several words long
        /// Thus, we use this function to only take the first word in the name, and make that the starting command.
        /// Later on when it's invoked, we take all of the user's input and check it against the whitelisted channels
        /// </summary>
        /// <param name="roleName"></param>
        /// <returns></returns>
        private string GetRoleNameCommand(string roleName)
        {
            if (roleName != null)
            {
                roleName = roleName.ToLower();
                if (roleName.Contains(" "))
                {
                    roleName = roleName.Split(' ')[0];
                }

                return roleName;
            }

            return null;
        }

        /// <summary>
        /// Gets the role by ID, used mainly for the Whitelist, which holds the whitelisted roles' IDs
        /// </summary>
        /// <param name="roleID"></param>
        /// <returns></returns>
        private SocketRole GetRoleByID(string roleID)
        {
            ulong id;
            if (ulong.TryParse(roleID, out id))
            {
                return server.getServer().GetRole(ulong.Parse(roleID));
            }
            else
            {
                return null;
            }
        }

        #region Event Handlers

        /// <summary>
        /// An event handler which is fired when a role has been deleted
        /// It manages the whitelist and commandlist as appropriate
        /// </summary>
        /// <param name="deletedRole"></param>
        /// <returns></returns>
        private Task _dc_RoleDeleted(SocketRole deletedRole)
        {
            return Task.Run(() =>
            {
                // If the deleted role is from this server and is whitelisted, properly remove it from all lists
                if (deletedRole.Guild.Id == server.getServer().Id && WhitelistedRoles.Contains(deletedRole.Id.ToString()))
                {
                    string deletedRoleName = GetRoleNameCommand(deletedRole.Name);

                    // Remove the old role from the list so we can check if there are other roles with the same name
                    WhitelistedRoles.Remove(deletedRole.Id.ToString());

                    // If none exist with the same name, we can safely remove the command
                    if (WhitelistedRoles.All((id) => GetRoleNameCommand(GetRoleByID(id)?.Name) != deletedRoleName))
                    {
                        server.Commandlist.Remove(deletedRoleName);
                    }

                    Console.WriteLine("The role " + deletedRole.Name + " was deleted, updated its whitelist/commandlist standings");
                }
            });
        }

        /// <summary>
        /// An event handler which is fired when a role has been updated
        /// It manages the CommandList as appropriate if the name has been changed
        /// </summary>
        /// <param name="oldRole"></param>
        /// <param name="newRole"></param>
        /// <returns></returns>
        private Task _dc_RoleUpdated(SocketRole oldRole, SocketRole newRole)
        {
            return Task.Run(() =>
            {
                // If the role updated was from this server, in the whitelist, and the name was updated, update the commandlist
                if (oldRole.Guild.Id == server.getServer().Id && WhitelistedRoles.Contains(oldRole.Id.ToString()) && oldRole.Name != newRole.Name)
                {
                    string oldRoleName = GetRoleNameCommand(oldRole.Name);

                    // Remove the old role from the list so we can check if there are other roles with the same name
                    WhitelistedRoles.Remove(oldRole.Id.ToString());

                    // If none exist with the same name, we can safely remove the command
                    if (WhitelistedRoles.All((id) => GetRoleNameCommand(GetRoleByID(id)?.Name) != oldRoleName))
                    {
                        server.Commandlist.Remove(oldRoleName);
                    }

                    // Readding the role because the ID didn't change, and it was just for the check above
                    WhitelistedRoles.Add(oldRole.Id.ToString());

                    // We might be trying to add the same command twice, because of roles that share the same first word
                    // For example: Stable Dweller exists, now trying to add Stable Pony.
                    if (!server.Commandlist.ContainsKey(GetRoleNameCommand(newRole.Name)))
                    {
                        server.Commandlist.Add(GetRoleNameCommand(newRole.Name), new Command(server, ModifyRolesByCommandName, PrivilegeLevel.Everyone, ""));
                    }

                    Console.WriteLine("The role " + oldRole.Name + " has been updated to the role " + newRole);
                }
            });
        }

        #endregion

        #region Admin Commands

        /// <summary>
        /// Sets the strike alert channel when a user receives more than 3 strikes
        /// </summary>
        /// <param name="e"></param>
        /// <param name="m"></param>
        private void SetTimeoutRole(ServerMessage e, string m)
        {
            m = m.ToLower();
            if (m.Length > 0 && m[0] == '#')
                m = m.Substring(1);
            var res = server.getServer().Roles.FirstOrDefault(x => x.Name.ToLower() == m);
            string message;
            if (res == null)
                message = "Could not find role: " + m;
            else
            {
                message = "Role was set to: " + res.Name;
                TimeoutRole = res;
                server.fileCommand(_timeoutFile, x => System.IO.File.WriteAllText(x, res.Name));
            }
            server.safeSendMessage(e.Channel, message);
        }


        /// <summary>
        /// Prints all the current whitelisted roles
        /// </summary>
        /// <param name="e"></param>
        /// <param name="s"></param>
        private void PrintWhitelistedRoles(ServerMessage e, string s)
        {
            if (!on)
                return;

            string roles = "";
            foreach (string roleID in WhitelistedRoles)
            {
                roles += GetRoleByID(roleID).Name + ", ";
            }

            if (roles.Length > 0)
            {
                roles = roles.Remove(roles.Length - 2);
            }

            server.safeSendMessage(e.Channel, "The whitelisted roles are as follows:\n" + roles);
        }

        /// <summary>
        /// Adds/removes whitelisted roles the users can't get via the !role command
        /// </summary>
        /// <param name="e"></param>
        /// <param name="roleName"></param>
        private void ModifyWhitelistedRole(ServerMessage e, string roleName)
        {
            if (!on)
                return;

            var specifiedRole = server.getServer().Roles.FirstOrDefault((r) => r.Name.ToLower() == roleName.ToLower());
            if (specifiedRole == null)
            {
                server.safeSendMessage(e.Channel, "The role " + roleName + " does not exist!");
            }
            // Add the role to the whitelist
            else if (!WhitelistedRoles.Contains(specifiedRole.Id.ToString()))
            {
                WhitelistedRoles.Add(specifiedRole.Id.ToString());

                // We might be trying to add the same command twice, because of roles that share the same first word
                // For example: Stable Dweller exists, now trying to add Stable Pony.
                if (!server.Commandlist.ContainsKey(GetRoleNameCommand(specifiedRole.Name)))
                {
                    server.Commandlist.Add(GetRoleNameCommand(specifiedRole.Name), new Command(server, ModifyRolesByCommandName, PrivilegeLevel.Everyone, ""));
                }

                server.safeSendMessage(e.Channel, "The role " + specifiedRole.Name + " has been added to the list of whitelisted roles users could get via the !role command, or by writing !<role name>");
            }
            // Remove the role from the whitelist
            else
            {
                WhitelistedRoles.Remove(specifiedRole.Id.ToString());

                string realRoleName = GetRoleNameCommand(specifiedRole.Name);

                // If none exist with the same name, we can safely remove the command
                if (WhitelistedRoles.All((id) => GetRoleNameCommand(GetRoleByID(id)?.Name) != realRoleName))
                {
                    server.Commandlist.Remove(realRoleName);
                }

                server.safeSendMessage(e.Channel, "The role " + specifiedRole.Name + " has been removed from the list of whitelisted roles users could get via the !role command, or by writing !<role_name>");
            }
        }

        #endregion

        #region User Commands

        /// <summary>
        /// Grants all current available 
        /// </summary>
        /// <param name="e"></param>
        /// <param name="message"></param>
        private void GrantFullAccess(ServerMessage e, string message)
        {
            if (!on || TimeoutRole.Members.Any((u) => u.Id == e.Author.Id))
                return;

            foreach (var roleID in WhitelistedRoles)
            {
                var specifiedRole = GetRoleByID(roleID);
                if (!specifiedRole.IsMentionable)
                {
                    e.Author.AddRoleAsync(specifiedRole);
                }
            }

            server.safeSendMessage(e.Channel, "*Woof!* I have borked all the available roles onto you!");
        }

        /// <summary>
        /// Grants/removes the user a specified role by the command name, like !wastelander instead of !role wasteland
        /// </summary>
        /// <param name="e"></param>
        /// <param name="message"></param>
        /// <param name="commandRoleName"></param>
        private void ModifyRolesByCommandName(ServerMessage e, string message)
        {
            if (!on || TimeoutRole.Members.Any((u) => u.Id == e.Author.Id))
                return;

            var command = server.parseMessageString(e.msg.Content);
            var fullRoleName = command.Item1;

            // This is to get the full name of the role, as specified by the user
            if (command.Item2 != "")
            {
                fullRoleName += " " + command.Item2;
            }

            ModifyRoles(e, fullRoleName);
        }

        /// <summary>
        /// Grants/removes the user the specified role
        /// </summary>
        /// <param name="e"></param>
        /// <param name="roleName"></param>
        private void ModifyRoles(ServerMessage e, string roleName)
        {
            if (!on || TimeoutRole.Members.Any((u) => u.Id == e.Author.Id))
                return;

            // Help!
            if (roleName == "" || roleName == "help")
            {
                server.safeSendMessage(e.Channel, "Just write ``!<role name> of the role you want to get/remove>``.\n" +
                                                  "For example: ``!wastelander`` or ``!stable dweller``");
                return;
            }

            // Sanitization, to stop @everyone pings
            roleName = roleName.Replace("@", "");

            var specifiedRole = server.getServer().Roles.FirstOrDefault((r) => r.Name.ToLower() == roleName.ToLower());

            // If there is no such role, print a response
            if (specifiedRole == null)
            {
                server.safeSendMessage(e.Channel, "Sorry, but the role '" + roleName + "' does not exist! Please write ``!role help`` for assistance!");
            }
            // If the role is whitelisted
            else if (WhitelistedRoles.Contains(specifiedRole.Id.ToString()))
            {
                // If the user has that role, remove it
                if (specifiedRole.Members.Any((u) => u.Id == e.Author.Id)) // You could probably check it the other way around too, but this already works and I'm pretty lazy
                {
                    e.Author.RemoveRoleAsync(specifiedRole);
                    server.safeSendMessage(e.Channel, "*Woof!* I have borked the " + roleName + " role off of you!");
                }
                // If the user doesn't have that role, add it
                else
                {
                    e.Author.AddRoleAsync(specifiedRole);
                    server.safeSendMessage(e.Channel, "*Woof!* I have borked the " + roleName + " role onto you!");
                }
            }
            // Message if the role selected is forbidden
            else
            {
                server.safeSendMessage(e.Channel, "This role is unavailable to simple plebians such as yourself.");
            }
        }

        #endregion

    }
}
