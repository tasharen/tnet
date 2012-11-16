using System;
using System.IO;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;
using System.Net;

namespace TNet
{
/// <summary>
/// Server-side logic.
/// </summary>

public class Server
{
	/// <summary>
	/// Server protocol version. Must match the client.
	/// </summary>

	public const int version = 1;

	/// <summary>
	/// List of players in a consecutive order for each looping.
	/// </summary>

	protected BetterList<Player> mPlayers = new BetterList<Player>();

	/// <summary>
	/// Dictionary list of players for easy access by ID.
	/// </summary>

	protected Dictionary<int, Player> mDictionary = new Dictionary<int, Player>();

	/// <summary>
	/// List of all the active channels.
	/// </summary>

	protected BetterList<Channel> mChannels = new BetterList<Channel>();

	/// <summary>
	/// Random number generator.
	/// </summary>

	protected RandomGenerator mRandom = new RandomGenerator();

	Buffer mOut = new Buffer();
	TcpListener mListener;
	Thread mThread;
	static int mPlayerCounter = 0;

	/// <summary>
	/// Start listening to incoming connections on the specified port.
	/// </summary>

	public bool Start (int port)
	{
		Stop();

		try
		{
			mListener = new TcpListener(IPAddress.Loopback, port);
			mListener.Start(10);
		}
		catch (System.Exception ex)
		{
			Error(ex.Message);
			return false;
		}

		mThread = new Thread(ThreadFunction);
		mThread.Start();
		return true;
	}

	/// <summary>
	/// Stop listening to incoming connections and disconnect all players.
	/// </summary>

	public void Stop ()
	{
		// Stop the worker thread
		if (mThread != null)
		{
			mThread.Abort();
			mThread = null;
		}

		// Stop listening to incoming connections
		if (mListener != null)
		{
			mListener.Stop();
			mListener = null;
		}

		// Remove all connected players
		for (int i = mPlayers.size; i > 0; ) RemovePlayer(mPlayers[i]);
	}

	/// <summary>
	/// Thread that will be processing incoming data.
	/// </summary>

	void ThreadFunction ()
	{
		for (; ; )
		{
			// Add all pending connections
			while (mListener.Pending())
			{
				Player p = AddPlayer(mListener.AcceptSocket());
				Console.WriteLine(p.address + " has connected");
			}

			bool received = false;
			long ms = DateTime.Now.Ticks / 10000;

			for (int i = 0; i < mPlayers.size; )
			{
				Player player = mPlayers[i];

				if (!player.socket.Connected)
				{
					// The socket has been disconnected -- remove this player
					Console.WriteLine(player.address + " has disconnected");
					RemovePlayer(player);
					continue;
				}
				else if (player.verified)
				{
					// Verified player -- receive their packets
					for (int b = 0; b < 100 && ReceivePacket(player); ++b) received = true;
				}
				else if (player.hasData)
				{
					// Non-verified player -- the first 4 bytes must be the version number
					int playerVersion = player.ReceiveVersion();

					// Send the server's version number to the player
					BinaryWriter writer = BeginSend(Packet.ResponseVersion);
					writer.Write(version);
					writer.Write(player.id);
					EndSend(player);

					// If the version matched, this player has now been verified
					if (playerVersion == version) player.verified = true;
					else RemovePlayer(player);
					continue;
				}
				else if (player.timestamp + 1000 < ms)
				{
					// Time out -- disconnect this player
					RemovePlayer(player);
					continue;
				}
				++i;
			}
			if (!received) Thread.Sleep(1);
		}
	}

	/// <summary>
	/// Log an error message.
	/// </summary>

	protected virtual void Error (string error) { Console.WriteLine("ERROR: " + error); }

	/// <summary>
	/// Add a new player entry.
	/// </summary>

	protected Player AddPlayer (Socket socket)
	{
		Player player = new Player();
		player.id = ++mPlayerCounter;
		player.socket = socket;
		player.timestamp = DateTime.Now.Ticks / 100000;
		player.address = ((IPEndPoint)socket.RemoteEndPoint).ToString();
		mDictionary.Add(player.id, player);
		mPlayers.Add(player);
		return player;
	}

