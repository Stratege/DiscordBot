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

All **persistence** is handled via [Persistant Dictionaries](/PersistantDict.cs), [Persistant Lists](/PersistantList.cs) and [Persistant Values](/PersistantValue.cs), which in turn call the VirtualServers' XMLSerialization methods to ensure proper isolation of the environment.


As a result, new modules can be developed **quickly and securely**, without having to spend a lot of time thinking about security or persistance.
