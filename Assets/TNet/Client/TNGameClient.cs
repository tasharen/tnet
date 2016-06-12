//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEngine;
#endif

namespace TNet
{
/// <summary>
/// Client-side logic.
/// </summary>

public class GameClient : TNEvents
{
	/// <summary>
	/// Custom packet listeners. You can set these to handle custom packets.
	/// </summary>

	public Dictionary<byte, OnPacket> packetHandlers = new Dictionary<byte, OnPacket>();
	public delegate void OnPacket (Packet response, BinaryReader reader, IPEndPoint source);

	/// <summary>
	/// Whether the game client should be actively processing messages or not.
	/// </summary>

	public bool isActive = true;

	// List of players in a dictionary format for quick lookup
	Dictionary<int, Player> mDictionary = new Dictionary<int, Player>();

	// TCP connection is the primary method of communication with the server.
	TcpProtocol mTcp = new TcpProtocol();

#if !UNITY_WEBPLAYER
	// UDP can be used for transmission of frequent packets, network broadcasts and NAT requests.
	// UDP is not available in the Unity web player because using UDP packets makes Unity request the
	// policy file every time the packet gets sent... which is obviously quite retarded.
	UdpProtocol mUdp = new UdpProtocol("Game Client");
	bool mUdpIsUsable = false;
#endif

	// Current time, time when the last ping was sent out, and time when connection was started
	long mTimeDifference = 0;
	long mMyTime = 0;
	long mPingTime = 0;

	// Last ping, and whether we can ping again
	int mPing = 0;
	bool mCanPing = false;

	// List of channels we're in
	TNet.List<Channel> mChannels = new TNet.List<Channel>();

	// Each GetFileList() call can specify its own callback
	Dictionary<string, OnGetFiles> mGetFiles = new Dictionary<string, OnGetFiles>();
	public delegate void OnGetFiles (string path, string[] files);

	// Each LoadFile() call can specify its own callback
	Dictionary<string, OnLoadFile> mLoadFiles = new Dictionary<string, OnLoadFile>();
	public delegate void OnLoadFile (string filename, byte[] data);

	// Server's UDP address
	IPEndPoint mServerUdpEndPoint;

	// Source of the UDP packet (available during callbacks)
	IPEndPoint mPacketSource;

	// Temporary, not important
	Buffer mBuffer;
	bool mIsAdmin = false;

	// List of channels we are currently in the process of joining
	List<int> mJoining = new List<int>();

	// Local server is used for socket-less mode
	GameServer mLocalServer;

	// Server configuration data
	DataNode mConfig = new DataNode("Version", Player.version);

	/// <summary>
	/// Whether the player has verified himself as an administrator.
	/// </summary>

	public bool isAdmin { get { return mIsAdmin; } }

	/// <summary>
	/// Set administrator privileges. Note that failing the password test will cause a disconnect.
	/// </summary>

	public void SetAdmin (string pass)
	{
		mIsAdmin = true;
		BeginSend(Packet.RequestVerifyAdmin).Write(pass);
		EndSend();
	}

	/// <summary>
	/// Channels the player belongs to. Don't modify this list.
	/// </summary>

	public TNet.List<Channel> channels { get { return mChannels; } }

	/// <summary>
	/// Current time on the server.
	/// </summary>

	public long serverTime { get { return mTimeDifference + (System.DateTime.UtcNow.Ticks / 10000); } }

	/// <summary>
	/// Whether the client is currently connected to the server.
	/// </summary>

	public bool isConnected { get { return mTcp.isConnected || mLocalServer != null; } }

	/// <summary>
	/// Whether we are currently trying to establish a new connection.
	/// </summary>

	public bool isTryingToConnect { get { return mTcp.isTryingToConnect; } }

	/// <summary>
	/// Whether we are currently in the process of joining a channel.
	/// To find out whether we are joining a specific channel, use the "IsJoiningChannel(id)" function.
	/// </summary>

	public bool isJoiningChannel { get { return mJoining.size != 0; } }

	/// <summary>
	/// Whether the client is currently in a channel.
	/// </summary>

	public bool isInChannel { get { return mChannels.size != 0; } }

	/// <summary>
	/// TCP end point, available only if we're actually connected to a server.
	/// </summary>

	public IPEndPoint tcpEndPoint { get { return mTcp.isConnected ? mTcp.tcpEndPoint : null; } }

	/// <summary>
	/// Port used to listen for incoming UDP packets. Set via StartUDP().
	/// </summary>

	public int listeningPort
	{
		get
		{
#if UNITY_WEBPLAYER
			return 0;
#else
			return mUdp.listeningPort;
#endif
		}
	}

	/// <summary>
	/// Forward and Create type packets write down their source.
	/// If the packet was sent by the server instead of another player, the ID will be 0.
	/// </summary>

	public int packetSourceID = 0;

	/// <summary>
	/// Source of the last packet.
	/// </summary>

