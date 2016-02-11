//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

namespace TNet
{
/// <summary>
/// Clients send requests to the server and receive responses back. Forwarded calls arrive as-is.
/// </summary>

public enum Packet
{
	/// <summary>
	/// Empty packet. Can be used to keep the connection alive.
	/// </summary>

	Empty,

	/// <summary>
	/// This packet indicates that an error has occurred.
	/// string: Description of the error.
	/// </summary>

	Error,

	/// <summary>
	/// This packet indicates that the connection should be severed.
	/// </summary>

	Disconnect,

	/// <summary>
	/// Add a new entry to the list of known servers. Used by the Lobby Server.
	/// ushort: Game ID.
	/// string: Server name.
	/// ushort: Number of connected players.
	/// IPEndPoint: Internal address
	/// IPEndPoint: External address
	/// </summary>

	RequestAddServer,

	/// <summary>
	/// Remove an existing server list entry. Used by the Lobby Server.
	/// ushort: Game ID.
	/// IPEndPoint: Internal address
	/// IPEndPoint: External address
	/// </summary>

	RequestRemoveServer,

	/// <summary>
	/// Request a list of all known servers for the specified game ID. Used by the Lobby Server.
	/// ushort: Game ID.
	/// </summary>

	RequestServerList,

	/// <summary>
	/// Response sent by the Lobby Server, listing servers.
	/// ushort: List size
	/// For each entry:
	/// string: Server name
	/// ushort: Player count
	/// IPEndPoint: Internal address
	/// IPEndPoint: External address
	/// </summary>

	ResponseServerList,

	/// <summary>
	/// Print a message on the server. Used to make verbose output possible.
	/// string: text to log.
	/// </summary>

	ServerLog,

	/// <summary>
	/// This should be the very first packet sent by the client.
	/// int32: Protocol version.
	/// string: Player Name.
	/// object: Player data.
	/// </summary>

	RequestID,

	/// <summary>
	/// Always the first packet to arrive from the server.
	/// If the protocol version didn't match the client, a disconnect may follow.
	/// int32: Protocol ID.
	/// int32: Player ID (only if the protocol ID matched).
	/// int64: Server time in milliseconds (only if the protocol ID matched).
	/// </summary>

	ResponseID,

	/// <summary>
	/// Clients should send a ping request periodically.
	/// </summary>

	RequestPing,

	/// <summary>
	/// Response to a ping request.
	/// </summary>

	ResponsePing,

	/// <summary>
	/// Set the remote UDP port for unreliable packets.
	/// ushort: port.
	/// </summary>

	RequestSetUDP,

	/// <summary>
	/// Set a UDP port used for communication.
	/// ushort: port. (0 means disabled)
	/// </summary>

	ResponseSetUDP,

	/// <summary>
	/// Activate UDP functionality on the server for this client. This must be sent via UDP and it has no response.
	/// int32: Player ID.
	/// </summary>

	RequestActivateUDP,

	/// <summary>
	/// Join the specified channel.
	/// int32: Channel ID (-1 = new random, -2 = existing random)
	/// string: Channel password.
	/// bool: Whether the channel should be persistent (left open even when the last player leaves).
	/// ushort: Player limit.
	/// </summary>

	RequestJoinChannel,

	/// <summary>
	/// Start of the channel joining process. Sent to the player who is joining the channel.
	/// 
	/// Parameters:
	/// int32: Channel ID.
	/// int16: Number of players.
	/// 
	/// Then for each player:
	/// int32: Player ID.
	/// bool: Whether player name and data follows. If a player is already known, it won't be sent.
	/// string: Player Name.
	/// DataNode: Player data.
	/// </summary>

	ResponseJoiningChannel,

	/// <summary>
	/// Inform the player that they have successfully joined a channel.
	/// int32: Channel ID.
	/// bool: Success or failure.
	/// string: Error string (if failed).
	/// </summary>

	ResponseJoinChannel,