	/// <summary>
	/// Remove the specified player.
	/// </summary>

	protected void RemovePlayer (Player p)
	{
		if (p != null && p.id != 0)
		{
			if (p.socket.Connected) p.socket.Disconnect(false);
			mDictionary.Remove(p.id);
			mPlayers.Remove(p);
			p.id = 0;
		}
	}

	/// <summary>
	/// Retrieve a player by their ID.
	/// </summary>

	protected Player GetPlayer (int id)
	{
		Player p = null;
		mDictionary.TryGetValue(id, out p);
		return p;
	}

	/// <summary>
	/// Create a new channel (or return an existing one).
	/// </summary>

	protected Channel CreateChannel (int channelID)
	{
		Channel channel;

		for (int i = 0; i < mChannels.size; ++i)
		{
			channel = mChannels[i];
			if (channel.id == channelID) return channel;
		}

		channel = new Channel();
		channel.id = channelID;
		mChannels.Add(channel);
		return channel;
	}

	/// <summary>
	/// Check to see if the specified channel exists.
	/// </summary>

	protected bool ChannelExists (int id)
	{
		for (int i = 0; i < mChannels.size; ++i) if (mChannels[i].id == id) return true;
		return false;
	}

	/// <summary>
	/// Start the sending process.
	/// </summary>

	protected BinaryWriter BeginSend (Packet type)
	{
		BinaryWriter writer = mOut.BeginPacket();
		writer.Write((byte)type);
		return writer;
	}

	/// <summary>
	/// Send the outgoing buffer to the specified player.
	/// </summary>

	protected void EndSend (Player player)
	{
		if (player.socket.Connected)
		{
			int size = mOut.EndPacket();
			player.socket.Send(mOut.buffer, 0, size, SocketFlags.None);
		}
	}

	/// <summary>
	/// Send the outgoing buffer to all players in the specified channel.
	/// </summary>

	protected void EndSend (Channel channel)
	{
		int size = mOut.EndPacket();
		byte[] buffer = mOut.buffer;
		SendToChannel(channel, buffer, 0, size);
	}

	/// <summary>
	/// Send the outgoing buffer to all connected players.
	/// </summary>

	protected void EndSend ()
	{
		int size = mOut.EndPacket();
		byte[] buffer = mOut.buffer;
		for (int b = 0; b < mChannels.size; ++b) SendToChannel(mChannels[b], buffer, 0, size);
	}

	/// <summary>
	/// Send the outgoing buffer to all players in the specified channel.
	/// </summary>

	protected void SendToChannel (Channel channel, byte[] buffer, int offset, int size)
	{
		for (int i = 0; i < channel.players.size; ++i)
		{
			Player player = channel.players[i];

			if (player.socket.Connected)
			{
				player.socket.Send(buffer, offset, size, SocketFlags.None);
			}
		}
	}

	/// <summary>
	/// Have the specified player assume control of the channel.
	/// </summary>

	protected void SendSetHost (Player player)
	{
		if (player.channel != null && player.channel.host != player)
		{
			player.channel.host = player;
			BinaryWriter writer = BeginSend(Packet.ResponseSetHost);
			writer.Write(player.id);
			EndSend(player.channel);
		}
	}

	/// <summary>
	/// Leave the channel the player is in.
	/// </summary>

	protected void SendLeaveChannel (Player player)
	{
		if (player.channel != null)
		{
			// Remove this player from the channel
			player.channel.players.Remove(player);

			// Inform everyone of this player leaving the channel
			BinaryWriter writer = BeginSend(Packet.ResponsePlayerLeft);
			writer.Write(player.id);
			EndSend(player.channel);

			// Are there other players left?
			if (player.channel.players.size > 0)
			{
				// If this player was the host, choose a new host
				if (player.channel.host == player) SendSetHost(player.channel.players[0]);
			}
			// No other players left -- delete this channel
			else mChannels.Remove(player.channel);
			player.channel = null;

			// Notify the player that they have left the channel
			BeginSend(Packet.ResponseLeftChannel);
			EndSend(player);
		}
	}

