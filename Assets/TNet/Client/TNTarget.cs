//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2020 Tasharen Entertainment Inc
//-------------------------------------------------

namespace TNet
{
/// <summary>
/// Helper enum -- the entries should be in the same order as in the Packet enum.
/// </summary>

[DoNotObfuscate] public enum Target
{
	/// <summary>
	/// Echo the packet to everyone in the room.
	/// </summary>

	All,

	/// <summary>
	/// Echo the packet to everyone in the room and everyone who joins later.
	/// </summary>

	AllSaved,

	/// <summary>
	/// Echo the packet to everyone in the room except the sender.
	/// </summary>

	Others,

	/// <summary>
	/// Echo the packet to everyone in the room (except the sender) and everyone who joins later.
	/// </summary>

	OthersSaved,

	/// <summary>
	/// Echo the packet to the channel's host. Note that this is only kept for backwards compatibility. There is no need for this target anymore,
	/// since you can (and should) send the RFC to TNObject's owner instead.
	/// </summary>

	Host,

	/// <summary>
	/// Broadcast is the same as "All", but it has a built-in spam checker. Ideal for global chat.
	/// </summary>

	Broadcast,

	/// <summary>
	/// Send this packet to administrators.
	/// </summary>

	Admin,

	/// <summary>
	/// Saves this packet on the server, without sending it to anyone else. Since it gets saved, all players that join will still receive this packet, however.
	/// Useful if you have a packet that includes data that lets clients calculate the up-to-date value themselves using interpolation or time.
	/// </summary>

	NoneSaved,
}
}