	/// <summary>
	/// Inform the channel that a new player has joined.
	/// int32: Channel ID.
	/// int32: Player ID.
	/// bool: Whether player name and data follows. If a player is already known, it won't be sent.
	/// string: Player name.
	/// DataNode: Player data.
	/// </summary>

	ResponsePlayerJoined,

	/// <summary>
	/// Leave the channel the player is in.
	/// int32: Channel ID.
	/// </summary>

	RequestLeaveChannel,

	/// <summary>
	/// Inform the player that they have left the channel they were in.
	/// int: Channel ID.
	/// </summary>

	ResponseLeaveChannel,

	/// <summary>
	/// Inform everyone of this player leaving the channel.
	/// int32: Channel ID.
	/// int32: Player ID.
	/// </summary>

	ResponsePlayerLeft,

	/// <summary>
	/// Mark the channel as closed. No further players will be able to join and saved data will be deleted.
	/// int32: Channel ID.
	/// </summary>

	RequestCloseChannel,

	/// <summary>
	/// Change the number of players that can be in this channel at the same time.
	/// int32: Channel ID;
	/// ushort: Player limit.
	/// </summary>

	RequestSetPlayerLimit,

	/// <summary>
	/// Load the specified level.
	/// int32: Channel ID;
	/// string: Level Name.
	/// </summary>

	RequestLoadLevel,

	/// <summary>
	/// Load the specified level. Should happen before all buffered calls.
	/// int32: Channel ID.
	/// string: Name of the level.
	/// </summary>

	ResponseLoadLevel,

	/// <summary>
	/// Player name change.
	/// string: Player name.
	/// </summary>

	RequestSetName,

	/// <summary>
	/// Change the specified player's name.
	/// int32: Player ID,
	/// string: Player name.
	/// </summary>

	ResponseRenamePlayer,

	/// <summary>
	/// Transfer the host status to the specified player. Only works if the sender is currently hosting.
	/// int32: Channel ID.
	/// int32: Player ID.
	/// </summary>

	RequestSetHost,

	/// <summary>
	/// Inform the player of who is hosting.
	/// int32: Channel ID.
	/// int32: Player ID.
	/// </summary>

	ResponseSetHost,

	/// <summary>
	/// Delete the specified buffered function.
	/// int32: Channel ID.
	/// uint32: Object ID (24 bits), RFC ID (8 bits).
	/// string: Function Name (only if RFC ID is 0).
	/// </summary>

	RequestRemoveRFC,

	/// <summary>
	/// Echo the packet to everyone in the room. Interpreting the packet is up to the client.
	/// int32: ID of the player that sent the packet.
	/// int32: Channel ID.
	/// uint32: Object ID (24 bits), RFC ID (8 bits).
	/// string: Function name (only if RFC ID is 0).
	/// Arbitrary amount of data follows.
	/// </summary>

	/// <summary>
	/// Instantiate a new object with the specified identifier.
	/// int32: ID of the player that sent the packet.
	/// int32: Channel ID.
	/// byte:
	///   0 = Local-only object. Only echoed to other clients.
	///   1 = Saved on the server, assigned a new owner when the existing owner leaves.
	///   2 = Saved on the server, destroyed when the owner leaves.
	///
	/// byte: RCC ID.
	/// string: Function name (only if RCC ID is 0).
	/// string: Path to the object in the Resources folder.
	/// Arbitrary amount of data follows. All of it will be passed along with the response call.
	/// </summary>

	RequestCreateObject,

	/// <summary>
	/// Create a new persistent entry.
	/// int32: ID of the player that requested this object to be created.
	/// int32: Channel ID.
	/// uint32: Unique Identifier (aka Object ID) if requested, 0 otherwise. 0-16777215 range.
	///
	/// byte: RCC ID.
	/// string: Function name (only if RCC ID is 0).
	/// string: Path to the object in the Resources folder.
	/// Arbitrary amount of data follows, same data that was passed along with the Create Request.
	/// </summary>

	ResponseCreateObject,

