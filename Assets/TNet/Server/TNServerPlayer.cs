//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

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

public class ServerPlayer : TcpProtocol
{
	/// <summary>
	/// Whether the connection has been verified to use the correct protocol version.
	/// </summary>

	public bool verified = false;

	/// <summary>
	/// Channel that the player is currently in.
	/// </summary>

	public Channel channel;

	/// <summary>
	/// Channel joining process involves multiple steps. It's faster to perform them all at once.
	/// </summary>

	public void FinishJoiningChannel ()
	{
		Buffer buffer = Buffer.Create();

		// Step 2: Tell the player who else is in the channel
		BinaryWriter writer = buffer.BeginPacket(Packet.ResponseJoiningChannel);
		{
			writer.Write(channel.id);
			writer.Write((short)channel.players.size);

			for (int i = 0; i < channel.players.size; ++i)
			{
				ServerPlayer tp = channel.players[i];
				writer.Write(tp.id);
				writer.Write(string.IsNullOrEmpty(tp.name) ? "Guest" : tp.name);
			}
		}

		// End the first packet, but remember where it ended
		int offset = buffer.EndPacket();

		// Step 3: Inform the player of who is hosting
		if (channel.host == null) channel.host = this;
		buffer.BeginPacket(Packet.ResponseSetHost, offset);
		writer.Write(channel.host.id);
		offset = buffer.EndPacket(offset);

		// Step 5: Inform the player of what level we're on
		buffer.BeginPacket(Packet.ResponseLoadLevel, offset);
		writer.Write(string.IsNullOrEmpty(channel.level) ? "" : channel.level);
		offset = buffer.EndPacket(offset);

		// Step 6: Send the list of objects that have been created
		for (int i = 0; i < channel.created.size; ++i)
		{
			Channel.CreatedObject obj = channel.created.buffer[i];
			buffer.BeginPacket(Packet.ResponseCreate, offset);
			writer.Write(obj.objectID);
			writer.Write(obj.uniqueID);
			writer.Write(obj.buffer.buffer, obj.buffer.position, obj.buffer.size);
			offset = buffer.EndPacket(offset);
		}

		// Step 7: Send the list of objects that have been destroyed
		if (channel.destroyed.size != 0)
		{
			buffer.BeginPacket(Packet.ResponseDestroy, offset);
			writer.Write((ushort)channel.destroyed.size);
			for (int i = 0; i < channel.destroyed.size; ++i)
				writer.Write(channel.destroyed.buffer[i]);
			offset = buffer.EndPacket(offset);
		}

		// Step 8: Send all buffered RFCs to the new player
		for (int i = 0; i < channel.rfcs.size; ++i)
		{
			Buffer rfcBuff = channel.rfcs[i].buffer;
			rfcBuff.BeginReading();
			buffer.BeginWriting(offset);
			writer.Write(rfcBuff.buffer, rfcBuff.position, rfcBuff.size);
			offset = buffer.EndWriting();
		}

		// Step 9: The join process is now complete
		buffer.BeginPacket(Packet.ResponseJoinChannel, offset);
		writer.Write(true);
		offset = buffer.EndPacket(offset);

		// Send the entire buffer
		SendPacket(buffer);
		buffer.Recycle();
	}
}
}