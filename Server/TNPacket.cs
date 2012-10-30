namespace TNet
{
/// <summary>
/// Clients send requests to the server and receive responses back. Forwarded calls arrive as-is.
/// </summary>

public enum Packet
{
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
	/// int16: RFC id.
	/// </summary>

	RequestRemoveRFC,

	//===================================================================================

	/// <summary>
	/// Always the first packet to arrive from the server. If the server version didn't match the client, a disconnect may follow.
	/// int32: Server version.
	/// int32: Player ID.
	/// </summary>

	ResponseVersion,

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

	//===================================================================================

	/// <summary>
	/// Echo the packet to everyone in the room.
	/// </summary>

	ForwardToAll,

	/// <summary>
	/// Echo the packet to everyone in the room and everyone who joins later.
	/// int16: RFC ID.
	/// </summary>

	ForwardToAllBuffered,

	/// <summary>
	/// Echo the packet to everyone in the room except the sender.
	/// </summary>

	ForwardToOthers,

	/// <summary>
	/// Echo the packet to everyone in the room (except the sender) and everyone who joins later.
	/// int16: RFC ID.
	/// </summary>

	ForwardToOthersBuffered,

	/// <summary>
	/// Echo the packet to the room's host.
	/// </summary>

	ForwardToHost,

	/// <summary>
	/// Echo the packet to the specified player.
	/// int32: Player ID
	/// </summary>

	ForwardToPlayer,
}
}