	/// <summary>
	/// Transfer the specified object (and all of is RFCs) to another channel.
	/// The player must be present in the 'from' channel in order for this to work.
	/// This command will only work on objects that have been created dynamically via TNManager.Create.
	/// int32: Channel ID where the object resides.
	/// int32: Channel ID where to transfer the object.
	/// uint32: Object ID.
	/// </summary>

	RequestTransferObject,

	/// <summary>
	/// Notification that the specified object has been transferred to another channel.
	/// This notification is only sent to players that are in both channels.
	/// int32: Old channel ID.
	/// int32: New channel ID.
	/// uint32: Old object ID.
	/// uint32: New object ID.
	/// </summary>

	ResponseTransferObject,

	/// <summary>
	/// Delete the specified Network Object.
	/// int32: Channel ID.
	/// uint32: Object ID.
	/// </summary>

	RequestDestroyObject,

	/// <summary>
	/// Delete the specified Unique Identifier and its associated entry.
	/// int32: Channel ID.
	/// ushort: Number of objects that will follow.
	/// uint32[] Unique Identifiers (aka Object IDs).
	/// </summary>

	ResponseDestroyObject,

	/// <summary>
	/// Get the list of files in the specified folder.
	/// string: Path.
	/// </summary>

	RequestGetFileList,

	/// <summary>
	/// Server returning a list of files from RequestGetFileList.
	/// string: Path.
	/// int32: Number of filenames that follow.
	/// string[] files.
	/// </summary>

	ResponseGetFileList,

	/// <summary>
	/// Save the specified data.
	/// string: Filename.
	/// int32: Size of the data in bytes.
	/// Arbitrary amount of data follows.
	/// </summary>

	RequestSaveFile,

	/// <summary>
	/// Load the requested data that was saved previously.
	/// string: Filename.
	/// </summary>

	RequestLoadFile,

	/// <summary>
	/// Loaded file response.
	/// string: Filename.
	/// int32: Number of bytes to follow.
	/// byte[]: Data.
	/// </summary>

	ResponseLoadFile,

	/// <summary>
	/// Delete the specified file.
	/// string: Filename.
	/// </summary>

	RequestDeleteFile,

	/// <summary>
	/// Improve latency of the established connection at the expense of network traffic.
	/// bool: Whether to improve it (enable NO_DELAY)
	/// </summary>

	RequestNoDelay,

	/// <summary>
	/// Set the channel's data field.
	/// int32: Channel ID.
	/// string: Path, such as "Unlocks/something". Can be an empty string to set the root node.
	/// object: Custom value. Can also be a DataNode to set that node.
	/// </summary>

	RequestSetChannelData,

	/// <summary>
	/// The channel's data has been changed.
	/// int32: Channel ID.
	/// string: Path, such as "Unlocks/something". Can be an empty string to set the root node.
	/// object: Custom value. Can also be a DataNode to set that node.
	/// </summary>

	ResponseSetChannelData,

	/// <summary>
	/// Request the list of open channels from the server.
	/// </summary>
	
	RequestChannelList,

	/// <summary>
	/// List open channels on the server.
	/// int32: number of channels to follow
	/// For each channel:
	///   int32: ID
	///   ushort: Number of players
	///   ushort: Player limit
	///   bool: Has a password
	///   bool: Is persistent
	///   string: Level
	///   DataNode: Custom data
	/// </summary>

	ResponseChannelList,

	/// <summary>
	/// Echo the packet to everyone in the room. Interpreting the packet is up to the client.
	/// int32: ID of the player that sent the packet.
	/// int32: Channel ID.
	/// uint32: Object ID (24 bits), RFC ID (8 bits).
	/// string: Function name (only if RFC ID is 0).
	/// Arbitrary amount of data follows.
	/// </summary>

	ForwardToAll,

	/// <summary>
	/// Echo the packet to everyone in the room and everyone who joins later.
	/// int32: ID of the player that sent the packet.
	/// int32: Channel ID.
	/// uint32: Object ID (24 bits), RFC ID (8 bits).
	/// string: Function name (only if RFC ID is 0).
	/// Arbitrary amount of data follows.
	/// </summary>