	/// <summary>
	/// Join the specified channel.
	/// Step 1: Inform the channel that the player is joining.
	/// Step 2: Inform the player that they have joined the channel and tell them who else is there.
	/// Step 3: Inform the player of who's the channel's host.
	/// Step 4: Send the list of objects that have been created.
	/// Step 5: Send the list of objects that have been destroyed.
	/// Step 6: Send all buffered RFC calls to the new player.
	/// Step 7: Inform the player that the joining process is now complete.
	/// </summary>

	protected void SendJoinChannel (Player player, int channelID)
	{
		if (player.channel == null || player.channel.id != channelID)
		{
			Channel channel = CreateChannel(channelID);

			// Step 1: Inform the channel that the player has joined
			BinaryWriter writer = BeginSend(Packet.ResponsePlayerJoined);
			{
				writer.Write(player.id);
				writer.Write(player.name);
			}
			EndSend(player.channel);

			// Add this player to the channel
			player.channel = channel;
			channel.players.Add(player);

			// Step 2: Tell the player who else is in the channel
			writer = BeginSend(Packet.ResponseJoiningChannel);
			{
				writer.Write(channelID);
				writer.Write((short)channel.players.size);

				for (int i = 0; i < channel.players.size; ++i)
				{
					Player tp = channel.players[i];
					writer.Write(tp.id);
					writer.Write(tp.name);
				}
			}
			EndSend(player.channel);

			// If the channel has no host, this player is automatically hosting
			if (player.channel.host == null) player.channel.host = player;

			// Step 3: Inform the player of who is hosting
			writer = BeginSend(Packet.ResponseSetHost);
			writer.Write(player.channel.host.id);
			EndSend(player);

			// Step 4: Send the list of objects that have been created
			//player.Send(player.channel.created.size);
			//for (int i = 0; i < player.channel.created.size; ++i)
			//	player.Send(player.channel.created.buffer, 0, player.channel.created.size);

			// Step 5: Send the list of objects that have been destroyed
			writer = BeginSend(Packet.ResponseDestroy);
			writer.Write((short)player.channel.destroyed.size);
			for (int i = 0; i < player.channel.created.size; ++i)
				writer.Write((short)player.channel.destroyed.buffer[i]);

			// Step 6: Send all buffered RFCs to the new player
			for (int i = 0; i < player.channel.rfcs.size; ++i) player.Send(player.channel.rfcs[i].buffer);

			// Step 7: The join process is now complete
			writer = BeginSend(Packet.ResponseJoinedChannel);
			EndSend(player);
		}
	}

	/// <summary>
	/// Receive and process a single incoming packet. Returns 'true' if a packet was received, 'false' otherwise.
	/// </summary>

