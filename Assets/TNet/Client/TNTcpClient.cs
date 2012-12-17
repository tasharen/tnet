//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

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

public class TcpClient
{
	public delegate void OnError (string message);
	public delegate void OnConnect (bool success, string message);
	public delegate void OnDisconnect ();
	public delegate void OnJoinChannel (bool success, string message);
	public delegate void OnLeftChannel ();
	public delegate void OnLoadLevel (string levelName);
	public delegate void OnPlayerJoined (Player p);
	public delegate void OnPlayerLeft (Player p);
	public delegate void OnSetHost (bool hosting);
	public delegate void OnRenamePlayer (Player p, string previous);
	public delegate void OnCreate (int index, uint objID, BinaryReader reader);
	public delegate void OnDestroy (uint objID);
	public delegate void OnForwardedPacket (BinaryReader reader);

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
	/// Notification sent when the channel's host changes.
	/// </summary>

	public OnSetHost onSetHost;

	/// <summary>
	/// Notification of some player changing their name.
	/// </summary>

	public OnRenamePlayer onRenamePlayer;

	/// <summary>
	/// Notification of a new object being created.
	/// </summary>

	public OnCreate onCreate;

	/// <summary>
	/// Notification of the specified object being destroyed.
	/// </summary>

	public OnDestroy onDestroy;

	/// <summary>
	/// Notification of a client packet arriving.
	/// </summary>

	public OnForwardedPacket onForwardedPacket;

	/// <summary>
	/// NAT punch-through facilitator address.
	/// </summary>

	//public IPEndPoint natFacilitator;

	/// <summary>
	/// List of players in the same channel as the client.
	/// </summary>

	public List<Player> players = new List<Player>();

	// Same list of players, but in a dictionary format for quick lookup
	Dictionary<int, Player> mDictionary = new Dictionary<int, Player>();

	// TCP connection is used for communication with the server.
	TcpProtocol mTcp = new TcpProtocol();

	// UDP can be used for communication with everyone on the same LAN. For example: local server discovery.
	UdpProtocol mUdp = new UdpProtocol();

	// ID of the host
	int mHost = 0;

	// Current time, time when the last ping was sent out, and time when connection was started
	long mTime = 0;
	long mPingTime = 0;

	// Last ping, and whether we can ping again
	int mPing = 0;
	bool mCanPing = false;

	// Server's UDP address
	IPEndPoint mServerUdpEndPoint;

	// Source of the UDP packet (available during callbacks)
	IPEndPoint mPacketSource;

	// Connection attempt address (used for NAT)
	IPEndPoint mConnectTarget;

	// Whether we've tried NAT punch-through
	//bool mTriedNAT = false;

	// Temporary, not important
	static Buffer mBuffer;

	/// <summary>
	/// Whether the client is currently connected to the server.
	/// </summary>

	public bool isConnected { get { return mTcp.isConnected; } }

	/// <summary>
	/// Whether this player is hosting the game.
	/// </summary>

	public bool isHosting { get { return !mTcp.isConnected || (mHost == mTcp.id) && (players.size > 0); } }

	/// <summary>
	/// Whether the client is currently in a channel.
	/// </summary>

	public bool isInChannel { get { return !mTcp.isConnected || players.size > 0; } }

	/// <summary>
	/// Port used to listen for incoming UDP packets. Set via Start().
	/// </summary>

	public int listeningPort { get { return mUdp.listeningPort; } }

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
				mTcp.name = value;

