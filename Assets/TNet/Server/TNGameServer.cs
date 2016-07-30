//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using System;
using System.IO;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.Text;

#if UNITY_EDITOR
using UnityEngine;
#endif

namespace TNet
{
/// <summary>
/// Game server logic. Handles new connections, RFCs, and pretty much everything else. Example usage:
/// GameServer gs = new GameServer();
/// gs.Start(5127);
/// </summary>

public class GameServer : FileServer
{
#if SINGLE_THREADED
	public const bool isMultiThreaded = false;
#else
	public const bool isMultiThreaded = true;
#endif

	/// <summary>
	/// You will want to make this a unique value.
	/// </summary>

	static public ushort gameID = 1;

	public delegate void OnCustomPacket (TcpPlayer player, Buffer buffer, BinaryReader reader, Packet request, bool reliable);
	public delegate void OnPlayerAction (Player p);
	public delegate void OnShutdown ();

	/// <summary>
	/// Any packet not already handled by the server will go to this function for processing.
	/// </summary>

	public OnCustomPacket onCustomPacket;

	/// <summary>
	/// Notification triggered when a player connects and authenticates successfully.
	/// </summary>

	public OnPlayerAction onPlayerConnect;

	/// <summary>
	/// Notification triggered when a player disconnects.
	/// </summary>

	public OnPlayerAction onPlayerDisconnect;

	/// <summary>
	/// Notification triggered when the server shuts down.
	/// </summary>

	public OnShutdown onShutdown;

	/// <summary>
	/// Give your server a name.
	/// </summary>

	public string name = "Game Server";

	/// <summary>
	/// Lobby server link, if one is desired.
	/// You can use this to automatically inform a remote lobby server of any changes to this server.
	/// </summary>

	public LobbyServerLink lobbyLink;

	// List of players in a consecutive order for each looping.
	List<TcpPlayer> mPlayerList = new List<TcpPlayer>();

	// Dictionary list of players for easy access by ID.
	Dictionary<int, TcpPlayer> mPlayerDict = new Dictionary<int, TcpPlayer>();

	// Dictionary list of players for easy access by IPEndPoint.
	Dictionary<IPEndPoint, TcpPlayer> mDictionaryEP = new Dictionary<IPEndPoint, TcpPlayer>();

	// List of all the active channels.
	List<Channel> mChannelList = new List<Channel>();

	// Dictionary of active channels to make lookup faster
	Dictionary<int, Channel> mChannelDict = new Dictionary<int, Channel>();

	// List of admin keywords
	List<string> mAdmin = new List<string>();

	// List of banned players
	List<string> mBan = new List<string>();

	// Random number generator.
	System.Random mRandom = new System.Random();
	Buffer mBuffer;
	TcpListener mListener;
	Thread mThread;
	int mListenerPort = 0;
	long mTime = 0;
	UdpProtocol mUdp = new UdpProtocol("Game Server");
	bool mAllowUdp = false;
	object mLock = 0;
	DataNode mServerData = null;
	string mFilename = "world.dat";
	long mNextSave = 0;
#if !STANDALONE
	GameClient mLocalClient = null;
#endif
	TcpPlayer mLocalPlayer = null;
	bool mIsActive = false;
	bool mServerDataChanged = false;

	/// <summary>
	/// Add a new entry to the list. Returns 'true' if a new entry was added.
	/// </summary>

	static bool AddUnique (List<string> list, string s)
	{
		if (!string.IsNullOrEmpty(s) && !list.Contains(s))
		{
			list.Add(s);
			return true;
		}
		return false;
	}

	/// <summary>
	/// Whether the server is currently actively serving players.
	/// </summary>

	public bool isActive { get { return mIsActive; } }

	/// <summary>
	/// Whether the server is listening for incoming connections.
	/// </summary>

	public bool isListening
	{
		get
		{
			return (mListener != null);
		}
		set
		{
			if (isListening != value)
			{
				if (Listen(value ? mListenerPort : 0))
				{
					if (lobbyLink != null)
					{
						lobbyLink.Start();
						lobbyLink.SendUpdate(this);
					}
				}
				else if (lobbyLink != null)
				{
					lock (mLock) { if (lobbyLink != null) lobbyLink.Stop(); }
				}
			}
		}
	}

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

	public int playerCount { get { return isActive ? mPlayerDict.Count : 0; } }

#if !STANDALONE
	/// <summary>
	/// Set to a client instance if not using sockets.
	/// </summary>

	public GameClient localClient
	{
		get
		{
			return mLocalClient;
		}
		set
		{
			if (mLocalPlayer != null)
			{
				RemovePlayer(mLocalPlayer);
				mLocalPlayer = null;
			}

			if (value != null)
			{
				lock (mLock)
				{
					mLocalClient = value;
					mLocalPlayer = new TcpPlayer();
					mLocalPlayer.id = 0;
					mLocalPlayer.name = "Guest";
					mLocalPlayer.stage = TcpProtocol.Stage.Verifying;
					mLocalPlayer.sendQueue = mLocalClient.receiveQueue;
					mLocalClient.sendQueue = mLocalPlayer.receiveQueue;
					mPlayerList.Add(mLocalPlayer);
				}
			}
		}
	}
#endif

	/// <summary>
	/// Listen to the specified port. This will overwrite any previous Listen() call as only one port can be listened to at a time.
	/// </summary>

	public bool Listen (int port)
	{
		if (mListenerPort == port) return true;

		lock (mLock)
		{
			if (mListener != null)
			{
				mListener.Stop();
				mListener = null;
			}

			mListenerPort = port;

			if (port != 0)
			{
				try
				{
					var addr = TcpProtocol.defaultListenerInterface.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;
					mListener = new TcpListener(addr, port);
					mListener.Start(50);
					//mListener.BeginAcceptSocket(OnAccept, null);
					return true;
				}
				catch (System.Exception ex)
				{
					Tools.LogError(ex.Message, ex.StackTrace, false);
				}
			}
		}
		return false;
	}

	/// <summary>
	/// Start listening to incoming connections on the specified port.
	/// </summary>

