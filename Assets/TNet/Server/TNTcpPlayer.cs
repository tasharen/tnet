//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2015 Tasharen Entertainment
//---------------------------------------------

using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Text;

namespace TNet
{
/// <summary>
/// Class containing information about connected players.
/// </summary>

public class TcpPlayer : TcpProtocol
{
	[System.Obsolete("Players can now subscribe to multiple channels at once, making the singular 'channel' obsolete.")]
	public Channel channel { get { return (channels.size != 0) ? channels[0] : null; } }

	/// <summary>
	/// Channel that the player is currently in.
	/// </summary>

	public List<Channel> channels = new List<Channel>();

	/// <summary>
	/// Whether the player is in the specified channel.
	/// </summary>

	public bool IsInChannel (int id)
	{
		for (int i = 0; i < channels.size; ++i)
			if (channels[i].id == id) return true;
		return false;
	}

	/// <summary>
	/// Return the specified channel if the player is currently within it, null otherwise.
	/// </summary>

	public Channel GetChannel (int id)
	{
		for (int i = 0; i < channels.size; ++i)
			if (channels[i].id == id) return channels[i];
		return null;
	}

	/// <summary>
	/// UDP end point if the player has one open.
	/// </summary>

	public IPEndPoint udpEndPoint;

	/// <summary>
	/// Whether the UDP has been confirmed as active and usable.
	/// </summary>

	public bool udpIsUsable = false;

	/// <summary>
	/// Whether this player has authenticated as an administrator.
	/// </summary>

	public bool isAdmin = false;

	/// <summary>
	/// Set during the verification stage, if at all. Used to automatically flag administrators when they connect.
	/// </summary>

	public string adminKey = null;

	/// <summary>
	/// Time of the next possible broadcast, used to catch spammers.
	/// </summary>

	public long nextBroadcast = 0;

	/// <summary>
	/// Count broadcasts done per second.
	/// </summary>

	public int broadcastCount = 0;

	/// <summary>
	/// Channel joining process involves multiple steps. It's faster to perform them all at once.
	/// </summary>

	public void FinishJoiningChannel (Channel channel, DataNode serverData, string requestedLevelName)
	{
		Buffer buffer = Buffer.Create();

		// Tell the player who else is in the channel
		BinaryWriter writer = buffer.BeginPacket(Packet.ResponseJoiningChannel);
		{
			writer.Write(channel.id);
			writer.Write((short)channel.players.size);

			for (int i = 0; i < channel.players.size; ++i)
			{
				Player tp = channel.players[i];
				writer.Write(tp.id);
				writer.Write(string.IsNullOrEmpty(tp.name) ? "Guest" : tp.name);
#if STANDALONE
				if (tp.data == null) writer.Write((byte)0);
				else writer.Write((byte[])tp.data);
#else
				writer.WriteObject(tp.data);
#endif
			}
		}

		// End the first packet, but remember where it ended
		int offset = buffer.EndPacket();

		// Inform the player of who is hosting
		if (channel.host == null) channel.host = this;
		writer = buffer.BeginPacket(Packet.ResponseSetHost, offset);
		writer.Write(channel.id);
		writer.Write(channel.host.id);
		offset = buffer.EndPacket(offset);

		// Send the channel's data
		if (channel.data != null)
		{
			writer = buffer.BeginPacket(Packet.ResponseSetChannelData, offset);
			writer.Write(channel.id);
			writer.Write(channel.data);
			offset = buffer.EndPacket(offset);
		}

		// Send the LoadLevel packet, but only if some level name was specified in the original LoadLevel request.
		if (!string.IsNullOrEmpty(requestedLevelName) && !string.IsNullOrEmpty(channel.level))
		{
			writer = buffer.BeginPacket(Packet.ResponseLoadLevel, offset);
			writer.Write(channel.id);
			writer.Write(channel.level);
			offset = buffer.EndPacket(offset);
		}

		// Send the list of objects that have been created
		for (int i = 0; i < channel.created.size; ++i)
		{
			Channel.CreatedObject obj = channel.created.buffer[i];

			bool isPresent = false;

			for (int b = 0; b < channel.players.size; ++b)
			{
				if (channel.players[b].id == obj.playerID)
				{
					isPresent = true;
					break;
				}
			}

			// If the previous owner is not present, transfer ownership to the host
			if (!isPresent) obj.playerID = channel.host.id;

			writer = buffer.BeginPacket(Packet.ResponseCreateObject, offset);
			writer.Write(channel.id);
			writer.Write(obj.playerID);
			writer.Write(obj.objectIndex);
			writer.Write(obj.objectID);
			writer.Write(obj.buffer.buffer, obj.buffer.position, obj.buffer.size);
			offset = buffer.EndPacket(offset);
		}

		// Send the list of objects that have been destroyed
		if (channel.destroyed.size != 0)
		{
			writer = buffer.BeginPacket(Packet.ResponseDestroyObject, offset);
			writer.Write(channel.id);
			writer.Write((ushort)channel.destroyed.size);
			for (int i = 0; i < channel.destroyed.size; ++i)
				writer.Write(channel.destroyed.buffer[i]);
			offset = buffer.EndPacket(offset);
		}

		// Send all buffered RFCs to the new player
		for (int i = 0; i < channel.rfcs.size; ++i)
		{
			Channel.RFC rfc = channel.rfcs[i];
			offset = rfc.WritePacket(channel.id, buffer, offset);
		}

		// Inform the player that the channel is now locked
		if (channel.locked)
		{
			writer = buffer.BeginPacket(Packet.ResponseLockChannel, offset);
			writer.Write(channel.id);
			writer.Write(true);
			offset = buffer.EndPacket(offset);
		}

		// The join process is now complete
		buffer.BeginPacket(Packet.ResponseJoinChannel, offset);
		writer.Write(channel.id);
		writer.Write(true);
		offset = buffer.EndPacket(offset);

		// Send the entire buffer
		SendTcpPacket(buffer);
		buffer.Recycle();
	}
}
}
