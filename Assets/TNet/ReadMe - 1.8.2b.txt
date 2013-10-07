-----------------------------------------------------
        TNet: Tasharen Networking Framework
    Copyright © 2012-2013 Tasharen Entertainment
                  Version 1.8.2b
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

1.8.2:
- NEW: Added Target.Broadcast for when you want to send an RFC call to everyone connected to the server (ex: world chat).

1.8.1:
- FIX: Executing remote function calls while offline should now work as expected.
- FIX: Default TNManager.Create function for pos/rot/vel/angVel should now work correctly again.

1.8.0:
- NEW: Redesigned the object creation code. It's now fully extensible.
- NEW: It's now possible to do TNManager.Create using a string name of an object in the Resources folder.
- FIX: TNBehaviours being enabled now force TNObjects to rebuild the list of RFCs.

1.7.3:
- NEW: Added the ability to specify player timeout on a per-player basis.
- FIX: SyncRigidbody was a bit out of date.
- FIX: Updated the server executable.

1.7.2:
- NEW: It's now possible to have nested TNObjects on prefabs.
- FIX: Now only open channels will be returned by RequestChannelList.
- FIX: TNObject's delayed function calls weren't being used. Now they are.
- FIX: Fixed an issue with web player connectivity.

1.7.1:
- FIX: iOS Local Address resolving fix.
- FIX: Connection fallback for certain routers.
- FIX: NAT Loopback failure work-around.
- FIX: TNManager.player's name will now always match TNManager.playerName.

1.7.0:
- NEW: Added TNObject.ownerID.
- FIX: Joining a channel now defaults to non-persistent.
- FIX: TNServerInstance.StartRemote now has correct return parameters.
- FIX: Non-windows platforms should now be able to properly join LAN servers on LANs that have no public IP access.

1.6.9:
- NEW: It's now possible to set the external IP discovery URL.
- NEW: It's now possible to perform the IP discovery asynchronously when desired.
- FIX: Starting the server should no longer break UPnP discovery.
- FIX: A few exception-related fixes.

1.6.8:
- NEW: TCP lobby client can now handle file save/load requests.
- FIX: Flat out disabled UDP in the Unity web player, since every UDP request requires the policy file.
- FIX: Fixed an issue with how UDP packets were sent.
- FIX: Fixed an issue with how UPnP would cause Unity to hang for a few seconds when the server would be stopped.

1.6.6:
- NEW: Restructured the server app to make it possible to use either TCP and UDP for the lobby.
- FIX: Variety of tweaks and fixes resulted from my development of Star Dots.

1.6.5:
- NEW: TNManager.channelID, in case you want to know what channel you're in.
- NEW: Added the ability to specify a custom string with each channel that can be used to add information about the channel.
- NEW: You will now get an error message in Unity when trying to execute an RFC function that doesn't exist.
- FIX: Saved files will no longer be loaded if their version doesn't match.
- FIX: TcpChannel is now just Channel, as it has nothing to do with TCP.
- FIX: TNManager.isInChannel will now only return 'true' if the player is connected and in a channel.
- FIX: Many cases of "if connected, send data" were replaced with "if in channel, send data", which is more correct.
- FIX: Assortment of other minor fixes.

1.6.0:
- NEW: Added a script that can instantiate an object when the player enters the scene (think: player avatar).
- NEW: It's now possible to create temporary game objects: they will be destroyed when the player that created them leaves.

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
