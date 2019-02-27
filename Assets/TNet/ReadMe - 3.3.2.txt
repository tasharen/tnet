------------------------------------------------------------
   TNet 3: Tasharen's Networking and Serialization Tools
     Copyright © 2012-2018 Tasharen Entertainment Inc.
                  Version 3.3.1b
       http://www.tasharen.com/?page_id=4518
------------------------------------------------------------

Thank you for buying TNet!

Tutorials can be found here: http://www.tasharen.com/forum/index.php?topic=13953.0

If you have any questions, suggestions, comments or feature requests, first search
on the forum here: http://www.tasharen.com/forum/index.php?board=9.0 -- then if you can't
find the answer, ask in Discord here: https://discord.gg/tasharen

Full class documentation can be found here: http://www.tasharen.com/tnet/docs/

TNet version 3.0.0 and newer now includes 7-Zip's LZMA library distributed under the public domain license.
http://www.7-zip.org/sdk.html

Need Steam Networking support? See here: http://www.tasharen.com/forum/index.php?topic=15512

--------------------------------------------------------------------------------------------------------------------
Q: How to start and stop a server from in-game?
--------------------------------------------------------------------------------------------------------------------

TNServerInstance.Start(tcpPort, [udpPort], [fileToLoad]);
TNServerInstance.Stop([fileToSave]]);

--------------------------------------------------------------------------------------------------------------------
Q: How to connect/disconnect?
--------------------------------------------------------------------------------------------------------------------

TNManager.Connect(address);
TNManager.Disconnect();

--------------------------------------------------------------------------------------------------------------------
Q: How to join/leave a channel?
--------------------------------------------------------------------------------------------------------------------

TNManager.JoinChannel(id, levelToLoad);
TNManager.LeaveChannel(id);

--------------------------------------------------------------------------------------------------------------------
Q: How to instantiate a new object?
--------------------------------------------------------------------------------------------------------------------

From a TNBehaviour-derived script:
Instantiate("FunctionName", "prefab", persistent, <parameters>);

Anywhere else:
TNManager.Instantiate(channelID, "FunctionName", "prefab", persistent, <parameters>);

--------------------------------------------------------------------------------------------------------------------
Q: How to destroy an object?
--------------------------------------------------------------------------------------------------------------------

If inside a TNBehaviour-derived script:
tno.DestroySelf();

Otherwise use (the slightly slower):
gameObject.DestroySelf();

--------------------------------------------------------------------------------------------------------------------
Q: How to call a function on all players?
--------------------------------------------------------------------------------------------------------------------

var tno = GetComponent<TNObject>(); // You can skip this line if you derived your script from TNBehaviour
tno.Send("FunctionName", target, <parameters>);

--------------------------------------------------------------------------------------------------------------------
Q: How does a simple remote function call (RFC) look like?
--------------------------------------------------------------------------------------------------------------------

[RFC]
protected void FunctionName (<parameters>) {}

--------------------------------------------------------------------------------------------------------------------
Q: What built-in notifications are there?
--------------------------------------------------------------------------------------------------------------------

TNManager.onError (error)
TNManager.onConnect (success, error);
TNManager.onDisconnect()
TNManager.onJoinChannel (channelID, success, error)
TNManager.onLeaveChannel (channelID)
TNManager.onLoadLevel (channelID, levelName)
TNManager.onPlayerJoin (channelID, player)
TNManager.onPlayerLeave (channelID, player)
TNManager.onRenamePlayer (player, previousName)
TNManager.onHostChanged (channel)
TNManager.onLockChannel (channelID, isLocked)
TNManager.onSetAdmin (player)
TNManager.onSetServerData (path, data)
TNManager.onSetChannelData (Channel, path, data)
TNManager.onSetPlayerData (Player, path, data)

If you want an example of how to subscribe to these events, have a look at the TNEventReceiver class.

--------------------------------------------------------------------------------------------------------------------
 Stand-Alone Server
--------------------------------------------------------------------------------------------------------------------

You can build a stand-alone server by extracting the contents of the "TNetServer.zip" file
into the project's root folder (outside the Assets folder), then opening up the TNServer
solution or csproj file. A pre-compiled stand-alone windows executable is also included
in the ZIP file for your convenience.

--------------------------------------------------------------------------------------------------------------------
  More information:
--------------------------------------------------------------------------------------------------------------------

http://www.tasharen.com/?page_id=4518

--------------------------------------------------------------------------------------------------------------------
 Version History
--------------------------------------------------------------------------------------------------------------------

3.3.1
- NEW: Updated the chat example to be able to get/set channel data in addition to server data.
- NEW: Added a TNUpdater.onQuit delegate.
- NEW: Made TNUpdater thread-safe, making it possible to add delegates to it from worker threads.
- NEW: Added TNet.List.RemoveRange to match the generic List.
- FIX: Made some older functions obsolete for clarity.
- FIX: Prototype change for a potential issue related to leaving channels and transferring objects.
- FIX: Fixes for game object serialization in Unity 2017 & 2018.
- FIX: Made TNet.List's Remove operation significantly faster.

