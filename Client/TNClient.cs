using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using System.Collections.Generic;

namespace TNet
{
/// <summary>
/// Server-side logic.
/// </summary>

public class Client
{
	enum Stage
	{
		Disconnected,
		Connecting,
		Verifying,
		Connected,
	}

	/// <summary>
	/// Client protocol version. Must match the server.
	/// </summary>

	public const int version = 1;

	/// <summary>
	/// List of players in the same channel as the client.
	/// </summary>

	public BetterList<Player> players = new BetterList<Player>();

	protected Dictionary<int, Player> mDictionary = new Dictionary<int, Player>();

	public delegate void OnError (string message);
	public delegate void OnConnect (bool success, string message);
	public delegate void OnDisconnect ();
	public delegate void OnPlayerJoined (Player p);
	public delegate void OnPlayerLeft (Player p);
	public delegate void OnChannelChanged (bool isInChannel, string message);
	public delegate void OnRenamePlayer (Player p, string previous);
	public delegate void OnCreate (int objectID, int viewID, BinaryReader reader);
	public delegate void OnDestroy (int viewID);
	public delegate void OnCustomPacket (int packetID, BinaryReader reader);

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
	/// Notification of the specified view being destroyed.
	/// </summary>

	public OnDestroy onDestroy;

	/// <summary>
	/// Notification of a client packet arriving.
	/// </summary>

	public OnCustomPacket onCustomPacket;

	int mSize = 0;
	int mPlayerID = 0;
	string mPlayerName = "";
	Socket mSocket;
	Buffer mIn = new Buffer();
	Buffer mOut = new Buffer();
	Stage mStage = Stage.Disconnected;
	long mTime = 0;
	long mConnectStart = 0;
	int mHost = 0;

	/// <summary>
	/// Whether the client is currently connected to the server.
	/// </summary>

	public bool isConnected { get { return mStage == Stage.Connected; } }

	/// <summary>
	/// Whether this player is hosting the game.
	/// </summary>

	public bool isHosting { get { return (mHost == mPlayerID) || (players.size == 0); } }

	/// <summary>
	/// Retrieve a player by their ID.
	/// </summary>

	public Player GetPlayer (int id)
	{
		Player player = null;
		mDictionary.TryGetValue(id, out player);
		return player;
	}

	/// <summary>
	/// Begin sending a new packet to the server.
	/// </summary>

	public BinaryWriter BeginSend (Packet request) { return BeginSend((int)request); }

	/// <summary>
	/// Begin sending a new packet to the server.
	/// </summary>

	public BinaryWriter BeginSend (int packetID)
	{
		BinaryWriter writer = mOut.BeginPacket();
		writer.Write((byte)packetID);
		return writer;
	}

	/// <summary>
	/// Send the outgoing buffer to the specified player.
	/// </summary>

	public void EndSend ()
	{
		int size = mOut.EndPacket();
		mSocket.Send(mOut.buffer, 0, size, SocketFlags.None);
	}

	/// <summary>
	/// Connect to the specified server.
	/// </summary>

	public void Connect (string addr, int port)
	{
		mTime = DateTime.Now.Ticks / 10000;
		mConnectStart = mTime;

		Disconnect();
		IPAddress destination = null;

		if (!IPAddress.TryParse(addr, out destination))
		{
			IPAddress[] ips = Dns.GetHostAddresses(addr);
			if (ips.Length > 0) destination = ips[0];
		}

		if (destination != null)
		{
			mSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			mSocket.Blocking = false;

			SocketAsyncEventArgs args = new SocketAsyncEventArgs();
			args.UserToken = mSocket;
			args.RemoteEndPoint = new IPEndPoint(destination, port);
			args.Completed += new EventHandler<SocketAsyncEventArgs>(OnConnectEvent);
			mSocket.ConnectAsync(args);
			mStage = Stage.Connecting;
		}
		else if (onConnect != null) onConnect(false, "Invalid address");
	}

	/// <summary>
	/// Async event: connection established (or failed).
	/// </summary>

	void OnConnectEvent (object sender, SocketAsyncEventArgs args)
	{
		bool success = (args.SocketError == SocketError.Success);

		if (onConnect != null)
		{
			if (success)
			{
				Console.WriteLine("Connected to " + ((IPEndPoint)mSocket.RemoteEndPoint).ToString());

				// Connection established -- let's verify the protocol version number
				mStage = Stage.Verifying;
				BinaryWriter writer = mOut.BeginWriting(4);
				writer.Write(version);
				mSocket.Send(mOut.buffer, 4, SocketFlags.None);
				return;
			}
			onConnect(false, args.SocketError.ToString());
		}
		Console.WriteLine("OnConnectEvent: " + args.SocketError);
	}

