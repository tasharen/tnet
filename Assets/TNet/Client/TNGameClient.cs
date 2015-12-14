//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2015 Tasharen Entertainment
//---------------------------------------------

#if UNITY_EDITOR
using UnityEngine;
#endif

using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using System.Collections.Generic;

namespace TNet
{
/// <summary>
/// Client-side logic.
/// </summary>

public class GameClient
{
	public delegate void OnPing (IPEndPoint ip, int milliSeconds);
	public delegate void OnError (string message);
	public delegate void OnConnect (bool success, string message);
	public delegate void OnDisconnect ();
	public delegate void OnJoinChannel (int channelID, bool success, string message);
	public delegate void OnLeftChannel (int channelID);
	public delegate void OnLoadLevel (int channelID, string levelName);
	public delegate void OnPlayerJoined (int channelID, Player p);
	public delegate void OnPlayerLeft (int channelID, Player p);
	public delegate void OnPlayerSync (Player p);
	public delegate void OnRenamePlayer (Player p, string previous);
	public delegate void OnSetHost (int channelID, bool hosting);
	public delegate void OnSetChannelData (int channelID, DataNode data);
	public delegate void OnCreate (int channelID, int creator, int index, uint objID, BinaryReader reader);
	public delegate void OnDestroy (int channelID, uint objID);
	public delegate void OnForwardedPacket (int channelID, BinaryReader reader);
	public delegate void OnPacket (Packet response, BinaryReader reader, IPEndPoint source);
	public delegate void OnGetFiles (string path, string[] files);
	public delegate void OnLoadFile (string filename, byte[] data);
	public delegate void OnServerData (DataNode data);
	public delegate void OnLockChannel (int channelID, bool locked);
	public delegate void OnSetAdmin (Player p);

	/// <summary>
	/// Custom packet listeners. You can set these to handle custom packets.
	/// </summary>

	public Dictionary<byte, OnPacket> packetHandlers = new Dictionary<byte, OnPacket>();

	/// <summary>
	/// Ping notification.
	/// </summary>

	public OnPing onPing;

	/// <summary>
	/// Error notification.
	/// </summary>

	public OnError onError;

	/// <summary>
	/// Connection attempt result indicating success or failure.
	/// </summary>

	public OnConnect onConnect;

	/// <summary>
	/// Notification sent after the connection terminates for any reason.
	/// </summary>

	public OnDisconnect onDisconnect;

	/// <summary>
	/// Notification sent when attempting to join a channel, indicating a success or failure.
	/// </summary>

	public OnJoinChannel onJoinChannel;

	/// <summary>
	/// Notification sent when leaving a channel.
	/// Also sent just before a disconnect (if inside a channel when it happens).
	/// </summary>

	public OnLeftChannel onLeftChannel;

	/// <summary>
	/// Notification sent when changing levels.
	/// </summary>

	public OnLoadLevel onLoadLevel;

	/// <summary>
	/// Notification sent when a new player joins the channel.
	/// </summary>

	public OnPlayerJoined onPlayerJoined;

	/// <summary>
	/// Notification sent when a player leaves the channel.
	/// </summary>

	public OnPlayerLeft onPlayerLeft;

	/// <summary>
	/// Notification sent when player data gets synchronized.
	/// </summary>

	public OnPlayerSync onPlayerSync;

	/// <summary>
	/// Notification of some player changing their name.
	/// </summary>

	public OnRenamePlayer onRenamePlayer;

	/// <summary>
	/// Notification sent when the channel's host changes.
	/// </summary>

	public OnSetHost onSetHost;

	/// <summary>
	/// Notification of the channel's custom data changing.
	/// </summary>

	public OnSetChannelData onSetChannelData;

	/// <summary>
	/// Notification of a new object being created.
	/// </summary>

	public OnCreate onCreate;

	/// <summary>
	/// Notification of the specified object being destroyed.
	/// </summary>