3.3.0
- NEW: Added overloaded operators to all Send functions, eliminating GC allocations for sub-5 parameter RFC calls.
- NEW: Added bandwidth tracking (both sent and received bytes per second): TNManager.sentBytes and TNManager.receivedBytes.
- NEW: Added TNManager.availablePacketSize and TNManager.incomingPacketSize.
- NEW: Added an explicit chat type packet type: TNManager.SendChat / TNManager.onChat.
- NEW: TCP lobby server now supports more packet types, including chat for a true cross-server global chat.
- NEW: Added TNManager.Validate to validate the state of a server-side property. Useful for detecting memory modifications done on the client.
- NEW: Added Serializer.EncodeAsString / DecodeFromString to encode and decode binary data as ASCII text.
- NEW: Added a DoNotObfuscate attribute type and started using it in places where non-public classes/structs should not get obfuscated.
- NEW: Added Tools.stackTrace that will contain the stack trace up to the point where it was used.
- NEW: In-editor game server will now check for the 'saved' server property. If 'false', it won't perform any saving. Useful for quickly testing changes without keeping the active state.
- FIX: Fixed the to-text serialization of DataNode hierarchies containing nested DataNodes.
- FIX: Compilation fixes for builds.

3.2.0
- NEW: DataNode can now export entire bundles (think AssetBundle, but with DataNode). Just select a folder (or multiple files) and use the Assets->DataNode->Export feature. It's up to you to determine what to do about importing the bundles (I use a custom Prefab Manager in Sightseer for example), but an example import functionality and function is included (Assets->DataNode->Import supports them).
- NEW: DataNode can now export audio clips.
- NEW: DataNode now exports textures in their native compressed state, rather than forcing RGBA.
- NEW: It's now possible to add custom serialization functions for all data types by using extensions, without the need for an interface. Read comments above #define SERIALIZATION_WITHOUT_INTERFACE for more info. This makes it possible to add custom serialization for all Unity and 3rd party data types as well.
- NEW: Made it possible to specify custom protocol handling which would bypass sockets. Immediate use: Steam's networking API.
- NEW: Added a low memory footprint mode to the game server. Use the 'lm' keyword to enable it once the server is running. LM mode means once the last player leaves, channel data will be unloaded. Can save memory at the expense of extra time spent loading channel data when players rejoin.
- NEW: Made it possible to assign a custom connection to both the TNManager, and individual connections of the Game Server. I used it in Sightseer to add support for Steam networking: basically instead of using sockets, TNet can now also use Steam's Networking API.
- NEW: It's now possible to specify the root directory on the file server (game server's parent class) to use for all file-based operations.
- NEW: Numerous DataNode serialization fixes and improvements to make it possible for it to serialize prefabs properly, including iffy types like ParticleSystems.
- NEW: Added a Max Packet Time to the game client that TNet will spend processing packets each Update. Can be used to automatically split up packet processing across multiple frames.
- NEW: Added #define options to the game client to provide packet profiling. If enabled, all incoming packets will show up in the Unity's Profiler.
- NEW: WorkerThread now supports coroutines for the main thread's callback.
- NEW: Replaced TNObject/TNBehaviour's Start functions with a custom setup to avoid a bug in Unity that causes disabling components with a Start() function to take 100+ times longer than normal.
- NEW: Added TNManager.Disconnect(delay). Can be useful if there are still packets that need to be sent out before disconnecting. Will prevent all further packets from being sent out or being received.
- NEW: Added a built-in server side check that prevents multiple players from requesting the same player save file.
- NEW: TNet now keeps a list of sent RFC names with their count while in the editor so that you can track which RFCs happen to be called too frequently.
- NEW: Added Vector2D and Vector3D -- double precision version of Unity's vectors.
- NEW: Added TNManager.Export and Import -- the ability to export and import objects from the server. This effectively sends all the RCC and RFCs from the server to the client that are necessary to import a group of objects in the future. In Sightseer I use it to save entire groups of pre-configured assets (such as a fully equipped player vehicle complete with inventory contents) then instantiate them easily after moving them somewhere.
- NEW: TNet's function calls will now automatically try to convert parameters if they don't quite match. For example changing an RFC to pass a Vector3D instead of a Vector3 will no longer break serialization.
- NEW: Added TNObject.Find(fullID) and TNObject.fullID. It's a combination of channel+object ID in one.
- NEW: Added a new "MODDING" #define. If enabled, TNet will be compiled in a stripped-down mode with serialization intact, but connectivity inoperable. This is meant for making exporter mod DLLs.
- NEW: Object and component references are now serialized using IDs instead of strings for less space and faster lookup.
- FIX: Updating a saved RFC on the server will now move it to the end of the saved RFC list, ensuring that it's called in the correct order on load.
- FIX: Fixed the Tcp Lobby link sending server updates every 5 seconds even if nothing changed. It now sends Ping packets instead.
- FIX: Changed ban and admin lists to be hashsets instead for faster lookups.
- FIX: Fixed DataNode's GetHierarchy causing GC allocations.
- FIX: Calling SetChannelData should now persist, even if nothing was actually instantiated in that channel.
- FIX: DataNode can now contain other DataNode values in its nodes' value field without breaking serialization.
- FIX: HTTP responses now use UTF-8.

