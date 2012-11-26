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

public class Client : Connection
{
	/// <summary>
	/// Client protocol version. Must match the server.
	/// </summary>

	public const int version = 1;

	public delegate void OnError (string message);
	public delegate void OnConnect (bool success, string message);
	public delegate void OnDisconnect ();
	public delegate void OnPlayerJoined (ClientPlayer p);
	public delegate void OnPlayerLeft (ClientPlayer p);
	public delegate void OnSetHost (bool hosting);
	public delegate void OnChannelChanged (bool isInChannel, string message);
	public delegate void OnRenamePlayer (ClientPlayer p, string previous);
	public delegate void OnCreate (int objectID, int objID, BinaryReader reader);
	public delegate void OnDestroy (int objID);
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
	/// Notification of joining or leaving a channel. Boolean value indicates presence in the channel.
	/// </summary>

	public OnChannelChanged onChannelChanged;

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
	/// List of players in the same channel as the client.
	/// </summary>

	public BetterList<ClientPlayer> players = new BetterList<ClientPlayer>();

	// Same list of players, but in a dictionary format for quick lookup
	Dictionary<int, ClientPlayer> mDictionary = new Dictionary<int, ClientPlayer>();

	enum Stage
	{
		Disconnected,
		Connecting,
		Verifying,
		Connected,
	}

	// Current connection stage
	Stage mStage = Stage.Disconnected;

	// ID of the player and the host
	int mPlayerID = 0;
	int mHost = 0;
	string mPlayerName = "";

	// Current time, time when the last ping was sent out, and time when connection was started
	long mTime = 0;
	long mPingTime = 0;

	// Last ping, and whether we can ping again
	int mPing = 0;
	bool mCanPing = false;
	Buffer mBuffer;

	/// <summary>
	/// Whether the client is currently connected to the server.
	/// </summary>

	new public bool isConnected { get { return mStage == Stage.Connected; } }

	/// <summary>
	/// Whether this player is hosting the game.
	/// </summary>

	public bool isHosting { get { return !isConnected || (mHost == mPlayerID) && (players.size > 0); } }

	/// <summary>
	/// Current ping to the server.
	/// </summary>

	public int ping { get { return mStage == Stage.Connected ? mPing : 0; } }

	/// <summary>
	/// Name of the player.
	/// </summary>

