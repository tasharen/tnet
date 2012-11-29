//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

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

	protected BetterList<ServerPlayer> mPlayers = new BetterList<ServerPlayer>();

	/// <summary>
	/// Dictionary list of players for easy access by ID.
	/// </summary>

	protected Dictionary<int, ServerPlayer> mDictionary = new Dictionary<int, ServerPlayer>();

	/// <summary>
	/// List of all the active channels.
	/// </summary>

	protected BetterList<Channel> mChannels = new BetterList<Channel>();

	/// <summary>
	/// Random number generator.
	/// </summary>

	protected RandomGenerator mRandom = new RandomGenerator();

	public class FileEntry
	{
		public string fileName;
		public byte[] data;
	};

	BetterList<FileEntry> savedFiles = new BetterList<FileEntry>();

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
			mListener = new TcpListener(IPAddress.Any, port);
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
		for (int i = mPlayers.size; i > 0; ) RemovePlayer(mPlayers[--i]);
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
				ServerPlayer p = AddPlayer(mListener.AcceptSocket());
				Console.WriteLine(p.address + " has connected");
			}

			bool received = false;
			long time = DateTime.Now.Ticks / 10000;

			for (int i = 0; i < mPlayers.size; )
			{
				ServerPlayer player = mPlayers[i];

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

	protected virtual void Error (ServerPlayer p, string error)
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

	protected ServerPlayer AddPlayer (Socket socket)
	{
		ServerPlayer player = new ServerPlayer();
		player.socket = socket;
		player.StartReceiving();
		mPlayers.Add(player);
		return player;
	}

	/// <summary>
	/// Remove the specified player.
	/// </summary>

	protected void RemovePlayer (ServerPlayer p)
	{
		if (p != null)
		{
			SendLeaveChannel(p, false);

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

	protected ServerPlayer GetPlayer (int id)
	{
		ServerPlayer p = null;
		mDictionary.TryGetValue(id, out p);
		return p;
	}

	/// <summary>
	/// Create a new channel (or return an existing one).
	/// </summary>

	protected Channel CreateChannel (int channelID, out bool isNew)
	{
		Channel channel;

		for (int i = 0; i < mChannels.size; ++i)
		{
			channel = mChannels[i];
			
			if (channel.id == channelID)
			{
				isNew = false;
				if (channel.closed) return null;
				return channel;
			}
		}

		channel = new Channel();
		channel.id = channelID;
		mChannels.Add(channel);
		isNew = true;
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

#if !UNITY_WEB_PLAYER
	/// <summary>
	/// Clean up the filename, ensuring that there is no funny business going on.
	/// </summary>

	string CleanupFilename (string fn) { return Path.GetFileName(fn); }
#endif

	/// <summary>
	/// Save the specified file.
	/// </summary>

	public void SaveFile (string fileName, byte[] data)
	{
		bool exists = false;

		for (int i = 0; i < savedFiles.size; ++i)
		{
			FileEntry fi = savedFiles[i];

			if (fi.fileName == fileName)
			{
				fi.data = data;
				exists = true;
				break;
			}
		}

		if (!exists)
		{
			FileEntry fi = new FileEntry();
			fi.fileName = fileName;
			fi.data = data;
			savedFiles.Add(fi);
		}
#if !UNITY_WEB_PLAYER
		try
		{
			File.WriteAllBytes(CleanupFilename(fileName), data);
		}
		catch (System.Exception ex)
		{
			Error(null, fileName + ": " + ex.Message);
		}
#endif
	}

	/// <summary>
	/// Load the specified file.
	/// </summary>

	public byte[] LoadFile (string fileName)
	{
		for (int i = 0; i < savedFiles.size; ++i)
		{
			FileEntry fi = savedFiles[i];
			if (fi.fileName == fileName) return fi.data;
		}
#if !UNITY_WEB_PLAYER
		string fn = CleanupFilename(fileName);

		if (File.Exists(fn))
		{
			try
			{
				byte[] bytes = File.ReadAllBytes(fn);

				if (bytes != null)
				{
					FileEntry fi = new FileEntry();
					fi.fileName = fileName;
					fi.data = bytes;
					savedFiles.Add(fi);
					return bytes;
				}
			}
			catch (System.Exception ex)
			{
				Error(null, fileName + ": " + ex.Message);
			}
		}
#endif
		return null;
	}

	/// <summary>
	/// Delete the specified file.
	/// </summary>

	public void DeleteFile (string fileName)
	{
		for (int i = 0; i < savedFiles.size; ++i)
		{
			FileEntry fi = savedFiles[i];

			if (fi.fileName == fileName)
			{
				savedFiles.RemoveAt(i);
#if !UNITY_WEB_PLAYER
				File.Delete(CleanupFilename(fileName));
#endif
				break;
			}
		}
	}

	/// <summary>
	/// Save the server's current state into the specified file so it can be easily restored later.
	/// </summary>

	public void SaveTo (string fileName)
	{
#if !UNITY_WEB_PLAYER
		fileName = CleanupFilename(fileName);
		FileStream stream;

		try
		{
			stream = new FileStream(fileName, FileMode.Create);
		}
		catch (System.Exception ex)
		{
			Error(null, ex.Message);
			return;
		}
		BinaryWriter writer = new BinaryWriter(stream);
		writer.Write(0);
		int count = 0;

		for (int i = 0; i < mChannels.size; ++i)
		{
			Channel ch = mChannels[i];
			
			if (!ch.closed && ch.persistent)
			{
				writer.Write(ch.id);
				ch.SaveTo(writer);
				++count;
			}
		}

		if (count > 0)
		{
			stream.Seek(0, SeekOrigin.Begin);
			writer.Write(count);
		}

		stream.Flush();
		stream.Dispose();
#endif
	}

	/// <summary>
	/// Load a previously saved server from the specified file.
	/// </summary>

	public bool LoadFrom (string fileName)
	{
#if !UNITY_WEB_PLAYER
		fileName = CleanupFilename(fileName);
		if (!File.Exists(fileName)) return false;

		try
		{
			FileStream stream = new FileStream(fileName, FileMode.Open);
			BinaryReader reader = new BinaryReader(stream);

			int channels = reader.ReadInt32();

			for (int i = 0; i < channels; ++i)
			{
				int chID = reader.ReadInt32();
				bool isNew;
				Channel ch = CreateChannel(chID, out isNew);
				if (isNew) ch.LoadFrom(reader);
			}

			stream.Dispose();
			return true;
		}
		catch (System.Exception ex)
		{
			Error(null, ex.Message);
			return false;
		}
#endif
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

	protected void EndSend (ServerPlayer player)
	{
		mBuffer.EndPacket();
		player.SendPacket(mBuffer);
		mBuffer.Recycle();
		mBuffer = null;
	}

	/// <summary>
	/// Send the outgoing buffer to all players in the specified channel.
	/// </summary>

	protected void EndSend (Channel channel, ServerPlayer exclude)
	{
		mBuffer.EndPacket();

		for (int i = 0; i < channel.players.size; ++i)
		{
			ServerPlayer player = channel.players[i];
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
				ServerPlayer player = channel.players[b];
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
			ServerPlayer player = channel.players[i];
			if (player.verified) player.SendPacket(buffer);
		}
		mBuffer.Recycle();
	}

	/// <summary>
	/// Have the specified player assume control of the channel.
	/// </summary>

	protected void SendSetHost (ServerPlayer player)
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

	protected void SendLeaveChannel (ServerPlayer player, bool notify)
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
			if (notify && player.isConnected)
			{
				BeginSend(Packet.ResponseLeftChannel);
				EndSend(player);
			}
		}
	}

	/// <summary>
	/// Join the specified channel.
	/// </summary>

	protected void SendJoinChannel (ServerPlayer player, Channel channel)
	{
		if (player.channel == null || player.channel != channel)
		{
			player.channel = channel;

			// Step 1: Inform the channel that a new player is joining
			BinaryWriter writer = BeginSend(Packet.ResponsePlayerJoined);
			{
				writer.Write(player.id);
				writer.Write(string.IsNullOrEmpty(player.name) ? "Guest" : player.name);
			}
			EndSend(channel, null);

			// Add this player to the channel
			player.channel = channel;
			channel.players.Add(player);

			// Everything else gets sent to the player, so it's faster to do it all at once
			player.FinishJoiningChannel();
		}
	}

	/// <summary>
	/// Receive and process a single incoming packet.
	/// Returns 'true' if a packet was received, 'false' otherwise.
	/// </summary>

	bool ReceivePacket (ServerPlayer player, long time)
	{
		Buffer buffer = player.ReceivePacket();
		if (buffer == null) return false;

		// Begin the reading process
		BinaryReader reader = buffer.BeginReading();

		// First byte is always the packet's identifier
		Packet request = (Packet)reader.ReadByte();

		// If the player has not yet been verified, the first packet must be an ID request
		if (!player.verified)
		{
			if (request == Packet.RequestID)
			{
				int clientVersion = reader.ReadInt32();
				player.name = reader.ReadString();
				
				// Version matches? Connection is now verified.
				if (clientVersion == ServerPlayer.version)
				{
					player.id = Interlocked.Increment(ref mPlayerCounter);
					player.verified = true;
					mDictionary.Add(player.id, player);
					buffer.Recycle();
				}

				// Send the player their ID
				BinaryWriter writer = BeginSend(Packet.ResponseID);
				writer.Write(ServerPlayer.version);
				writer.Write(player.id);
				EndSend(player);

				// If the version matches, move on to the next packet
				if (clientVersion == ServerPlayer.version) return true;
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
				ServerPlayer target = GetPlayer(reader.ReadInt32());

				if (target != null && target.socket.Connected)
				{
					// Reset the position back to the beginning (4 bytes for size, 1 byte for ID)
					buffer.position = buffer.position - 5;
					target.SendPacket(buffer);
				}
				break;
			}
			case Packet.RequestJoinChannel:
			{
				// Join the specified channel
				int channelID = reader.ReadInt32();
				string pass = reader.ReadString();
				string levelName = reader.ReadString();
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
					bool isNew;
					Channel channel = CreateChannel(channelID, out isNew);

					if (channel == null)
					{
						BinaryWriter writer = BeginSend(Packet.ResponseJoinChannel);
						writer.Write(false);
						writer.Write("The requested channel is closed.");
						EndSend(player);
					}
					else if (isNew)
					{
						channel.password = pass;
						channel.persistent = persist;
						channel.level = levelName;

						SendLeaveChannel(player, false);
						SendJoinChannel(player, channel);
					}
					else if (string.IsNullOrEmpty(channel.password) || (channel.password == pass))
					{
						SendLeaveChannel(player, false);
						SendJoinChannel(player, channel);
					}
					else
					{
						BinaryWriter writer = BeginSend(Packet.ResponseJoinChannel);
						writer.Write(false);
						writer.Write("Wrong password.");
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
			case Packet.RequestSaveFile:
			{
				string fileName = reader.ReadString();
				byte[] data = reader.ReadBytes(reader.ReadInt32());
				SaveFile(fileName, data);
				break;
			}
			case Packet.RequestLoadFile:
			{
				string fn = reader.ReadString();
				byte[] data = LoadFile(fn);

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
				DeleteFile(reader.ReadString());
				break;
			}
			case Packet.Error:
			{
				Error(player, reader.ReadString());
				break;
			}
			case Packet.Disconnect:
			{
				RemovePlayer(player);
				break;
			}
			default:
			{
				// Other packets can only be processed while in a channel
				if (player.channel != null)
				{
					if ((int)request >= (int)Packet.ForwardToAll)
					{
						ProcessForwardPacket(player, buffer, reader, request);
					}
					else
					{
						ProcessChannelPacket(player, buffer, reader, request);
					}
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
	/// Process a packet that's meant to be forwarded.
	/// </summary>

	void ProcessForwardPacket (ServerPlayer player, Buffer buffer, BinaryReader reader, Packet request)
	{
		switch (request)
		{
			case Packet.ForwardToHost:
			{
				// Reset the position back to the beginning (4 bytes for size, 1 byte for ID)
				buffer.position = buffer.position - 5;

				// Forward the packet to the channel's host
				player.channel.host.SendPacket(buffer);
				break;
			}
			case Packet.ForwardToPlayerBuffered:
			{
				// 4 bytes for size, 1 byte for ID
				int origin = buffer.position - 5;

				// Figure out who the intended recipient is
				ServerPlayer targetPlayer = GetPlayer(reader.ReadInt32());

				// Save this function call
				int target = reader.ReadInt32();
				string funcName = ((target & 0xFF) == 0) ? reader.ReadString() : null;
				buffer.position = origin;
				player.channel.CreateRFC(target, funcName, buffer);

				// Forward the packet to the target player
				if (targetPlayer != null && targetPlayer.socket.Connected) targetPlayer.SendPacket(buffer);
				break;
			}
			default:
			{
				// We want to exclude the player if the request was to forward to others
				ServerPlayer exclude = (
					request == Packet.ForwardToOthers ||
					request == Packet.ForwardToOthersSaved) ? player : null;

				// 4 bytes for size, 1 byte for ID
				int origin = buffer.position - 5;

				// If the request should be saved, let's do so
				if (request == Packet.ForwardToAllSaved || request == Packet.ForwardToOthersSaved)
				{
					int target = reader.ReadInt32();
					string funcName = ((target & 0xFF) == 0) ? reader.ReadString() : null;
					buffer.position = origin;
					player.channel.CreateRFC(target, funcName, buffer);
				}
				else buffer.position = origin;

				// Forward the packet to everyone except the sender
				for (int i = 0; i < player.channel.players.size; ++i)
				{
					ServerPlayer tp = player.channel.players[i];
					if (tp != exclude) tp.SendPacket(buffer);
				}
				break;
			}
		}
	}

	/// <summary>
	/// Process a packet from the player.
	/// </summary>

	void ProcessChannelPacket (ServerPlayer player, Buffer buffer, BinaryReader reader, Packet request)
	{
		switch (request)
		{
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
			case Packet.RequestLoadLevel:
			{
				// Change the currently loaded level
				if (player.channel.host == player)
				{
					player.channel.Reset();
					player.channel.level = reader.ReadString();

					BinaryWriter writer = BeginSend(Packet.ResponseLoadLevel);
					writer.Write(string.IsNullOrEmpty(player.channel.level) ? "" : player.channel.level);
					EndSend(player.channel, null);
				}
				break;
			}
			case Packet.RequestSetHost:
			{
				// Transfer the host state from one player to another
				if (player.channel.host == player)
				{
					ServerPlayer newHost = GetPlayer(reader.ReadInt32());
					if (newHost != null && newHost.channel == player.channel) SendSetHost(newHost);
				}
				break;
			}
			case Packet.RequestLeaveChannel:
			{
				SendLeaveChannel(player, true);
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
				int id = reader.ReadInt32();
				string funcName = ((id & 0xFF) == 0) ? reader.ReadString() : null;
				player.channel.DeleteRFC(id, funcName);
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

	protected virtual void OnPacket (ServerPlayer player, Buffer buffer, BinaryReader reader, int packetID)
	{
		Error(player, "Unrecognized packet with ID of " + packetID);
	}
}
}