	ForwardToAllSaved,

	/// <summary>
	/// Echo the packet to everyone in the room except the sender. Interpreting the packet is up to the client.
	/// int32: ID of the player that sent the packet.
	/// int32: Channel ID.
	/// uint32: Object ID (24 bits), RFC ID (8 bits).
	/// string: Function name (only if RFC ID is 0).
	/// Arbitrary amount of data follows.
	/// </summary>

	ForwardToOthers,

	/// <summary>
	/// Echo the packet to everyone in the room (except the sender) and everyone who joins later.
	/// int32: ID of the player that sent the packet.
	/// int32: Channel ID.
	/// uint32: Object ID (24 bits), RFC ID (8 bits).
	/// string: Function name (only if RFC ID is 0).
	/// Arbitrary amount of data follows.
	/// </summary>

	ForwardToOthersSaved,

	/// <summary>
	/// Echo the packet to the room's host. Interpreting the packet is up to the client.
	/// int32: ID of the player that sent the packet.
	/// int32: Channel ID.
	/// uint32: Object ID (24 bits), RFC ID (8 bits).
	/// string: Function name (only if RFC ID is 0).
	/// Arbitrary amount of data follows.
	/// </summary>

	ForwardToHost,

	/// <summary>
	/// Echo the packet to the specified player.
	/// int32: ID of the player that sent the packet.
	/// int32: Player ID.
	/// int32: Channel ID.
	/// uint32: Object ID (24 bits), RFC ID (8 bits).
	/// string: Function name (only if RFC ID is 0).
	/// Arbitrary amount of data follows.
	/// </summary>

	ForwardToPlayer,

	/// <summary>
	/// Echo the packet to the specified player.
	/// int32: ID of the player that sent the packet.
	/// string: Player name.
	/// int32: Channel ID.
	/// uint32: Object ID (24 bits), RFC ID (8 bits).
	/// string: Function name (only if RFC ID is 0).
	/// Arbitrary amount of data follows.
	/// </summary>

	ForwardByName,

	/// <summary>
	/// Server notification sent when the target requested by ForwardByName was not found.
	/// string: Player name.
	/// </summary>

	ForwardTargetNotFound,

	/// <summary>
	/// Echo this message to everyone connected to the server.
	/// int32: ID of the player that sent the packet.
	/// int32: Channel ID.
	/// uint32: Object ID (24 bits), RFC ID (8 bits).
	/// string: Function name (only if RFC ID is 0).
	/// Arbitrary amount of data follows.
	/// </summary>

	Broadcast,

	/// <summary>
	/// Echo this message to administrators connected to the server. Same as Broadcast, but only goes to admins.
	/// int32: ID of the player that sent the packet.
	/// int32: Channel ID.
	/// uint32: Object ID (24 bits), RFC ID (8 bits).
	/// string: Function name (only if RFC ID is 0).
	/// Arbitrary amount of data follows.
	/// </summary>

	BroadcastAdmin,

	/// <summary>
	/// By default, the player gets disconnected after 10 seconds of inactivity. You can change this on a per-player basis.
	/// Setting this value to '0' will turn off this functionality altogether -- however it's a good idea to keep it at some
	/// valid non-zero value. If you know the player is going to be loading a level for up to a minute, set it to 2 minutes (120).
	/// int32: timeout delay in seconds
	/// </summary>

	RequestSetTimeout,

	/// <summary>
	/// Set the player's 'data' property. When a client sends this packet to the server,
	/// the same packet will be echoed to everyone except the sender.
	/// int32: Player ID who's data should be synchronized. Must match the player that sent the request.
	/// string: Path, such as "Unlocks/something". Can be an empty string to set the root node.
	/// object: Custom value. Can also be a DataNode to set that node.
	/// </summary>

	RequestSetPlayerData,