	public string name
	{
		get
		{
			return mPlayerName;
		}
		set
		{
			if (mPlayerName != value)
			{
				mPlayerName = value;

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

	public ClientPlayer GetPlayer (int id)
	{
		ClientPlayer player = null;
		mDictionary.TryGetValue(id, out player);
		return player;
	}

	/// <summary>
	/// Begin sending a new packet to the server.
	/// </summary>

	public BinaryWriter BeginSend (Packet type)
	{
		//Console.WriteLine("Sending " + type);
		mBuffer = Buffer.Create(false);
		return mBuffer.BeginPacket(type);
	}

	/// <summary>
	/// Begin sending a new packet to the server.
	/// </summary>

	public BinaryWriter BeginSend (byte packetID)
	{
		//Console.WriteLine("Sending " + packetID);
		mBuffer = Buffer.Create(false);
		return mBuffer.BeginPacket(packetID);
	}

	/// <summary>
	/// Send the outgoing buffer to the specified player.
	/// </summary>

	public void EndSend ()
	{
		mBuffer.EndPacket();
		SendPacket(mBuffer);
		mBuffer = null;
	}

	/// <summary>
	/// Try to establish a connection with the specified address.
	/// </summary>

	public void Connect (string addr, int port)
	{
		Disconnect();

		IPAddress destination = null;

		if (!IPAddress.TryParse(addr, out destination))
		{
			IPAddress[] ips = Dns.GetHostAddresses(addr);
			if (ips.Length > 0) destination = ips[0];
		}

		mStage = Stage.Connecting;
		address = addr + ":" + port;
		socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		socket.BeginConnect(destination, port, OnConnectResult, socket);
	}

	/// <summary>
	/// Connection attempt result.
	/// </summary>

	void OnConnectResult (IAsyncResult result)
	{
		Socket sock = (Socket)result.AsyncState;

		try
		{
			sock.EndConnect(result);
		}
		catch (System.Exception ex)
		{
			Error(ex.Message);
			Close(false);
			return;
		}

		// We can now receive data
		mStage = Stage.Verifying;
		StartReceiving();

		// Request a player ID
		BinaryWriter writer = BeginSend(Packet.RequestID);
		writer.Write(version);
		EndSend();
	}

	/// <summary>
	/// Join the specified channel.
	/// </summary>

	public void JoinChannel (int channelID, string password, bool persistent)
	{
		if (isConnected)
		{
			BinaryWriter writer = BeginSend(Packet.RequestJoinChannel);
			writer.Write(channelID);
			writer.Write(string.IsNullOrEmpty(password) ? "" : password);
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
	/// Change the hosting player.
	/// </summary>

	public void SetHost (ClientPlayer player)
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
		if (mStage == Stage.Disconnected) return;
		mTime = DateTime.Now.Ticks / 10000;

		// Request pings every so often, letting the server know we're still here.
		if (isConnected && mStage == Stage.Connected && mCanPing && mPingTime + 4000 < mTime)
		{
			mCanPing = false;
			mPingTime = mTime;
			BeginSend(Packet.RequestPing);
			EndSend();
		}

		Buffer buffer;

		// Read all incoming packets one at a time
		while ((buffer = ReceivePacket()) != null)
		{
			BinaryReader reader = buffer.BeginReading();
			int packetID = reader.ReadByte();
			Packet response = (Packet)packetID;

			//Console.WriteLine("======= " + response + " (" + buffer.size + " bytes)");

			switch (response)
			{
				case Packet.ForwardToAll:
				case Packet.ForwardToOthers:
				case Packet.ForwardToAllBuffered:
				case Packet.ForwardToOthersBuffered:
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
					if (mStage == Stage.Verifying)
					{
						int serverVersion = reader.ReadInt32();
						mPlayerID = reader.ReadInt32();

						if (serverVersion == version)
						{
							mCanPing = true;
							mStage = Stage.Connected;
							if (onConnect != null) onConnect(true, null);
						}
						else
						{
							if (onConnect != null) onConnect(false, "Version mismatch. Server is running version " + serverVersion +
								", while you have version " + version);
							Close(true);
						}
					}
					break;
				}
				case Packet.ResponsePlayerLeft:
				{
					ClientPlayer p = GetPlayer(reader.ReadInt32());
					if (p != null) mDictionary.Remove(p.id);
					players.Remove(p);
					if (onPlayerLeft != null) onPlayerLeft(p);
					break;
				}
				case Packet.ResponsePlayerJoined:
				{
					ClientPlayer p = new ClientPlayer();
					p.id = reader.ReadInt32();
					p.name = reader.ReadString();
					mDictionary.Add(p.id, p);
					players.Add(p);
					if (onPlayerJoined != null) onPlayerJoined(p);
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
						ClientPlayer p = new ClientPlayer();
						p.id = reader.ReadInt32();
						p.name = reader.ReadString();
						mDictionary.Add(p.id, p);
						players.Add(p);
					}
					break;
				}
				case Packet.ResponseSetHost:
				{
					mHost = reader.ReadInt32();
					if (onSetHost != null) onSetHost(isHosting);
					break;
				}
				case Packet.ResponseJoinedChannel:
				{
					if (onChannelChanged != null) onChannelChanged(true, null);
					break;
				}
				case Packet.ResponseJoinFailed:
				{
					if (onChannelChanged != null) onChannelChanged(false, reader.ReadString());
					break;
				}
				case Packet.ResponseLeftChannel:
				{
					mDictionary.Clear();
					players.Clear();
					if (onChannelChanged != null) onChannelChanged(false, null);
					break;
				}
				case Packet.ResponseRenamePlayer:
				{
					ClientPlayer p = GetPlayer(reader.ReadInt32());
					string oldName = p.name;
					if (p != null) p.name = reader.ReadString();
					if (onRenamePlayer != null) onRenamePlayer(p, oldName);
					break;
				}
				case Packet.ResponseCreate:
				{
					if (onCreate != null)
					{
						short objectID = reader.ReadInt16();
						int objID = reader.ReadInt32();
						onCreate(objectID, objID, reader);
					}
					break;
				}
				case Packet.ResponseDestroy:
				{
					if (onDestroy != null)
					{
						int count = reader.ReadInt16();
						for (int i = 0; i < count; ++i) onDestroy(reader.ReadInt32());
					}
					break;
				}
				case Packet.Error:
				{
					if (mStage != Stage.Connected && onConnect != null)
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
					mStage = Stage.Disconnected;
					if (onDisconnect != null) onDisconnect();
					break;
				}
				default:
				{
					if (onError != null) onError("Unknown packet ID: " + packetID);
					break;
				}
			}
			buffer.Recycle();
		}
	}
}
}