3.1.0
- NEW: Added the ability to compile entire projects at runtime using RuntimeCode.Add(source file code). Requires Unity 5+.
- NEW: Expanded TypeExtensions with even better caching for much faster lookups.
- NEW: TypeExtensions.AddAssembly and RemoveAssembly to add/remove plugins at run-time.
- NEW: Ping response now returns the server time and number of connected clients.
- NEW: Ping response now performs a built-in time speed hack check, for convenience.
- NEW: Added ban/unban functionality to the lobby servers.
- NEW: Added the ability to change TNObject.owner at will.
- NEW: TNManager.currentRccObjectID is now available at the time of object creation, in case you want to use its ID as a random seed.
- NEW: Added a convenience method tno.canSend to check if it's currently possible to send messages through this object.
- NEW: Added TNet.Tools.CreatePath(path).
- FIX: TNObject channel ID and TNManager.IsInChannel calls with multiple channels will now return proper values even when testing in offline mode.
- FIX: Fix for DestroySelf() not working properly offline since the last set of changes.
- FIX: OnLoadLevel notification will now clean up all objects belonging to the channel, effectively removing objects that would have been removed as a result of a normal Unity scene change anyway.
- ALT: TNGameServer is now all protected instead of private, making it possible to inherit from it easier.
- ALT: TNGameServer's OnCustomPacket now accepts a byte ID instead of a Packet enum.
- DEL: Got rid of Tools.FindType (use GetType instead). Its code is in TypeExtensions.GetType now.

3.0.9
- NEW: TNManager.Instantiate will now always assign a TNObject to the created object, even if there isn't one. If you want to create a local-only (non-networked) object, use an RFC instead.
- FIX: Better visualization for game server addition/removal on the lobby server.