	public bool Start (int tcpPort = 0, int udpPort = 0)
	{
		Stop();

		Tools.LoadList("ServerConfig/ban.txt", mBan);
		Tools.LoadList("ServerConfig/admin.txt", mAdmin);

		// Banning by IPs is only good as a temporary measure
		for (int i = mBan.size; i > 0; )
		{
			IPAddress ip;
			if (IPAddress.TryParse(mBan[--i], out ip))
				mBan.RemoveAt(i);
		}

		Tools.Log("Admins: " + mAdmin.size);
		Tools.Log("Bans: " + mBan.size);

		if (tcpPort > 0 && !Listen(tcpPort)) return false;

#if STANDALONE
		Tools.Print("Game server started on port " + tcpPort + " using protocol version " + Player.version);
#endif
		if (udpPort > 0)
		{
			// Twice just in case the first try falls on a taken port
			if (TcpProtocol.defaultListenerInterface.AddressFamily == AddressFamily.InterNetworkV6 &&
				UdpProtocol.defaultNetworkInterface.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
			{
				if (!mUdp.Start(udpPort, IPAddress.IPv6Any))
				{
					Tools.LogError("Unable to listen to UDP port " + udpPort, null);
					Stop();
					return false;
				}
			}
			else if (!mUdp.Start(udpPort))
			{
				Tools.LogError("Unable to listen to UDP port " + udpPort, null);
				Stop();
				return false;
			}
		}

		mAllowUdp = (udpPort > 0);
		mIsActive = true;

		if (lobbyLink != null)
		{
			lobbyLink.Start();
			lobbyLink.SendUpdate(this);
		}

#if !SINGLE_THREADED
		mThread = new Thread(ThreadFunction);
		mThread.Start();
#endif
		return true;
	}

	/// <summary>
	/// Call this function when you've disabled multi-threading.
	/// </summary>

	public void Update () { if (mThread == null && isActive) ThreadFunction(); }

	/// <summary>
	/// Accept socket callback.
	/// </summary>

	//void OnAccept (IAsyncResult result) { AddPlayer(mListener.EndAcceptSocket(result)); }

	/// <summary>
	/// Stop listening to incoming connections and disconnect all players.
	/// </summary>

	public void Stop ()
	{
		Save();

		if (onShutdown != null) onShutdown();
		if (lobbyLink != null) lobbyLink.Stop();

		mAllowUdp = false;

		// Stop the worker thread
		if (mThread != null)
		{
			mThread.Interrupt();
			mThread.Join();
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
		for (int i = mPlayerList.size; i > 0; ) RemovePlayer(mPlayerList[--i]);
		mChannelList.Clear();
		mChannelDict.Clear();

		// Player counter should be reset
		Player.ResetPlayerCounter();
		mLocalPlayer = null;
#if !STANDALONE
		mLocalClient = null;
#endif
		mIsActive = false;
	}

	/// <summary>
	/// Thread that will be processing incoming data.
	/// </summary>

	void ThreadFunction ()
	{
#if !SINGLE_THREADED
		for (; ; )
#endif
		{
#if !SINGLE_THREADED && !STANDALONE
			if (TNManager.isPaused)
			{
				Thread.Sleep(500);
				continue;
			}
#endif
#if !SINGLE_THREADED
			bool received = false;
#endif
			lock (mLock)
			{
				Buffer buffer;
				mTime = DateTime.UtcNow.Ticks / 10000;
				IPEndPoint ip;

				// Add all pending connections
				while (mListener != null && mListener.Pending())
				{
					Socket socket = mListener.AcceptSocket();

					try
					{
						if (socket != null && socket.Connected)
						{
							IPEndPoint remote = socket.RemoteEndPoint as IPEndPoint;

							if (remote == null || mBan.Contains(remote.Address.ToString()))
							{
								socket.Close();
							}
							else AddPlayer(socket);
						}
					}
					catch (Exception)
					{
						if (socket != null)
						{
							try { socket.Close(); }
							catch (Exception) { }
						}
					}
				}

				// Process datagrams first
				while (mUdp.listeningPort != 0 && mUdp.ReceivePacket(out buffer, out ip))
				{
					if (buffer.size > 0)
					{
						TcpPlayer player = GetPlayer(ip);

						if (player != null)
						{
							if (!player.udpIsUsable) player.udpIsUsable = true;

							try
							{
#if SINGLE_THREADED
								ProcessPlayerPacket(buffer, player, false);
#else
								if (ProcessPlayerPacket(buffer, player, false)) received = true;
#endif
							}
							catch (System.Exception ex)
							{
								Tools.LogError(ex.Message, ex.StackTrace, true);
								RemovePlayer(player);
							}
						}
						else if (buffer.size > 0)
						{
							Packet request = Packet.Empty;

							try
							{
								BinaryReader reader = buffer.BeginReading();
								request = (Packet)reader.ReadByte();

								if (request == Packet.RequestActivateUDP)
								{
									int pid = reader.ReadInt32();
									player = GetPlayer(pid);

									// This message must arrive after RequestSetUDP which sets the UDP end point.
									// We do an additional step here because in some cases UDP port can be changed
									// by the router so that it appears that packets come from a different place.
									if (player != null && player.udpEndPoint != null && player.udpEndPoint.Address == ip.Address)
									{
										player.udpEndPoint = ip;
										player.udpIsUsable = true;
										mUdp.SendEmptyPacket(player.udpEndPoint);
									}
								}
								else if (request == Packet.RequestPing)
								{
									BeginSend(Packet.ResponsePing);
									EndSend(ip);
								}
							}
							catch (System.Exception ex)
							{
								if (player != null) player.LogError(ex.Message, ex.StackTrace);
								else Tools.LogError(ex.Message, ex.StackTrace);
								RemovePlayer(player);
							}
						}
					}
					buffer.Recycle();
				}

				// Process player connections next
				for (int i = 0; i < mPlayerList.size; )
				{
					TcpPlayer player = mPlayerList[i];

					// Remove disconnected players
					if (player != mLocalPlayer && !player.isSocketConnected)
					{
						RemovePlayer(player);
						continue;
					}

					// Process up to 100 packets at a time
					for (int b = 0; b < 100 && player.ReceivePacket(out buffer); ++b)
					{
						if (buffer.size > 0)
						{
#if SINGLE_THREADED
							ProcessPlayerPacket(buffer, player, true);
#else
							try
							{
								if (ProcessPlayerPacket(buffer, player, true))
									received = true;
							}
 #if STANDALONE
							catch (System.Exception ex)
							{
								player.LogError(ex.Message, ex.StackTrace);
								RemovePlayer(player);
								buffer.Recycle();
								continue;
							}
 #else
							catch (System.Exception ex)
							{
								player.LogError(ex.Message, ex.StackTrace);
								RemovePlayer(player);
							}
 #endif
#endif
						}
						buffer.Recycle();
					}

					if (player != mLocalPlayer)
					{
						// Time out -- disconnect this player
						if (player.stage == TcpProtocol.Stage.Connected)
						{
							// If the player doesn't send any packets in a while, disconnect him
							if (player.timeoutTime > 0 && player.lastReceivedTime + player.timeoutTime < mTime)
							{
								RemovePlayer(player);
								continue;
							}
						}
						else if (player.lastReceivedTime + 2000 < mTime)
						{
							RemovePlayer(player);
							continue;
						}
					}
					++i;
				}

				// Save periodically
				if (mNextSave != 0 && mNextSave < mTime) Save();
			}
#if !SINGLE_THREADED
			if (!received)
			{
				try { Thread.Sleep(1); }
				catch (System.Threading.ThreadInterruptedException) { return; }
			}
#endif
		}
	}

	/// <summary>
	/// Add a new player entry.
	/// </summary>

	TcpPlayer AddPlayer (Socket socket)
	{
		TcpPlayer player = new TcpPlayer();
		player.id = 0;
		player.name = "Guest";
		player.StartReceiving(socket);
		mPlayerList.Add(player);
		return player;
	}

	/// <summary>
	/// Remove the specified player.
	/// </summary>

	void RemovePlayer (TcpPlayer p)
	{
		if (p != null)
		{
			SavePlayer(p);
#if STANDALONE
			if (p.id != 0) Tools.Log(p.name + " (" + p.address + "): Disconnected [" + p.id + "]");
#endif
			LeaveAllChannels(p);

			p.Release();
			p.savePath = null;
			mPlayerList.Remove(p);

			if (p.udpEndPoint != null)
			{
				mDictionaryEP.Remove(p.udpEndPoint);
				p.udpEndPoint = null;
				p.udpIsUsable = false;
			}

			if (p.id != 0)
			{
				if (mPlayerDict.Remove(p.id))
				{
					if (lobbyLink != null) lobbyLink.SendUpdate(this);
					if (onPlayerDisconnect != null) onPlayerDisconnect(p);
				}
				p.id = 0;
			}
		}
	}

	/// <summary>
	/// Retrieve a player by their ID.
	/// </summary>

	TcpPlayer GetPlayer (int id)
	{
		TcpPlayer p = null;
		mPlayerDict.TryGetValue(id, out p);
		return p;
	}

	/// <summary>
	/// Retrieve a player by their name.
	/// </summary>

	TcpPlayer GetPlayer (string name)
	{
		if (!string.IsNullOrEmpty(name))
		{
			// Exact name match
			for (int i = 0; i < mPlayerList.size; ++i)
			{
				if (mPlayerList[i].name == name)
					return mPlayerList[i];
			}

			// Partial name match
			for (int i = 0; i < mPlayerList.size; ++i)
			{
				if (mPlayerList[i].name.IndexOf(name, StringComparison.CurrentCultureIgnoreCase) != -1)
					return mPlayerList[i];
			}

			// Alias match
			for (int i = 0; i < mPlayerList.size; ++i)
			{
				TcpPlayer p = mPlayerList[i];
				if (p.HasAlias(name)) return p;
			}
		}
		return null;
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
		player.udpIsUsable = false;
		if (udp != null) mDictionaryEP[udp] = player;
	}

	/// <summary>
	/// Create a new channel (or return an existing one).
	/// </summary>

	Channel CreateChannel (int channelID, out bool isNew)
	{
		Channel channel;

		if (mChannelDict.TryGetValue(channelID, out channel))
		{
			isNew = false;
			if (channel.closed) return null;
			return channel;
		}

		channel = new Channel();
		channel.id = channelID;
		mChannelList.Add(channel);
		mChannelDict[channelID] = channel;
		isNew = true;
		return channel;
	}

	/// <summary>
	/// Check to see if the specified channel exists.
	/// </summary>

	bool ChannelExists (int id) { return mChannelDict.ContainsKey(id); }

	/// <summary>
	/// Start the sending process.
	/// </summary>

	BinaryWriter BeginSend (Packet type)
	{
		mBuffer = Buffer.Create();
		BinaryWriter writer = mBuffer.BeginPacket(type);
		return writer;
	}

	/// <summary>
	/// Send the outgoing buffer to the specified remote destination.
	/// </summary>

	void EndSend (IPEndPoint ip)
	{
		mBuffer.EndPacket();
		mUdp.Send(mBuffer, ip);
		mBuffer.Recycle();
		mBuffer = null;
	}

	/// <summary>
	/// Send the outgoing buffer to the specified player.
	/// </summary>

	void EndSend (bool reliable, TcpPlayer player)
	{
		mBuffer.EndPacket();
		if (mBuffer.size > 1024) reliable = true;

		if (reliable || !player.udpIsUsable || player.udpEndPoint == null || !mAllowUdp)
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

	void EndSend (Channel channel, TcpPlayer exclude, bool reliable)
	{
		mBuffer.EndPacket();

		if (mBuffer.size > 1024) reliable = true;

		for (int i = 0; i < channel.players.size; ++i)
		{
			TcpPlayer player = (TcpPlayer)channel.players[i];

			if (player.stage == TcpProtocol.Stage.Connected && player != exclude)
			{
				if (reliable || !player.udpIsUsable || player.udpEndPoint == null || !mAllowUdp)
				{
					player.SendTcpPacket(mBuffer);
				}
				else mUdp.Send(mBuffer, player.udpEndPoint);
			}
		}

		mBuffer.Recycle();
		mBuffer = null;
	}

	List<TcpPlayer> mSentList = new List<TcpPlayer>();

	/// <summary>
	/// Send the outgoing buffer to all players in the same channels as the source player.
	/// </summary>

	void EndSendToOthers (TcpPlayer source, TcpPlayer exclude, bool reliable)
	{
		mBuffer.EndPacket();
		if (mBuffer.size > 1024) reliable = true;
		SendToOthers(mBuffer, source, exclude, reliable);
		mSentList.Clear();
		mBuffer.Recycle();
		mBuffer = null;
	}

	/// <summary>
	/// Send the specified buffer to all players in the same channel as the source.
	/// </summary>

	void SendToOthers (Buffer buffer, TcpPlayer source, TcpPlayer exclude, bool reliable)
	{
		for (int b = 0; b < source.channels.size; ++b)
		{
			Channel ch = source.channels[b];

			for (int i = 0; i < ch.players.size; ++i)
			{
				TcpPlayer p = (TcpPlayer)ch.players[i];

				if (p != exclude && !mSentList.Contains(p))
				{
					mSentList.Add(p);

					if (p.stage == TcpProtocol.Stage.Connected)
					{
						if (reliable || !p.udpIsUsable || p.udpEndPoint == null || !mAllowUdp)
						{
							p.SendTcpPacket(buffer);
						}
						else mUdp.Send(buffer, p.udpEndPoint);
					}
				}
			}
		}
	}

	/// <summary>
	/// Send the outgoing buffer to all connected players.
	/// </summary>

	void EndSend (bool reliable)
	{
		mBuffer.EndPacket();

		if (mBuffer.size > 1024) reliable = true;

		for (int i = 0; i < mChannelList.size; ++i)
		{
			Channel channel = mChannelList[i];

			for (int b = 0; b < channel.players.size; ++b)
			{
				TcpPlayer player = (TcpPlayer)channel.players[b];

				if (player.stage == TcpProtocol.Stage.Connected)
				{
					if (reliable || !player.udpIsUsable || player.udpEndPoint == null || !mAllowUdp)
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

	void SendToChannel (bool reliable, Channel channel, Buffer buffer)
	{
		mBuffer.MarkAsUsed();

		if (mBuffer.size > 1024) reliable = true;

		for (int i = 0; i < channel.players.size; ++i)
		{
			TcpPlayer player = (TcpPlayer)channel.players[i];

			if (player.stage == TcpProtocol.Stage.Connected)
			{
				if (reliable || !player.udpIsUsable || player.udpEndPoint == null || !mAllowUdp)
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

	void SendSetHost (Channel ch, TcpPlayer player)
	{
		if (ch != null && ch.host != player)
		{
			ch.host = player;
			BinaryWriter writer = BeginSend(Packet.ResponseSetHost);
			writer.Write(ch.id);
			writer.Write(player.id);
			EndSend(ch, null, true);
		}
	}

	// Temporary buffer used in SendLeaveChannel below
	List<uint> mTemp = new List<uint>();

	/// <summary>
	/// Leave all of the channels the player is in.
	/// </summary>

	void LeaveAllChannels (TcpPlayer player)
	{
		while (player.channels.size > 0)
		{
			Channel ch = player.channels[0];
			if (ch != null) SendLeaveChannel(player, ch, true);
			else player.channels.RemoveAt(0);
		}
	}

	/// <summary>
	/// Leave the channel the player is in.
	/// </summary>

	void SendLeaveChannel (TcpPlayer player, Channel ch, bool notify)
	{
		if (ch == null) return;

		// Remove this player from the channel
		ch.RemovePlayer(player, mTemp);

		if (player.channels.Remove(ch))
		{
			// Are there other players left?
			if (ch.players.size > 0)
			{
				BinaryWriter writer;

				// Inform the other players that the player's objects should be destroyed
				if (mTemp.size > 0)
				{
					writer = BeginSend(Packet.ResponseDestroyObject);
					writer.Write(ch.id);
					writer.Write((ushort)mTemp.size);
					for (int i = 0; i < mTemp.size; ++i) writer.Write(mTemp[i]);
					EndSend(ch, null, true);
				}

				// If this player was the host, choose a new host
				if (ch.host == null) SendSetHost(ch, (TcpPlayer)ch.players[0]);

				// Inform everyone of this player leaving the channel
				writer = BeginSend(Packet.ResponsePlayerLeft);
				writer.Write(ch.id);
				writer.Write(player.id);
				EndSend(ch, null, true);
			}
			else if (!ch.persistent)
			{
				// No other players left -- delete this channel
				mChannelDict.Remove(ch.id);
				mChannelList.Remove(ch);
			}

			// Notify the player that they have left the channel
			if (notify && player.isConnected)
			{
				BeginSend(Packet.ResponseLeaveChannel).Write(ch.id);
				EndSend(true, player);
			}
		}
	}

	/// <summary>
	/// Handles joining the specified channel.
	/// </summary>

	void SendJoinChannel (TcpPlayer player, int channelID, string pass, string levelName, bool persist, ushort playerLimit)
	{
		// Join a random existing channel or create a new one
		if (channelID == -2)
		{
			bool randomLevel = string.IsNullOrEmpty(levelName);
			channelID = -1;

			for (int i = 0; i < mChannelList.size; ++i)
			{
				Channel ch = mChannelList[i];

				if (ch.isOpen && (randomLevel || levelName.Equals(ch.level)) &&
					(string.IsNullOrEmpty(ch.password) || (ch.password == pass)))
				{
					channelID = ch.id;
					break;
				}
			}

			// If no level name has been specified and no channels were found, we're done
			if (randomLevel && channelID == -1)
			{
				BinaryWriter writer = BeginSend(Packet.ResponseJoinChannel);
				writer.Write(channelID);
				writer.Write(false);
				writer.Write("No suitable channels found");
				EndSend(true, player);
				return;
			}
		}

		// Join a random new channel
		if (channelID == -1)
		{
			channelID = 10001 + mRandom.Next(100000000);

			for (int i = 0; i < 1000; ++i)
			{
				if (!ChannelExists(channelID)) break;
				channelID = 10001 + mRandom.Next(100000000);
			}
		}

		if (player.channels.size == 0 || !player.IsInChannel(channelID))
		{
			bool isNew;
			Channel channel = CreateChannel(channelID, out isNew);

			if (channel == null || !channel.isOpen)
			{
				BinaryWriter writer = BeginSend(Packet.ResponseJoinChannel);
				writer.Write(channelID);
				writer.Write(false);
				writer.Write("The requested channel is closed");
				EndSend(true, player);
			}
			else if (isNew)
			{
				channel.password = pass;
				channel.persistent = persist;
				channel.level = levelName;
				channel.playerLimit = playerLimit;

				SendJoinChannel(player, channel, levelName);
			}
			else if (string.IsNullOrEmpty(channel.password) || (channel.password == pass))
			{
				SendJoinChannel(player, channel, channel.level);
			}
			else
			{
				BinaryWriter writer = BeginSend(Packet.ResponseJoinChannel);
				writer.Write(channelID);
				writer.Write(false);
				writer.Write("Wrong password");
				EndSend(true, player);
			}
		}
	}

	/// <summary>
	/// Join the specified channel.
	/// </summary>

	void SendJoinChannel (TcpPlayer player, Channel channel, string requestedLevelName)
	{
		if (player.IsInChannel(channel.id)) return;

		// Set the player's channel
		player.channels.Add(channel);

		// Everything else gets sent to the player, so it's faster to do it all at once
		Buffer buffer = Buffer.Create();

		// Tell the player who else is in the channel
		BinaryWriter writer = buffer.BeginPacket(Packet.ResponseJoiningChannel);
		{
			writer.Write(channel.id);
			writer.Write((short)channel.players.size);

			for (int i = 0; i < channel.players.size; ++i)
			{
				Player tp = channel.players[i];
				writer.Write(tp.id);

				if (!player.IsKnownTo(tp, channel))
				{
					writer.Write(true);
					writer.Write(string.IsNullOrEmpty(tp.name) ? "Guest" : tp.name);
					writer.Write(tp.dataNode);
				}
				else writer.Write(false);
			}
		}

		// End the first packet, but remember where it ended
		int offset = buffer.EndPacket();

		// Inform the player of who is hosting
		if (channel.host == null) channel.host = player;
		writer = buffer.BeginPacket(Packet.ResponseSetHost, offset);
		writer.Write(channel.id);
		writer.Write(channel.host.id);
		offset = buffer.EndTcpPacketStartingAt(offset);

		// Send the channel's data
		if (channel.dataNode != null)
		{
			writer = buffer.BeginPacket(Packet.ResponseSetChannelData, offset);
			writer.Write(channel.id);
			writer.Write("");
			writer.WriteObject(channel.dataNode);
			offset = buffer.EndTcpPacketStartingAt(offset);
		}

		// Send the LoadLevel packet, but only if some level name was specified in the original LoadLevel request.
		if (!string.IsNullOrEmpty(requestedLevelName) && !string.IsNullOrEmpty(channel.level))
		{
			writer = buffer.BeginPacket(Packet.ResponseLoadLevel, offset);
			writer.Write(channel.id);
			writer.Write(channel.level);
			offset = buffer.EndTcpPacketStartingAt(offset);
		}

		// Send the list of objects that have been created
		for (int i = 0; i < channel.created.size; ++i)
		{
			Channel.CreatedObject obj = channel.created.buffer[i];

			bool isPresent = false;

			for (int b = 0; b < channel.players.size; ++b)
			{
				if (channel.players[b].id == obj.playerID)
				{
					isPresent = true;
					break;
				}
			}

			// If the previous owner is not present, transfer ownership to the host
			if (!isPresent) obj.playerID = channel.host.id;

			writer = buffer.BeginPacket(Packet.ResponseCreateObject, offset);
			writer.Write(obj.playerID);
			writer.Write(channel.id);
			writer.Write(obj.objectID);
			writer.Write(obj.buffer.buffer, obj.buffer.position, obj.buffer.size);
			offset = buffer.EndTcpPacketStartingAt(offset);
		}

		// Send the list of objects that have been destroyed
		if (channel.destroyed.size != 0)
		{
			writer = buffer.BeginPacket(Packet.ResponseDestroyObject, offset);
			writer.Write(channel.id);
			writer.Write((ushort)channel.destroyed.size);
			for (int i = 0; i < channel.destroyed.size; ++i)
				writer.Write(channel.destroyed.buffer[i]);
			offset = buffer.EndTcpPacketStartingAt(offset);
		}

		// Send all buffered RFCs to the new player
		for (int i = 0; i < channel.rfcs.size; ++i)
		{
			Channel.RFC rfc = channel.rfcs[i];
			offset = rfc.WritePacket(channel.id, buffer, offset);
		}

		// Inform the player that the channel is now locked
		if (channel.isLocked)
		{
			writer = buffer.BeginPacket(Packet.ResponseLockChannel, offset);
			writer.Write(channel.id);
			writer.Write(true);
			offset = buffer.EndTcpPacketStartingAt(offset);
		}

		// The join process is now complete
		buffer.BeginPacket(Packet.ResponseJoinChannel, offset);
		writer.Write(channel.id);
		writer.Write(true);
		offset = buffer.EndTcpPacketStartingAt(offset);

		// Send the entire buffer
		player.SendTcpPacket(buffer);
		buffer.Recycle();

		// Inform the channel that a new player is joining
		for (int i = 0; i < channel.players.size; ++i)
		{
			TcpPlayer p = (TcpPlayer)channel.players[i];

			writer = p.BeginSend(Packet.ResponsePlayerJoined);
			{
				writer.Write(channel.id);
				writer.Write(player.id);

				if (!player.IsKnownTo(p, channel))
				{
					writer.Write(true);
					writer.Write(string.IsNullOrEmpty(player.name) ? "Guest" : player.name);
					writer.Write(player.dataNode);
				}
				else writer.Write(false);
			}
			p.EndSend();
		}

		// Add this player to the channel now that the joining process is complete
		channel.players.Add(player);
	}

	/// <summary>
	/// Extra verification steps, if necessary.
	/// </summary>

	protected virtual bool Verify (BinaryReader reader) { return true; }

	/// <summary>
	/// Receive and process a single incoming packet.
	/// Returns 'true' if a packet was received, 'false' otherwise.
	/// </summary>

	bool ProcessPlayerPacket (Buffer buffer, TcpPlayer player, bool reliable)
	{
		// Save every 30 seconds
		if (mNextSave == 0) mNextSave = mTime + 30000;
		BinaryReader reader = buffer.BeginReading();

		// If the player has not yet been verified, the first packet must be an ID request
		if (player.stage == TcpProtocol.Stage.Verifying)
		{
			if (player.VerifyRequestID(reader, buffer, true))
			{
				player.isAdmin = (player.address == null ||
					player.address == "0.0.0.0:0" ||
					player.address.StartsWith("127.0.0.1:"));

				if (player.isAdmin || !mBan.Contains(player.name))
				{
					mPlayerDict.Add(player.id, player);

					BinaryWriter writer = player.BeginSend(Packet.ResponseID);
					writer.Write(TcpPlayer.version);
					writer.Write(player.id);
					writer.Write((Int64)(System.DateTime.UtcNow.Ticks / 10000));
					player.EndSend();

					if (mServerData != null)
					{
						writer = player.BeginSend(Packet.ResponseSetServerData);
						writer.Write("");
						writer.WriteObject(mServerData);
						player.EndSend();
					}

					if (player.isAdmin)
					{
						player.BeginSend(Packet.ResponseVerifyAdmin).Write(player.id);
						player.EndSend();
					}

					if (lobbyLink != null) lobbyLink.SendUpdate(this);
					if (onPlayerConnect != null) onPlayerConnect(player);
					return true;
				}
				else
				{
					player.Log("User is banned");
					RemovePlayer(player);
					return false;
				}
			}

			BeginSend(Packet.ResponseID).Write(0);
			EndSend(true, player);

			Tools.Print(player.address + " has failed the verification step");
			RemovePlayer(player);
			return false;
		}

		Packet request = (Packet)reader.ReadByte();

#if DEBUG_PACKETS && !STANDALONE
 #if UNITY_EDITOR
		if (request != Packet.RequestPing && request != Packet.ResponsePing)
			UnityEngine.Debug.Log("Server: " + request + " (" + buffer.size.ToString("N0") + " bytes)");
 #elif !SINGLE_THREADED
		if (request != Packet.RequestPing && request != Packet.ResponsePing)
			Tools.Print("Server: " + request + " (" + buffer.size.ToString("N0") + " bytes)");
 #endif
#endif
		switch (request)
		{
			case Packet.Empty:
			{
				break;
			}
			case Packet.Error:
			{
				player.LogError(reader.ReadString());
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
				player.BeginSend(Packet.ResponsePing);
				player.EndSend();
				break;
			}
			case Packet.RequestSetUDP:
			{
				int port = reader.ReadUInt16();

				if (port != 0 && mUdp.isActive && player.tcpEndPoint != null)
				{
					IPAddress ip = new IPAddress(player.tcpEndPoint.Address.GetAddressBytes());
					SetPlayerUdpEndPoint(player, new IPEndPoint(ip, port));
				}
				else SetPlayerUdpEndPoint(player, null);

				// Let the player know if we are hosting an active UDP connection
				ushort udp = mUdp.isActive ? (ushort)mUdp.listeningPort : (ushort)0;
				player.BeginSend(Packet.ResponseSetUDP).Write(udp);
				player.EndSend();

				// Send an empty packet to the target player to open up UDP for communication
				if (player.udpEndPoint != null) mUdp.SendEmptyPacket(player.udpEndPoint);
				break;
			}
			case Packet.RequestActivateUDP:
			{
				player.udpIsUsable = true;
				if (player.udpEndPoint != null) mUdp.SendEmptyPacket(player.udpEndPoint);
				break;
			}
			case Packet.RequestJoinChannel:
			{
				// Join the specified channel
				int		channelID	= reader.ReadInt32();
				string	pass		= reader.ReadString();
				string	levelName	= reader.ReadString();
				bool	persist		= reader.ReadBoolean();
				ushort	playerLimit = reader.ReadUInt16();

				if (mServerData != null)
				{
					int min = mServerData.GetChild<int>("minAlias", 0);
					int aliasCount = (player.aliases == null ? 0 : player.aliases.size);

					if (aliasCount < min)
					{
						player.Log("Player has " + aliasCount + " aliases, expected at least " + min);
						RemovePlayer(player);
						return false;
					}
				}

				SendJoinChannel(player, channelID, pass, levelName, persist, playerLimit);
				break;
			}
			case Packet.RequestSetName:
			{
				// Change the player's name
				player.name = reader.ReadString();

				if (mBan.Contains(player.name))
				{
					player.Log("FAILED a ban check: " + player.name);
					RemovePlayer(player);
					break;
				}

				BinaryWriter writer = BeginSend(Packet.ResponseRenamePlayer);
				writer.Write(player.id);
				writer.Write(player.name);
				EndSendToOthers(player, null, true);
				break;
			}
			case Packet.RequestSetPlayerData:
			{
				// 4 bytes for the size, 1 byte for the ID
				int origin = buffer.position - 5;

				// Set the local data
				int playerID = reader.ReadInt32();
				string str = reader.ReadString();
				object obj = reader.ReadObject();

				if (player.id == playerID)
				{
					player.Set(str, obj);
					player.saveNeeded = true;

					// Change the packet type to a response before sending it as-is
					buffer.buffer[origin + 4] = (byte)Packet.ResponseSetPlayerData;
					buffer.position = origin;

					// Forward the packet to everyone that knows this player
					for (int i = 0; i < mPlayerList.size; ++i)
					{
						TcpPlayer tp = mPlayerList[i];
						if (tp != player && tp.IsKnownTo(player)) tp.SendTcpPacket(buffer);
					}
				}
				else player.LogError("Players should only set their own data. Ignoring.", null, false);
				break;
			}
			case Packet.RequestSetPlayerSave:
			{
				// Delete the previous save
				if (!string.IsNullOrEmpty(player.savePath))
					Tools.DeleteFile(player.savePath);

				// Load and set the player's data from the specified file
				player.savePath = reader.ReadString();
				player.saveType = (DataNode.SaveType)reader.ReadByte();
				player.dataNode = DataNode.Read(player.savePath);
				player.saveNeeded = false;

				Buffer buff = Buffer.Create();
				BinaryWriter writer = buff.BeginPacket(Packet.ResponseSetPlayerData);
				writer.Write(player.id);
				writer.Write("");
				writer.WriteObject(player.dataNode);
				buff.EndPacket();

				player.SendTcpPacket(buff);
				SendToOthers(buff, player, player, true);
				buff.Recycle();
				break;
			}
			case Packet.RequestSaveFile:
			{
				try
				{
					string fileName = reader.ReadString();
					byte[] data = reader.ReadBytes(reader.ReadInt32());
					
					if (!string.IsNullOrEmpty(fileName))
					{
						if (data == null || data.Length == 0)
						{
							if (DeleteFile(fileName))
								player.Log("Deleted " + fileName);
						}
						else if (SaveFile(fileName, data))
						{
							player.Log("Saved " + fileName + " (" + (data != null ? data.Length.ToString("N0") : "0") + " bytes)");
						}
						else player.LogError("Unable to save " + fileName);
					}
				}
				catch (Exception ex)
				{
					player.LogError(ex.Message, ex.StackTrace);
					RemovePlayer(player);
				}
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
				else writer.Write(0);

				EndSend(true, player);
				break;
			}
			case Packet.RequestDeleteFile:
			{
				string fileName = reader.ReadString();

				if (!string.IsNullOrEmpty(fileName))
				{
					if (DeleteFile(fileName))
						player.Log("Deleted " + fileName);
				}
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

				int count = 0;
				for (int i = 0; i < mChannelList.size; ++i)
					if (!mChannelList[i].closed) ++count;

				writer.Write(count);

				for (int i = 0; i < mChannelList.size; ++i)
				{
					Channel ch = mChannelList[i];

					if (!ch.closed)
					{
						writer.Write(ch.id);
						writer.Write((ushort)ch.players.size);
						writer.Write(ch.playerLimit);
						writer.Write(!string.IsNullOrEmpty(ch.password));
						writer.Write(ch.persistent);
						writer.Write(ch.level);
						writer.Write(ch.dataNode);
					}
				}
				EndSend(true, player);
				break;
			}
			case Packet.ServerLog:
			{
#if UNITY_EDITOR
				reader.ReadString();
#else
				string s = reader.ReadString();
				player.Log(s);
#endif
				break;
			}
			case Packet.RequestSetTimeout:
			{
				// The passed value is in seconds, but the stored value is in milliseconds (to avoid a math operation)
				player.timeoutTime = reader.ReadInt32() * 1000;
				break;
			}
			case Packet.ForwardToPlayer:
			{
				// Forward this packet to the specified player
				int start = buffer.position - 5;

				if (reader.ReadInt32() == player.id) // Validate the packet's source
				{
					TcpPlayer target = GetPlayer(reader.ReadInt32());

					if (target != null && target.isConnected)
					{
						buffer.position = start;
						target.SendTcpPacket(buffer);
					}
				}
				break;
			}
			case Packet.ForwardByName:
			{
				int start = buffer.position - 5;

				if (reader.ReadInt32() == player.id) // Validate the packet's source
				{
					string name = reader.ReadString();
					TcpPlayer target = GetPlayer(name);

					if (target != null && target.isConnected)
					{
						buffer.position = start;
						target.SendTcpPacket(buffer);
					}
					else if (reliable)
					{
						BeginSend(Packet.ForwardTargetNotFound).Write(name);
						EndSend(true, player);
					}
				}
				break;
			}
			case Packet.BroadcastAdmin:
			case Packet.Broadcast:
			{
				// 4 bytes for the size, 1 byte for the ID
				int origin = buffer.position - 5;

				//Tools.Print("Broadcast: " + player.name + ", " + player.address);

				if (!player.isAdmin)
				{
					if (player.nextBroadcast < mTime)
					{
						player.nextBroadcast = mTime + 500;
						player.broadcastCount = 0;
					}
					else if (++player.broadcastCount > 5)
					{
						player.Log("SPAM filter trigger!");
						RemovePlayer(player);
						break;
					}
					else if (player.broadcastCount > 2)
					{
						player.Log("Possible spam!");
					}
				}

				int playerID = reader.ReadInt32();

				// Exploit: echoed packet of another player
				if (playerID != player.id)
				{
					player.LogError("Tried to echo a broadcast packet (" + playerID + " vs " + player.id + ")", null);
					RemovePlayer(player);
					break;
				}

				buffer.position = origin;

				// Forward the packet to everyone connected to the server
				for (int i = 0; i < mPlayerList.size; ++i)
				{
					TcpPlayer tp = mPlayerList[i];
					if (!tp.isConnected) continue;
					if (request == Packet.BroadcastAdmin && !tp.isAdmin) continue;

					if (reliable || !tp.udpIsUsable || tp.udpEndPoint == null || !mAllowUdp)
					{
						tp.SendTcpPacket(buffer);
					}
					else mUdp.Send(buffer, tp.udpEndPoint);
				}
				break;
			}
			case Packet.RequestVerifyAdmin:
			{
				string pass = reader.ReadString();

				if (!string.IsNullOrEmpty(pass) && mAdmin.Contains(pass))
				{
					if (!player.isAdmin)
					{
						player.isAdmin = true;
						player.Log("Admin verified");
						player.BeginSend(Packet.ResponseVerifyAdmin).Write(player.id);
						player.EndSend();
					}
				}
				else
				{
					player.LogError("Tried to authenticate as admin and failed (" + pass + ")");
					RemovePlayer(player);
				}
				break;
			}
			case Packet.RequestCreateAdmin:
			{
				string s = reader.ReadString();

				if (player.isAdmin)
				{
					if (!mAdmin.Contains(s)) mAdmin.Add(s);
					player.Log("Added an admin (" + s + ")");
					Tools.SaveList("ServerConfig/admin.txt", mAdmin);
				}
				else
				{
					player.LogError("Tried to add an admin (" + s + ") and failed");
					RemovePlayer(player);
				}
				break;
			}
			case Packet.RequestRemoveAdmin:
			{
				string s = reader.ReadString();

				// First administrator can't be removed
				if (player.isAdmin && (mAdmin.size == 0 || mAdmin[0] != s))
				{
					mAdmin.Remove(s);
					player.Log("Removed an admin (" + s + ")");
					Tools.SaveList("ServerConfig/admin.txt", mAdmin);
				}
				else
				{
					player.LogError("Tried to remove an admin (" + s + ") without authorization", null);
					RemovePlayer(player);
				}
				break;
			}
			case Packet.RequestSetAlias:
			{
				string s = reader.ReadString();
				if (!SetAlias(player, s)) break;

				if (mAdmin.Contains(s))
				{
					player.isAdmin = true;
					player.Log("Admin verified");
					player.BeginSend(Packet.ResponseVerifyAdmin).Write(player.id);
					player.EndSend();
				}

				if (mServerData != null)
				{
					int max = mServerData.GetChild<int>("maxAlias", int.MaxValue);
					int aliasCount = (player.aliases == null ? 0 : player.aliases.size);

					if (aliasCount > max)
					{
						player.Log("Player has " + aliasCount + "/" + max + " aliases");
						RemovePlayer(player);
						return false;
					}
				}
				break;
			}
			case Packet.RequestUnban:
			{
				string s = reader.ReadString();

				if (player.isAdmin)
				{
					mBan.Remove(s);
					Tools.SaveList("ServerConfig/ban.txt", mBan);
					player.Log("Removed an banned keyword (" + s + ")");
				}
				else
				{
					player.LogError("Tried to unban (" + s + ") without authorization", null);
					RemovePlayer(player);
				}
				break;
			}
			case Packet.RequestSetBanList:
			{
				string s = reader.ReadString();

				if (player.isAdmin)
				{
					if (!string.IsNullOrEmpty(s))
					{
						string[] lines = s.Split('\n');
						mBan.Clear();
						for (int i = 0; i < lines.Length; ++i) mBan.Add(lines[i]);
					}
					else mBan.Clear();
				}
				else
				{
					player.LogError("Tried to set the ban list without authorization", null);
					RemovePlayer(player);
				}
				break;
			}
			case Packet.RequestReloadServerConfig:
			{
				if (player.isAdmin)
				{
					Tools.LoadList("ServerConfig/ban.txt", mBan);
					Tools.LoadList("ServerConfig/admin.txt", mAdmin);
					LoadConfig();

					if (mServerData == null) mServerData = new DataNode("Version", Player.version);

					Buffer buff = Buffer.Create();
					var writer = buff.BeginPacket(Packet.ResponseSetServerData);
					writer.Write("");
					writer.WriteObject(mServerData);
					buff.EndPacket();

					// Forward the packet to everyone connected to the server
					for (int i = 0; i < mPlayerList.size; ++i)
					{
						TcpPlayer tp = mPlayerList[i];
						tp.SendTcpPacket(buff);
					}
					buff.Recycle();
				}
				else
				{
					player.LogError("Tried to request reloaded server data without authorization", null);
					RemovePlayer(player);
				}
				break;
			}
			case Packet.RequestSetServerData:
			{
				if (player.isAdmin)
				{
					if (mServerData == null) mServerData = new DataNode("Version", Player.version);

					// 4 bytes for size, 1 byte for ID
					int origin = buffer.position - 5;

					// Change the local configuration
					mServerData.SetHierarchy(reader.ReadString(), reader.ReadObject());
					mServerDataChanged = true;

					// Change the packet type to a response before sending it as-is
					buffer.buffer[origin + 4] = (byte)Packet.ResponseSetServerData;
					buffer.position = origin;

					// Forward the packet to everyone connected to the server
					for (int i = 0; i < mPlayerList.size; ++i)
					{
						TcpPlayer tp = mPlayerList[i];
						tp.SendTcpPacket(buffer);
					}
				}
				else
				{
					player.LogError("Tried to set the server data without authorization", null);
					RemovePlayer(player);
				}
				break;
			}
			case Packet.RequestKick:
			{
				int channelID = reader.ReadInt32();
				int id = reader.ReadInt32();
				string s = (id != 0) ? null : reader.ReadString();
				TcpPlayer other = (id != 0) ? GetPlayer(id) : GetPlayer(s);

				if (player.isAdmin)
				{
					player.Log(player.name + " kicked " + other.name + " (" + other.address + ")");
					LeaveAllChannels(other);
				}
				else
				{
					Channel ch;

					if (mChannelDict.TryGetValue(channelID, out ch) && ch != null && ch.host == player)
					{
						player.Log(player.name + " kicked " + other.name + " (" + other.address + ") from channel " + channelID);
						SendLeaveChannel(other, ch, true);
					}
				}
				break;
			}
			case Packet.RequestBan:
			{
				int id = reader.ReadInt32();
				string s = (id != 0) ? null : reader.ReadString();
				TcpPlayer other = (id != 0) ? GetPlayer(id) : GetPlayer(s);

				bool playerBan = (other == player && mServerData != null && mServerData.GetChild<bool>("playersCanBan"));

				if (player.isAdmin || playerBan)
				{
					if (other != null)
					{
						Ban(player, other);
					}
					else if (id == 0)
					{
						player.Log("BANNED " + s);
						string banText = "// [" + s + "] banned by [" + player.name + "]- " + (player.aliases != null &&
							player.aliases.size > 0 ? player.aliases[0] : player.address);
						AddUnique(mBan, banText);
						AddUnique(mBan, s);
						Tools.SaveList("ServerConfig/ban.txt", mBan);
					}
				}
				else if (!playerBan)
				{
					// Do nothing -- players can't ban other players, even themselves for security reasons
				}
				else
				{
					player.LogError("Tried to ban " + (other != null ? other.name : s) + " without authorization", null);
					RemovePlayer(player);
				}
				break;
			}
			case Packet.RequestGetFileList:
			{
				string original = reader.ReadString();
				string path = Tools.FindDirectory(original, player.isAdmin);

				BinaryWriter writer = player.BeginSend(Packet.ResponseGetFileList);
				writer.Write(original);

				if (!string.IsNullOrEmpty(path))
				{
					string[] files = Tools.GetFiles(path);
					writer.Write(files.Length);
					for (int i = 0, imax = files.Length; i < imax; ++i)
						writer.Write(files[i]);
				}
				else writer.Write(0);

				player.EndSend();
				break;
			}
			case Packet.RequestLockChannel:
			{
				int channelID = reader.ReadInt32();
				Channel ch = null;
				mChannelDict.TryGetValue(channelID, out ch);
				bool locked = reader.ReadBoolean();

				if (ch != null)
				{
					if (player.isAdmin)
					{
						ch.isLocked = locked;
						BinaryWriter writer = BeginSend(Packet.ResponseLockChannel);
						writer.Write(ch.id);
						writer.Write(locked);
						EndSend(true);
					}
					else
					{
						player.LogError("RequestLockChannel(" + ch.id + ", " + locked + ") without authorization", null);
						RemovePlayer(player);
					}
				}
				break;
			}
			case Packet.RequestHTTPGet:
			{
				if (player.stage == TcpProtocol.Stage.WebBrowser)
				{
					// string requestText = reader.ReadString();
					// Example of an HTTP request:
					// GET / HTTP/1.1
					// Host: 127.0.0.1:5127
					// Connection: keep-alive
					// User-Agent: Chrome/47.0.2526.80

					StringBuilder sb = new StringBuilder();

					// Server name
					sb.Append("Name: ");
					sb.AppendLine(name);

					// Number of connected clients
					sb.Append("Clients: ");
					sb.AppendLine(playerCount.ToString());

					// Detailed list of clients
					for (int i = 0, count = 0; i < mPlayerList.size; ++i)
					{
						TcpPlayer p = (TcpPlayer)mPlayerList[i];

						if (p.stage == TcpProtocol.Stage.Connected)
						{
							sb.Append(++count);
							sb.Append(" ");
							sb.AppendLine(p.name);
						}
					}

					// Create the header indicating that the connection should be severed after receiving the data
					string text = sb.ToString();
					sb = new StringBuilder();
					sb.AppendLine("HTTP/1.1 200 OK");
					sb.AppendLine("Server: TNet 3");
					sb.AppendLine("Content-Length: " + text.Length);
					sb.AppendLine("Content-Type: text/plain");
					sb.AppendLine("Connection: Closed\n");
					sb.Append(text);

					// Send the response
					mBuffer = Buffer.Create();
					BinaryWriter bw = mBuffer.BeginWriting(false);
					bw.Write(Encoding.ASCII.GetBytes(sb.ToString()));
					player.SendTcpPacket(mBuffer);
					mBuffer.Recycle();
					mBuffer = null;
				}
				break;
			}
			case Packet.RequestRenameServer:
			{
				name = reader.ReadString();
				break;
			}
			default:
			{
				if (player.channels.size != 0 && (int)request < (int)Packet.UserPacket)
				{
					// Other packets can only be processed while in a channel
					if (request >= Packet.ForwardToAll && request < Packet.ForwardToPlayer)
					{
						ProcessForwardPacket(player, buffer, reader, request, reliable);
					}
					else
					{
						ProcessChannelPacket(player, buffer, reader, request);
					}
				}
				else if (onCustomPacket != null)
				{
					onCustomPacket(player, buffer, reader, request, reliable);
				}
				break;
			}
		}
		return true;
	}

	/// <summary>
	/// Set an alias and check it against the ban list.
	/// </summary>

	bool SetAlias (TcpPlayer player, string s)
	{
		if (mBan.Contains(s))
		{
			player.Log("FAILED a ban check: " + s);
			RemovePlayer(player);
			return false;
		}
		else
		{
			if (s.Length == 17 && s.StartsWith("7656"))
			{
				long sid;

				if (long.TryParse(s, out sid))
				{
					if (sid > 76561199999999999)
					{
						player.Log("FAILED a ban check: " + s);
						return false;
					}
				}
			}

			player.Log("Passed a ban check: " + s);
			if (player.aliases == null) player.aliases = new List<string>();
			AddUnique(player.aliases, s);
			return true;
		}
	}

	/// <summary>
	/// Ban the specified player.
	/// </summary>

	void Ban (TcpPlayer player, TcpPlayer other)
	{
		player.Log("BANNED " + other.name + " (" + (other.aliases != null &&
			other.aliases.size > 0 ? other.aliases[0] : other.address) + ")");

		// Just to show the name of the player
		string banText = "// [" + other.name + "]";

		if (player != other)
		{
			banText += " banned by [" + player.name + "]- " + (other.aliases != null &&
				player.aliases.size > 0 ? player.aliases[0] : player.address);
		}

		AddUnique(mBan, banText);
		AddUnique(mBan, other.tcpEndPoint.Address.ToString());

		if (other.aliases != null)
			for (int i = 0; i < other.aliases.size; ++i)
				AddUnique(mBan, other.aliases[i]);

		Tools.SaveList("ServerConfig/ban.txt", mBan);
		RemovePlayer(other);
	}

	/// <summary>
	/// Process a packet that's meant to be forwarded.
	/// </summary>

	void ProcessForwardPacket (TcpPlayer player, Buffer buffer, BinaryReader reader, Packet request, bool reliable)
	{
		// 4 bytes for packet size, 1 byte for packet ID
		int start = buffer.position - 5;
		int playerID = reader.ReadInt32();
		int channelID = reader.ReadInt32();

		// Exploit: echoed packet of another player
		if (playerID != player.id)
		{
			player.LogError("Tried to echo a " + request + " packet (" + playerID + " vs " + player.id + ")", null);
			RemovePlayer(player);
			return;
		}

		// The channel must exist
		Channel ch;
		mChannelDict.TryGetValue(channelID, out ch);
		if (ch == null) return;

		// We can't send unreliable packets if UDP is not active
		if (!mUdp.isActive || buffer.size > 1024) reliable = true;

		if (request == Packet.ForwardToHost)
		{
			TcpPlayer host = (TcpPlayer)ch.host;
			if (host == null) return;
			buffer.position = start;

			// Forward the packet to the channel's host
			if (reliable || !player.udpIsUsable || host.udpEndPoint == null || !mAllowUdp)
			{
				host.SendTcpPacket(buffer);
			}
			else mUdp.Send(buffer, host.udpEndPoint);
		}
		else
		{
			// We want to exclude the player if the request was to forward to others
			TcpPlayer exclude = (
				request == Packet.ForwardToOthers ||
				request == Packet.ForwardToOthersSaved) ? player : null;

			// If the request should be saved, let's do so
			if (request == Packet.ForwardToAllSaved || request == Packet.ForwardToOthersSaved)
			{
				if (!ch.isLocked || player.isAdmin)
				{
					uint target = reader.ReadUInt32();
					string funcName = ((target & 0xFF) == 0) ? reader.ReadString() : null;
					ch.AddRFC(target, funcName, buffer);
				}
			}

			buffer.position = start;

			// Forward the packet to everyone except the sender
			for (int i = 0; i < ch.players.size; ++i)
			{
				TcpPlayer tp = (TcpPlayer)ch.players[i];
					
				if (tp != exclude)
				{
					if (reliable || !tp.udpIsUsable || tp.udpEndPoint == null || !mAllowUdp)
					{
						tp.SendTcpPacket(buffer);
					}
					else mUdp.Send(buffer, tp.udpEndPoint);
				}
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
			case Packet.RequestCreateObject:
			{
				int playerID = reader.ReadInt32();

				// Exploit: echoed packet of another player
				if (playerID != player.id)
				{
					player.LogError("Tried to echo a create packet (" + playerID + " vs " + player.id + ")", null);
					RemovePlayer(player);
					return;
				}

				int channelID = reader.ReadInt32();
				Channel ch = player.GetChannel(channelID);
				byte type = reader.ReadByte();

				if (ch != null && (!ch.isLocked || player.isAdmin))
				{
					uint uniqueID = 0;

					if (type != 0)
					{
						uniqueID = ch.GetUniqueID();

						Channel.CreatedObject obj = new Channel.CreatedObject();
						obj.playerID = player.id;
						obj.objectID = uniqueID;
						obj.type = type;

						if (buffer.size > 0)
						{
							buffer.MarkAsUsed();
							obj.buffer = buffer;
						}
						ch.AddCreatedObject(obj);
					}

					// Inform the channel
					BinaryWriter writer = BeginSend(Packet.ResponseCreateObject);
					writer.Write(playerID);
					writer.Write(channelID);
					writer.Write(uniqueID);
					writer.Write(buffer.buffer, buffer.position, buffer.size);
					EndSend(ch, null, true);
				}
				break;
			}
			case Packet.RequestDestroyObject:
			{
				Channel ch = player.GetChannel(reader.ReadInt32());
				uint objectID = reader.ReadUInt32();

				if (ch != null && (!ch.isLocked || player.isAdmin) && ch.DestroyObject(objectID))
				{
					// Inform all players in the channel that the object should be destroyed
					BinaryWriter writer = BeginSend(Packet.ResponseDestroyObject);
					writer.Write(ch.id);
					writer.Write((ushort)1);
					writer.Write(objectID);
					EndSend(ch, null, true);
				}
				break;
			}
			case Packet.RequestTransferObject:
			{
				bool isNew;
				Channel from = player.GetChannel(reader.ReadInt32());
				Channel to = CreateChannel(reader.ReadInt32(), out isNew);
				uint objectID = reader.ReadUInt32();

				if (from != null && to != null && from != to)
				{
					Channel.CreatedObject obj = from.TransferObject(objectID, to);

					if (obj != null)
					{
						// Notify players in the old channel
						for (int i = 0; i < from.players.size; ++i)
						{
							TcpPlayer p = (TcpPlayer)from.players[i];

							if (to.players.Contains(p))
							{
								// The player is also present in the other channel -- inform them of the transfer
								BinaryWriter writer = p.BeginSend(Packet.ResponseTransferObject);
								writer.Write(from.id);
								writer.Write(to.id);
								writer.Write(objectID);
								writer.Write(obj.objectID);
								p.EndSend();
							}
							else
							{
								// The player is not present in the other channel -- delete this object
								BinaryWriter writer = p.BeginSend(Packet.ResponseDestroyObject);
								writer.Write(from.id);
								writer.Write((ushort)1);
								writer.Write(objectID);
								p.EndSend();
							}
						}

						// Notify players in the new channel
						for (int i = 0; i < to.players.size; ++i)
						{
							TcpPlayer p = (TcpPlayer)to.players[i];

							if (!from.players.Contains(p))
							{
								Buffer temp = Buffer.Create();

								// Object creation notification
								BinaryWriter writer = temp.BeginPacket(Packet.ResponseCreateObject);
								writer.Write(obj.playerID);
								writer.Write(to.id);
								writer.Write(obj.objectID);
								writer.Write(obj.buffer.buffer, obj.buffer.position, obj.buffer.size);
								int offset = temp.EndPacket();

								// Send all buffered RFCs associated with this object
								for (int b = 0; b < to.rfcs.size; ++b)
								{
									Channel.RFC rfc = to.rfcs[b];
									if (rfc.objectID == obj.objectID)
										offset = rfc.WritePacket(to.id, temp, offset);
								}

								p.SendTcpPacket(temp);
								temp.Recycle();
							}
						}
					}
				}
				break;
			}
			case Packet.RequestLoadLevel:
			{
				Channel ch = player.GetChannel(reader.ReadInt32());
				string lvl = reader.ReadString();

				// Change the currently loaded level
				if (ch.host == player && ch != null && (!ch.isLocked || player.isAdmin))
				{
					ch.Reset();
					ch.level = lvl;

					BinaryWriter writer = BeginSend(Packet.ResponseLoadLevel);
					writer.Write(ch.id);
					writer.Write(string.IsNullOrEmpty(ch.level) ? "" : ch.level);
					EndSend(ch, null, true);
				}
				break;
			}
			case Packet.RequestSetHost:
			{
				Channel ch = player.GetChannel(reader.ReadInt32());
				int pid = reader.ReadInt32();

				// Transfer the host state from one player to another
				if (ch != null && ch.host == player)
				{
					TcpPlayer newHost = GetPlayer(pid);
					if (newHost != null && newHost.IsInChannel(ch.id))
						SendSetHost(ch, newHost);
				}
				break;
			}
			case Packet.RequestLeaveChannel:
			{
				Channel ch = player.GetChannel(reader.ReadInt32());
				if (ch != null) SendLeaveChannel(player, ch, true);
				break;
			}
			case Packet.RequestCloseChannel:
			{
				Channel ch = player.GetChannel(reader.ReadInt32());

				if (ch != null && (mServerData == null || mServerData.GetChild<bool>("canCloseChannels", true)))
				{
					if (!ch.persistent && ch.host == player)
					{
						ch.persistent = false;
						ch.closed = true;
					}
					else if (player.isAdmin)
					{
						player.Log("Closing channel " + ch.id);
						ch.persistent = false;
						ch.closed = true;
					}
					else
					{
						player.LogError("Tried to call a close channel " + ch.id + " while not authorized", null);
						RemovePlayer(player);
					}
				}
				break;
			}
			case Packet.RequestDeleteChannel:
			{
				int id = reader.ReadInt32();
				bool dc = reader.ReadBoolean();

				if (player.isAdmin)
				{
					player.Log("Deleting channel " + id);

					Channel ch;
					
					if (mChannelDict.TryGetValue(id, out ch))
					{
						for (int b = ch.players.size; b > 0; )
						{
							TcpPlayer p = (TcpPlayer)ch.players[--b];

							if (p != null)
							{
								if (dc) RemovePlayer(p);
								else SendLeaveChannel(p, ch, true);
							}
						}

						ch.persistent = false;
						ch.closed = true;
						ch.Reset();

						mChannelDict.Remove(id);
						mChannelList.Remove(ch);
					}
				}
				else
				{
					player.LogError("Tried to call a delete a channel #" + id + " while not authorized", null);
					RemovePlayer(player);
				}
				break;
			}
			case Packet.RequestSetPlayerLimit:
			{
				Channel ch;
				mChannelDict.TryGetValue(reader.ReadInt32(), out ch);
				ushort limit = reader.ReadUInt16();

				if (ch != null)
				{
					if (player.isAdmin || mServerData == null ||
						(ch.host == player && mServerData.GetChild<bool>("hostCanSetPlayerLimit", true)))
					{
						ch.playerLimit = limit;
					}
				}
				break;
			}
			case Packet.RequestRemoveRFC:
			{
				Channel ch = player.GetChannel(reader.ReadInt32());
				uint id = reader.ReadUInt32();
				string funcName = ((id & 0xFF) == 0) ? reader.ReadString() : null;
				if (ch != null && (!ch.isLocked || player.isAdmin))
					ch.DeleteRFC(id, funcName);
				break;
			}
			case Packet.RequestSetChannelData:
			{
				// 4 bytes for the size, 1 byte for the ID
				int origin = buffer.position - 5;

				bool isNew;
				Channel ch = CreateChannel(reader.ReadInt32(), out isNew);

				if (ch != null && (!ch.isLocked || player.isAdmin))
				{
					if (ch.players.size == 0) ch.persistent = true;
					if (ch.dataNode == null) ch.dataNode = new DataNode("Version", Player.version);

					// Set the local data
					ch.dataNode.SetHierarchy(reader.ReadString(), reader.ReadObject());

					// Change the packet type to a response before sending it as-is
					buffer.buffer[origin + 4] = (byte)Packet.ResponseSetChannelData;
					buffer.position = origin;

					// Forward the packet to everyone in this channel
					for (int i = 0; i < mPlayerList.size; ++i)
					{
						TcpPlayer tp = mPlayerList[i];
						tp.SendTcpPacket(buffer);
					}
				}
				break;
			}
		}
	}

#if !UNITY_WEBPLAYER && !UNITY_FLASH
	// Cached to reduce memory allocation
	MemoryStream mWriteStream = null;
	BinaryWriter mWriter = null;
#endif

	/// <summary>
	/// Save the server's current state into the file that was loaded previously with Load().
	/// </summary>

	void Save ()
	{
		mNextSave = 0;

#if !UNITY_WEBPLAYER && !UNITY_FLASH
		if (!isActive || string.IsNullOrEmpty(mFilename)) return;

		Tools.SaveList("ServerConfig/ban.txt", mBan);
		Tools.SaveList("ServerConfig/admin.txt", mAdmin);

		if (mWriteStream == null)
		{
			mWriteStream = new MemoryStream();
			mWriter = new BinaryWriter(mWriteStream);
		}
		else
		{
			mWriter.Seek(0, SeekOrigin.Begin);
			mWriteStream.SetLength(0);
		}

		lock (mLock)
		{
			mWriter.Write(0);
			int count = 0;

			for (int i = 0; i < mChannelList.size; ++i)
			{
				Channel ch = mChannelList[i];

				if (!ch.closed && ch.persistent && ch.hasData)
				{
					mWriter.Write(ch.id);
					ch.SaveTo(mWriter);
					++count;
				}
			}

			if (count > 0)
			{
				mWriteStream.Seek(0, SeekOrigin.Begin);
				mWriter.Write(count);
			}
		}

		Tools.WriteFile(mFilename, mWriteStream);

		// Save the server configuration data
		if (mServerDataChanged && mServerData != null && !string.IsNullOrEmpty(mFilename))
		{
			mServerDataChanged = false;
			try { mServerData.Write(mFilename + ".config", DataNode.SaveType.Text, true); }
			catch (Exception) { }
		}

		// Save the player data
		for (int i = 0; i < mPlayerList.size; ++i)
			SavePlayer(mPlayerList[i]);
#endif
	}

	/// <summary>
	/// Save this player's data.
	/// </summary>

	void SavePlayer (TcpPlayer player)
	{
		if (player == null || !player.saveNeeded || string.IsNullOrEmpty(player.savePath)) return;
		player.saveNeeded = false;

		if (player.dataNode == null || player.dataNode.children.size == 0)
		{
			if (DeleteFile(player.savePath))
				player.Log("Deleted " + player.savePath);
		}
		else
		{
			byte[] bytes = player.dataNode.ToArray(player.saveType);

			if (SaveFile(player.savePath, bytes))
			{
				player.Log("Saved " + player.savePath + " (" + (bytes != null ? bytes.Length.ToString("N0") : "0") + " bytes)");
			}
			else player.LogError("Unable to save " + player.savePath);
		}
	}

	/// <summary>
	/// Load the server's human-readable data.
	/// </summary>

	void LoadConfig ()
	{
		if (!string.IsNullOrEmpty(mFilename))
		{
			try
			{
				byte[] data = File.ReadAllBytes(mFilename + ".config");
				mServerData = DataNode.Read(data, DataNode.SaveType.Text);
			}
			catch (Exception) { mServerData = null; }
			mServerDataChanged = false;
		}
	}

	[System.Obsolete("Use Load() instead")]
	public bool LoadFrom (string fileName) { return Load(fileName); }

	/// <summary>
	/// Load a previously saved server from the specified file.
	/// </summary>

	public bool Load (string fileName)
	{
		mFilename = fileName;
		mNextSave = 0;

#if UNITY_WEBPLAYER || UNITY_FLASH
		// There is no file access in the web player.
		return false;
#else
		LoadConfig();

		byte[] bytes = Tools.ReadFile(fileName);
		if (bytes == null) return false;

		MemoryStream stream = new MemoryStream(bytes);

		lock (mLock)
		{
			try
			{
				BinaryReader reader = new BinaryReader(stream);

				int channels = reader.ReadInt32();

				for (int i = 0; i < channels; ++i)
				{
					int chID = reader.ReadInt32();
					bool isNew;
					Channel ch = CreateChannel(chID, out isNew);
					if (isNew) ch.LoadFrom(reader);
				}
			}
			catch (System.Exception ex)
			{
				Tools.LogError("Loading from " + fileName + ": " + ex.Message, ex.StackTrace);
				return false;
			}
		}
		return true;
#endif
	}
}
}
