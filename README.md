# NR Bot

A modular discord bot with modules for
- Server Administration
- Message Proxying
- TTRPG Support

# Architecture

For each server the bot is in it creates a [VirtualServer](/VirtualServer.cs) object.

Further, each **module** gets one object instance created for each VirtualServer and registers its command handlers with it (in the constructor of the VirtualServer). Modules also get a reference to the VirtualServer which they can use for getting access to hooks more directly.

Whenever any **message** is **received** from the API it gets routed through the VirtualServer which then dispatches it via the command handlers or the hooks, depending on the message. PMs are routed to a VirtualServer based on which one the particular User's active VirtualServer is (since most users are only in 1 server with the bot, that case uses a defaulting method to set the VirtualServer).

All **message sending** is routed through the VirtualServer object via server.safeSendMessage or server.safeSendEmbed, giving a central place to enforce security constraints.

All **persistence** is handled via [Persistent Dictionaries](/Persistence/PersistentDict.cs), [Persistent Lists](/Persistence/PersistentList.cs) and [Persistent Values](/Persistence/PersistentValue.cs), which in turn call the VirtualServers' XMLSerialization methods to ensure proper isolation of the environment.


As a result, new modules can be developed **quickly and securely**, without having to spend a lot of time thinking about security or persistence.

# Current Modules

## Administration
- Admingreet/Adminleave - send a message in a designated admin channel whenever a user joins or leaves (legacy feature from before discord had join notifications)
- Autogreet - greet new people with a MSG in PM
- EmoteModule - counts usages of emotes on the server to get empirical data on which emotes are most or least popular to help with choosing which to replace when all slots are used
- GloriousDirectDemocracy - a module to run votes via the bot
- KillUserMessages - remove all messages of a specific user from the server, e.g. to combat spambots
- Move - module to move N random users from one voice channel to another, used by Supreme Commander streamers to randomly pick people to be in voice chat and play with them in the next match
- Reminder - set up reminders to regularly post in a channel
- Report - module to anonymously contact the moderators as well as reply functionality based on report ID.
- Roles - Allow users to set their own roles based on a whitelist
- Strikes - keep track of rules infractions by users
- UntaggedUsers - find out who does not have any tags or who has a specific tag (legacy feature from before discord had a UI for this)
- UserInfo - get basic info on when a user joined the server and their permissions (legacy feature from before discord had a UI for this)

## User Functionality
- Dice - basic dice rolling functionality
- Math - basic math as well as advanced dice rolling functionality (can roll amount of dice based on dice rolls, as well as comparisons, etc)
- Experience - in RP channels users can get Experience per message (based on message length), used in the Northern Reaches Megagame to provide XP rewards for chat based roleplay outside of sessions
- Proxy - allows message proxying via a prefix (or in RP channels - without). Distinguishes between RP mode and non-RP mode with RP proxies only allowed in designated RP channels (and no normal message or non-RP proxies allowed in those channels)
- Scryfall - allows looking up mtg cards on scryfall via the common \[\[\<cardname\>\]\] syntax

## Just For Fun
- Bork (Admin Only) - adds a per-user chosen emote to that user's message every n-th message
- Echo (Admin Only) - send messages as the bot
- Roulette
- Someone (Admin Only) - ping a random user
