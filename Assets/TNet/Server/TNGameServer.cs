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
/// Game server logic. Handles new connections, RFCs, and pretty much everything else.
/// </summary>

public class GameServer
{
	static int mPlayerCounter = 0;

	/// <summary>
	/// You will want to make this a unique value.
	/// </summary>

	public const ushort gameID = 1;

	/// <summary>
	/// Give your server a name.
	/// </summary>

	public string name = "Game Server";

	/// <summary>
	/// List of players in a consecutive order for each looping.
	/// </summary>

	List<TcpPlayer> mPlayers = new List<TcpPlayer>();

	/// <summary>
	/// Dictionary list of players for easy access by ID.
	/// </summary>

	Dictionary<int, TcpPlayer> mDictionaryID = new Dictionary<int, TcpPlayer>();

	/// <summary>
	/// Dictionary list of players for easy access by ID.
	/// </summary>

	Dictionary<IPEndPoint, TcpPlayer> mDictionaryEP = new Dictionary<IPEndPoint, TcpPlayer>();

	/// <summary>
	/// List of all the active channels.
	/// </summary>

	List<TcpChannel> mChannels = new List<TcpChannel>();

	/// <summary>
	/// Random number generator.
	/// </summary>

	Random mRandom = new Random();

	Buffer mBuffer;
	TcpListener mListener;
	Thread mThread;
	string mLocalAddress;
	int mListenerPort = 0;
	long mTime = 0;
	UdpProtocol mUdp = new UdpProtocol();

	/// <summary>
	/// You can save files on the server, such as player inventory, Fog of War map updates, player avatars, etc.
	/// </summary>

	struct FileEntry
	{
		public string fileName;
		public byte[] data;
	}

	List<FileEntry> mSavedFiles = new List<FileEntry>();

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

	public int tcpPort { get { return (mListener != null) ? mListenerPort : 0; } }

	/// <summary>
	/// Listening port for UDP packets.
	/// </summary>

	public int udpPort { get { return mUdp.listeningPort; } }

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

	public bool Start (int tcpPort) { return Start(tcpPort, 0); }

	/// <summary>
	/// Start listening to incoming connections on the specified port.
	/// </summary>