	public OnDestroy onDestroy;

	/// <summary>
	/// Server data is stored separately from the game data and can be changed only by admins.
	/// It's also sent to all players as soon as they join, and can be used for such things as MOTD.
	/// </summary>

	public OnServerData onServerOption;

	/// <summary>
	/// Callback triggered when the channel becomes locked or unlocked.
	/// </summary>

	public OnLockChannel onLockChannel;

	/// <summary>
	/// Callback triggered when the player gets verified as an administrator.
	/// </summary>

	public OnSetAdmin onSetAdmin;

	/// <summary>
	/// Server data associated with the connected server. Don't try to change it manually.
	/// </summary>

	public DataNode serverOptions;

	/// <summary>
	/// Notification of a client packet arriving.
	/// </summary>

	public OnForwardedPacket onForwardedPacket;

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
	UdpProtocol mUdp = new UdpProtocol();
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

	// Each LoadFile() call can specify its own callback
	Dictionary<string, OnLoadFile> mLoadFiles = new Dictionary<string, OnLoadFile>();

	// Server's UDP address
	IPEndPoint mServerUdpEndPoint;

	// Source of the UDP packet (available during callbacks)
	IPEndPoint mPacketSource;

	// Temporary, not important
	Buffer mBuffer;

	// Packets should not be sent in between of level-switching operations.
	bool mCanSend = true;
	bool mIsAdmin = false;

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

	public bool isConnected { get { return mTcp.isConnected; } }

	/// <summary>
	/// Whether we are currently trying to establish a new connection.
	/// </summary>

	public bool isTryingToConnect { get { return mTcp.isTryingToConnect; } }

	/// <summary>
	/// Whether we are currently in the process of switching scenes.
	/// </summary>

	public bool isSwitchingScenes { get { return !mCanSend; } }

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
	/// Source of the last packet.
	/// </summary>

	public IPEndPoint packetSource { get { return mPacketSource != null ? mPacketSource : mTcp.tcpEndPoint; } }

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
	/// Get or set the player's data.
	/// </summary>

	public object playerData
	{
		get
		{
			return mTcp.data;
		}
		set
		{
			mTcp.data = value;
			SyncPlayerData();
		}
	}

	/// <summary>
	/// Immediately sync the player's data.
	/// </summary>

