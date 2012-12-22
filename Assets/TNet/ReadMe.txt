-----------------------------------------------------
        TNet: Tasharen Networking Framework
      Copyright © 2012 Tasharen Entertainment
                  Version 1.0.0
  http://www.tasharen.com/forum/index.php?board=2.0
               support@tasharen.com
-----------------------------------------------------

Thank you for buying TNet!

If you have any questions, suggestions, comments or feature requests, please
drop by the NGUI forum, found here: http://www.tasharen.com/forum/index.php?board=2.0

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
  More information:
-----------------------------------------------------

http://www.tasharen.com/forum/index.php?topic=2513.0