	public bool Start (int tcpPort, int udpPort)
	{
		Stop();

		try
		{
			mListenerPort = tcpPort;
			mListener = new TcpListener(IPAddress.Any, tcpPort);
			mListener.Start(50);
			//mListener.BeginAcceptSocket(OnAccept, null);
		}
		catch (System.Exception ex)
		{
			Error(null, ex.Message);
			return false;
		}

		if (udpPort != 0 && !mUdp.Start(udpPort))
		{
			Error(null, "Unable to listen to UDP port " + udpPort);
			Stop();
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

		// Stop listening
		if (mListener != null)
		{
			mListener.Stop();
			mListener = null;
		}
		mUdp.Stop();

		// Remove all connected players and clear the list of channels
		for (int i = mPlayers.size; i > 0; ) RemovePlayer(mPlayers[--i]);
		mChannels.Clear();
	}

	/// <summary>
	/// Stop listening to incoming connections but keep the server running.
	/// </summary>

	public void MakePrivate () { mListenerPort = 0; }

	/// <summary>
	/// Thread that will be processing incoming data.
	/// </summary>

	void ThreadFunction ()
	{
		for (; ; )
		{
			// Stop the listener if the port is 0 (MakePrivate() was called)
			if (mListenerPort == 0)
			{
				if (mListener != null)
				{
					mListener.Stop();
					mListener = null;
				}
			}
			else
			{
				// Add all pending connections
				while (mListener != null && mListener.Pending())
				{
#if STANDALONE
					TcpPlayer p = AddPlayer(mListener.AcceptSocket());
					Console.WriteLine(p.address + " has connected");
#else
					AddPlayer(mListener.AcceptSocket());
#endif
				}
			}

			bool received = false;
			mTime = DateTime.Now.Ticks / 10000;
			Buffer buffer;
			IPEndPoint ip;

			// Process datagrams first
			while (mUdp.ReceivePacket(out buffer, out ip))
			{
				if (buffer.size > 0)
				{
					TcpPlayer player = GetPlayer(ip);

					if (player != null)
					{
						try
						{
							if (ProcessPlayerPacket(buffer, player, false))
								received = true;
						}
						catch (System.Exception) { RemovePlayer(player); }
					}
				}
				buffer.Recycle();
			}

			// Process player connections next
			for (int i = 0; i < mPlayers.size; )
			{
				TcpPlayer player = mPlayers[i];

				// Process up to 100 packets at a time
				for (int b = 0; b < 100 && player.ReceivePacket(out buffer); ++b)
				{
					if (buffer.size > 0)
					{
						try
						{
							if (ProcessPlayerPacket(buffer, player, true))
								received = true;
						}
						catch (System.Exception) { RemovePlayer(player); }
					}
					buffer.Recycle();
				}

				// Time out -- disconnect this player
				if (player.stage == TcpProtocol.Stage.Connected)
				{
					// Up to 10 seconds can go without a single packet before the player is removed
					if (player.timestamp + 10000 < mTime)
					{
#if STANDALONE
						Console.WriteLine(player.address + " has timed out");
#endif
						RemovePlayer(player);
						continue;
					}
				}
				else if (player.timestamp + 2000 < mTime)
				{
#if STANDALONE
					Console.WriteLine(player.address + " has timed out");
#endif
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

	void Error (TcpPlayer p, string error)
	{
#if UNITY_EDITOR
		if (p != null) UnityEngine.Debug.LogError(error + " (" + p.address + ")");
		else UnityEngine.Debug.LogError(error);
#elif STANDALONE
		if (p != null) Console.WriteLine(p.address + " ERROR: " + error);
		else Console.WriteLine("ERROR: " + error);
#endif
	}

	/// <summary>
	/// Add a new player entry.
	/// </summary>

	TcpPlayer AddPlayer (Socket socket)
	{
		TcpPlayer player = new TcpPlayer();
		player.StartReceiving(socket);
		mPlayers.Add(player);
		return player;
	}

	/// <summary>
	/// Remove the specified player.
	/// </summary>

	void RemovePlayer (TcpPlayer p)
	{
		if (p != null)
		{
			SendLeaveChannel(p, false);
#if STANDALONE
			Console.WriteLine(p.address + " has disconnected");
#endif
			p.Release();
			mPlayers.Remove(p);

			if (p.id != 0)
			{
				mDictionaryID.Remove(p.id);
				p.id = 0;
			}

			if (p.udpEndPoint != null)
			{
				mDictionaryEP.Remove(p.udpEndPoint);
				p.udpEndPoint = null;
			}
		}
	}

	/// <summary>
	/// Retrieve a player by their ID.
	/// </summary>

	TcpPlayer GetPlayer (int id)
	{
		TcpPlayer p = null;
		mDictionaryID.TryGetValue(id, out p);
		return p;
	}

	/// <summary>
	/// Retrieve a player by their UDP end point.
	/// </summary>

	TcpPlayer GetPlayer (IPEndPoint ip)
	{
		TcpPlayer p = null;
		mDictionaryEP.TryGetValue(ip, out p);
		return p;
	}

	/// <summary>
	/// Change the player's UDP end point and update the local dictionary.
	/// </summary>

	void SetPlayerUdpEndPoint (TcpPlayer player, IPEndPoint udp)
	{
		if (player.udpEndPoint != null) mDictionaryEP.Remove(player.udpEndPoint);
		player.udpEndPoint = udp;
		if (udp != null) mDictionaryEP[udp] = player;
	}

	/// <summary>
	/// Create a new channel (or return an existing one).
	/// </summary>

	TcpChannel CreateChannel (int channelID, out bool isNew)
	{
		TcpChannel channel;

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

		channel = new TcpChannel();
		channel.id = channelID;
		mChannels.Add(channel);
		isNew = true;
		return channel;
	}

	/// <summary>
	/// Check to see if the specified channel exists.
	/// </summary>

	bool ChannelExists (int id)
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

		for (int i = 0; i < mSavedFiles.size; ++i)
		{
			FileEntry fi = mSavedFiles[i];

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
			mSavedFiles.Add(fi);
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
		for (int i = 0; i < mSavedFiles.size; ++i)
		{
			FileEntry fi = mSavedFiles[i];
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
					mSavedFiles.Add(fi);
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
		for (int i = 0; i < mSavedFiles.size; ++i)
		{
			FileEntry fi = mSavedFiles[i];

			if (fi.fileName == fileName)
			{
				mSavedFiles.RemoveAt(i);
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
#if !UNITY_WEBPLAYER && !UNITY_FLASH
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
			TcpChannel ch = mChannels[i];
			
			if (!ch.closed && ch.persistent && ch.hasData)
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
		stream.Close();
#endif
	}

	/// <summary>
	/// Load a previously saved server from the specified file.
	/// </summary>

	public bool LoadFrom (string fileName)
	{
#if UNITY_WEBPLAYER || UNITY_FLASH
		// There is no file access in the web player.
		return false;
#else
		fileName = CleanupFilename(fileName);
		if (!File.Exists(fileName)) return false;

		FileStream stream = null;

		try
		{
			stream = new FileStream(fileName, FileMode.Open);
			BinaryReader reader = new BinaryReader(stream);

			int channels = reader.ReadInt32();

			for (int i = 0; i < channels; ++i)
			{
				int chID = reader.ReadInt32();
				bool isNew;
				TcpChannel ch = CreateChannel(chID, out isNew);
				if (isNew) ch.LoadFrom(reader);
			}

			stream.Close();
		}
		catch (System.Exception ex)
		{
			Error(null, "Loading from " + fileName + ": " + ex.Message);
			if (stream != null) stream.Close();
			return false;
		}
		return true;
#endif
	}

	/// <summary>
	/// Start the sending process.
	/// </summary>

	BinaryWriter BeginSend (Packet type)
	{
		mBuffer = Buffer.Create();
		BinaryWriter writer = mBuffer.BeginTcpPacket(type);
		return writer;
	}

	/// <summary>
	/// Send the outgoing buffer to the specified remote destination.
	/// </summary>

	void EndSend (IPEndPoint ip)
	{
		mBuffer.EndTcpPacket();
		mUdp.Send(mBuffer, ip);
		mBuffer.Recycle();
		mBuffer = null;
	}

	/// <summary>
	/// Send the outgoing buffer to the specified player.
	/// </summary>

	void EndSend (bool reliable, TcpPlayer player)
	{
		mBuffer.EndTcpPacket();
		if (mBuffer.size > 1024) reliable = true;

		if (reliable || player.udpEndPoint == null)
		{
			player.SendTcpPacket(mBuffer);
		}
		else mUdp.Send(mBuffer, player.udpEndPoint);
		
		mBuffer.Recycle();
		mBuffer = null;
	}

	/// <summary>
	/// Send the outgoing buffer to all players in the specified channel.
	/// </summary>

	void EndSend (bool reliable, TcpChannel channel, TcpPlayer exclude)
	{
		mBuffer.EndTcpPacket();
		if (mBuffer.size > 1024) reliable = true;

		for (int i = 0; i < channel.players.size; ++i)
		{
			TcpPlayer player = channel.players[i];
			
			if (player.stage == TcpProtocol.Stage.Connected && player != exclude)
			{
				if (reliable || player.udpEndPoint == null)
				{
					player.SendTcpPacket(mBuffer);
				}
				else mUdp.Send(mBuffer, player.udpEndPoint);
			}
		}

		mBuffer.Recycle();
		mBuffer = null;
	}

	/// <summary>
	/// Send the outgoing buffer to all connected players.
	/// </summary>

	void EndSend (bool reliable)
	{
		mBuffer.EndTcpPacket();
		if (mBuffer.size > 1024) reliable = true;

		for (int i = 0; i < mChannels.size; ++i)
		{
			TcpChannel channel = mChannels[i];

			for (int b = 0; b < channel.players.size; ++b)
			{
				TcpPlayer player = channel.players[b];
				
				if (player.stage == TcpProtocol.Stage.Connected)
				{
					if (reliable || player.udpEndPoint == null)
					{
						player.SendTcpPacket(mBuffer);
					}
					else mUdp.Send(mBuffer, player.udpEndPoint);
				}
			}
		}
		mBuffer.Recycle();
		mBuffer = null;
	}

	/// <summary>
	/// Send the outgoing buffer to all players in the specified channel.
	/// </summary>

	void SendToChannel (bool reliable, TcpChannel channel, Buffer buffer)
	{
		mBuffer.MarkAsUsed();
		if (mBuffer.size > 1024) reliable = true;

		for (int i = 0; i < channel.players.size; ++i)
		{
			TcpPlayer player = channel.players[i];
			
			if (player.stage == TcpProtocol.Stage.Connected)
			{
				if (reliable || player.udpEndPoint == null)
				{
					player.SendTcpPacket(mBuffer);
				}
				else mUdp.Send(mBuffer, player.udpEndPoint);
			}
		}
		mBuffer.Recycle();
	}

	/// <summary>
	/// Have the specified player assume control of the channel.
	/// </summary>

	void SendSetHost (TcpPlayer player)
	{
		if (player.channel != null && player.channel.host != player)
		{
			player.channel.host = player;
			BinaryWriter writer = BeginSend(Packet.ResponseSetHost);
			writer.Write(player.id);
			EndSend(true, player.channel, null);
		}
	}

	/// <summary>
	/// Leave the channel the player is in.
	/// </summary>

	void SendLeaveChannel (TcpPlayer player, bool notify)
	{
		if (player.channel != null)
		{
			// Remove this player from the channel
			TcpChannel ch = player.channel;
			player.channel.RemovePlayer(player);
			player.channel = null;

			// Are there other players left?
			if (ch.players.size > 0)
			{
				// If this player was the host, choose a new host
				if (ch.host == null) SendSetHost(ch.players[0]);

				// Inform everyone of this player leaving the channel
				BinaryWriter writer = BeginSend(Packet.ResponsePlayerLeft);
				writer.Write(player.id);
				EndSend(true, ch, null);
			}
			else if (!ch.persistent)
			{
				// No other players left -- delete this channel
				mChannels.Remove(ch);
			}

			// Notify the player that they have left the channel
			if (notify && player.isConnected)
			{
				BeginSend(Packet.ResponseLeaveChannel);
				EndSend(true, player);
			}
		}
	}

	/// <summary>
	/// Join the specified channel.
	/// </summary>

	void SendJoinChannel (TcpPlayer player, TcpChannel channel)
	{
		if (player.channel == null || player.channel != channel)
		{
			// Set the player's channel
			player.channel = channel;

			// Everything else gets sent to the player, so it's faster to do it all at once
			player.FinishJoiningChannel();

			// Inform the channel that a new player is joining
			BinaryWriter writer = BeginSend(Packet.ResponsePlayerJoined);
			{
				writer.Write(player.id);
				writer.Write(string.IsNullOrEmpty(player.name) ? "Guest" : player.name);
			}
			EndSend(true, channel, null);

			// Add this player to the channel now that the joining process is complete
			channel.players.Add(player);
		}
	}

	/// <summary>
	/// Receive and process a single incoming packet.
	/// Returns 'true' if a packet was received, 'false' otherwise.
	/// </summary>

	bool ProcessPlayerPacket (Buffer buffer, TcpPlayer player, bool reliable)
	{
		BinaryReader reader = buffer.BeginReading();
		Packet request = (Packet)reader.ReadByte();

//#if UNITY_EDITOR // DEBUG
//		if (request != Packet.RequestPing) UnityEngine.Debug.Log("Server: " + request + " " + buffer.position + " " + buffer.size);
//#else
//		if (request != Packet.RequestPing) Console.WriteLine("Server: " + request + " " + buffer.position + " " + buffer.size);
//#endif

		// If the player has not yet been verified, the first packet must be an ID request
		if (player.stage == TcpProtocol.Stage.Verifying)
		{
			if (request == Packet.RequestID)
			{
				int clientVersion = reader.ReadInt32();
				player.name = reader.ReadString();
				
				// Version matches? Connection is now verified.
				if (clientVersion == TcpPlayer.version)
				{
					player.id = Interlocked.Increment(ref mPlayerCounter);
					player.stage = TcpProtocol.Stage.Connected;
					mDictionaryID.Add(player.id, player);
				}

				// Send the player their ID
				BinaryWriter writer = BeginSend(Packet.ResponseID);
				writer.Write(TcpPlayer.version);
				writer.Write(player.id);
				EndSend(true, player);

				// If the version matches, move on to the next packet
				if (clientVersion == TcpPlayer.version) return true;
			}
#if STANDALONE
			Console.WriteLine(player.address + " has failed the verification step");
#endif
			RemovePlayer(player);
			return false;
		}

		switch (request)
		{
			case Packet.Empty:
			{
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
			case Packet.RequestPing:
			{
				// Respond with a ping back
				BeginSend(Packet.ResponsePing);
				EndSend(true, player);
				break;
			}
			case Packet.RequestSetUDP:
			{
				int port = reader.ReadUInt16();

				if (port != 0)
				{
					IPAddress ip = new IPAddress(player.tcpEndPoint.Address.GetAddressBytes());
					SetPlayerUdpEndPoint(player, new IPEndPoint(ip, port));
				}
				else SetPlayerUdpEndPoint(player, null);

				// Let the player know if we are hosting an active UDP connection
				ushort udp = mUdp.isActive ? (ushort)mUdp.listeningPort : (ushort)0;
				BeginSend(Packet.ResponseSetUDP).Write(udp);
				EndSend(true, player);

				// Send an empty packet to the target player to open up UDP for communication
				if (player.udpEndPoint != null) mUdp.SendEmptyPacket(player.udpEndPoint);
				break;
			}
			case Packet.RequestJoinChannel:
			{
				// Join the specified channel
				int channelID = reader.ReadInt32();
				string pass = reader.ReadString();
				string levelName = reader.ReadString();
				bool persist = reader.ReadBoolean();
				ushort playerLimit = reader.ReadUInt16();

				// Join a random existing channel
				if (channelID == -2)
				{
					channelID = -1;

					for (int i = 0; i < mChannels.size; ++i)
					{
						TcpChannel ch = mChannels[i];
						
						if (ch.isOpen)
						{
							channelID = ch.id;
							break;
						}
					}
				}

				// Join a random new channel
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
					TcpChannel channel = CreateChannel(channelID, out isNew);

					if (channel == null || !channel.isOpen)
					{
						BinaryWriter writer = BeginSend(Packet.ResponseJoinChannel);
						writer.Write(false);
						writer.Write("The requested channel is closed.");
						EndSend(true, player);
					}
					else if (isNew)
					{
						channel.password = pass;
						channel.persistent = persist;
						channel.level = levelName;
						channel.playerLimit = playerLimit;

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
						EndSend(true, player);
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
				EndSend(true, player.channel, null);
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
				EndSend(true, player);
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
					TcpChannel ch = mChannels[i];
					writer.Write(ch.id);
					writer.Write(ch.players.size);
					writer.Write(!string.IsNullOrEmpty(ch.password));
					writer.Write(ch.persistent);
					writer.Write(ch.level);
				}
				EndSend(true, player);
				break;
			}
			case Packet.ForwardToPlayer:
			{
				// Forward this packet to the specified player
				TcpPlayer target = GetPlayer(reader.ReadInt32());

				if (target != null && target.isConnected)
				{
					// Reset the position back to the beginning (4 bytes for size, 1 byte for ID, 4 bytes for player)
					buffer.position = buffer.position - 9;
					target.SendTcpPacket(buffer);
				}
				break;
			}
			default:
			{
				if (player.channel != null && (int)request <= (int)Packet.ForwardToPlayerBuffered)
				{
					// Other packets can only be processed while in a channel
					if ((int)request >= (int)Packet.ForwardToAll)
					{
						ProcessForwardPacket(player, buffer, reader, request, reliable);
					}
					else
					{
						ProcessChannelPacket(player, buffer, reader, request);
					}
				}
				break;
			}
		}
		return true;
	}

	/// <summary>
	/// Process a packet that's meant to be forwarded.
	/// </summary>

	void ProcessForwardPacket (TcpPlayer player, Buffer buffer, BinaryReader reader, Packet request, bool reliable)
	{
		// We can't send unreliable packets if UDP is not active
		if (!mUdp.isActive || buffer.size > 1024) reliable = true;

		switch (request)
		{
			case Packet.ForwardToHost:
			{
				// Reset the position back to the beginning (4 bytes for size, 1 byte for ID)
				buffer.position = buffer.position - 5;

				// Forward the packet to the channel's host
				if (reliable || player.channel.host.udpEndPoint == null)
				{
					player.channel.host.SendTcpPacket(buffer);
				}
				else mUdp.Send(buffer, player.channel.host.udpEndPoint);
				break;
			}
			case Packet.ForwardToPlayerBuffered:
			{
				// 4 bytes for size, 1 byte for ID
				int origin = buffer.position - 5;

				// Figure out who the intended recipient is
				TcpPlayer targetPlayer = GetPlayer(reader.ReadInt32());

				// Save this function call
				uint target = reader.ReadUInt32();
				string funcName = ((target & 0xFF) == 0) ? reader.ReadString() : null;
				buffer.position = origin;
				player.channel.CreateRFC(target, funcName, buffer);

				// Forward the packet to the target player
				if (targetPlayer != null && targetPlayer.isConnected)
				{
					if (reliable || targetPlayer.udpEndPoint == null)
					{
						targetPlayer.SendTcpPacket(buffer);
					}
					else mUdp.Send(buffer, targetPlayer.udpEndPoint);
				}
				break;
			}
			default:
			{
				// We want to exclude the player if the request was to forward to others
				TcpPlayer exclude = (
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
					TcpPlayer tp = player.channel.players[i];
					
					if (tp != exclude)
					{
						if (reliable || tp.udpEndPoint == null)
						{
							tp.SendTcpPacket(buffer);
						}
						else mUdp.Send(buffer, tp.udpEndPoint);
					}
				}
				break;
			}
		}
	}

	/// <summary>
	/// Process a packet from the player.
	/// </summary>

	void ProcessChannelPacket (TcpPlayer player, Buffer buffer, BinaryReader reader, Packet request)
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
					TcpChannel.CreatedObject obj = new TcpChannel.CreatedObject();
					obj.playerID = player.id;
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
				writer.Write(player.id);
				writer.Write(objectIndex);
				writer.Write(uniqueID);
				if (buffer.size > 0) writer.Write(buffer.buffer, buffer.position, buffer.size);
				EndSend(true, player.channel, null);
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
					TcpChannel.CreatedObject obj = player.channel.created[i];
					
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
				EndSend(true, player.channel, null);
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
					EndSend(true, player.channel, null);
				}
				break;
			}
			case Packet.RequestSetHost:
			{
				// Transfer the host state from one player to another
				if (player.channel.host == player)
				{
					TcpPlayer newHost = GetPlayer(reader.ReadInt32());
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
			case Packet.RequestSetPlayerLimit:
			{
				player.channel.playerLimit = reader.ReadUInt16();
				break;
			}
			case Packet.RequestRemoveRFC:
			{
				uint id = reader.ReadUInt32();
				string funcName = ((id & 0xFF) == 0) ? reader.ReadString() : null;
				player.channel.DeleteRFC(id, funcName);
				break;
			}
		}
	}
}
}
