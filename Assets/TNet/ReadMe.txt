-----------------------------------------------------
        TNet: Tasharen Networking Framework
      Copyright © 2012 Tasharen Entertainment
                  Version 1.0.1
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