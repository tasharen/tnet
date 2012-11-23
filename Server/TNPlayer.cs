using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Collections.Generic;
using System.Threading;

namespace TNet
{
/// <summary>
/// Class containing information about connected players.
/// </summary>

public class Player : Connection
{
	/// <summary>
	/// Server protocol version. Must match the client.
	/// </summary>

	public const int version = 1;

	/// <summary>
	/// Connection ID.
	/// </summary>

	public int id = 0;

	/// <summary>
	/// Whether the connection has been verified to use the correct protocol version.
	/// </summary>

	public bool verified = false;

	/// <summary>
	/// Player's name.
	/// </summary>

	public string name;

	/// <summary>
	/// Channel that the player is currently in.
	/// </summary>

	public Channel channel;

	/// <summary>
	/// Channel joining process involves multiple steps. It's faster to perform them all at once.
	/// </summary>

	public void FinishJoiningChannel (int channelID)
	{
		Buffer buffer = Connection.CreateBuffer();

		// Step 2: Tell the player who else is in the channel
		BinaryWriter writer = buffer.BeginPacket(Packet.ResponseJoiningChannel);
		{
			writer.Write(channelID);
			writer.Write((short)channel.players.size);

			for (int i = 0; i < channel.players.size; ++i)
			{
				Player tp = channel.players[i];
				writer.Write(tp.id);
				writer.Write(string.IsNullOrEmpty(tp.name) ? "<Guest>" : tp.name);
			}
		}

		// End the first packet, but remember where it ended
		int offset = buffer.EndPacket();

		// Step 3: Inform the player of who is hosting
		buffer.BeginPacket(Packet.ResponseSetHost, offset);
		writer.Write(channel.host.id);
		offset = buffer.EndPacket(offset);

		// Step 4: Send the list of objects that have been created
		for (int i = 0; i < channel.created.size; ++i)
		{
			Channel.CreatedObject obj = channel.created.buffer[i];
			buffer.BeginPacket(Packet.ResponseCreate, offset);
			writer.Write(obj.objectID);
			writer.Write(obj.uniqueID);
			writer.Write(obj.buffer.buffer);
			offset = buffer.EndPacket(offset);
		}

		// Step 5: Send the list of objects that have been destroyed
		buffer.BeginPacket(Packet.ResponseDestroy, offset);
		writer.Write((short)channel.destroyed.size);
		for (int i = 0; i < channel.destroyed.size; ++i)
			writer.Write((short)channel.destroyed.buffer[i]);
		offset = buffer.EndPacket(offset);

		// Step 6: Send all buffered RFCs to the new player
		for (int i = 0; i < channel.rfcs.size; ++i)
		{
			Buffer rfcBuff = channel.rfcs[i].buffer;
			rfcBuff.BeginReading();
			buffer.BeginWriting(offset);
			writer.Write(rfcBuff.buffer, rfcBuff.position, rfcBuff.size);
			offset = buffer.EndWriting();
		}

		// Step 7: The join process is now complete
		buffer.BeginPacket(Packet.ResponseJoinedChannel, offset);
		offset = buffer.EndPacket(offset);

		// Send the entire buffer
		SendPacket(buffer);
	}
}
}