	public IPEndPoint packetSourceIP { get { return mPacketSource != null ? mPacketSource : mTcp.tcpEndPoint; } }

	/// <summary>
	/// Enable or disable the Nagle's buffering algorithm (aka NO_DELAY flag).
	/// Enabling this flag will improve latency at the cost of increased bandwidth.
	/// http://en.wikipedia.org/wiki/Nagle's_algorithm
	/// </summary>

	public bool noDelay
	{
		get
		{
			return mTcp.noDelay;
		}
		set
		{
			if (mTcp.noDelay != value)
			{
				mTcp.noDelay = value;
				
				// Notify the server as well so that the server does the same
				BeginSend(Packet.RequestNoDelay).Write(value);
				EndSend();
			}
		}
	}

	/// <summary>
	/// Current ping to the server.
	/// </summary>

	public int ping { get { return isConnected ? mPing : 0; } }

	/// <summary>
	/// Whether we can communicate with the server via UDP.
	/// </summary>

	public bool canUseUDP
	{
		get
		{
#if UNITY_WEBPLAYER
			return false;
#else
			return mUdp.isActive && mServerUdpEndPoint != null;
#endif
		}
	}

	/// <summary>
	/// Server data associated with the connected server. Don't try to change it manually.
	/// </summary>

	public DataNode serverData
	{
		get
		{
			return mConfig;
		}
		set
		{
			if (isAdmin)
			{
				mConfig = value;
				var writer = BeginSend(Packet.RequestSetServerData);
				writer.Write("");
				writer.WriteObject(value);
				EndSend();
			}
		}
	}

	/// <summary>
	/// Return the local player.
	/// </summary>

	public Player player { get { return mTcp; } }

	/// <summary>
	/// The player's unique identifier.
	/// </summary>

	public int playerID { get { return mTcp.id; } }

	/// <summary>
	/// Name of this player.
	/// </summary>

	public string playerName
	{
		get
		{
			return mTcp.name;
		}
		set
		{
			if (mTcp.name != value)
			{
				if (isConnected)
				{
					BinaryWriter writer = BeginSend(Packet.RequestSetName);
					writer.Write(value);
					EndSend();
				}
				else mTcp.name = value;
			}
		}
	}

	/// <summary>
	/// Get or set the player's data. Read-only. Use SetPlayerData to change the contents.
	/// </summary>

	public DataNode playerData { get { return mTcp.dataNode; } set { mTcp.dataNode = value; } }

	/// <summary>
	/// Direct access to the incoming queue to deposit messages in. Don't forget to lock it before using it.
	/// </summary>

	public Queue<Buffer> receiveQueue { get { return mTcp.receiveQueue; } }

	/// <summary>
	/// If sockets are not used, an outgoing queue can be specified instead. Don't forget to lock it before using it.
	/// </summary>

	public Queue<Buffer> sendQueue { get { return mTcp.sendQueue; } set { mTcp.sendQueue = value; } }

	/// <summary>
	/// Set the specified value on the player.
	/// </summary>

	public void SetPlayerData (string path, object val)
	{
		DataNode node = mTcp.Set(path, val);

		if (isConnected)
		{
			BinaryWriter writer = BeginSend(Packet.RequestSetPlayerData);
			writer.Write(mTcp.id);
			writer.Write(path);
			writer.WriteObject(val);
			EndSend();
		}

		if (onSetPlayerData != null)
			onSetPlayerData(mTcp, path, node);
	}

	/// <summary>
	/// Whether the client is currently trying to join the specified channel.
	/// </summary>

	public bool IsJoiningChannel (int id) { return mJoining.Contains(id); }

	/// <summary>
	/// Whether the player is currently in the specified channel.
	/// </summary>

