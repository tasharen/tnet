-----------------------------------------------------
        TNet: Tasharen Networking Framework
      Copyright © 2012 Tasharen Entertainment
                  Version 1.6.0
       http://www.tasharen.com/?page_id=4518
               support@tasharen.com
-----------------------------------------------------

Thank you for buying TNet!

If you have any questions, suggestions, comments or feature requests, please
drop by the NGUI forum, found here: http://www.tasharen.com/forum/index.php

-----------------------------------------------------
  Basic Usage
-----------------------------------------------------

Q: How to start and stop a server from in-game?

TNServerInstance.Start(tcpPort, udpPort, [fileToLoad]);
TNServerInstance.Stop([fileToSave]]);

Q: How to connect/disconnect?

TNManager.Connect(address);
TNManager.Disconnect();

Q: How to join/leave a channel?

TNManager.JoinChannel(id, levelToLoad);
TNManager.LeaveChannel();

Q: How to instantiate new objects and then destroy them?

TNManager.Create(gameObject, position, rotation);
TNManager.Destroy(gameObject);

Q: How to send a remote function call?

TNObject tno = GetComponent<TNObject>(); // You can skip this line if you derived your script from TNBehaviour
tno.Send("FunctionName", target, <parameters>);

Q: What built-in notifications are there?

OnNetworkConnect (success, error);
OnNetworkDisconnect()
OnNetworkJoinChannel (success, error)
OnNetworkLeaveChannel()
OnNetworkPlayerJoin (player)
OnNetworkPlayerLeave (player)
OnNetworkPlayerRenamed (player, previousName)
OnNetworkError (error)

-----------------------------------------------------
  Stand-Alone Server
-----------------------------------------------------

You can build a stand-alone server by extracting the contents of the "TNetServer.zip" file
into the project's root folder (outside the Assets folder), then opening up the TNServer
solution or csproj file. A pre-compiled stand-alone windows executable is also included
in the ZIP file for your convenience.

-----------------------------------------------------
  More information:
-----------------------------------------------------

http://www.tasharen.com/?page_id=4518

-----------------------------------------------------
 Version History
-----------------------------------------------------

1.5.0:
- NEW: Added Universal Plug & Play functionality to easily open ports on the gateway/router.
- NEW: TNet Server app now supports port parameters and can also start the discovery server.
- NEW: Added TNObject.isMine flag that will only be 'true' on the client that instantiated it (or the host if that player leaves).
- NEW: Redesigned the discovery client. There is now several game Lobby server / clients instead.
- NEW: Game server can now automatically register itself with a remote lobby server.
- NEW: Added Tools.externalAddress that will return your internet-visible IP.
- FIX: TNet will no longer silently stop using UDP on the web player. UDP in the web player is simply no longer supported.
- MOD: Moved localAddress and IsLocalAddress() functions into Tools and made them static.

1.3.2:
- NEW: Server list now contains the number of players on the server.
- FIX: Some minor tweaks.

1.3.1
- FIX: Unified usage of Object IDs -- they are now all UINTs.
- FIX: Minor tweaks to how things work.

1.3.0
- NEW: Added a way to join a random existing channel.
- NEW: Added a way to limit the number of players in the channel.

1.2.0
- NEW: Added TNManager.CloseChannel.
- FIX: TNManager.isHosting was not correct if the host was alone.
- FIX: TNAutoSync will now start properly on runtime-instantiated objects.

1.1.0
- NEW: Added AutoSync script that can automatically synchronize properties of your choice.
- NEW: Added AutoJoin script that can quickly join a server when the scene starts.
- NEW: Added a pair of new scenes to test the Auto scripts.
- NEW: It's now possible to figure out which player requested an object to be created when the ResponseCreate packet arrives.
- NEW: You can quickly check TNManager.isThisMyObject in a script's Awake function to determine if you're the one who created it.
- NEW: You can now instantiate objects with velocity.
- NEW: Added native support for ushort and uint data types (and their arrays).
- FIX: Fixed a bug with sending data directly to the specified player.
- FIX: Resolving a player address will no longer result in an exception with an invalid address.
- FIX: Changed the order of some notifications. A new host will always be chosen before "player left" notification, for example.
