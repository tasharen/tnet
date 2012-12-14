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

	protected List<ServerPlayer> mPlayers = new List<ServerPlayer>();

	/// <summary>
	/// Dictionary list of players for easy access by ID.
	/// </summary>

	protected Dictionary<int, ServerPlayer> mDictionary = new Dictionary<int, ServerPlayer>();

	/// <summary>
	/// List of all the active channels.
	/// </summary>

	protected List<Channel> mChannels = new List<Channel>();

	/// <summary>
	/// Random number generator.
	/// </summary>

	protected Random mRandom = new Random();

	public class FileEntry
	{
		public string fileName;
		public byte[] data;
	};

	List<FileEntry> savedFiles = new List<FileEntry>();

	Buffer mBuffer;
	TcpListener mListener;
	Thread mThread;
	string mLocalAddress;
	int mListenerPort = 0;
	long mTime = 0;

	/// <summary>
	/// Whether the server is currently actively serving players.
	/// </summary>

	public bool isActive { get { return mThread != null; } }

	/// <summary>
	/// Whether the server is listening for incoming connections.
	/// </summary>

	public bool isListening { get { return (mListener != null); } }

	/// <summary>
	/// Port used for listening to incoming connections. Set when the server is started.
	/// </summary>

	public int listeningPort { get { return (mListener != null) ? mListenerPort : 0; } }

	/// <summary>
	/// How many players are currently connected to the server.
	/// </summary>

	public int playerCount { get { return isActive ? mPlayers.size : 0; } }

	/// <summary>
	/// Server's local address on the network. For example: 192.168.1.10
	/// </summary>

	public string localAddress
	{
		get
		{
			if (mLocalAddress == null)
			{
				IPAddress[] ips = Dns.GetHostAddresses(Dns.GetHostName());
				mLocalAddress = ips[0].ToString() + ":" + mListenerPort;
			}
			return mLocalAddress;
		}
	}

	/// <summary>
	/// Start listening to incoming connections on the specified port.
	/// </summary>

	public bool Start (int listenPort)
	{
		Stop();

		try
		{
			mListenerPort = listenPort;
			mListener = new TcpListener(IPAddress.Any, listenPort);
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
		// Stop listening to incoming connections
		MakePrivate();

		// Stop the worker thread
		if (mThread != null)
		{
			mThread.Abort();
			mThread = null;
		}

		// Remove all connected players
		for (int i = mPlayers.size; i > 0; ) RemovePlayer(mPlayers[--i]);
	}

	/// <summary>
	/// Stop listening to incoming connections but keep the server running.
	/// </summary>

	public void MakePrivate ()
	{
		if (mListener != null)
		{
			mListener.Stop();
			mListener = null;
		}
	}

	/// <summary>
	/// Thread that will be processing incoming data.
	/// </summary>

	void ThreadFunction ()
	{
		for (; ; )
		{
			// Add all pending connections
			while (mListener != null && mListener.Pending())
			{
				ServerPlayer p = AddPlayer(mListener.AcceptSocket());
				Console.WriteLine(p.address + " has connected");
			}

			bool received = false;
			mTime = DateTime.Now.Ticks / 10000;

			for (int i = 0; i < mPlayers.size; )
			{
				ServerPlayer player = mPlayers[i];

				// Process up to 100 packets at a time
				for (int b = 0; b < 100; ++b)
				{
					if (ReceivePacket(player, mTime)) received = true;
					else break;
				}

				// Time out -- disconnect this player
				if (player.stage == TcpProtocol.Stage.Connected)
				{
					// Up to 10 seconds can go without a single packet before the player is removed
					if (player.timestamp + 10000 < mTime)
					{
						Console.WriteLine(player.address + " has timed out");
						RemovePlayer(player);
						continue;
					}
				}
				else if (player.timestamp + 2000 < mTime)
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
#if UNITY_EDITOR
		if (p != null) UnityEngine.Debug.LogError(error + " (" + p.address + ")");
		else UnityEngine.Debug.LogError(error);
#else
		if (p != null) Console.WriteLine(p.address + " ERROR: " + error);
		else Console.WriteLine("ERROR: " + error);
#endif
	}

	/// <summary>
	/// Add a new player entry.
	/// </summary>

	protected ServerPlayer AddPlayer (Socket socket)
	{
		ServerPlayer player = new ServerPlayer();
		player.StartReceiving(socket);
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

#if !UNITY_WEBPLAYER
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
#if !UNITY_WEBPLAYER
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
#if !UNITY_WEBPLAYER
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
#if !UNITY_WEBPLAYER
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
#if !UNITY_WEBPLAYER
		if (mListener == null) return;
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
#if !UNITY_WEBPLAYER
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
		}
#endif
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
			if (player.stage == TcpProtocol.Stage.Connected && player != exclude) player.SendPacket(mBuffer);
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
				if (player.stage == TcpProtocol.Stage.Connected) player.SendPacket(mBuffer);
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
			if (player.stage == TcpProtocol.Stage.Connected) player.SendPacket(buffer);
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
		if (player.stage == TcpProtocol.Stage.Verifying)
		{
			if (request == Packet.RequestID)
			{
				int clientVersion = reader.ReadInt32();
				player.name = reader.ReadString();
				
				// Version matches? Connection is now verified.
				if (clientVersion == ServerPlayer.version)
				{
					player.id = Interlocked.Increment(ref mPlayerCounter);
					player.stage = TcpProtocol.Stage.Connected;
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
			case Packet.RequestPing:
			{
				// Respond with a ping back
				BeginSend(Packet.ResponsePing);
				EndSend(player);
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
					channelID = mRandom.Next(100000000);

					for (int i = 0; i < 1000; ++i)
					{
						if (!ChannelExists(channelID)) break;
						channelID = mRandom.Next(100000000);
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
			case Packet.RequestNoDelay:
			{
				player.noDelay = reader.ReadBoolean();
				break;
			}
			case Packet.RequestChannelList:
			{
				BinaryWriter writer = BeginSend(Packet.ResponseChannelList);

				writer.Write(mChannels.size);

				for (int i = 0; i < mChannels.size; ++i)
				{
					Channel ch = mChannels[i];
					writer.Write(ch.id);
					writer.Write(ch.players.size);
					writer.Write(!string.IsNullOrEmpty(ch.password));
					writer.Write(ch.persistent);
					writer.Write(ch.level);
				}
				EndSend(player);
				break;
			}
			case Packet.ForwardToPlayer:
			{
				// Forward this packet to the specified player
				ServerPlayer target = GetPlayer(reader.ReadInt32());

				if (target != null && target.isConnected)
				{
					// Reset the position back to the beginning (4 bytes for size, 1 byte for ID)
					buffer.position = buffer.position - 5;
					target.SendPacket(buffer);
				}
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
				else if ((int)request > (int)Packet.ForwardToPlayerBuffered)
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
				uint target = reader.ReadUInt32();
				string funcName = ((target & 0xFF) == 0) ? reader.ReadString() : null;
				buffer.position = origin;
				player.channel.CreateRFC(target, funcName, buffer);

				// Forward the packet to the target player
				if (targetPlayer != null && targetPlayer.isConnected) targetPlayer.SendPacket(buffer);
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
					uint target = reader.ReadUInt32();
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
				ushort objectIndex = reader.ReadUInt16();

				// Dynamically created Network Object IDs should always start out being negative
				uint uniqueID = 0;

				if (reader.ReadByte() != 0)
				{
					uniqueID = --player.channel.objectCounter;

					// 24 bit precision
					if (uniqueID < 32767)
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
				uint uniqueID = reader.ReadUInt32();

				// If this object has already been destroyed, ignore this packet
				if (player.channel.destroyed.Contains(uniqueID)) break;
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
				if (!wasCreated)
					player.channel.destroyed.Add(uniqueID);

				// Remove all RFCs associated with this object
				player.channel.DeleteObjectRFCs(uniqueID);

				// Inform all players in the channel that the object should be destroyed
				BinaryWriter writer = BeginSend(Packet.ResponseDestroy);
				writer.Write((ushort)1);
				writer.Write(uniqueID);
				EndSend(player.channel, null);
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
				uint id = reader.ReadUInt32();
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