	public void SyncPlayerData ()
	{
		if (isConnected)
		{
			BinaryWriter writer = BeginSend(Packet.SyncPlayerData);
			writer.Write(mTcp.id);
			writer.WriteObject(mTcp.data);
			EndSend();
		}
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

	public Player GetPlayer (int id)
	{
		if (id == mTcp.id) return mTcp;

		if (isConnected)
		{
			Player player = null;
			mDictionary.TryGetValue(id, out player);
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

	public Channel GetChannel (int channelID)
	{
		for (int i = 0; i < mChannels.size; ++i)
		{
			Channel ch = mChannels[i];
			if (ch.id == channelID) return ch;
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

	public void EndSend ()
	{
		if (mBuffer != null)
		{
			mBuffer.EndPacket();
			if (mCanSend) mTcp.SendTcpPacket(mBuffer);
			mBuffer.Recycle();
			mBuffer = null;
		}
	}

	/// <summary>
	/// Send the outgoing buffer.
	/// </summary>

	public void EndSendForced ()
	{
		if (mBuffer != null)
		{
			mBuffer.EndPacket();
			mTcp.SendTcpPacket(mBuffer);
			mBuffer.Recycle();
			mBuffer = null;
		}
	}

	/// <summary>
	/// Send the outgoing buffer.
	/// </summary>

	public void EndSend (bool reliable)
	{
		mBuffer.EndPacket();

		if (mCanSend)
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
		if (mCanSend) mUdp.Broadcast(mBuffer, port);
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
		if (mCanSend) mUdp.Send(mBuffer, target);
#endif
		mBuffer.Recycle();
		mBuffer = null;
	}

	/// <summary>
	/// Try to establish a connection with the specified address.
	/// </summary>

	public void Connect (IPEndPoint externalIP, IPEndPoint internalIP)
	{
		Disconnect();
		mTcp.Connect(externalIP, internalIP);
	}

	/// <summary>
	/// Disconnect from the server.
	/// </summary>

	public void Disconnect () { mTcp.Disconnect(); }

	/// <summary>
	/// Start listening to incoming UDP packets on the specified port.
	/// </summary>

	public bool StartUDP (int udpPort)
	{
#if !UNITY_WEBPLAYER
		if (mUdp.Start(udpPort))
		{
			if (isConnected)
			{
				BeginSend(Packet.RequestSetUDP).Write((ushort)udpPort);
				EndSend();
			}
			return true;
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
		if (isConnected)
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
			mCanSend = false;
		}
	}

	/// <summary>
	/// Close the channel the player is in. New players will be prevented from joining.
	/// Once a channel has been closed, it cannot be re-opened.
	/// </summary>

	public void CloseChannel (int channelID)
	{
		if (isConnected && isInChannel)
		{
			BeginSend(Packet.RequestCloseChannel).Write(channelID);
			EndSend();
		}
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
		if (isConnected && isInChannel)
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

	public void LoadLevel (int channelID, string levelName)
	{
		if (isConnected && isInChannel)
		{
			BinaryWriter writer = BeginSend(Packet.RequestLoadLevel);
			writer.Write(channelID);
			writer.Write(levelName);
			EndSend();
			mCanSend = false;
		}
	}

	/// <summary>
	/// Change the hosting player.
	/// </summary>

	public void SetHost (int channelID, Player player)
	{
		if (isConnected && isInChannel && GetHost(channelID) == mTcp)
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
		if (mTcp.isConnected && mCanPing && mCanSend && mPingTime + 4000 < mMyTime)
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

//#if !UNITY_EDITOR // DEBUG
//        if (response != Packet.ResponsePing) Console.WriteLine("Client: " + response + " " + buffer.position + " of " + buffer.size + ((ip == null) ? " (TCP)" : " (UDP)"));
//#else
//        if (response != Packet.ResponsePing && response != Packet.Broadcast)
//            UnityEngine.Debug.Log("Client: " + response + " " + buffer.position + " of " + buffer.size + ((ip == null) ? " (TCP)" : " (UDP)"));
//#endif

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
				int channelID = reader.ReadInt32();
				if (onForwardedPacket != null) onForwardedPacket(channelID, reader);
				break;
			}
			case Packet.ForwardToPlayer:
			{
				int channelID = reader.ReadInt32();
				reader.ReadInt32(); // Skip the player ID
				if (onForwardedPacket != null) onForwardedPacket(channelID, reader);
				break;
			}
			case Packet.ForwardByName:
			{
				int channelID = reader.ReadInt32();
				reader.ReadString(); // Skip the player name
				if (onForwardedPacket != null) onForwardedPacket(channelID, reader);
				break;
			}
			case Packet.SyncPlayerData:
			{
				Player target = GetPlayer(reader.ReadInt32());

				if (target != null)
				{
					target.data = reader.ReadObject();
					if (onPlayerSync != null) onPlayerSync(target);
				}
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

				if (port != 0)
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
#if UNITY_EDITOR
				if (mCanSend) UnityEngine.Debug.LogError("'mCanSend' flag is in the wrong state");
#endif
				int channelID = reader.ReadInt32();
				int count = reader.ReadInt16();

				Channel ch = new Channel();
				ch.id = channelID;

				for (int i = 0; i < count; ++i)
				{
					Player p = new Player();
					p.id = reader.ReadInt32();
					p.name = reader.ReadString();
					p.data = reader.ReadObject();
					mDictionary[p.id] = p;
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
				mCanSend = false;
				return false;
			}
			case Packet.ResponsePlayerJoined:
			{
				int channelID = reader.ReadInt32();

				Channel ch = GetChannel(channelID);

				if (ch != null)
				{
					Player p = new Player();
					p.id = reader.ReadInt32();
					p.name = reader.ReadString();
					p.data = reader.ReadObject();
					ch.players.Add(p);
					mDictionary[p.id] = p;
					if (onPlayerJoined != null) onPlayerJoined(channelID, p);
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
					if (onPlayerLeft != null) onPlayerLeft(channelID, p);
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
						break;
					}
				}

				if (onSetHost != null) onSetHost(channelID, hostID == playerID);
				break;
			}
			case Packet.ResponseSetChannelData:
			{
				int channelID = reader.ReadInt32();
				DataNode data = reader.ReadDataNode();
				Channel ch = GetChannel(channelID);
				if (ch != null) ch.data = data;
				if (onSetChannelData != null) onSetChannelData(channelID, data);
				break;
			}
			case Packet.ResponseJoinChannel:
			{
				mCanSend = true;
				int channelID = reader.ReadInt32();
				bool success = reader.ReadBoolean();
				string msg = success ? null : reader.ReadString();
#if UNITY_EDITOR
				if (!success) UnityEngine.Debug.LogError("ResponseJoinChannel: " + success + ", " + msg);
#endif
				if (success)
				{
					Channel ch = new Channel();
					ch.id = channelID;
					mChannels.Add(ch);
				}

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
				if (onLeftChannel != null) onLeftChannel(channelID);

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
			case Packet.ResponseCreate:
			{
				if (onCreate != null)
				{
					int channelID = reader.ReadInt32();
					int playerID = reader.ReadInt32();
					ushort index = reader.ReadUInt16();
					uint objID = reader.ReadUInt32();
					onCreate(channelID, playerID, index, objID, reader);
				}
				break;
			}
			case Packet.ResponseDestroy:
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
			case Packet.Error:
			{
				if (mTcp.stage != TcpProtocol.Stage.Connected && onConnect != null)
				{
					onConnect(false, reader.ReadString());
				}
				else if (onError != null)
				{
					onError(reader.ReadString());
				}
				break;
			}
			case Packet.Disconnect:
			{
				if (onLeftChannel != null)
				{
					while (mChannels.size > 0)
					{
						int index = mChannels.size - 1;
						Channel ch = mChannels[index];
						mChannels.RemoveAt(index);
						onLeftChannel(ch.id);
					}
				}

				mChannels.Clear();
				mDictionary.Clear();
				mTcp.Close(false);
				mLoadFiles.Clear();
				mGetFiles.Clear();
				mIsAdmin = false;
				if (onDisconnect != null) onDisconnect();
				mCanSend = true;
				serverOptions = null;
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
			case Packet.ResponseServerOptions:
			{
				serverOptions = reader.ReadDataNode();

				if (onServerOption != null)
				{
					for (int i = 0; i < serverOptions.children.size; ++i)
					{
						DataNode child = serverOptions.children[i];
						onServerOption(child);
					}
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
			case Packet.ResponseSetServerOption:
			{
				if (serverOptions == null) serverOptions = new DataNode("Version", Player.version);
				DataNode node = reader.ReadDataNode();

				if (node.value == null && node.children.size == 0)
				{
					serverOptions.RemoveChild(node.name);
				}
				else
				{
					DataNode child = serverOptions.ReplaceChild(node);
					if (onServerOption != null) onServerOption(child);
				}
				break;
			}
			case Packet.ResponseLockChannel:
			{
				int chID = reader.ReadInt32();
				bool isLocked = reader.ReadBoolean();
				Channel ch = GetChannel(chID);
				if (ch != null) ch.locked = isLocked;
				if (onLockChannel != null) onLockChannel(chID, isLocked);
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

	public DataNode GetServerOption (string key) { return (serverOptions != null) ? serverOptions.GetChild(key) : null; }

	/// <summary>
	/// Retrieve the specified server option.
	/// </summary>

	public T GetServerOption<T> (string key) { return (serverOptions != null) ? serverOptions.GetChild<T>(key) : default(T); }

	/// <summary>
	/// Retrieve the specified server option.
	/// </summary>

	public T GetServerOption<T> (string key, T def) { return (serverOptions != null) ? serverOptions.GetChild<T>(key, def) : def; }

	/// <summary>
	/// Set the specified server option.
	/// </summary>

	public void SetServerOption (string key, object val)
	{
		DataNode node = new DataNode(key, val);
		BeginSend(Packet.RequestSetServerOption).Write(node);
		EndSend();
	}

	/// <summary>
	/// Set the specified server option.
	/// </summary>

	public void SetServerOption (DataNode node)
	{
		BeginSend(Packet.RequestSetServerOption).Write(node);
		EndSend();
	}

	/// <summary>
	/// Return the channel data for the specified channel. This only works for channels that the player is currently in.
	/// After modifying don't forget to call SyncChannelData().
	/// </summary>

	public DataNode GetChannelData (int channelID)
	{
		Channel ch = GetChannel(channelID);
		return (ch != null) ? ch.data : null;
	}

	/// <summary>
	/// Set the channel data for the specified channel. Use this to set data for channels other than the ones where the player resides.
	/// </summary>

	public void SetChannelData (int channelID, DataNode val)
	{
		BinaryWriter bw = BeginSend(Packet.RequestSetChannelData);
		bw.Write(channelID);
		bw.Write(val);
		EndSend();
	}

	/// <summary>
	/// Retrieve the specified server option.
	/// </summary>

	public DataNode GetChannelOption (int channelID, string key)
	{
		DataNode data = GetChannelData(channelID);
		return (data != null) ? data.GetChild(key) : null;
	}

	/// <summary>
	/// Retrieve the specified server option.
	/// </summary>

	public T GetChannelOption<T> (int channelID, string key)
	{
		DataNode data = GetChannelData(channelID);
		return (data != null) ? data.GetChild<T>(key) : default(T);
	}

	/// <summary>
	/// Retrieve the specified server option.
	/// </summary>

	public T GetChannelOption<T> (int channelID, string key, T def)
	{
		DataNode data = GetChannelData(channelID);
		return (data != null) ? data.GetChild<T>(key, def) : def;
	}

	/// <summary>
	/// Set the specified server option.
	/// </summary>

	public void SetChannelOption (int channelID, string key, object val)
	{
		Channel ch = GetChannel(channelID);

		if (ch != null)
		{
			if (!ch.locked || isAdmin)
			{
				if (ch.data == null) ch.data = new DataNode("Version", Player.version);
				ch.data.SetChild(key, val);
				BinaryWriter bw = BeginSend(Packet.RequestSetChannelData);
				bw.Write(channelID);
				bw.Write(ch.data);
				EndSend();
			}
		}
	}

	/// <summary>
	/// Set the specified server option.
	/// </summary>

	public void SetChannelOption (int channelID, DataNode node)
	{
		DataNode data = GetChannelData(channelID);
		if (data == null) data = new DataNode("Version", Player.version);
		data.ReplaceChild(node);
		BinaryWriter bw = BeginSend(Packet.RequestSetChannelData);
		bw.Write(channelID);
		bw.Write(data);
		EndSend();
	}

	/// <summary>
	/// Set the specified server option.
	/// </summary>

	public void RemoveChannelOption (int channelID, string key)
	{
		DataNode data = GetChannelData(channelID);

		if (data != null)
		{
			data.RemoveChild(key);
			BinaryWriter bw = BeginSend(Packet.RequestSetChannelData);
			bw.Write(channelID);
			bw.Write(data);
			EndSend();
		}
	}
}
}