				if (isConnected)
				{
					BinaryWriter writer = BeginSend(Packet.RequestSetName);
					writer.Write(value);
					EndSend();
				}
			}
		}
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
	/// Begin sending a new packet to the server.
	/// </summary>

	public BinaryWriter BeginSend (Packet type)
	{
		mBuffer = Buffer.Create();
		return mBuffer.BeginTcpPacket(type);
	}

	/// <summary>
	/// Begin sending a new packet to the server.
	/// </summary>

	public BinaryWriter BeginSend (byte packetID)
	{
		mBuffer = Buffer.Create();
		return mBuffer.BeginTcpPacket(packetID);
	}

	/// <summary>
	/// Send the outgoing buffer.
	/// </summary>

	public void EndSend ()
	{
		mBuffer.EndTcpPacket();
		mTcp.SendTcpPacket(mBuffer);
		mBuffer.Recycle();
		mBuffer = null;
	}

	/// <summary>
	/// Send the outgoing buffer.
	/// </summary>

	public void EndSend (bool reliable)
	{
		mBuffer.EndTcpPacket();

		if (reliable || mServerUdpEndPoint == null || !mUdp.isActive)
		{
			mTcp.SendTcpPacket(mBuffer);
			mBuffer.Recycle();
		}
		else
		{
			mBuffer.EndTcpPacket();
			mUdp.Send(mBuffer, mServerUdpEndPoint);
		}
		mBuffer = null;
	}

	/// <summary>
	/// Broadcast the outgoing buffer to the entire LAN via UDP.
	/// </summary>

	public void EndSend (int port)
	{
		mBuffer.EndTcpPacket();
		mUdp.Broadcast(mBuffer, port);
		mBuffer = null;
	}

	/// <summary>
	/// Send this packet to a remote UDP listener.
	/// </summary>

	public void EndSend (IPEndPoint target)
	{
		mBuffer.EndTcpPacket();
		mUdp.Send(mBuffer, target);
		mBuffer = null;
	}

	/// <summary>
	/// Try to establish a connection with the specified address.
	/// </summary>

	public void Connect (string addr, int port)
	{
		//mTriedNAT = false;
		mConnectTarget = Player.ResolveEndPoint(addr, port);
		if (mConnectTarget != null) mTcp.Connect(mConnectTarget);
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
		if (mUdp.Start(udpPort))
		{
			if (isConnected)
			{
				BeginSend(Packet.RequestSetUDP).Write((ushort)udpPort);
				EndSend();
			}
			return true;
		}
		return false;
	}

	/// <summary>
	/// Stop listening to incoming broadcasts.
	/// </summary>

	public void StopUDP ()
	{
		if (mUdp.isActive)
		{
			if (isConnected)
			{
				BeginSend(Packet.RequestSetUDP).Write((ushort)0);
				EndSend();
			}
			mUdp.Stop();
		}
	}

	/// <summary>
	/// Join the specified channel.
	/// </summary>
	/// <param name="channelID">ID of the channel. Every player joining this channel will see one another.</param>
	/// <param name="levelName">Level that will be loaded first.</param>
	/// <param name="persistent">Whether the channel will remain active even when the last player leaves.</param>
	/// <param name="password">Password for the channel. First player sets the password.</param>

	public void JoinChannel (int channelID, string levelName, bool persistent, string password)
	{
		if (isConnected)
		{
			BinaryWriter writer = BeginSend(Packet.RequestJoinChannel);
			writer.Write(channelID);
			writer.Write(string.IsNullOrEmpty(password) ? "" : password);
			writer.Write(string.IsNullOrEmpty(levelName) ? "" : levelName);
			writer.Write(persistent);
			EndSend();
		}
	}

	/// <summary>
	/// Leave the current channel.
	/// </summary>

	public void LeaveChannel ()
	{
		if (isConnected)
		{
			BeginSend(Packet.RequestLeaveChannel);
			EndSend();
		}
	}

	/// <summary>
	/// Switch the current level.
	/// </summary>

	public void LoadLevel (string levelName)
	{
		if (isConnected)
		{
			BeginSend(Packet.RequestLoadLevel).Write(levelName);
			EndSend();
		}
	}

	/// <summary>
	/// Change the hosting player.
	/// </summary>

	public void SetHost (Player player)
	{
		if (isConnected && isHosting)
		{
			BinaryWriter writer = BeginSend(Packet.RequestSetHost);
			writer.Write(player.id);
			EndSend();
		}
	}

	/// <summary>
	/// Process all incoming packets.
	/// </summary>

	public void ProcessPackets ()
	{
		mTime = DateTime.Now.Ticks / 10000;

		// Request pings every so often, letting the server know we're still here.
		if (mTcp.isConnected && mCanPing && mPingTime + 4000 < mTime)
		{
			mCanPing = false;
			mPingTime = mTime;
			BeginSend(Packet.RequestPing);
			EndSend();
		}

		Buffer buffer = null;
		IPEndPoint ip = null;
		bool keepGoing = true;

		while (keepGoing && mUdp.ReceivePacket(out buffer, out ip))
		{
			keepGoing = ProcessPacket(buffer, ip);
			buffer.Recycle();
		}

		while (keepGoing && mTcp.ReceivePacket(out buffer))
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

//#if !UNITY_EDITOR // DEBUG
//		if (response != Packet.ResponsePing) Console.WriteLine("Client: " + response + " " + buffer.position + " " + buffer.size);
//#else
//		if (response != Packet.ResponsePing) UnityEngine.Debug.Log("Client: " + response + " " + buffer.position + " " + buffer.size);
//#endif

		switch (response)
		{
			case Packet.Empty: break;
			case Packet.ForwardToAll:
			case Packet.ForwardToOthers:
			case Packet.ForwardToAllSaved:
			case Packet.ForwardToOthersSaved:
			case Packet.ForwardToHost:
			{
				if (onForwardedPacket != null) onForwardedPacket(reader);
				break;
			}
			case Packet.ForwardToPlayer:
			{
				// Skip the player ID
				reader.ReadInt32();
				if (onForwardedPacket != null) onForwardedPacket(reader);
				break;
			}
			case Packet.ResponsePing:
			{
				mPing = (int)(mTime - mPingTime);
				mCanPing = true;
				break;
			}
			case Packet.ResponseID:
			{
				if (mTcp.stage == TcpProtocol.Stage.Verifying)
				{
					int serverVersion = reader.ReadInt32();

					if (mTcp.VerifyVersion(serverVersion, reader.ReadInt32()))
					{
						if (mUdp.isActive)
						{
							// If we have a UDP listener active, tell the server
							BeginSend(Packet.RequestSetUDP).Write(mUdp.isActive ? (ushort)mUdp.listeningPort : (ushort)0);
							EndSend();
						}
						
						mCanPing = true;
						if (onConnect != null) onConnect(true, null);
					}
					else if (onConnect != null)
					{
						onConnect(false, "Version mismatch. Server is running version " +
							serverVersion + ", while you have version " + Player.version);
					}
				}
				break;
			}
			case Packet.ResponseSetUDP:
			{
				// The server has a new port for UDP traffic
				ushort port = reader.ReadUInt16();
				mServerUdpEndPoint = (port != 0) ? new IPEndPoint(new IPAddress(mTcp.tcpEndPoint.Address.GetAddressBytes()), port) : null;
				
				if (mServerUdpEndPoint != null && mUdp.isActive)
				{
					// Send an empty packet to the server, opening up the communication channel
					mUdp.SendEmptyPacket(mServerUdpEndPoint);
				}
				break;
			}
			case Packet.ResponseJoiningChannel:
			{
				mDictionary.Clear();
				players.Clear();

				//int channelID =
					reader.ReadInt32();
				int count = reader.ReadInt16();

				for (int i = 0; i < count; ++i)
				{
					Player p = new Player();
					p.id = reader.ReadInt32();
					p.name = reader.ReadString();
					mDictionary.Add(p.id, p);
					players.Add(p);
				}
				break;
			}
			case Packet.ResponseLoadLevel:
			{
				// Purposely return after loading a level, ensuring that all future callbacks happen after loading
				if (onLoadLevel != null) onLoadLevel(reader.ReadString());
				return false;
			}
			case Packet.ResponsePlayerLeft:
			{
				Player p = GetPlayer(reader.ReadInt32());
				if (p != null) mDictionary.Remove(p.id);
				players.Remove(p);
				if (onPlayerLeft != null) onPlayerLeft(p);
				break;
			}
			case Packet.ResponsePlayerJoined:
			{
				Player p = new Player();
				p.id = reader.ReadInt32();
				p.name = reader.ReadString();
				mDictionary.Add(p.id, p);
				players.Add(p);
				if (onPlayerJoined != null) onPlayerJoined(p);
				break;
			}
			case Packet.ResponseSetHost:
			{
				mHost = reader.ReadInt32();
				if (onSetHost != null) onSetHost(isHosting);
				break;
			}
			case Packet.ResponseJoinChannel:
			{
				bool success = reader.ReadBoolean();
				if (onJoinChannel != null) onJoinChannel(success, success ? null : reader.ReadString());
				break;
			}
			case Packet.ResponseLeaveChannel:
			{
				mDictionary.Clear();
				players.Clear();
				if (onLeftChannel != null) onLeftChannel();
				break;
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
					short index = reader.ReadInt16();
					uint objID = reader.ReadUInt32();
					onCreate(index, objID, reader);
				}
				break;
			}
			case Packet.ResponseDestroy:
			{
				if (onDestroy != null)
				{
					int count = reader.ReadUInt16();
					for (int i = 0; i < count; ++i) onDestroy(reader.ReadUInt32());
				}
				break;
			}
			//case Packet.ResponseNAT:
			//{
			//    // TODO: Share the TCP socket, create a new socket on the same port, and try the connect here.
			//    IPEndPoint ip = Player.ResolveEndPoint(reader.ReadString(), reader.ReadInt16());
			//    if (ip != null) mTcp.Connect(ip);
			//    break;
			//}
			case Packet.Error:
			{
				if (mTcp.stage != TcpProtocol.Stage.Connected && onConnect != null)
				{
					// Direct TCP connection failed: try to go via the NAT punch-through facilitator
					//if (!mTriedNAT && natFacilitator != null && mConnectTarget != null && mUdp.isActive)
					//{
					//    mTriedNAT = true;
					//    BinaryWriter writer = BeginSend(Packet.RequestNAT);
					//    writer.Write(mTcp.target.Address.ToString());
					//    writer.Write((ushort)mTcp.target.Port);
					//    EndSend(natFacilitator);
					//    mTcp.Connect(mConnectTarget);
					//}
					//else
					{
						onConnect(false, reader.ReadString());
					}
				}
				else if (onError != null)
				{
					onError(reader.ReadString());
				}
				break;
			}
			case Packet.Disconnect:
			{
				if (isInChannel && onLeftChannel != null) onLeftChannel();
				players.Clear();
				mDictionary.Clear();
				mTcp.Close(false);
				if (onDisconnect != null) onDisconnect();
				break;
			}
			default:
			{
				if (onError != null) onError("Unknown packet ID: " + packetID);
				break;
			}
		}
		return true;
	}
}
}