	bool ReceivePacket (Player player)
	{
		BinaryReader reader = player.ReceivePacket();
		if (reader == null) return false;
		int packetID = reader.ReadByte();
		Packet request = (Packet)packetID;

		Console.WriteLine("Packet: " + request);

		if (request == Packet.ForwardToPlayer)
		{
			// Forward this packet to the specified player
			Player tp = GetPlayer(reader.ReadInt32());
			if (tp == null) player.ForwardLastPacket(tp);
		}
		else if (request == Packet.RequestJoinChannel)
		{
			// Join the specified channel
			int channelID = reader.ReadInt32();
			string pass = reader.ReadString();

			if (channelID == -1)
			{
				channelID = mRandom.Range(1, 100000000);

				for (int i = 0; i < 1000; ++i)
				{
					if (!ChannelExists(channelID)) break;
					channelID = mRandom.Range(1, 100000000);
				}
			}

			if (player.channel.id != channelID)
			{
				Channel channel = CreateChannel(channelID);

				if (channel.players.size == 0)
				{
					channel.password = pass;
					SendLeaveChannel(player);
					SendJoinChannel(player, channelID);
				}
				else if (string.IsNullOrEmpty(channel.password) || (channel.password == pass))
				{
					SendLeaveChannel(player);
					SendJoinChannel(player, channelID);
				}
				else
				{
					BeginSend(Packet.ResponseWrongPassword);
					EndSend(player);
				}
			}
		}
		else if (request == Packet.RequestLeaveChannel)
		{
			SendLeaveChannel(player);
		}
		else if (request == Packet.RequestSetName)
		{
			// Change the player's name
			player.name = reader.ReadString();
			BinaryWriter writer = BeginSend(Packet.ResponseRenamePlayer);
			writer.Write(player.id);
			writer.Write(player.name);
			EndSend(player.channel);
		}
		else if (request == Packet.RequestSetHost)
		{
			// Transfer the host state from one player to another
			if (player.channel != null && player.channel.host == player)
			{
				Player newHost = GetPlayer(reader.ReadInt32());
				if (newHost != null && newHost.channel == player.channel) SendSetHost(newHost);
			}
		}
		else if (request == Packet.RequestRemoveRFC)
		{
			if (player.channel != null)
			{
				player.channel.DeleteRFC(reader.ReadInt32(), reader.ReadInt16());
			}
		}
		else if (player.channel != null)
		{
			if (request == Packet.ForwardToAll)
			{
				// Forward the packet to everyone in the same channel
				for (int i = 0; i < player.channel.players.size; ++i)
				{
					Player tp = player.channel.players[i];
					player.ForwardLastPacket(tp);
				}
			}
			else if (request == Packet.ForwardToOthers)
			{
				// Forward the packet to everyone except the sender
				for (int i = 0; i < player.channel.players.size; ++i)
				{
					Player tp = player.channel.players[i];
					if (tp != player) player.ForwardLastPacket(tp);
				}
			}
			else if (request == Packet.ForwardToAllBuffered)
			{
				// Save this packet for future users
				int viewID = reader.ReadInt32();
				short rfcID = reader.ReadInt16();
				player.channel.CreateRFC(viewID, rfcID).buffer = player.CopyBuffer();

				// Forward the packet to everyone in the same channel
				for (int i = 0; i < player.channel.players.size; ++i)
				{
					Player tp = player.channel.players[i];
					player.ForwardLastPacket(tp);
				}
			}
			else if (request == Packet.ForwardToOthersBuffered)
			{
				// Save this packet for future users
				int viewID = reader.ReadInt32();
				short rfcID = reader.ReadInt16();
				player.channel.CreateRFC(viewID, rfcID).buffer = player.CopyBuffer();

				// Forward the packet to everyone except the sender
				for (int i = 0; i < player.channel.players.size; ++i)
				{
					Player tp = player.channel.players[i];
					if (tp != player) player.ForwardLastPacket(tp);
				}
			}
			else if (request == Packet.ForwardToHost)
			{
				// Forward the packet to the channel's host
				player.ForwardLastPacket(player.channel.host);
			}
			else if (request == Packet.RequestCreate)
			{
				if (player.channel != null)
				{
					// Create a new object
					short objectID = reader.ReadInt16();

					// Dynamically created Network Views should always start out being negative
					int viewID = (reader.ReadByte() == 0) ? 0 : --player.channel.viewCounter;

					// Remember that we've created a new view
					if (viewID != 0) player.channel.created.Add(viewID);

					// Inform the channel
					BinaryWriter writer = BeginSend(Packet.ResponseCreate);
					writer.Write(objectID);
					writer.Write(viewID);

					// If there is any data left, append it to the end
					int bytes = player.buffer.size - player.buffer.position;
					if (bytes > 0) writer.Write(player.buffer.buffer, player.buffer.position, bytes);
					EndSend(player.channel);
				}
			}
			else if (request == Packet.RequestDestroy)
			{
				// Destroy the specified network view
				int viewID = reader.ReadInt32();

				if (!player.channel.destroyed.Contains(viewID))
				{
					// If this view was not created dynamically, we should remember it
					if (!player.channel.created.Remove(viewID))
						player.channel.destroyed.Add(viewID);

					BinaryWriter writer = BeginSend(Packet.ResponseDestroy);
					writer.Write((short)1);
					writer.Write(viewID);
					EndSend(player.channel);
				}
			}
			else OnPacket(packetID, reader);
		}
		else OnPacket(packetID, reader);
		return true;
	}

	/// <summary>
	/// If custom functionality is needed, all unrecognized packets will arrive here.
	/// </summary>

	protected virtual void OnPacket (int packetID, BinaryReader reader)
	{
		Error("Unrecognized packet with ID of " + packetID);
	}
}
}