	/// <summary>
	/// Set the player data associated with the specified player.
	/// int32: Player ID who's data should be synchronized.
	/// string: Path, such as "Unlocks/something". Can be an empty string to set the root node.
	/// object: Custom value. Can also be a DataNode to set that node.
	/// </summary>

	ResponseSetPlayerData,

	/// <summary>
	/// Mark the channel as closed and kick out all the players.
	/// int32: channel ID.
	/// bool: whether to disconnect the players, or just make them leave the channel.
	/// </summary>

	RequestDeleteChannel,

	/// <summary>
	/// Request to be made an administrator.
	/// string: password.
	/// </summary>

	RequestVerifyAdmin,

	/// <summary>
	/// Request to add a new admin.
	/// string: admin keyword to add.
	/// </summary>

	RequestCreateAdmin,

	/// <summary>
	/// Remove this admin from the list.
	/// string: admin keyword to remove.
	/// </summary>

	RequestRemoveAdmin,

	/// <summary>
	/// Kick the specified player.
	/// int32: Channel ID.
	/// int32: Player ID.
	/// string: player name or address
	/// </summary>

	RequestKick,

	/// <summary>
	/// Ban this player.
	/// int32: Player ID.
	/// string: player name or address (if ID is '0')
	/// </summary>

	RequestBan,

	/// <summary>
	/// Assigns the specified alias to the player. If this alias fails to pass the ban list, the player get disconnected.
	/// string: alias to add.
	/// </summary>

	RequestSetAlias,

	/// <summary>
	/// Remove ban from this keyword.
	/// string: data to remove.
	/// </summary>

	RequestUnban,

	/// <summary>
	/// No longer used.
	/// </summary>

	RequestLogPlayers,

	/// <summary>
	/// Change the ban list to the specified one. Only administrators can do this.
	/// string: ban list's contents.
	/// </summary>

	RequestSetBanList,

	/// <summary>
	/// Reload configuration, admin and ban list data. Only administrators can use this command.
	/// </summary>

	RequestReloadServerConfig,

	/// <summary>
	/// Sets a server option. Only administrators can do this.
	/// string: Path, such as "Unlocks/something". Can be an empty string to set the root node.
	/// object: Custom value. Can also be a DataNode to set that node.
	/// </summary>

	RequestSetServerData,

	/// <summary>
	/// Server option sent back from the server to all connected clients in response to RequestSetServerOption.
	/// string: Path, such as "Unlocks/something". Can be an empty string to set the root node.
	/// object: Custom value. Can also be a DataNode to set that node.
	/// </summary>

	ResponseSetServerData,

	/// <summary>
	/// Response coming from the server for authenticated administrators.
	/// int32: ID of the player.
	/// </summary>

	ResponseVerifyAdmin,

	/// <summary>
	/// Lock the current channel, preventing all forms of create, delete and saved RFCs.
	/// Anyone trying to call create, delete or saved RFCs will be logged and ignored.
	/// Only administrators can lock channels.
	/// int32: channel ID.
	/// bool: whether it should be locked.
	/// </summary>

	RequestLockChannel,

	/// <summary>
	/// Response coming from the server that sets the local locked channel flag.
	/// int32: channel ID.
	/// bool: whether it's locked.
	/// </summary>

	ResponseLockChannel,

	/// <summary>
	/// Special message indicates that the connected player was actually a web browser.
	/// string: Path of the HTTP GET request.
	/// </summary>

	RequestHTTPGet,

	/// <summary>
	/// Change the server's name without restarting it. Admin-only.
	/// string: Server's new name.
	/// </summary>

	RequestRenameServer,

	/// <summary>
	/// Request to set the specified filename to be associated with player saves.
	/// The data within the file will be loaded and a ResponseSetPlayerData will be sent back.
	/// TNet will automatically save the player's data into this file from this moment onward.
	/// string: Filename to use for player saves.
	/// byte: Save type. 0 = Text. 1 = Binary. 2 = Compressed.
	/// </summary>

	RequestSetPlayerSave,

	/// <summary>
	/// Begin custom packets here.
	/// </summary>

	UserPacket = 128,
}
}