	/// <summary>
	/// Disconnect from the server.
	/// </summary>

	public void Disconnect ()
	{
		if (mSocket != null)
		{
			mTime = DateTime.Now.Ticks / 10000;

			if (mSocket.Connected) mSocket.Disconnect(false);
			mSocket.Close();
			mSocket = null;

			mStage = Stage.Disconnected;
			if (onDisconnect != null) onDisconnect();
		}
	}

	/// <summary>
	/// Join the specified channel.
	/// </summary>

	public void JoinChannel (int channelID, string password)
	{
		if (isConnected)
		{
			BinaryWriter writer = BeginSend(Packet.RequestJoinChannel);
			writer.Write(channelID);
			writer.Write(password);
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
	/// Change the player's name.
	/// </summary>

	public void SetName (string newName)
	{
		mPlayerName = newName;

		if (isConnected)
		{
			BinaryWriter writer = BeginSend(Packet.RequestSetName);
			writer.Write(newName);
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
	/// Receive a packet from the associated socket.
	/// </summary>

	BinaryReader ReceivePacket ()
	{
		// We must have at least 4 bytes to work with
		if (mSocket.Available < 4) return null;

		// Determine the size of the packet
		if (mSize == 0) mSize = mIn.Receive(mSocket, 4).ReadInt32();

		// If we don't have the entire packet waiting, don't do anything.
		if (mSocket.Available < mSize)
		{
			Console.WriteLine("Expecting " + mSize + " bytes, have " + mSocket.Available);
			return null;
		}

		// Receive the entire packet
		return mIn.Receive(mSocket, mSize);
	}

	/// <summary>
	/// Process all incoming packets.
	/// </summary>

	public void ProcessPackets ()
	{
		if (mStage == Stage.Disconnected || mSocket == null) return;
		mTime = DateTime.Now.Ticks / 10000;
		BinaryReader reader;

		// Read all incoming packets one at a time
		while ((reader = ReceivePacket()) != null)
		{
			int packetID = reader.ReadByte();
			Packet response = (Packet)packetID;

			Console.WriteLine("Packet: " + response);

			switch (response)
			{
				case Packet.Custom:
				{
					if (onCustomPacket != null) onCustomPacket(reader.ReadByte(), reader);
					break;
				}
				case Packet.ResponseVersion:
				{
					if (mStage == Stage.Verifying)
					{
						int serverVersion = reader.ReadInt32();
						mPlayerID = reader.ReadInt32();

						if (serverVersion == version)
						{
							mStage = Stage.Connected;
							if (onConnect != null) onConnect(true, null);
						}
						else
						{
							if (onConnect != null) onConnect(false, "Version mismatch. Server is running version " + serverVersion +
								", while you have version " + version);
							Disconnect();
						}
					}
					break;
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
				case Packet.ResponseJoiningChannel:
				{
					mDictionary.Clear();
					players.Clear();

					int channelID = reader.ReadInt32();
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
				case Packet.ResponseJoinedChannel:
				{
					if (onChannelChanged != null) onChannelChanged(true, null);
					break;
				}
				case Packet.ResponseLeftChannel:
				{
					mDictionary.Clear();
					players.Clear();
					if (onChannelChanged != null) onChannelChanged(false, null);
					break;
				}
				case Packet.ResponseWrongPassword:
				{
					if (onChannelChanged != null) onChannelChanged(false, "Wrong password");
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
				case Packet.ResponseSetHost:
				{
					mHost = reader.ReadInt32();
					break;
				}
				case Packet.ResponseCreate:
				{
					if (onCreate != null)
					{
						short objectID = reader.ReadInt16();
						int viewID = reader.ReadInt32();
						onCreate(objectID, viewID, reader);
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
				default:
				{
					if (onError != null) onError("Unknown packet ID: " + packetID);
					break;
				}
			}
		}
		
		// No longer connected? Send out a disconnect notification.
		if (mStage == Stage.Connected && !mSocket.Connected)
		{
			mStage = Stage.Disconnected;
			mSocket.Close();
			mSocket = null;
			if (onDisconnect != null) onDisconnect();
		}
	}
}
}