	public bool IsInChannel (int channelID)
	{
		if (isConnected)
		{
			for (int i = 0; i < mChannels.size; ++i)
			{
				Channel ch = mChannels[i];
				if (ch.id == channelID) return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Get the player hosting the specified channel. Only works for the channels the player is in.
	/// </summary>

	public Player GetHost (int channelID)
	{
		if (isConnected)
		{
			for (int i = 0; i < mChannels.size; ++i)
			{
				Channel ch = mChannels[i];
				if (ch.id == channelID) return ch.host;
			}
		}
		return null;
	}

	/// <summary>
	/// Retrieve a player by their ID.
	/// </summary>

	public Player GetPlayer (int id, bool createIfMissing = false)
	{
		if (id == mTcp.id) return mTcp;

		if (isConnected)
		{
			Player player = null;
			mDictionary.TryGetValue(id, out player);

			if (player == null && createIfMissing)
			{
				player = new Player();
				player.id = id;
				mDictionary[id] = player;
			}
			return player;
		}
		return null;
	}

	/// <summary>
	/// Retrieve a player by their name.
	/// </summary>

	public Player GetPlayer (string name)
	{
		for (int i = 0; i < mChannels.size; ++i)
		{
			Channel ch = mChannels[i];

			for (int b = 0; b < ch.players.size; ++b)
			{
				Player p = ch.players[b];
				if (p.name == name) return p;
			}
		}
		return null;
	}

	/// <summary>
	/// Return a channel with the specified ID.
	/// </summary>

	public Channel GetChannel (int channelID, bool createIfMissing = false)
	{
		for (int i = 0; i < mChannels.size; ++i)
		{
			Channel ch = mChannels[i];
			if (ch.id == channelID) return ch;
		}

		if (createIfMissing)
		{
			Channel ch = new Channel();
			ch.id = channelID;
			mChannels.Add(ch);
			return ch;
		}
		return null;
	}

	/// <summary>
	/// Begin sending a new packet to the server.
	/// </summary>

	public BinaryWriter BeginSend (Packet type)
	{
		mBuffer = Buffer.Create();
		return mBuffer.BeginPacket(type);
	}

	/// <summary>
	/// Begin sending a new packet to the server.
	/// </summary>

	public BinaryWriter BeginSend (byte packetID)
	{
		mBuffer = Buffer.Create();
		return mBuffer.BeginPacket(packetID);
	}

	/// <summary>
	/// Cancel the send operation.
	/// </summary>

	public void CancelSend ()
	{
		if (mBuffer != null)
		{
			mBuffer.EndPacket();
			mBuffer.Recycle();
			mBuffer = null;
		}
	}

	/// <summary>
	/// Send the outgoing buffer.
	/// </summary>

	public void EndSend (bool forced = false)
	{
		if (mBuffer != null)
		{
			mBuffer.EndPacket();
			if (isActive || forced) mTcp.SendTcpPacket(mBuffer);
			mBuffer.Recycle();
			mBuffer = null;
		}
	}

	/// <summary>
	/// Send the outgoing buffer.
	/// </summary>

	public void EndSend (int channelID, bool reliable)
	{
		mBuffer.EndPacket();

		if (isActive)
		{
#if UNITY_WEBPLAYER
			mTcp.SendTcpPacket(mBuffer);
#else
			if (reliable || !mUdpIsUsable || mServerUdpEndPoint == null || !mUdp.isActive)
			{
				mTcp.SendTcpPacket(mBuffer);
			}
			else mUdp.Send(mBuffer, mServerUdpEndPoint);
#endif
		}

		mBuffer.Recycle();
		mBuffer = null;
	}

	/// <summary>
	/// Broadcast the outgoing buffer to the entire LAN via UDP.
	/// </summary>

	public void EndSend (int port)
	{
		mBuffer.EndPacket();
#if !UNITY_WEBPLAYER
		if (isActive) mUdp.Broadcast(mBuffer, port);
#endif
		mBuffer.Recycle();
		mBuffer = null;
	}

	/// <summary>
	/// Send this packet to a remote UDP listener.
	/// </summary>

	public void EndSend (IPEndPoint target)
	{
		mBuffer.EndPacket();
#if !UNITY_WEBPLAYER
		if (isActive) mUdp.Send(mBuffer, target);
#endif
		mBuffer.Recycle();
		mBuffer = null;
	}

	/// <summary>
	/// Establish a local connection without using sockets.
	/// </summary>

	public void Connect (GameServer server)
	{
		Disconnect();

		if (server != null)
		{
			mLocalServer = server;
			server.localClient = this;

			mTcp.stage = TcpProtocol.Stage.Verifying;
			BinaryWriter writer = BeginSend(Packet.RequestID);
			writer.Write(TcpProtocol.version);
#if UNITY_EDITOR
			writer.Write(string.IsNullOrEmpty(mTcp.name) ? "Editor" : mTcp.name);
#else
			writer.Write(string.IsNullOrEmpty(mTcp.name) ? "Guest" : mTcp.name);
#endif
			writer.WriteObject(mTcp.dataNode);
			EndSend();
		}
	}

	/// <summary>
	/// Try to establish a connection with the specified address.
	/// </summary>

	public void Connect (IPEndPoint externalIP, IPEndPoint internalIP = null)
	{
		Disconnect();
		if (externalIP == null) UnityEngine.Debug.LogError("Expecting a valid IP address or a local server to be running");
		else mTcp.Connect(externalIP, internalIP);
	}

	/// <summary>
	/// Disconnect from the server.
	/// </summary>

	public void Disconnect ()
	{
		if (mLocalServer != null)
		{
			mLocalServer.localClient = null;
			mLocalServer = null;
		}
		mTcp.Disconnect();
	}

	/// <summary>
	/// Start listening to incoming UDP packets on the specified port.
	/// </summary>

	public bool StartUDP (int udpPort)
	{
#if !UNITY_WEBPLAYER
		if (mLocalServer == null)
		{
			if (TcpProtocol.defaultListenerInterface.AddressFamily == AddressFamily.InterNetworkV6 &&
				UdpProtocol.defaultNetworkInterface.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
			{
				if (mUdp.Start(udpPort, IPAddress.IPv6Any))
				{
					if (isConnected)
					{
						BeginSend(Packet.RequestSetUDP).Write((ushort)udpPort);
						EndSend();
					}
					return true;
				}
			}
			else if (mUdp.Start(udpPort))
			{
				if (isConnected)
				{
					BeginSend(Packet.RequestSetUDP).Write((ushort)udpPort);
					EndSend();
				}
				return true;
			}
		}
#endif
		return false;
	}

	/// <summary>
	/// Stop listening to incoming broadcasts.
	/// </summary>

	public void StopUDP ()
	{
#if !UNITY_WEBPLAYER
		if (mUdp.isActive)
		{
			if (isConnected)
			{
				BeginSend(Packet.RequestSetUDP).Write((ushort)0);
				EndSend();
			}
			mUdp.Stop();
			mUdpIsUsable = false;
		}
#endif
	}

	/// <summary>
	/// Join the specified channel.
	/// </summary>
	/// <param name="channelID">ID of the channel. Every player joining this channel will see one another.</param>
	/// <param name="levelName">Level that will be loaded first.</param>
	/// <param name="persistent">Whether the channel will remain active even when the last player leaves.</param>
	/// <param name="playerLimit">Maximum number of players that can be in this channel at once.</param>
	/// <param name="password">Password for the channel. First player sets the password.</param>

	public void JoinChannel (int channelID, string levelName, bool persistent, int playerLimit, string password)
	{
		if (isConnected && !IsInChannel(channelID) && !mJoining.Contains(channelID))
		{
			BinaryWriter writer = BeginSend(Packet.RequestJoinChannel);
			writer.Write(channelID);
			writer.Write(string.IsNullOrEmpty(password) ? "" : password);
			writer.Write(string.IsNullOrEmpty(levelName) ? "" : levelName);
			writer.Write(persistent);
			writer.Write((ushort)playerLimit);
			EndSend();

			// Prevent all further packets from going out until the join channel response arrives.
			// This prevents the situation where packets are sent out between LoadLevel / JoinChannel
			// requests and the arrival of the OnJoinChannel/OnLoadLevel responses, which cause RFCs
			// from the previous scene to be executed in the new one.
			mJoining.Add(channelID);
		}
	}

	/// <summary>
	/// Close the channel the player is in. New players will be prevented from joining.
	/// Once a channel has been closed, it cannot be re-opened.
	/// </summary>

	public bool CloseChannel (int channelID)
	{
		if (isConnected && IsInChannel(channelID))
		{
			BeginSend(Packet.RequestCloseChannel).Write(channelID);
			EndSend();
			return true;
		}
		return false;
	}

	/// <summary>
	/// Leave the current channel.
	/// </summary>

	public bool LeaveChannel (int channelID)
	{
		if (isConnected)
		{
			for (int i = 0; i < mChannels.size; ++i)
			{
				Channel ch = mChannels[i];
				
				if (ch.id == channelID)
				{
					mChannels.RemoveAt(i);
					BeginSend(Packet.RequestLeaveChannel).Write(channelID);
					EndSend();
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>
	/// Leave all channels.
	/// </summary>

	public void LeaveAllChannels ()
	{
		if (isConnected)
		{
			mJoining.Clear();

			for (int i = mChannels.size; i > 0; )
			{
				Channel ch = mChannels[--i];
				BeginSend(Packet.RequestLeaveChannel).Write(ch.id);
				EndSend();
				mChannels.RemoveAt(i);
			}
		}
	}

	/// <summary>
	/// Delete the specified channel.
	/// </summary>

	public void DeleteChannel (int id, bool disconnect)
	{
		if (isConnected)
		{
			BinaryWriter writer = BeginSend(Packet.RequestDeleteChannel);
			writer.Write(id);
			writer.Write(disconnect);
			EndSend();
		}
	}

	/// <summary>
	/// Change the maximum number of players that can join the channel the player is currently in.
	/// </summary>

	public void SetPlayerLimit (int channelID, int max)
	{
		if (isConnected && IsInChannel(channelID))
		{
			BinaryWriter writer = BeginSend(Packet.RequestSetPlayerLimit);
			writer.Write(channelID);
			writer.Write((ushort)max);
			EndSend();
		}
	}

	/// <summary>
	/// Switch the current level.
	/// </summary>

	public bool LoadLevel (int channelID, string levelName)
	{
		if (isConnected && IsInChannel(channelID))
		{
			BinaryWriter writer = BeginSend(Packet.RequestLoadLevel);
			writer.Write(channelID);
			writer.Write(levelName);
			EndSend();
			return true;
		}
		return false;
	}

	/// <summary>
	/// Change the hosting player.
	/// </summary>

	public void SetHost (int channelID, Player player)
	{
		if (isConnected && GetHost(channelID) == mTcp)
		{
			BinaryWriter writer = BeginSend(Packet.RequestSetHost);
			writer.Write(channelID);
			writer.Write(player.id);
			EndSend();
		}
	}

	/// <summary>
	/// Set the timeout for the player. By default it's 10 seconds. If you know you are about to load a large level,
	/// and it's going to take, say 60 seconds, set this timeout to 120 seconds just to be safe. When the level
	/// finishes loading, change this back to 10 seconds so that dropped connections gets detected correctly.
	/// </summary>

	public void SetTimeout (int seconds)
	{
		if (isConnected)
		{
			BeginSend(Packet.RequestSetTimeout).Write(seconds);
			EndSend();
		}
	}

	/// <summary>
	/// Send a remote ping request to the specified TNet server.
	/// </summary>

	public void Ping (IPEndPoint udpEndPoint, OnPing callback)
	{
		onPing = callback;
		mPingTime = DateTime.UtcNow.Ticks / 10000;
		BeginSend(Packet.RequestPing);
		EndSend(udpEndPoint);
	}

	/// <summary>
	/// Retrieve a list of files from the server.
	/// </summary>

	public void GetFiles (string path, OnGetFiles callback)
	{
		mGetFiles[path] = callback;
		BinaryWriter writer = BeginSend(Packet.RequestGetFileList);
		writer.Write(path);
		EndSend();
	}

	/// <summary>
	/// Load the specified file from the server.
	/// </summary>

	public void LoadFile (string filename, OnLoadFile callback)
	{
		mLoadFiles[filename] = callback;
		BinaryWriter writer = BeginSend(Packet.RequestLoadFile);
		writer.Write(filename);
		EndSend();
	}

	/// <summary>
	/// Save the specified file on the server.
	/// </summary>

	public void SaveFile (string filename, byte[] data)
	{
		if (data != null)
		{
			BinaryWriter writer = BeginSend(Packet.RequestSaveFile);
			writer.Write(filename);
			writer.Write(data.Length);
			writer.Write(data);
		}
		else
		{
			BinaryWriter writer = BeginSend(Packet.RequestDeleteFile);
			writer.Write(filename);
		}
		EndSend();
	}

	/// <summary>
	/// Delete the specified file on the server.
	/// </summary>

	public void DeleteFile (string filename)
	{
		BinaryWriter writer = BeginSend(Packet.RequestDeleteFile);
		writer.Write(filename);
		EndSend();
	}

	/// <summary>
	/// Process all incoming packets.
	/// </summary>

	public void ProcessPackets ()
	{
		mMyTime = DateTime.UtcNow.Ticks / 10000;

		// Request pings every so often, letting the server know we're still here.
		if (mLocalServer != null)
		{
			mPing = 0;
			mPingTime = mMyTime;
		}
		else if (isActive && mTcp.isConnected && mCanPing && mPingTime + 4000 < mMyTime)
		{
			mCanPing = false;
			mPingTime = mMyTime;
			BeginSend(Packet.RequestPing);
			EndSend();
		}

		Buffer buffer = null;
		bool keepGoing = true;

#if !UNITY_WEBPLAYER
		IPEndPoint ip = null;

		while (keepGoing && isActive && mUdp.ReceivePacket(out buffer, out ip))
		{
			mUdpIsUsable = true;
			keepGoing = ProcessPacket(buffer, ip);
			buffer.Recycle();
		}
#endif
		while (keepGoing && isActive && mTcp.ReceivePacket(out buffer))
		{
			keepGoing = ProcessPacket(buffer, null);
			buffer.Recycle();
		}
	}

	/// <summary>
	/// Process a single incoming packet. Returns whether we should keep processing packets or not.
	/// </summary>

	bool ProcessPacket (Buffer buffer, IPEndPoint ip)
	{
		mPacketSource = ip;
		BinaryReader reader = buffer.BeginReading();
		if (buffer.size == 0) return true;

		int packetID = reader.ReadByte();
		Packet response = (Packet)packetID;

#if DEBUG_PACKETS && !STANDALONE
		if (response != Packet.ResponsePing && response != Packet.Broadcast)
			UnityEngine.Debug.Log("Client: " + response + " (" + buffer.size + " bytes) " + ((ip == null) ? "(TCP)" : "(UDP)"));
#endif
		// Verification step must be passed first
		if (response == Packet.ResponseID || mTcp.stage == TcpProtocol.Stage.Verifying)
		{
			if (mTcp.VerifyResponseID(response, reader))
			{
				mTimeDifference = reader.ReadInt64() - (System.DateTime.UtcNow.Ticks / 10000);

#if !UNITY_WEBPLAYER
				if (mUdp.isActive)
				{
					// If we have a UDP listener active, tell the server
					BeginSend(Packet.RequestSetUDP).Write((ushort)mUdp.listeningPort);
					EndSend();
				}
#endif
				mCanPing = true;
				if (onConnect != null) onConnect(true, null);
			}
			return true;
		}

		OnPacket callback;

		if (packetHandlers.TryGetValue((byte)response, out callback) && callback != null)
		{
			callback(response, reader, ip);
			return true;
		}

		switch (response)
		{
			case Packet.Empty: break;
			case Packet.ForwardToAll:
			case Packet.ForwardToOthers:
			case Packet.ForwardToAllSaved:
			case Packet.ForwardToOthersSaved:
			case Packet.ForwardToHost:
			case Packet.BroadcastAdmin:
			case Packet.Broadcast:
			{
				packetSourceID = reader.ReadInt32();
				int channelID = reader.ReadInt32();
				if (onForwardedPacket != null) onForwardedPacket(channelID, reader);
				break;
			}
			case Packet.ForwardToPlayer:
			{
				packetSourceID = reader.ReadInt32();
				reader.ReadInt32(); // Skip the target player ID
				int channelID = reader.ReadInt32();
				if (onForwardedPacket != null) onForwardedPacket(channelID, reader);
				break;
			}
			case Packet.ForwardByName:
			{
				packetSourceID = reader.ReadInt32();
				reader.ReadString(); // Skip the player name
				int channelID = reader.ReadInt32();
				if (onForwardedPacket != null) onForwardedPacket(channelID, reader);
				break;
			}
			case Packet.ResponseSetPlayerData:
			{
				int pid = reader.ReadInt32();
				Player target = GetPlayer(pid);

				if (target != null)
				{
					string path = reader.ReadString();
					DataNode node = target.Set(path, reader.ReadObject());
					if (onSetPlayerData != null) onSetPlayerData(target, path, node);
				}
				else UnityEngine.Debug.LogError("Not found: " + pid);
				break;
			}
			case Packet.ResponsePing:
			{
				int ping = (int)(mMyTime - mPingTime);

				if (ip != null)
				{
					if (onPing != null && ip != null) onPing(ip, ping);
				}
				else
				{
					mCanPing = true;
					mPing = ping;
				}
				break;
			}
			case Packet.ResponseSetUDP:
			{
#if !UNITY_WEBPLAYER
				// The server has a new port for UDP traffic
				ushort port = reader.ReadUInt16();

				if (port != 0 && mTcp.tcpEndPoint != null)
				{
					IPAddress ipa = new IPAddress(mTcp.tcpEndPoint.Address.GetAddressBytes());
					mServerUdpEndPoint = new IPEndPoint(ipa, port);

					// Send the first UDP packet to the server
					if (mUdp.isActive)
					{
						mBuffer = Buffer.Create();
						mBuffer.BeginPacket(Packet.RequestActivateUDP).Write(playerID);
						mBuffer.EndPacket();
						mUdp.Send(mBuffer, mServerUdpEndPoint);
						mBuffer.Recycle();
						mBuffer = null;
					}
				}
				else mServerUdpEndPoint = null;
#endif
				break;
			}
			case Packet.ResponseJoiningChannel:
			{
				int channelID = reader.ReadInt32();
				int count = reader.ReadInt16();
				Channel ch = GetChannel(channelID, true);

				for (int i = 0; i < count; ++i)
				{
					int pid = reader.ReadInt32();
					Player p = GetPlayer(pid, true);

					if (reader.ReadBoolean())
					{
						p.name = reader.ReadString();
						p.dataNode = reader.ReadDataNode();
					}
					ch.players.Add(p);
				}
				break;
			}
			case Packet.ResponseLoadLevel:
			{
				// Purposely return after loading a level, ensuring that all future callbacks happen after loading
				int channelID = reader.ReadInt32();
				string scene = reader.ReadString();
				if (onLoadLevel != null) onLoadLevel(channelID, scene);
				return false;
			}
			case Packet.ResponsePlayerJoined:
			{
				int channelID = reader.ReadInt32();

				Channel ch = GetChannel(channelID);

				if (ch != null)
				{
					Player p = GetPlayer(reader.ReadInt32(), true);

					if (reader.ReadBoolean())
					{
						p.name = reader.ReadString();
						p.dataNode = reader.ReadDataNode();
					}

					ch.players.Add(p);
					if (onPlayerJoin != null) onPlayerJoin(channelID, p);
				}
				break;
			}
			case Packet.ResponsePlayerLeft:
			{
				int channelID = reader.ReadInt32();
				int playerID = reader.ReadInt32();

				Channel ch = GetChannel(channelID);

				if (ch != null)
				{
					Player p = ch.GetPlayer(playerID);
					ch.players.Remove(p);
					RebuildPlayerDictionary();
					if (onPlayerLeave != null) onPlayerLeave(channelID, p);
				}
				break;
			}
			case Packet.ResponseSetHost:
			{
				int channelID = reader.ReadInt32();
				int hostID = reader.ReadInt32();

				for (int i = 0; i < mChannels.size; ++i)
				{
					Channel ch = mChannels[i];

					if (ch.id == channelID)
					{
						ch.host = GetPlayer(hostID);
						if (onHostChanged != null) onHostChanged(ch);
						break;
					}
				}
				break;
			}
			case Packet.ResponseSetChannelData:
			{
				int channelID = reader.ReadInt32();
				Channel ch = GetChannel(channelID);

				if (ch != null)
				{
					string path = reader.ReadString();
					DataNode node = ch.Set(path, reader.ReadObject());
					if (onSetChannelData != null) onSetChannelData(ch, path, node);
				}
				break;
			}
			case Packet.ResponseJoinChannel:
			{
				int channelID = reader.ReadInt32();
				bool success = reader.ReadBoolean();
				string msg = success ? null : reader.ReadString();

				// mJoining can contain -2 and -1 when joining random channels
				if (!mJoining.Remove(channelID))
				{
					for (int i = 0; i < mJoining.size; ++i)
					{
						int id = mJoining[i];

						if (id < 0)
						{
							mJoining.RemoveAt(i);
							break;
						}
					}
				}
#if UNITY_EDITOR
				if (!success) UnityEngine.Debug.LogError("ResponseJoinChannel: " + success + ", " + msg);
#endif
				if (onJoinChannel != null) onJoinChannel(channelID, success, msg);
				break;
			}
			case Packet.ResponseLeaveChannel:
			{
				int channelID = reader.ReadInt32();

				for (int i = 0; i < mChannels.size; ++i)
				{
					Channel ch = mChannels[i];

					if (ch.id == channelID)
					{
						mChannels.RemoveAt(i);
						break;
					}
				}

				RebuildPlayerDictionary();
				if (onLeaveChannel != null) onLeaveChannel(channelID);

				// Purposely exit after receiving a "left channel" notification so that other packets get handled in the next frame.
				return false;
			}
			case Packet.ResponseRenamePlayer:
			{
				Player p = GetPlayer(reader.ReadInt32());
				string oldName = p.name;
				if (p != null) p.name = reader.ReadString();
				if (onRenamePlayer != null) onRenamePlayer(p, oldName);
				break;
			}
			case Packet.ResponseCreateObject:
			{
				if (onCreate != null)
				{
					int playerID = reader.ReadInt32();
					int channelID = reader.ReadInt32();
					uint objID = reader.ReadUInt32();
					onCreate(channelID, playerID, objID, reader);
				}
				break;
			}
			case Packet.ResponseDestroyObject:
			{
				if (onDestroy != null)
				{
					int channelID = reader.ReadInt32();
					int count = reader.ReadUInt16();

					for (int i = 0; i < count; ++i)
					{
						uint val = reader.ReadUInt32();
						onDestroy(channelID, val);
					}
				}
				break;
			}
			case Packet.ResponseTransferObject:
			{
				if (onTransfer != null)
				{
					int from = reader.ReadInt32();
					int to = reader.ReadInt32();
					uint id0 = reader.ReadUInt32();
					uint id1 = reader.ReadUInt32();
					onTransfer(from, to, id0, id1);
				}
				break;
			}
			case Packet.Error:
			{
				string err = reader.ReadString();
				if (onError != null) onError(err);
				if (mTcp.stage != TcpProtocol.Stage.Connected && onConnect != null) onConnect(false, err);
				break;
			}
			case Packet.Disconnect:
			{
				if (onLeaveChannel != null)
				{
					while (mChannels.size > 0)
					{
						int index = mChannels.size - 1;
						Channel ch = mChannels[index];
						mChannels.RemoveAt(index);
						onLeaveChannel(ch.id);
					}
				}

				mChannels.Clear();
				mGetChannelsCallbacks.Clear();
				mDictionary.Clear();
				mTcp.Close(false);
				mLoadFiles.Clear();
				mGetFiles.Clear();
				mJoining.Clear();
				mIsAdmin = false;

				if (mLocalServer != null)
				{
					mLocalServer.localClient = null;
					mLocalServer = null;
				}

#if !UNITY_WEBPLAYER
				mUdp.Stop();
#endif
				if (onDisconnect != null) onDisconnect();
				mConfig = new DataNode("Version", Player.version);
				break;
			}
			case Packet.ResponseGetFileList:
			{
				string filename = reader.ReadString();
				int size = reader.ReadInt32();
				string[] files = null;

				if (size > 0)
				{
					files = new string[size];
					for (int i = 0; i < size; ++i)
						files[i] = reader.ReadString();
				}

				OnGetFiles cb = null;
				if (mGetFiles.TryGetValue(filename, out cb))
					mGetFiles.Remove(filename);

				if (cb != null)
				{
					try
					{
						cb(filename, files);
					}
#if UNITY_EDITOR
					catch (System.Exception ex)
					{
						Debug.LogError(ex.Message + ex.StackTrace);
					}
#else
					catch (System.Exception) {}
#endif
				}
				break;
			}
			case Packet.ResponseLoadFile:
			{
				string filename = reader.ReadString();
				int size = reader.ReadInt32();
				byte[] data = reader.ReadBytes(size);
				OnLoadFile cb = null;

				if (mLoadFiles.TryGetValue(filename, out cb))
					mLoadFiles.Remove(filename);

				if (cb != null)
				{
					try
					{
						cb(filename, data);
					}
#if UNITY_EDITOR
					catch (System.Exception ex)
					{
						Debug.LogError(ex.Message + ex.StackTrace);
					}
#else
					catch (System.Exception) {}
#endif
				}
				break;
			}
			case Packet.ResponseVerifyAdmin:
			{
				int pid = reader.ReadInt32();
				Player p = GetPlayer(pid);
				if (p == player) mIsAdmin = true;
				if (onSetAdmin != null) onSetAdmin(p);
				break;
			}
			case Packet.ResponseSetServerData:
			{
				string path = reader.ReadString();
				object obj = reader.ReadObject();

				if (obj != null)
				{
					DataNode node = mConfig.SetHierarchy(path, obj);
					if (onSetServerData != null) onSetServerData(path, node);
				}
				else
				{
					DataNode node = mConfig.RemoveHierarchy(path);
					if (onSetServerData != null) onSetServerData(path, node);
				}
				break;
			}
			case Packet.ResponseChannelList:
			{
				if (mGetChannelsCallbacks.Count != 0)
				{
					OnGetChannels cb = mGetChannelsCallbacks.Dequeue();
					List<Channel.Info> channels = new List<Channel.Info>();
					int count = reader.ReadInt32();

					for (int i = 0; i < count; ++i)
					{
						Channel.Info info = new Channel.Info();
						info.id = reader.ReadInt32();
						info.players = reader.ReadUInt16();
						info.limit = reader.ReadUInt16();
						info.hasPassword = reader.ReadBoolean();
						info.isPersistent = reader.ReadBoolean();
						info.level = reader.ReadString();
						info.data = reader.ReadDataNode();
						channels.Add(info);
					}

					if (cb != null) cb(channels);
				}
				break;
			}
			case Packet.ResponseLockChannel:
			{
				int channelID = reader.ReadInt32();
				bool isLocked = reader.ReadBoolean();
				Channel ch = GetChannel(channelID);
				if (ch != null) ch.isLocked = isLocked;
				if (onLockChannel != null) onLockChannel(channelID, isLocked);
				break;
			}
		}
		return true;
	}

	/// <summary>
	/// Rebuild the player dictionary from the list of players in all of the channels we're currently in.
	/// </summary>

	void RebuildPlayerDictionary ()
	{
		mDictionary.Clear();

		for (int i = 0; i < mChannels.size; ++i)
		{
			Channel ch = mChannels[i];

			for (int b = 0; b < ch.players.size; ++b)
			{
				Player p = ch.players[b];
				mDictionary[p.id] = p;
			}
		}
	}

	/// <summary>
	/// Retrieve the specified server option.
	/// </summary>

	public DataNode GetServerData (string key) { return (mConfig != null) ? mConfig.GetHierarchy(key) : null; }

	/// <summary>
	/// Retrieve the specified server option.
	/// </summary>

	public T GetServerData<T> (string key) { return (mConfig != null) ? mConfig.GetHierarchy<T>(key) : default(T); }

	/// <summary>
	/// Retrieve the specified server option.
	/// </summary>

	public T GetServerData<T> (string key, T def) { return (mConfig != null) ? mConfig.GetHierarchy<T>(key, def) : def; }

	/// <summary>
	/// Set the specified server option.
	/// </summary>

	public void SetServerData (DataNode node)
	{
		BinaryWriter writer = BeginSend(Packet.RequestSetServerData);
		writer.Write(node.name);
		writer.WriteObject(node);
		EndSend();
	}

	/// <summary>
	/// Set the specified server option.
	/// </summary>

	public void SetServerData (string key, object val)
	{
		BinaryWriter writer = BeginSend(Packet.RequestSetServerData);
		writer.Write(key);
		writer.WriteObject(val);
		EndSend();
	}

	/// <summary>
	/// Set the specified server option.
	/// </summary>

	public void SetChannelData (int channelID, string path, object val)
	{
		Channel ch = GetChannel(channelID);

		if (ch != null)
		{
			if (!ch.isLocked || isAdmin)
			{
				DataNode node = ch.dataNode;

				if (node == null)
				{
					if (val == null) return;
					node = new DataNode("Version", Player.version);
				}

				node.SetHierarchy(path, val);
				BinaryWriter bw = BeginSend(Packet.RequestSetChannelData);
				bw.Write(channelID);
				bw.Write(path);
				bw.WriteObject(val);
				EndSend();
			}
		}
	}

	public delegate void OnGetChannels (List<Channel.Info> list);
	Queue<OnGetChannels> mGetChannelsCallbacks = new Queue<OnGetChannels>();

	/// <summary>
	/// Get a list of channels from the server.
	/// </summary>

	public void GetChannelList (OnGetChannels callback)
	{
		mGetChannelsCallbacks.Enqueue(callback);
		BeginSend(Packet.RequestChannelList);
		EndSend();
	}
}
}
