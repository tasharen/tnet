namespace TNet
{
/// <summary>
/// Clients send requests to the server and receive responses back. Forwarded calls arrive as-is.
/// </summary>

public enum Packet
{
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
	/// This should be the very first packet sent by the client.
	/// int32: Protocol version.
	/// </summary>

	RequestID,

	/// <summary>
	/// Clients should send a ping request periodically.
	/// </summary>

	RequestPing,

	/// <summary>
	/// Join the specified channel.
	/// int32: Channel ID
	/// string: Channel password
	/// </summary>

	RequestJoinChannel,

	/// <summary>
	/// Leave the channel the player is in.
	/// </summary>

	RequestLeaveChannel,

	/// <summary>
	/// Player name change.
	/// string: Player name.
	/// </summary>

	RequestSetName,

	/// <summary>
	/// Transfer the host status to the specified player. Only works if the sender is currently hosting.
	/// int32: Player ID.
	/// </summary>

	RequestSetHost,

	/// <summary>
	/// Delete the specified buffered function.
	/// int32: Object ID (24 bits), RFC ID (8 bits).
	/// </summary>

	RequestRemoveRFC,

	/// <summary>
	/// Instantiate a new object with the specified identifier.
	/// int16: Object index.
	/// byte: 1 if a new Object ID is requested, 0 otherwise.
	/// Arbitrary amount of data follows. All of it will be passed along with the response call.
	/// </summary>

	RequestCreate,

	/// <summary>
	/// Delete the specified Network Object.
	/// int32: Object ID.
	/// </summary>

	RequestDestroy,

	//===================================================================================

	/// <summary>
	/// Response to a ping request.
	/// </summary>

	ResponsePing,

	/// <summary>
	/// Always the first packet to arrive from the server.
	/// If the protocol version didn't match the client, a disconnect may follow.
	/// int32: Protocol ID.
	/// int32: Player ID.
	/// </summary>

	ResponseID,

	/// <summary>
	/// Inform everyone of this player leaving the channel.
	/// int32: Player ID.
	/// </summary>

	ResponsePlayerLeft,

	/// <summary>
	/// Inform the channel that a new player has joined.
	/// 
	/// Parameters:
	/// int32: Player ID,
	/// string: Player name.
	/// </summary>

	ResponsePlayerJoined,

	/// <summary>
	/// Start of the channel joining process. Sent to the player who is joining the channel.
	/// 
	/// Parameters:
	/// int32: Channel ID,
	/// int16: Number of players.
	/// 
	/// Then for each player:
	/// int32: Player ID,
	/// string: Player Name.
	/// </summary>

	ResponseJoiningChannel,

	/// <summary>
	/// Inform the player that they have successfully joined a channel.
	/// </summary>

	ResponseJoinedChannel,

	/// <summary>
	/// Sent when the player leaves the channel.
	/// </summary>

	ResponseLeftChannel,

	/// <summary>
	/// Inform the player that the join request failed due to a wrong password.
	/// </summary>

	ResponseWrongPassword,

	/// <summary>
	/// Change the specified player's name.
	/// int32: Player ID,
	/// string: Player name.
	/// </summary>

	ResponseRenamePlayer,

	/// <summary>
	/// Inform the player of who is hosting.
	/// int32: Player ID.
	/// </summary>

	ResponseSetHost,

	/// <summary>
	/// Create a new persistent entry.
	/// int16: Object ID.
	/// int32: Unique Identifier (aka Object ID) if requested, 0 otherwise.
	/// Arbitrary amount of data follows, same data that was passed along with the Create Request.
	/// </summary>

	ResponseCreate,

	/// <summary>
	/// Delete the specified Unique Identifier and its associated entry.
	/// int16: Number of objs that will follow.
	/// int32[] Unique Identifiers (aka Object IDs).
	/// </summary>

	ResponseDestroy,

	//===================================================================================

	/// <summary>
	/// Echo the packet to everyone in the room. Interpreting the packet is up to the client.
	/// int32: Object ID (24 bits), RFC ID (8 bits).
	/// Arbitrary amount of data follows.
	/// </summary>

	ForwardToAll,

	/// <summary>
	/// Echo the packet to everyone in the room and everyone who joins later.
	/// int32: Object ID (24 bits), RFC ID (8 bits).
	/// Arbitrary amount of data follows.
	/// </summary>

	ForwardToAllBuffered,

	/// <summary>
	/// Echo the packet to everyone in the room except the sender. Interpreting the packet is up to the client.
	/// int32: Object ID (24 bits), RFC ID (8 bits).
	/// Arbitrary amount of data follows.
	/// </summary>

	ForwardToOthers,

	/// <summary>
	/// Echo the packet to everyone in the room (except the sender) and everyone who joins later.
	/// int32: Object ID (24 bits), RFC ID (8 bits).
	/// Arbitrary amount of data follows.
	/// </summary>

	ForwardToOthersBuffered,

	/// <summary>
	/// Echo the packet to the room's host. Interpreting the packet is up to the client.
	/// int32: Object ID (24 bits), RFC ID (8 bits).
	/// Arbitrary amount of data follows.
	/// </summary>

	ForwardToHost,

	/// <summary>
	/// Echo the packet to the specified player.
	/// int32: Player ID
	/// int32: Object ID (24 bits), RFC ID (8 bits).
	/// Arbitrary amount of data follows.
	/// </summary>

	ForwardToPlayer,
}
}