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
	static int mPlayerCounter = 0;

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

	Buffer mBuffer;
	TcpListener mListener;
	Thread mThread;

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
			//mListener.BeginAcceptSocket(OnAccept, null);
		}
		catch (System.Exception ex)
		{
			Error(null, ex.Message);
			return false;
		}

		mThread = new Thread(ThreadFunction);
		mThread.Start();
		return true;
	}

	/// <summary>
	/// Accept socket callback.
	/// </summary>

	//void OnAccept (IAsyncResult result) { AddPlayer(mListener.EndAcceptSocket(result)); }

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
			long time = DateTime.Now.Ticks / 10000;

			for (int i = 0; i < mPlayers.size; )
			{
				Player player = mPlayers[i];

				// Process up to 100 packets at a time
				for (int b = 0; b < 100; ++b)
				{
					if (ReceivePacket(player, time)) received = true;
					else break;
				}

				// Time out -- disconnect this player
				if (player.verified)
				{
					if (player.timestamp + 10000 < time)
					{
						Console.WriteLine(player.address + " has timed out");
						RemovePlayer(player);
						continue;
					}
				}
				else if (player.timestamp + 2000 < time)
				{
					Console.WriteLine(player.address + " has timed out");
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

	protected virtual void Error (Player p, string error)
	{
		if (p != null)
		{
			Console.WriteLine(p.address + " ERROR: " + error);
		}
		else
		{
			Console.WriteLine("ERROR: " + error);
		}
	}

	/// <summary>
	/// Add a new player entry.
	/// </summary>

	protected Player AddPlayer (Socket socket)
	{
		Player player = new Player();
		player.socket = socket;
		player.StartReceiving();
		mPlayers.Add(player);
		return player;
	}

	/// <summary>
	/// Remove the specified player.
	/// </summary>

	protected void RemovePlayer (Player p)
	{
		if (p != null)
		{
			Console.WriteLine(p.address + " has disconnected");
			p.Release();
			mPlayers.Remove(p);

			if (p.id != 0)
			{
				mDictionary.Remove(p.id);
				p.id = 0;
			}
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
			
			if (channel.id == channelID)
			{
				if (channel.closed) return null;
				return channel;
			}
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
		mBuffer = Buffer.Create();
		BinaryWriter writer = mBuffer.BeginPacket(type);
		//Console.WriteLine("Sending " + type);
		return writer;
	}

	/// <summary>
	/// Send the outgoing buffer to the specified player.
	/// </summary>

	protected void EndSend (Player player)
	{
		mBuffer.EndPacket();
		player.SendPacket(mBuffer);
		mBuffer.Recycle();
		mBuffer = null;
	}

	/// <summary>
	/// Send the outgoing buffer to all players in the specified channel.
	/// </summary>

	protected void EndSend (Channel channel, Player exclude)
	{
		mBuffer.EndPacket();

		for (int i = 0; i < channel.players.size; ++i)
		{
			Player player = channel.players[i];
			if (player.verified && player != exclude) player.SendPacket(mBuffer);
		}
		mBuffer.Recycle();
		mBuffer = null;
	}

	/// <summary>
	/// Send the outgoing buffer to all connected players.
	/// </summary>

	protected void EndSend ()
	{
		mBuffer.EndPacket();

		for (int i = 0; i < mChannels.size; ++i)
		{
			Channel channel = mChannels[i];

			for (int b = 0; b < channel.players.size; ++b)
			{
				Player player = channel.players[b];
				if (player.verified) player.SendPacket(mBuffer);
			}
		}
		mBuffer.Recycle();
		mBuffer = null;
	}

	/// <summary>
	/// Send the outgoing buffer to all players in the specified channel.
	/// </summary>

	protected void SendToChannel (Channel channel, Buffer buffer)
	{
		mBuffer.MarkAsUsed();

		for (int i = 0; i < channel.players.size; ++i)
		{
			Player player = channel.players[i];
			if (player.verified) player.SendPacket(buffer);
		}
		mBuffer.Recycle();
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
			EndSend(player.channel, null);
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
			player.channel.RemovePlayer(player);

			// Are there other players left?
			if (player.channel.players.size > 0)
			{
				// Inform everyone of this player leaving the channel
				BinaryWriter writer = BeginSend(Packet.ResponsePlayerLeft);
				writer.Write(player.id);
				EndSend(player.channel, null);

				// If this player was the host, choose a new host
				if (player.channel.host == null) SendSetHost(player.channel.players[0]);
			}
			else if (!player.channel.persistent)
			{
				// No other players left -- delete this channel
				mChannels.Remove(player.channel);
			}
			player.channel = null;

			// Notify the player that they have left the channel
			if (player.isConnected)
			{
				BeginSend(Packet.ResponseLeftChannel);
				EndSend(player);
			}
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

			// Step 1: Inform the channel that a new player is joining
			BinaryWriter writer = BeginSend(Packet.ResponsePlayerJoined);
			{
				writer.Write(player.id);
				writer.Write(string.IsNullOrEmpty(player.name) ? "<Guest>" : player.name);
			}
			EndSend(channel, null);

			// Add this player to the channel
			player.channel = channel;
			channel.players.Add(player);

			// Everything else gets sent to the player, so it's faster to do it all at once
			player.FinishJoiningChannel(channelID);
		}
	}

	/// <summary>
	/// Receive and process a single incoming packet.
	/// Returns 'true' if a packet was received, 'false' otherwise.
	/// </summary>

	bool ReceivePacket (Player player, long time)
	{
		Buffer buffer = player.ReceivePacket();
		if (buffer == null) return false;

		BinaryReader reader = buffer.BeginReading();
		int size = buffer.size;

		// First byte is always the packet's identifier
		Packet request = (Packet)reader.ReadByte();

		//Console.WriteLine("...packet: " + request + " (" + size + " bytes)");

		// If the player has not yet been verified, the first packet must be an ID request
		if (!player.verified)
		{
			if (request == Packet.RequestID)
			{
				int clientVersion = reader.ReadInt32();
				
				// Version matches? Connection is now verified.
				if (clientVersion == Player.version)
				{
					player.id = Interlocked.Increment(ref mPlayerCounter);
					player.verified = true;
					mDictionary.Add(player.id, player);
					buffer.Recycle();
				}

				// Send the player their ID
				BinaryWriter writer = BeginSend(Packet.ResponseID);
				writer.Write(Player.version);
				writer.Write(player.id);
				EndSend(player);

				// If the version matches, move on to the next packet
				if (clientVersion == Player.version) return true;
			}
			Console.WriteLine(player.address + " has failed the verification step");
			RemovePlayer(player);
			return false;
		}

		switch (request)
		{
			case Packet.RequestPing:
			{
				// Respond with a ping back
				BeginSend(Packet.ResponsePing);
				EndSend(player);
				break;
			}
			case Packet.ForwardToPlayer:
			{
				// Forward this packet to the specified player
				Player target = GetPlayer(reader.ReadInt32());

				if (target != null && target.socket.Connected)
				{
					buffer.position = 0;
					target.SendPacket(buffer);
				}
				break;
			}
			case Packet.RequestJoinChannel:
			{
				// Join the specified channel
				int channelID = reader.ReadInt32();
				string pass = reader.ReadString();
				bool persist = reader.ReadBoolean();

				if (channelID == -1)
				{
					channelID = mRandom.Range(1, 100000000);

					for (int i = 0; i < 1000; ++i)
					{
						if (!ChannelExists(channelID)) break;
						channelID = mRandom.Range(1, 100000000);
					}
				}

				if (player.channel == null || player.channel.id != channelID)
				{
					Channel channel = CreateChannel(channelID);

					if (channel == null)
					{
						BeginSend(Packet.ResponseJoinFailed).Write("The requested channel is closed.");
						EndSend(player);
					}
					else if (channel.players.size == 0)
					{
						channel.password = pass;
						channel.persistent = persist;
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
						BeginSend(Packet.ResponseJoinFailed).Write("Wrong password.");
						EndSend(player);
					}
				}
				break;
			}
			case Packet.RequestSetName:
			{
				// Change the player's name
				player.name = reader.ReadString();
				BinaryWriter writer = BeginSend(Packet.ResponseRenamePlayer);
				writer.Write(player.id);
				writer.Write(player.name);
				EndSend(player.channel, null);
				break;
			}
			default:
			{
				// Other packets can only be processed while in a channel
				if (player.channel != null)
				{
					ProcessChannelPacket(player, buffer, reader, request);
				}
				else
				{
					OnPacket(player, buffer, reader, (int)request);
				}
				break;
			}
		}
		// We're done with this packet
		buffer.Recycle();
		return true;
	}

	/// <summary>
	/// Process a packet from the player.
	/// </summary>

	void ProcessChannelPacket (Player player, Buffer buffer, BinaryReader reader, Packet request)
	{
		switch (request)
		{
			case Packet.ForwardToAll:
			{
				buffer.position = 0;

				// Forward the packet to everyone in the same channel
				for (int i = 0; i < player.channel.players.size; ++i)
				{
					Player tp = player.channel.players[i];
					tp.SendPacket(buffer);
				}
				break;
			}
			case Packet.ForwardToOthers:
			{
				buffer.position = 0;

				// Forward the packet to everyone except the sender
				for (int i = 0; i < player.channel.players.size; ++i)
				{
					Player tp = player.channel.players[i];
					if (tp != player) tp.SendPacket(buffer);
				}
				break;
			}
			case Packet.ForwardToAllBuffered:
			{
				int target = reader.ReadInt32();
				buffer.position = 0;
				player.channel.CreateRFC(target, buffer);

				// Forward the packet to everyone in the same channel
				for (int i = 0; i < player.channel.players.size; ++i)
				{
					Player tp = player.channel.players[i];
					if (player.socket.Connected) tp.SendPacket(buffer);
				}
				break;
			}
			case Packet.ForwardToOthersBuffered:
			{
				int target = reader.ReadInt32();
				buffer.position = 0;
				player.channel.CreateRFC(target, buffer);

				// Forward the packet to everyone except the sender
				for (int i = 0; i < player.channel.players.size; ++i)
				{
					Player tp = player.channel.players[i];
					if (tp != player && player.socket.Connected) tp.SendPacket(buffer);
				}
				break;
			}
			case Packet.ForwardToHost:
			{
				// Forward the packet to the channel's host
				buffer.position = 0;
				player.channel.host.SendPacket(buffer);
				break;
			}
			case Packet.RequestCreate:
			{
				// Create a new object
				short objectIndex = reader.ReadInt16();

				// Dynamically created Network Object IDs should always start out being negative
				int uniqueID = 0;

				if (reader.ReadByte() != 0)
				{
					uniqueID = --player.channel.objectCounter;

					// 24 bit precision
					if (uniqueID < -0xFFFFFF)
					{
						player.channel.objectCounter = 0xFFFFFF;
						uniqueID = 0xFFFFFF;
					}
				}

				// If a unique ID was requested then this call should be persistent
				if (uniqueID != 0)
				{
					Channel.CreatedObject obj = new Channel.CreatedObject();
					obj.objectID = objectIndex;
					obj.uniqueID = uniqueID;

					if (buffer.size > 0)
					{
						obj.buffer = buffer;
						buffer.MarkAsUsed();
					}
					player.channel.created.Add(obj);
				}

				// Inform the channel
				BinaryWriter writer = BeginSend(Packet.ResponseCreate);
				writer.Write(objectIndex);
				writer.Write(uniqueID);
				if (buffer.size > 0) writer.Write(buffer.buffer, buffer.position, buffer.size);
				EndSend(player.channel, null);
				break;
			}
			case Packet.RequestDestroy:
			{
				// Destroy the specified network object
				int uniqueID = reader.ReadInt32();

				if (!player.channel.destroyed.Contains(uniqueID))
				{
					bool wasCreated = false;

					// Determine if we created this object earlier
					for (int i = 0; i < player.channel.created.size; ++i)
					{
						Channel.CreatedObject obj = player.channel.created[i];
						
						if (obj.uniqueID == uniqueID)
						{
							// Remove it
							if (obj.buffer != null) obj.buffer.Recycle();
							player.channel.created.RemoveAt(i);
							wasCreated = true;
							break;
						}
					}

					// If the object was not created dynamically, we should remember it
					if (!wasCreated) player.channel.destroyed.Add(uniqueID);

					// Remove all RFCs associated with this object
					player.channel.DeleteObjectRFCs(uniqueID);

					// Inform all players in the channel that the object should be destroyed
					BinaryWriter writer = BeginSend(Packet.ResponseDestroy);
					writer.Write((short)1);
					writer.Write(uniqueID);
					EndSend(player.channel, null);
				}
				break;
			}
			case Packet.RequestSetHost:
			{
				// Transfer the host state from one player to another
				if (player.channel.host == player)
				{
					Player newHost = GetPlayer(reader.ReadInt32());
					if (newHost != null && newHost.channel == player.channel) SendSetHost(newHost);
				}
				break;
			}
			case Packet.RequestLeaveChannel:
			{
				SendLeaveChannel(player);
				break;
			}
			case Packet.RequestCloseChannel:
			{
				player.channel.persistent = false;
				player.channel.closed = true;
				break;
			}
			case Packet.RequestRemoveRFC:
			{
				player.channel.DeleteRFC(reader.ReadInt32());
				break;
			}
			case Packet.RequestSaveFile:
			{
				string fileName = reader.ReadString();
				int size = reader.ReadInt32();
				byte[] data = reader.ReadBytes(size);
				player.channel.SaveFile(fileName, data);
				break;
			}
			case Packet.RequestLoadFile:
			{
				string fn = reader.ReadString();
				byte[] data = player.channel.LoadFile(fn);

				BinaryWriter writer = BeginSend(Packet.ResponseLoadFile);
				writer.Write(fn);

				if (data != null)
				{
					writer.Write(data.Length);
					writer.Write(data);
				}
				else
				{
					writer.Write(0);
				}
				EndSend(player);
				break;
			}
			case Packet.RequestDeleteFile:
			{
				string fn = reader.ReadString();
				player.channel.DeleteFile(fn);
				break;
			}
			case Packet.Error:
			{
				Error(player, reader.ReadString());
				break;
			}
			case Packet.Disconnect:
			{
				if (player.channel != null) SendLeaveChannel(player);
				RemovePlayer(player);
				break;
			}
			default:
			{
				OnPacket(player, buffer, reader, (int)request);
				break;
			}
		}
	}

	/// <summary>
	/// If custom functionality is needed, all unrecognized packets will arrive here.
	/// </summary>

	protected virtual void OnPacket (Player player, Buffer buffer, BinaryReader reader, int packetID)
	{
		Error(player, "Unrecognized packet with ID of " + packetID);
	}
}
}