3.0.8
- NEW: It's now possible to "soft-destroy" TNObjects by setting tno.ignoreDestroyCall = false in TNObject's onDestroy delegate callback. This will effectively make TNet behave like this object was destroyed already, without actually destroying the game object. Example usage would be immediately destroying a networked object (such as the player's car) as far as networking is concerned, while still keeping a copy for post-processing, such as making it break up into pieces before an explosion.
- NEW: Added an "assign unique ID" button to static TNObjects shown when the ID is 0.
- NEW: WorkerThread.totalExecutionTime shows the total execution time for this function, cumulative over repeated execution calls (multi-stage execution).
- ALT: WorkerThread.currentExecutionTime can be checked in worker thread's main thread (OnFinished) callbacks to check how long the function has been executing so far. Replaces 'elapsedMilliseconds'.
- ALT: WorkerThread.mainFrameTimeExceeded can be checked in the main thread's (OnFinished) callbacks to see if the multi-stage callback should exit early and continue next update to prevent FPS degradation. Replaces 'mainThreadTimeExceeded'.
- ALT: DataNodeExporter.ShowExportDialog / ShowImportDialog were moved to UnityEditorExtensions.
- FIX: Fixes to TNet.Counter not serializing the time properly in some cases.
- FIX: Fix to RequestSetUDP packet parsing IP from address bytes, causing issues in some cases. It now uses it directly.

3.0.7
- NEW: It's now possible to disable the TNBehaviour in an Awake() function, preventing it from sending out messages or trying to ensure that a TNObject actually exists. Useful for when you need to render an object with network scripts into an off-screen texture, for example.
- NEW: TNet now tracks sent and received packets per second. TNManager.sentPackets / receivedPackets.
- NEW: TNManager now has one source for LoadScene functions -- an easily changed pair of delegates.
- NEW: TNObject.IsJoiningChannel convenience function.
- NEW: Added TNObject.RemoveSavedRFC to remove a previously sent RFC function.
- NEW: Added an "ignore warnings" flag to the TNObject that will suppress messages about missing RFCs.
- NEW: All of TNet's thread creation is now routed through a TNet.Tools.CreateThread function.
- FIX: TNManager now listens to application quit messages, preventing its singleton from being created after.
- FIX: When transferring objects between channels, their RFCs will now be kept in the same order.
- FIX: Get/Set functions on the TNObject no longer work if the object's ID is 0.
- FIX: TNSyncRigidbody will behave better when synchronizing an object that's marked as kinematic.
- FIX: WorkerThread.remainingCallbackCount now considers active threads as well.
- NOTE: TNBehaviour no longer has a virtual OnEnable(), but now it has a virtual Awake() and Start() functions.

3.0.6
- NEW: Added a string ID to RFC that you can specify to uniquely identify identical RFCs underneath the same TNObject. For example: having two turrets underneath one TNObject with each script having a "Fire" function. You can now specify a name of the property that will uniquely identify the RFC, thus making it possible to call only that one RFC instead of both at once. To call only that RFC, instead of tno.Send("name", ...) use tno.Send("name/property", ...);
- NEW: WorkerThread now has a "priority" queue. All items in the priority queue gets processed before the regular queue.
- NEW: Added OnInit() function to TNBehaviour. Use it instead of Awake() as it will be called as soon as the object gets created, but unlike Awake() it's called after the TNObject's ID has been set.
- NEW: TNManager.playedTime will show the player's /played time. Played time is tracked automatically  via the player's save file.
- NEW: It's now possible to assign the TNObject's data on creation. Useful if you mean to pass some starting data to your RCC.
- NEW: Selecting a TNObject in Unity will now show its associated DataNode's data in inspector.
- NEW: Added WorkerThread.elapsedMilliseconds you can check at the end of your finished function callbacks to see how long the worker thread's functions took to execute.
- NEW: WorkerThread now has a SINGLE_THREADED #define to aid with debugging.
- NEW: WorkerThread's maximum milliseconds per frame spent in the update function is now settable at run-time.
- NEW: Added a simple TNet.Counter class that can be used for server-side resource counters that automatically change with time. For example: energy production at your game's base.
- NEW: Added TNManager.IsHosting(channel) and added a warning to the TNManager.isHosting property.
- FIX: Fixed some places that still used TNManager.isHosting.
- FIX: Fixed a bug in DataNode related to serialization of custom classes as text.
- FIX: DataNode.SetHierarchy(path, null) will no longer remove the node's parent.
- FIX: When retrieving a list of network interfaces, unknown status interfaces will no longer be ignored.
- FIX: Improvements of how nested TNObjects work.

3.0.5
- NEW: TNet now automatically forces Application.runInBackground to be 'true' when connected.
- NEW: Exposed TNObject.dataNode in case you need to run through its data manually.
- NEW: TNObject's Set now accepts a hierarchical path, not just a single value name.
- NEW: Added WorkerThread.remainingCallbackCount that returns the number of callbacks still waiting to be executed.
- NEW: Added DataNode.FindChild that can search for a child with the specified name.
- NEW: Player data under the "Server" child node will now be only settable by the server.
       Trying to set anything under the "Server" node from the client side is simply ignored by the server.
- NEW: The server now automatically tracks the player's played time: player.Get<long>("Server/playerTime").
- FIX: Setting player data offline will now trigger the onSetPlayerData notification.
- FIX: TNObject now stores static and dynamic IDs separately
- FIX: Added proper double support to the TNet's serializer.
- FIX: TNet.Tools.GetDocumentsPath will now return a valid path even if no applicationDirectory has been set.

3.0.4
- NEW: Added a convenient set of Get and Set functions on the TNBehaviour and TNObject classes for simple per-object property persistence.
- NEW: Added TNManager.serverUptime and TNManager.time (how long the server was up).
- NEW: Added multi-stage callback support to the WorkerThread's main thread callbacks.
- NEW: Added a maximum time limiter to the WorkerThread's main thread execution, limiting the time that it takes up in the Update().

3.0.3
- NEW: Added a robust WorkerThread class that can be used as a simple to use thread pool: WorkerThread.Create(delegate, <optional>), where <optional> delegate will be executed on the main thread when the threaded delegate finishes.
- NEW: TNServer executable now supports -ipv6 to use IPv6.
- NEW: TNServer executable now supports -fn [filename] to set the save filename.
- FIX: TCP lobby client's error string will be cleared when reconnected.
- FIX: TNSerializer will now serialize DateTime as a long.
- FIX: TNManager.Connect(address) now supports IPv6 just like TNManager.Connect(address, port) already did.
- FIX: More tweaks to IPv6, making it more robust.
- FIX: Tools.externalAddress will now reveal an IPv6 address if using IPv6.

3.0.2
- NEW: Full IPv6 support.
- NEW: Serialization of structs without public fields now defaults to serializing properties.
- NEW: Added Type.IsStruct() extension method.
- NEW: Added [SerializeProperties] attribute that makes TNet serialize get/set properties, not just fields.
- FIX: Now explicitly specifying Unity's full library name for better type retrieval in Unity 5.

3.0.1
- NEW: Added 2 new tutorial PDFs: executing runtime code and runtime C# behaviours.
- NEW: DataNode's Merge() function now returns 'true' if any existing node's values were actually altered.
- FIX: Replaced Thread.Abort() calls with Interrupt() and Join() combos. This aids iOS compatibility.
- FIX: Fix for ForwardToPlayer and ForwardByName not parsing the packets properly.
- FIX: Updated the stand-alone server solution to compile with the latest changes.

3.0.0
- NEW: DataNode is now fully capable of serializing entire hierarchies of game objects, making it trivial to export and save entire game objects, complete with mesh and texture information embedded in the data. TNet will keep references to items in the Resources folder and will include the raw data of those that aren't. Example usage: where you were using prefabs before you can now use exported DataNode binaries, making this data easily moddable (remember, Resources.Load only works on internal content!)
- NEW: TNet now seamlessly supports instantiation of DataNode-exported objects as if they were prefabs both via TNManager.Instantiate as well as manually via DataNode.Instantiate().
- NEW: Added support for multiple simultaneous channel subscriptions. You can now enter and leave multiple channels at will, effectively creating streamed content.
- NEW: Added the ability to seamlessly transfer instantiated objects from one channel to another.
- NEW: Added a new example showing multiple channel support and the ability to transfer objects.
- NEW: Added the LZMA library to TNet in order to support LZMA-compressed serialization for maximum bandwidth reduction when desired.
- NEW: TNBehaviour-derived scripts will now try to find the TNObject in Start() if it was not found in OnEnable.
- NEW: Added TNManager.WriteCache and TNManager.ReadCache for when you want to store server-specific files on the client side, such as downloaded textures.
- NEW: All of TNet's resource loading and type retrieval functions can now be overwritten via TNet.UnityTools in case you want to expand it / limit it somehow. Example: support loading data from mod folders.
- NEW: Added UnityTools.LoadPrefab(path) that is able to load both prefabs as well as DataNode-exported data files.
- NEW: Added a variety of extensions to UnityTools, such as GameObject.AddChild(prefab) and Transform.GetHierarchy(target).
- NEW: UnityTools.GetMD5Hash() can be used to compute a checksum of data. Example usage: check the local hash against server's before downloading a large file.
- NEW: DataNode.GetHierarchy("Full/Path/To/Node") and DataNode.SetHierarchy("Full/Path/To/Node", value).
- NEW: Expanded DataNode serialization of data, making it capable of serializing everything from common types to meshes, materials, textures, references to objects, and much more.
- NEW: Added System.Type extension methods such as Type.GetMethodOrExtension that's capable of searching all loaded assemblies for a desired extension.
- NEW: All types can now be made serializable into DataNode without deriving from an interface by simply adding an extension to their class such as "void Serialize (this Type, DataNode)". Look inside TNUnitySerializers for examples. Same with deserialization by adding a Deserialize extension.
- NEW: Added object.Invoke("method", params) extension for convenience.
- NEW: Added Unity menu options that can be used to export/import the selected object as a DataNode binary. Look for them in the Assets/DataNode submenu.
- NEW: Added TNManager.onObjectCreated callback that will be called every time any object gets created.
- NEW: TNet is now able to respond to a simple HTTP browser GET request. Simply connect to your server via http://127.0.0.1:5127/
- NEW: Added DataNode.ToArray([type]) to quickly convert DataNode to byte[].
- NEW: Added TNManager.packetSourceID that identifies the last Forward and Create type packet's source player ID.
- NEW: Added an offline mode to TNet that still supports full functionality identical to that of playing alone on a local server.
- NEW: DataNode now supports exporting prefabs using asset bundles export for situations when regular binary export is not suitable.
- NEW: It's now possible to pass an empty string to TNManager.Instantiate causing TNet to pass back a dummy object on creation, thus letting the game object's contents be procedurally set.
- NEW: Serialization.Convert<T>(value) will use TNet's serialization to convert types.
- NEW: RCCs no longer require an ID and can be called by their function name.
- NEW: It's no longer necessary to call TNManager.AddRCCs. TNet will find all RCCs automatically.
- NEW: Expanded the chat example to support /get and /set commands that change server configuration.
- NEW: Added TNManager.GetChannelList().
- NEW: Added a new example showing a simple car with a frequent input and an infrequent rigidbody sync.
- NEW: TNManager.SetServerData, TNManager.SetChannelData and TNManager.SetPlayerData now all set only the path requested, not the entire thing.
- NEW: Calling TNManager.SetPlayerSave(filename) will now load previously saved player data and will auto-save the player's data into that file.
- NEW: GameServer will now periodically auto-save on its own, and no longer requires you to call SaveTo().
- NEW: Added DestroySelf(delay) functions to TNObject and TNBehaviour.
- NEW: TNManager will no longer send out expensive broadcasts such as OnNetworkConnect. Subscribe to an appropriate delegate instead, such as TNManager.onConnect. Examine TNEventReceiver for more details: http://pastebin.com/qE3xqG9h
- NEW: Added FieldOrProperty: a convenience class that's able to get/set values of both fields and properties with the same code, and with automatic type conversion via TNet's serialization. Example: transform.SetFieldOrPropertyValue("position", "(1, 2, 3)");
- NEW: Added an optional RuntimeCode extension package that makes executing run-time C# code trivial.
- FIX: Player connecting to the TNServerInstance will now be its admin by default.
- FIX: Changing server options now immediately saves the server's configuration.
- FIX: TNet will no longer store RFCs for objects that have been deleted.
- FIX: TNet's threads will now go into extended sleep while the application is paused.
- FIX: DataNode with no name set should now be correctly text-serializable.
- FIX: Prefabs no longer need to be brought into the scene to export them as DataNode.
- DEL: Removed TNManager.SyncPlayerData(). Calling TNManager.SetPlayerData(...) will now sync automatically.

2.1.1
- NEW: DataNode now has limited Game Object serialization functionality. All MonoBehaviour script properties can be serialized, as well as common Unity types: collider, rigidbody, mesh, renderer. Optimal use: saving entire game objects into an easy to read/modify text format on disk.
- NEW: DataNode.Instantiate has been added to create a new game object serialized via DataNode.
- NEW: TNBehaviours can now be attached anywhere underneath a TNObject in hierarchy.
- NEW: Added TNManager.SetAlias convenience function.
- NEW: Added connect/disconnect notifications.
- FIX: Fix for channel data not being set/cleared properly in some cases.
- FIX: Fixed a long-standing issue with rare packet corruption being caused by improper buffer reuse.

2.1.0
- NEW: Users can now authenticate as administrators for additional functionality. Some requests now require admin authentication.
- NEW: Added Server Options -- a persistent DataNode stored alongside the server save (TNManager.SetServerOption, TNManager.GetServerOption).
- NEW: It's now possible to lock channels, preventing further modification.
- NEW: The log is now more robust, and a copy of all errors is now saved separately.
- NEW: Added aliases to all players. Use them to add identification to players, such as User/Steam IDs.
- NEW: Added kick and ban functionality for administrators.
- NEW: Added spam detection for server-wide broadcasts.
- NEW: Added a way to get a list of files in a remote directory (TNGameClient.GetFiles).
- NEW: Stand-alone server can now parse DataNode.
- FIX: Fixed rare data corruption that would sometimes occur with high number of players and large packets.
- FIX: Stand-alone server should now use a lot less memory.

2.0.6
- NEW: Added a new integer type that isn't stored as a plain integer in memory (guard against CheatEngine).
- FIX: Calling Disconnect() will now always ensure that the OnNetworkLeaveChannel gets called before OnNetworkDisconnect.
- FIX: Optimizations related to memory allocation, greatly reducing TNet's impact on GC.

2.0.5
- NEW: TNet's level loading is now asynchronous.
- NEW: Added SortByPlayers() and SortAlphabetic() functions to the TNServerList.
- NEW: Added support for LZMA-based DataNode compression. It's used in Windward and requires the public LZMA library.
- FIX: File saving should now work on Android properly.
- FIX: TCP lobby link now sends "keep alive" messages to ensure that the stale servers get removed properly.
- FIX: Variety of minor tweaks and improvements merged from Windward's development branch.
- FIX: UDP multicasting will now be off by default on iOS devices.
- FIX: Float parsing from text format should now work with floats specified as "1,23", not just "1.23".
- FIX: Disconnect() call will now properly shut down a connection attempt in progress.

2.0.4
- NEW: Added timestamps and player IDs to the server log messages.
- NEW: Added a new packet that can be used to send messages to be printed directly to the server's log.
- NEW: TNet's ReadFile and similar functions can no longer access files outside the executable's folder and Documents.
- FIX: Added some extra code to ensure that stale servers get removed from the Lobby Server's list.
- FIX: UPnP should now work better.
- FIX: Fixed string[] serialization (apparently there is a strange edge case in C#)
- FIX: Strings sent via RFC parameters can now be empty.
- FIX: Renamed players should no longer affect other clients.
- FIX: TNManager.isHosting should now check for the connected state as well.
- FIX: Game servers should now respond to UDP pings correctly.
- FIX: You can no longer receive NaNs through TNet. They will now automatically get set to zero.
- FIX: Removed warnings from web player compilation.
- FIX: TNet should now handle packets that have been sent only partially.

2.0.3
- NEW: Added an option to TNServerInstance.Start to not open the port via UPnP (for private servers).
- NEW: TNServer.exe can now be run as a background process and will save periodically.
- NEW: Added int[] and float[] serialization.
- FIX: Added a try/catch to multicasting membership subscription as it apparently doesn't work on some computers.

2.0.2
- NEW: Added tno.Send via player ID. No need to FindPlayer anymore.
- NEW: Added a #define to TNSerializer you can use to ignore errors.
- NEW: TNet will now automatically remove the read-only flag when using Tools.WriteFile.
- FIX: TNSyncRigidbody will no longer try to set velocity if the rigidbody is kinematic.
- FIX: Added some extra code to ensure that sockets get cleaned up properly.
- FIX: Got rid of the != Component comparison, fixing the CompareBaseObjectsInternal error.
- FIX: Added 'long' type serialization for DataNode.
- FIX: TNAutoSync's updates per second value is now a float, matching TNSyncRigidbody.
- FIX: WP8 compile fixes.

2.0.1
- NEW: TNet can save files in the user's My Documents folder if you like.
- FIX: Fixed an issue with RFCs not being stored correctly in some cases.
- FIX: TNManager.Destroy will now also mark objects as destroyed, so RFCs sent between the destroy request and the response will be ignored.
- FIX: TNet will now automatically block outgoing messages between JoinChannel/LoadLevel and level loaded/channel joined notifications.
- FIX: Fixed System.Collections.Generic.List<> serialization.

2.0.0
- NEW: Added the ability to send messages players by name rather than ID (think private chat messages).
- NEW: Saved/loaded files should now be kept in a dictionary for faster lookup.
- FIX: TNSerializer's WriteInt didn't work for negative values.
- FIX: Custom RCCs didn't seem to work quite right on the stand-alone server.
- FIX: More tweaks regarding object ownership transfer.

1.9.9
- NEW: TNManager.serverTime (in milliseconds)
- NEW: Added automatic serialization support for long, ulong, long[] and ulong[] types.
- NEW: TNObjects now have a DestroySelf function which TNBehaviours call that ensures the object's destruction.
- FIX: tno.isMine was not set properly for new players after the original owner left.
- DEL: Removed the setter from TNObject.ownerID, as it's handled properly on the server now.

1.9.8
- NEW: TNBehaviour's DestroySelf() function is now virtual.
- NEW: TNManager.onPlayerSync and TNManager.SyncPlayerData().
- NEW: String arrays are now serialized more efficiently within the DataNode.
- NEW: TNSyncRigidbody's updatesPerSecond is now a float so you can have 1 update per X seconds.
- NEW: TNManager.isJoiningChannel, set to 'true' between JoinChannel and OnNetworkJoinChannel.
- NEW: TNet's server instance can now be single-threaded for easier debugging in Unity.
- NEW: You can now pass TNObjects as RFC parameters.
- FIX: It's now possible to save the server properly even while it's running.
- FIX: TNet will no longer save non-persistent game objects when saved to disk.
- FIX: Int values can now be auto-converted to floats.
- FIX: Quite a few DataNode serialization changes/fixes.

1.9.7
- NEW: TNet is now fully Unity 5-compliant.
- NEW: SendRFC sent to the player's self will now result in immediate execution (think Target.Host).
- NEW: Added better/more informative error messages when RFCs or RCCs fail.
- NEW: TNObject inspector will now show its player owner and whether the player owns this object (at run time).
- FIX: TNManager.JoinChannel will now load the level even without TNManager.
- FIX: TNObjects will now have valid IDs even without TNManager.
- FIX: Added a null check in PrintException for when working with static RCC functions.
- FIX: OnNetworkDisconnect will now be called when the connection is shut down in a non-graceful manner.
- FIX: DataNode should have been clearing the child list after resolving custom data types.

1.9.6
- NEW: TNet will now use UDP multicasting instead of broadcasting by default.
- NEW: Added convenience methods to retrieve player data in DataNode form.
- NEW: Faster way of getting the external IP address.
- NEW: Example menu now shows your internal and external IPs.
- NEW: TNet.Tools.ResolveIPs can now be called by itself with no callback.
- NEW: TNet.UdpProtocol will now choose the default network interface on its own.
- FIX: LAN server list is now no longer cleared every time a new server arrives.
- FIX: Read/write/delete file functions are now wrapped in try/catch blocks.
- FIX: Fixed the TCP lobby server (it was throwing exceptions).
- FIX: Fixed the ability to host a local TCP-based lobby server alongside the game server.
- FIX: Added Ping packet handling to the lobby servers.

1.9.5
- NEW: TNManager.SaveFile, TNManager.LoadFile, TNManager.Ping.
- NEW: TNManager.playerData, synchronized across the network. SyncPlayerData() to sync it if modified via TNManager.playerDataNode.
- NEW: Added DataNode.Read(byte[] data, bool binary) for creating a data node tree from raw data.
- NEW: Added OnPing, OnPlayerSync, and OnLoadFile notifications to the Game Client.
- NEW: Custom packet handlers will now be checked first, in case you want to overwrite the default handling.
- NEW: TNServerInstance.SaveTo can now be used to save the server's state manually.
- FIX: Variety of serialization-related fixes and additions.
- FIX: Better error handling when connecting and better error messages.

1.9.1
- FIX: Error about TNObjects with ID of 0 will now only show up when playing the game.
- FIX: If an RFC cannot be executed, the error message will explain why.

1.9.0
- NEW: TNManager no longer needs to be present in the scene for you to use TNet.
- NEW: You can now send just about any type of data across the network via RFCs, not just specific types.
- NEW: Added custom serialization functionality to send custom classes via RFCs more efficiently.
- NEW: Added a DataNode tree-like data structure that's capable of serializing both to binary and to text format.
- NEW: AutoSync can now be set to only sync when new players join.
- NEW: Added support for multiple network interfaces (Hamachi etc).
- NEW: Added a bunch of serialization extension methods to BinaryWriter.
- NEW: TNet will now show the inner exception when an RFC fails.
- FIX: Better handling of mis-matched protocol IDs.

1.8.5
- NEW: It's now possible to add RCCs via TNManager.AddRCCs function that are not under TNManager.
- NEW: TNSyncRigidbody now has the "isImportant" flag just like TNAutoSync.
- FIX: TNManager.isActive set to false no longer prevents ping requests from being sent out.
- FIX: Added an extra step before enabling UDP traffic to avoid cases where it gets enabled erroneously.
- FIX: TNet.Tools.localAddress will now use GetHostAddresses instead of GetHostEntry.
- FIX: Unity 4.5 and 4.6 compile fixes.

1.8.4
- FIX: Host player will now assume ownership of TNObjects with no owner when joining a persistent channel.

1.8.3
- FIX: Eliminated obsolete warnings in the latest version of Unity.

1.8.2
- NEW: Added Target.Broadcast for when you want to send an RFC call to everyone connected to the server (ex: world chat).

1.8.1
- FIX: Executing remote function calls while offline should now work as expected.
- FIX: Default TNManager.Create function for pos/rot/vel/angVel should now work correctly again.

1.8.0
- NEW: Redesigned the object creation code. It's now fully extensible.
- NEW: It's now possible to do TNManager.Create using a string name of an object in the Resources folder.
- FIX: TNBehaviours being enabled now force TNObjects to rebuild the list of RFCs.

1.7.3
- NEW: Added the ability to specify player timeout on a per-player basis.
- FIX: SyncRigidbody was a bit out of date.
- FIX: Updated the server executable.

1.7.2
- NEW: It's now possible to have nested TNObjects on prefabs.
- FIX: Now only open channels will be returned by RequestChannelList.
- FIX: TNObject's delayed function calls weren't being used. Now they are.
- FIX: Fixed an issue with web player connectivity.

1.7.1
- FIX: iOS Local Address resolving fix.
- FIX: Connection fallback for certain routers.
- FIX: NAT Loopback failure work-around.
- FIX: TNManager.player's name will now always match TNManager.playerName.

1.7.0
- NEW: Added TNObject.ownerID.
- FIX: Joining a channel now defaults to non-persistent.
- FIX: TNServerInstance.StartRemote now has correct return parameters.
- FIX: Non-windows platforms should now be able to properly join LAN servers on LANs that have no public IP access.

1.6.9
- NEW: It's now possible to set the external IP discovery URL.
- NEW: It's now possible to perform the IP discovery asynchronously when desired.
- FIX: Starting the server should no longer break UPnP discovery.
- FIX: A few exception-related fixes.

1.6.8
- NEW: TCP lobby client can now handle file save/load requests.
- FIX: Flat out disabled UDP in the Unity web player, since every UDP request requires the policy file.
- FIX: Fixed an issue with how UDP packets were sent.
- FIX: Fixed an issue with how UPnP would cause Unity to hang for a few seconds when the server would be stopped.

1.6.6
- NEW: Restructured the server app to make it possible to use either TCP and UDP for the lobby.
- FIX: Variety of tweaks and fixes resulted from my development of Star Dots.

1.6.5
- NEW: TNManager.channelID, in case you want to know what channel you're in.
- NEW: Added the ability to specify a custom string with each channel that can be used to add information about the channel.
- NEW: You will now get an error message in Unity when trying to execute an RFC function that doesn't exist.
- FIX: Saved files will no longer be loaded if their version doesn't match.
- FIX: TcpChannel is now just Channel, as it has nothing to do with TCP.
- FIX: TNManager.isInChannel will now only return 'true' if the player is connected and in a channel.
- FIX: Many cases of "if connected, send data" were replaced with "if in channel, send data", which is more correct.
- FIX: Assortment of other minor fixes.

1.6.0
- NEW: Added a script that can instantiate an object when the player enters the scene (think: player avatar).
- NEW: It's now possible to create temporary game objects: they will be destroyed when the player that created them leaves.

1.5.0
- NEW: Added Universal Plug & Play functionality to easily open ports on the gateway/router.
- NEW: TNet Server app now supports port parameters and can also start the discovery server.
- NEW: Added TNObject.isMine flag that will only be 'true' on the client that instantiated it (or the host if that player leaves).
- NEW: Redesigned the discovery client. There is now several game Lobby server / clients instead.
- NEW: Game server can now automatically register itself with a remote lobby server.
- NEW: Added Tools.externalAddress that will return your internet-visible IP.
- FIX: TNet will no longer silently stop using UDP on the web player. UDP in the web player is simply no longer supported.
- MOD: Moved localAddress and IsLocalAddress() functions into Tools and made them static.

1.3.2
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
