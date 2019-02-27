//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
//-------------------------------------------------

#define USE_MAX_PACKET_TIME
//#define COUNT_PACKETS
//#define PROFILE_PACKETS

#pragma warning disable 0162

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
#if USE_MAX_PACKET_TIME
		// Maximum amount of time to spend processing packets per frame, in milliseconds.
		// Useful for breaking up a stream of packets, ensuring that they get processed across multiple frames.
		const long MaxPacketTime = 20;
#endif
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

		/// <summary>
		/// TCP Protocol the client is using.
		/// </summary>

		public TcpProtocol protocol { get { return mTcp; } }

		/// <summary>
		/// Custom protocol, if used.
		/// </summary>

		public IConnection custom { get { return mTcp.custom; } set { mTcp.custom = value; } }

		/// <summary>
		/// Current connection stage.
		/// </summary>

		public TcpProtocol.Stage stage { get { return mTcp.stage; } set { mTcp.stage = value; } }

#if !UNITY_WEBPLAYER && !MODDING
		// UDP can be used for transmission of frequent packets, network broadcasts and NAT requests.
		// UDP is not available in the Unity web player because using UDP packets makes Unity request the
		// policy file every time the packet gets sent... which is obviously quite retarded.
		UdpProtocol mUdp = new UdpProtocol("Game Client");
		bool mUdpIsUsable = false;
#endif

		// Current time, time when the last ping was sent out, and time when connection was started
		long mTimeDifference = 0;
		long mMyTime = 0;
		long mStartTime = 0;

#if !MODDING
		long mPingTime = 0;
		int mReceivedPacketCount = 0, mReceivedBytesCount = 0;
		long mNextReset = 0;
		bool mCanPing = false;
#endif
		// Used to keep track of how many packets get sent / received per second
		int mSentPackets = 0, mSentBytes = 0, mReceivedPackets = 0, mReceivedBytes = 0;
		int mSentPacketCount = 0, mSentBytesCount = 0;

		// Last ping, and whether we can ping again
		int mPing = 0;

		// List of channels we're in
		TNet.List<Channel> mChannels = new TNet.List<Channel>();

		// Each GetFileList() call can specify its own callback
		Dictionary<string, OnGetFiles> mGetFiles = new Dictionary<string, OnGetFiles>();
		public delegate void OnGetFiles (string path, string[] files);

		// Each LoadFile() call can specify its own callback
		Dictionary<string, OnLoadFile> mLoadFiles = new Dictionary<string, OnLoadFile>();
		public delegate void OnLoadFile (string filename, byte[] data);

#if !MODDING
		// Server's UDP address
		IPEndPoint mServerUdpEndPoint;

		// Source of the UDP packet (available during callbacks)
		IPEndPoint mPacketSource;

		// Local server is used for socket-less mode
		GameServer mLocalServer;
#endif
		// Temporary, not important
		Buffer mBuffer;
		bool mIsAdmin = false;

		// List of channels we are currently in the process of joining
		List<int> mJoining = new List<int>();

		// Server configuration data
		DataNode mConfig = new DataNode("Version", Player.version);
		int mDataHash = 0;

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
		/// Perform the server configuration hash validation against the current data in memory. Useful for detecting memory modification.
		/// </summary>

		public bool ValidateHash ()
		{
			if (mConfig.children.size == 0) return true;
			return mDataHash == mConfig.CalculateHash();
		}

		/// <summary>
		/// Request the server-side validation of the specified property.
		/// </summary>

		public void Validate (string name, object val)
		{
			if (isConnected)
			{
				var writer = BeginSend(Packet.RequestValidate);
				writer.Write(name);
				writer.WriteObject(val);
				EndSend(true);
			}
		}

		/// <summary>
		/// Channels the player belongs to. Don't modify this list.
		/// </summary>

		public TNet.List<Channel> channels { get { return mChannels; } }

		/// <summary>
		/// Current time on the server in milliseconds.
		/// </summary>

		public long serverTime { get { return mTimeDifference + mMyTime; } }

		/// <summary>
		/// Server's uptime in milliseconds.
		/// </summary>

		public long serverUptime { get { return serverTime - mStartTime; } }

		/// <summary>
		/// How many packets were sent in the last second.
		/// </summary>

		public int sentPackets { get { return mSentPackets; } }

		/// <summary>
		/// How many bytes were sent in the last second.
		/// </summary>

		public int sentBytes { get { return mSentBytes; } }

		/// <summary>
		/// How many packets have been received in the last second.
		/// </summary>

		public int receivedPackets { get { return mReceivedPackets; } }

		/// <summary>
		/// How many bytes have been received in the last second.
		/// </summary>

		public int receivedBytes { get { return mReceivedBytes; } }

		/// <summary>
		/// Whether the client is currently connected to the server.
		/// </summary>

#if MODDING
		public bool isConnected { get { return false; } }
#else
		public bool isConnected { get { return mTcp.isConnected || mLocalServer != null; } }
#endif
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
#if UNITY_WEBPLAYER || MODDING
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

#if MODDING
		public IPEndPoint packetSourceIP { get { return null; } }
#else
		public IPEndPoint packetSourceIP { get { return mPacketSource != null ? mPacketSource : mTcp.tcpEndPoint; } }
#endif

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
#if UNITY_WEBPLAYER || MODDING
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
#if !MODDING
					var writer = BeginSend(Packet.RequestSetServerData);
					writer.Write("");
					writer.WriteObject(value);
					EndSend();
#endif
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
#if !MODDING
					if (isConnected)
					{
						var writer = BeginSend(Packet.RequestSetName);
						writer.Write(value);
						EndSend();
					}
					else mTcp.name = value;
#else
					mTcp.name = value;
#endif
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
		/// Immediately sync the player data. Call if it changing the player's DataNode manually.
		/// </summary>

		public void SyncPlayerData ()
		{
#if !MODDING
			var writer = BeginSend(Packet.RequestSetPlayerData);
			writer.Write(mTcp.id);
			writer.Write("");
			writer.WriteObject(mTcp.dataNode);
			EndSend();
#endif
		}

		/// <summary>
		/// Set the specified value on the player.
		/// </summary>

		public void SetPlayerData (string path, object val)
		{
			var node = mTcp.Set(path, val);
#if !MODDING
			if (isConnected)
			{
				var writer = BeginSend(Packet.RequestSetPlayerData);
				writer.Write(mTcp.id);
				writer.Write(path);
				writer.WriteObject(val);
				EndSend();
			}
#endif
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
				if (mJoining.Contains(channelID)) return false;

				for (int i = 0; i < mChannels.size; ++i)
				{
					var ch = mChannels.buffer[i];
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
					var ch = mChannels.buffer[i];
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
			foreach (var p in mDictionary)
			{
				if (p.Value.name == name)
					return p.Value;
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
				var ch = mChannels.buffer[i];
				if (ch.id == channelID) return ch;
			}

			if (createIfMissing)
			{
				var ch = new Channel();
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
			if (mBuffer == null) return;
			++mSentPacketCount;
			mSentBytesCount += mBuffer.EndPacket();
			if (isActive || forced) mTcp.SendTcpPacket(mBuffer);
			mBuffer.Recycle();
			mBuffer = null;
		}

		/// <summary>
		/// Send the outgoing buffer.
		/// </summary>

		public void EndSend (int channelID, bool reliable)
		{
			if (mBuffer == null) return;
			++mSentPacketCount;
			mSentBytesCount += mBuffer.EndPacket();

			if (isActive)
			{
#if UNITY_WEBPLAYER || MODDING
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
			if (mBuffer == null) return;
			++mSentPacketCount;
			mSentBytesCount += mBuffer.EndPacket();
#if !UNITY_WEBPLAYER && !MODDING
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
			if (mBuffer == null) return;
			++mSentPacketCount;
			mSentBytesCount += mBuffer.EndPacket();
#if !UNITY_WEBPLAYER && !MODDING
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
#if !MODDING
			Disconnect();

			if (server != null)
			{
				mLocalServer = server;
				server.localClient = this;

				mTcp.stage = TcpProtocol.Stage.Verifying;
				var writer = BeginSend(Packet.RequestID);
				writer.Write(TcpProtocol.version);
#if UNITY_EDITOR
				writer.Write(string.IsNullOrEmpty(mTcp.name) ? "Editor" : mTcp.name);
#else
				writer.Write(string.IsNullOrEmpty(mTcp.name) ? "Guest" : mTcp.name);
#endif
				writer.Write(mTcp.dataNode);
				EndSend();
			}
#endif
		}

		/// <summary>
		/// Try to establish a connection with the specified address.
		/// </summary>

		public void Connect (IPEndPoint externalIP, IPEndPoint internalIP = null)
		{
#if !MODDING
			Disconnect();
			if (externalIP == null) UnityEngine.Debug.LogError("Expecting a valid IP address or a local server to be running");
			else mTcp.Connect(externalIP, internalIP);
#endif
		}

		/// <summary>
		/// Disconnect from the server.
		/// </summary>

		public void Disconnect ()
		{
#if !MODDING
			if (mLocalServer != null) DisconnectNow();
			else mTcp.Disconnect();
#endif
		}

#if !MODDING
		void DisconnectNow ()
		{
			if (onLeaveChannel != null)
			{
				while (mChannels.size > 0)
				{
					int index = mChannels.size - 1;
					var ch = mChannels.buffer[index];
					ch.isLeaving = true;
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
			mOnExport.Clear();
			mOnImport.Clear();
			mMyTime = 0;

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
		}
#endif
		/// <summary>
		/// Start listening to incoming UDP packets on the specified port.
		/// </summary>

		public bool StartUDP (int udpPort)
		{
#if !MODDING
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
#endif
			return false;
		}

		/// <summary>
		/// Stop listening to incoming broadcasts.
		/// </summary>

		public void StopUDP ()
		{
#if !MODDING
#if !UNITY_WEBPLAYER
			if (mUdp.isActive)
			{
				if (isConnected)
				{
					BeginSend(Packet.RequestSetUDP).Write((ushort)0);
					EndSend();
				}

				mUdp.Stop();
				mServerUdpEndPoint = null;
				mUdpIsUsable = false;
			}
#endif
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
#if !MODDING
			if (isConnected && !IsInChannel(channelID) && !mJoining.Contains(channelID))
			{
				if (playerLimit > 65535) playerLimit = 65535;
				else if (playerLimit < 0) playerLimit = 0;

				var writer = BeginSend(Packet.RequestJoinChannel);
				writer.Write(channelID);
				writer.Write(string.IsNullOrEmpty(password) ? "" : password);
				writer.Write(string.IsNullOrEmpty(levelName) ? "" : levelName);
				writer.Write(persistent);
				writer.Write((ushort)playerLimit);
				EndSend(true);

				// Prevent all further packets from going out until the join channel response arrives.
				// This prevents the situation where packets are sent out between LoadLevel / JoinChannel
				// requests and the arrival of the OnJoinChannel/OnLoadLevel responses, which cause RFCs
				// from the previous scene to be executed in the new one.
				mJoining.Add(channelID);
			}
#endif
		}

		/// <summary>
		/// Close the channel the player is in. New players will be prevented from joining.
		/// Once a channel has been closed, it cannot be re-opened.
		/// </summary>

		public bool CloseChannel (int channelID)
		{
#if !MODDING
			if (isConnected && IsInChannel(channelID))
			{
				BeginSend(Packet.RequestCloseChannel).Write(channelID);
				EndSend();
				return true;
			}
#endif
			return false;
		}

		/// <summary>
		/// Leave the current channel.
		/// </summary>

		public bool LeaveChannel (int channelID)
		{
#if !MODDING
			if (isConnected)
			{
				for (int i = 0; i < mChannels.size; ++i)
				{
					var ch = mChannels.buffer[i];

					if (ch.id == channelID)
					{
						if (ch.isLeaving) return false;
						ch.isLeaving = true;
						BeginSend(Packet.RequestLeaveChannel).Write(channelID);
						EndSend();
						return true;
					}
				}
			}
#endif
			return false;
		}

		/// <summary>
		/// Leave all channels.
		/// </summary>

		public void LeaveAllChannels ()
		{
#if !MODDING
			if (isConnected)
			{
				mJoining.Clear();

				for (int i = mChannels.size; i > 0;)
				{
					var ch = mChannels.buffer[--i];

					if (!ch.isLeaving)
					{
						ch.isLeaving = true;
						BeginSend(Packet.RequestLeaveChannel).Write(ch.id);
						EndSend();
					}
				}
			}
#endif
		}

		/// <summary>
		/// Delete the specified channel.
		/// </summary>

		public void DeleteChannel (int id, bool disconnect)
		{
#if !MODDING
			if (isConnected)
			{
				var writer = BeginSend(Packet.RequestDeleteChannel);
				writer.Write(id);
				writer.Write(disconnect);
				EndSend();
			}
#endif
		}

		/// <summary>
		/// Change the maximum number of players that can join the channel the player is currently in.
		/// </summary>

		public void SetPlayerLimit (int channelID, int max)
		{
#if !MODDING
			if (isConnected && IsInChannel(channelID))
			{
				var writer = BeginSend(Packet.RequestSetPlayerLimit);
				writer.Write(channelID);
				writer.Write((ushort)max);
				EndSend();
			}
#endif
		}

		/// <summary>
		/// Switch the current level.
		/// </summary>

		public bool LoadLevel (int channelID, string levelName)
		{
#if !MODDING
			if (isConnected && IsInChannel(channelID))
			{
				var writer = BeginSend(Packet.RequestLoadLevel);
				writer.Write(channelID);
				writer.Write(levelName);
				EndSend();
				return true;
			}
#endif
			return false;
		}

		/// <summary>
		/// Change the hosting player.
		/// </summary>

		public void SetHost (int channelID, Player player)
		{
#if !MODDING
			if (isConnected && GetHost(channelID) == mTcp)
			{
				var writer = BeginSend(Packet.RequestSetHost);
				writer.Write(channelID);
				writer.Write(player.id);
				EndSend();
			}
#endif
		}

		/// <summary>
		/// Set the timeout for the player. By default it's 10 seconds. If you know you are about to load a large level,
		/// and it's going to take, say 60 seconds, set this timeout to 120 seconds just to be safe. When the level
		/// finishes loading, change this back to 10 seconds so that dropped connections gets detected correctly.
		/// </summary>

		public void SetTimeout (int seconds)
		{
#if !MODDING
			if (isConnected)
			{
				BeginSend(Packet.RequestSetTimeout).Write(seconds);
				EndSend();
			}
#endif
		}

		/// <summary>
		/// Send a remote ping request to the specified TNet server.
		/// </summary>

		public void Ping (IPEndPoint udpEndPoint, OnPing callback)
		{
#if !MODDING
			onPing = callback;
			mPingTime = DateTime.UtcNow.Ticks / 10000;
			BeginSend(Packet.RequestPing);
			EndSend(udpEndPoint);
#endif
		}

		/// <summary>
		/// Retrieve a list of files from the server.
		/// </summary>

		public void GetFiles (string path, OnGetFiles callback)
		{
#if !MODDING
			mGetFiles[path] = callback;
			var writer = BeginSend(Packet.RequestGetFileList);
			writer.Write(path);
			EndSend();
#endif
		}

		/// <summary>
		/// Load the specified file from the server.
		/// </summary>

		public void LoadFile (string filename, OnLoadFile callback)
		{
#if !MODDING
			mLoadFiles[filename] = callback;
			var writer = BeginSend(Packet.RequestLoadFile);
			writer.Write(filename);
			EndSend();
#endif
		}

		/// <summary>
		/// Save the specified file on the server.
		/// </summary>

		public void SaveFile (string filename, byte[] data)
		{
#if !MODDING
			if (data != null)
			{
				var writer = BeginSend(Packet.RequestSaveFile);
				writer.Write(filename);
				writer.Write(data.Length);
				writer.Write(data);
			}
			else
			{
				var writer = BeginSend(Packet.RequestDeleteFile);
				writer.Write(filename);
			}
			EndSend();
#endif
		}

		/// <summary>
		/// Delete the specified file on the server.
		/// </summary>

		public void DeleteFile (string filename)
		{
#if !MODDING
			var writer = BeginSend(Packet.RequestDeleteFile);
			writer.Write(filename);
			EndSend();
#endif
		}

		/// <summary>
		/// Send out a chat message.
		/// </summary>

		public void SendChat (string text, Player target = null)
		{
#if !MODDING
			var writer = BeginSend(Packet.RequestSendChat);
			writer.Write(target != null ? target.id : 0);
			writer.Write(text);
			EndSend();
#endif
		}

#if USE_MAX_PACKET_TIME && !MODDING
		[System.NonSerialized] System.Diagnostics.Stopwatch mSw;
#endif
		/// <summary>
		/// Process all incoming packets.
		/// </summary>

		public void ProcessPackets ()
		{
#if !MODDING
			var time = DateTime.UtcNow.Ticks / 10000;

			// If the time differs by more than 30 seconds, bail out. This prevents players from modifying their client time
			// and causing potential problems when the game is based on TNManager.serverTime-based time.
			if (mMyTime != 0)
			{
				var delta = time - mMyTime;

				if (delta < 0 || delta > 30000)
				{
					Disconnect();
					return;
				}
			}

			mMyTime = time;

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

#if USE_MAX_PACKET_TIME
			if (mSw == null)
			{
				mSw = System.Diagnostics.Stopwatch.StartNew();
			}
			else
			{
				mSw.Reset();
				mSw.Start();
			}
#endif

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
				++mReceivedPacketCount;
				mReceivedBytesCount += buffer.size;
				UnityEngine.Profiling.Profiler.BeginSample("ProcessPacket");
				keepGoing = ProcessPacket(buffer, null);
				UnityEngine.Profiling.Profiler.EndSample();
				buffer.Recycle();
#if USE_MAX_PACKET_TIME
				if (isJoiningChannel && mSw.ElapsedMilliseconds > MaxPacketTime) break;
#endif
			}

			if (mNextReset < mMyTime) ResetPacketCount();

#if USE_MAX_PACKET_TIME
			mSw.Stop();
#endif
#endif
		}

		/// <summary>
		/// Immediately reset the packet count.
		/// </summary>

		public void ResetPacketCount ()
		{
#if !MODDING
			mNextReset = mMyTime + 1000;

			mSentPackets = mSentPacketCount;
			mSentBytes = mSentBytesCount;
			mReceivedPackets = mReceivedPacketCount;
			mReceivedBytes = mReceivedBytesCount;

			mSentPacketCount = 0;
			mSentBytesCount = 0;
			mReceivedPacketCount = 0;
			mReceivedBytesCount = 0;

#if UNITY_EDITOR && COUNT_PACKETS
			var temp = TNObject.lastSentDictionary;
			temp.Clear();
			TNObject.lastSentDictionary = TNObject.sentDictionary;
			TNObject.sentDictionary = temp;
#endif
#endif
		}

#if PROFILE_PACKETS
		[System.NonSerialized] static System.Collections.Generic.Dictionary<int, string> mPacketNames = new Dictionary<int, string>();
#endif

#if !MODDING
		/// <summary>
		/// Process a single incoming packet. Returns whether we should keep processing packets or not.
		/// </summary>

		bool ProcessPacket (Buffer buffer, IPEndPoint ip)
		{
			mPacketSource = ip;
			var reader = buffer.BeginReading();
			if (buffer.size == 0) return true;

			int packetID = reader.ReadByte();
			var response = (Packet)packetID;

#if DEBUG_PACKETS && !STANDALONE
			if (response != Packet.ResponsePing)
				UnityEngine.Debug.Log("Client: " + response + " (" + buffer.size + " bytes) " + ((ip == null) ? "(TCP)" : "(UDP)") + " " + UnityEngine.Time.time);
#endif
			// Verification step must be passed first
			if (response == Packet.ResponseID || mTcp.stage == TcpProtocol.Stage.Verifying)
			{
				if (mTcp.VerifyResponseID(response, reader))
				{
					mMyTime = DateTime.UtcNow.Ticks / 10000;
					mTimeDifference = reader.ReadInt64() - mMyTime;
					mStartTime = reader.ReadInt64();

#if !UNITY_WEBPLAYER
					if (mUdp.isActive)
					{
						// If we have a UDP listener active, tell the server
						BeginSend(Packet.RequestSetUDP).Write((ushort)mUdp.listeningPort);
						EndSend();
					}
#endif
					mCanPing = true;
				}
				return true;
			}

#if PROFILE_PACKETS
			string packetName;

			if (!mPacketNames.TryGetValue(packetID, out packetName))
			{
				packetName = response.ToString();
				mPacketNames.Add(packetID, packetName);
			}

			UnityEngine.Profiling.Profiler.BeginSample(packetName);
#endif
			OnPacket callback;

			if (packetHandlers.TryGetValue((byte)response, out callback) && callback != null)
			{
				callback(response, reader, ip);
#if PROFILE_PACKETS
				UnityEngine.Profiling.Profiler.EndSample();
#endif
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
				case Packet.ResponseSendChat:
				{
					var player = GetPlayer(reader.ReadInt32());
					var msg = reader.ReadString();
					var prv = reader.ReadBoolean();
					if (onChat != null) onChat(player, msg, prv);
					break;
				}
				case Packet.ResponseSetPlayerData:
				{
					int pid = reader.ReadInt32();
					var target = GetPlayer(pid);

					if (target != null)
					{
						var path = reader.ReadString();
						var node = target.Set(path, reader.ReadObject());
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

					// Trivial time speed hack check
					/*var expectedTime = reader.ReadInt64();
					reader.ReadUInt16();
					var diff = (serverTime - expectedTime) - ping;

					if ((diff < 0 ? -diff : diff) > 10000)
					{
#if W2
						var s = "Server time is too different: " + diff.ToString("N0") + " milliseconds apart, ping " + ping;
						GameChat.NotifyAdmins(s);
						if (onError != null) onError(s);
						TNManager.Disconnect(1f);
#else
						if (onError != null) onError("Server time is too different: " + diff.ToString("N0") + " milliseconds apart, ping " + ping);
						Disconnect();
#endif
						break;
					}*/
					break;
				}
				case Packet.ResponseSetUDP:
				{
#if !UNITY_WEBPLAYER
					// The server has a new port for UDP traffic
					ushort port = reader.ReadUInt16();

					if (port != 0 && mTcp.tcpEndPoint != null)
					{
						var ipa = new IPAddress(mTcp.tcpEndPoint.Address.GetAddressBytes());
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
					var ch = GetChannel(channelID, true);

					for (int i = 0; i < count; ++i)
					{
						int pid = reader.ReadInt32();
						var p = GetPlayer(pid, true);

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
#if PROFILE_PACKETS
					UnityEngine.Profiling.Profiler.EndSample();
#endif
					return false;
				}
				case Packet.ResponsePlayerJoined:
				{
					int channelID = reader.ReadInt32();

					var ch = GetChannel(channelID);

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

					var ch = GetChannel(channelID);

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
						var ch = mChannels.buffer[i];

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
							int id = mJoining.buffer[i];

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
						var ch = mChannels.buffer[i];

						if (ch.id == channelID)
						{
							ch.isLeaving = true;
							mChannels.RemoveAt(i);
							break;
						}
					}

					RebuildPlayerDictionary();
					if (onLeaveChannel != null) onLeaveChannel(channelID);
#if PROFILE_PACKETS
					UnityEngine.Profiling.Profiler.EndSample();
#endif
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
						packetSourceID = reader.ReadInt32();
						int channelID = reader.ReadInt32();
						uint objID = reader.ReadUInt32();
						onCreate(channelID, packetSourceID, objID, reader);
					}
					break;
				}
				case Packet.ResponseDestroyObject:
				{
					if (onDestroy != null)
					{
						packetSourceID = reader.ReadInt32();
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
						packetSourceID = reader.ReadInt32();
						int from = reader.ReadInt32();
						int to = reader.ReadInt32();
						uint id0 = reader.ReadUInt32();
						uint id1 = reader.ReadUInt32();
						onTransfer(from, to, id0, id1);
					}
					break;
				}
				case Packet.ResponseExport:
				{
					var requestID = reader.ReadInt32();
					var byteCount = reader.ReadInt32();
					var data = (byteCount > 0) ? reader.ReadBytes(byteCount) : null;

					ExportCallback cb;

					if (mOnExport.TryGetValue(requestID, out cb))
					{
						mOnExport.Remove(requestID);
						if (cb.callback0 != null) cb.callback0(data);
						else if (cb.callback1 != null) cb.callback1(data != null ? DecodeExportedObjects(cb.objects, data) : null);
					}
					break;
				}
				case Packet.ResponseImport:
				{
					var requestID = reader.ReadInt32();
					reader.ReadInt32(); // The request already knows what channel it was made in
					var size = reader.ReadInt32();
					var result = new uint[size];
					for (int i = 0; i < size; ++i) result[i] = reader.ReadUInt32();

					Action<uint[]> cb;

					if (mOnImport.TryGetValue(requestID, out cb))
					{
						mOnImport.Remove(requestID);
						if (cb != null) cb(result);
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
					DisconnectNow();
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
					if (!ValidateHash())
					{
#if W2
						Game.MAC("Edited the server configuration in memory");
#else
						Disconnect();
#endif
						break;
					}

					var path = reader.ReadString();
					var obj = reader.ReadObject();

					if (obj != null)
					{
						var node = mConfig.SetHierarchy(path, obj);
						mDataHash = mConfig.CalculateHash();
						if (onSetServerData != null) onSetServerData(path, node);
					}
					else
					{
						var node = mConfig.RemoveHierarchy(path);
						mDataHash = mConfig.CalculateHash();
						if (onSetServerData != null) onSetServerData(path, node);
					}
					break;
				}
				case Packet.ResponseConnected:
				{
					if (onConnect != null) onConnect(true, null);
					break;
				}
				case Packet.ResponseChannelList:
				{
					if (mGetChannelsCallbacks.Count != 0)
					{
						var cb = mGetChannelsCallbacks.Dequeue();
						var channels = new List<Channel.Info>();
						var count = reader.ReadInt32();

						for (int i = 0; i < count; ++i)
						{
							var info = new Channel.Info();
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
				case Packet.ResponseUpdateChannel:
				{
					var ch = GetChannel(reader.ReadInt32());
					var playerLimit = reader.ReadUInt16();
					var val = reader.ReadUInt16();

					if (ch != null)
					{
						ch.playerLimit = playerLimit;
						ch.isPersistent = ((val & 1) != 0);
						ch.isClosed = ((val & 2) != 0);
						ch.isLocked = ((val & 4) != 0);
						if (onUpdateChannel != null) onUpdateChannel(ch);
					}
					break;
				}
				case Packet.ResponseSetOwner:
				{
					var channelID = reader.ReadInt32();
					var objID = reader.ReadUInt32();
					var playerID = reader.ReadInt32();
					onChangeOwner(channelID, objID, playerID != 0 ? GetPlayer(playerID) : null);
					break;
				}
			}
#if PROFILE_PACKETS
			UnityEngine.Profiling.Profiler.EndSample();
#endif
			return true;
		}
#endif // !MODDING

		/// <summary>
		/// Rebuild the player dictionary from the list of players in all of the channels we're currently in.
		/// </summary>

		void RebuildPlayerDictionary ()
		{
			mDictionary.Clear();

			for (int i = 0; i < mChannels.size; ++i)
			{
				var ch = mChannels.buffer[i];

				for (int b = 0; b < ch.players.size; ++b)
				{
					var p = ch.players.buffer[b];
					if (!mDictionary.ContainsKey(p.id)) mDictionary[p.id] = p;
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
#if !MODDING
			var writer = BeginSend(Packet.RequestSetServerData);
			writer.Write(node.name);
			writer.WriteObject(node);
			EndSend();
#endif
		}

		/// <summary>
		/// Set the specified server option.
		/// </summary>

		public void SetServerData (string key, object val)
		{
#if !MODDING
			if (val != null)
			{
				mConfig.SetHierarchy(key, val);
				mDataHash = mConfig.CalculateHash();
			}
			else
			{
				mConfig.RemoveHierarchy(key);
				mDataHash = mConfig.CalculateHash();
			}

			var writer = BeginSend(Packet.RequestSetServerData);
			writer.Write(key);
			writer.WriteObject(val);
			EndSend();
#endif
		}

		/// <summary>
		/// Set the specified server option.
		/// </summary>

		public void SetChannelData (int channelID, string path, object val)
		{
#if !MODDING
			var ch = GetChannel(channelID);

			if (ch != null && !string.IsNullOrEmpty(path))
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

					var bw = BeginSend(Packet.RequestSetChannelData);
					bw.Write(channelID);
					bw.Write(path);
					bw.WriteObject(val);
					EndSend();
				}
#if UNITY_EDITOR
				else Debug.LogWarning("Trying to SetChannelData on a locked channel: " + channelID);
#endif
			}
#if UNITY_EDITOR
			else Debug.LogWarning("Calling SetChannelData with invalid parameters: " + channelID + " = " + (ch != null) + ", " + path);
#endif
#endif
		}

		public delegate void OnGetChannels (List<Channel.Info> list);
		Queue<OnGetChannels> mGetChannelsCallbacks = new Queue<OnGetChannels>();

		/// <summary>
		/// Get a list of channels from the server.
		/// </summary>

		public void GetChannelList (OnGetChannels callback)
		{
#if !MODDING
			mGetChannelsCallbacks.Enqueue(callback);
			BeginSend(Packet.RequestChannelList);
			EndSend();
#endif
		}

#if !MODDING
		int mRequestID = 0;
		Dictionary<int, ExportCallback> mOnExport = new Dictionary<int, ExportCallback>();
		Dictionary<int, Action<uint[]>> mOnImport = new Dictionary<int, Action<uint[]>>();

		struct ExportCallback
		{
			public List<TNObject> objects;
			public Action<byte[]> callback0;
			public Action<DataNode> callback1;
		}
#endif

		/// <summary>
		/// Export the specified objects from the server. The server will return the byte[] necessary to re-instantiate all of the specified objects and restore their state.
		/// </summary>

		public void ExportObjects (List<TNObject> list, Action<byte[]> callback)
		{
#if !MODDING
			if (isConnected && list.size > 0)
			{
				var cb = new ExportCallback();
				cb.objects = list;
				cb.callback0 = callback;

				mOnExport.Add(++mRequestID, cb);

				var writer = BeginSend(Packet.RequestExport);
				writer.Write(mRequestID);
				writer.Write(list.size);

				foreach (var obj in list)
				{
					writer.Write(obj.channelID);
					writer.Write(obj.uid);
				}

				EndSend();
			}
#endif
		}

		/// <summary>
		/// Export the specified objects from the server. The server will return the DataNode necessary to re-instantiate all of the specified objects and restore their state.
		/// </summary>

		public void ExportObjects (List<TNObject> list, Action<DataNode> callback)
		{
#if !MODDING
			if (isConnected && list.size > 0)
			{
				var cb = new ExportCallback();
				cb.objects = list;
				cb.callback1 = callback;

				mOnExport.Add(++mRequestID, cb);

				var writer = BeginSend(Packet.RequestExport);
				writer.Write(mRequestID);
				writer.Write(list.size);

				foreach (var obj in list)
				{
					writer.Write(obj.channelID);
					writer.Write(obj.uid);
				}

				EndSend();
			}
#endif
		}

		/// <summary>
		/// Import previously exported objects in the specified channel.
		/// </summary>

		public void ImportObjects (int channelID, byte[] data, Action<uint[]> callback = null)
		{
#if !MODDING
			if (isConnected && data != null && data.Length > 0)
			{
				++mRequestID;
				if (callback != null) mOnImport.Add(mRequestID, callback);

				var writer = BeginSend(Packet.RequestImport);
				writer.Write(mRequestID);
				writer.Write(channelID);
				writer.Write(data);
				EndSend();
			}
#endif
		}

		/// <summary>
		/// Import previously exported objects in the specified channel.
		/// </summary>

		public void ImportObjects (int channelID, DataNode node, Action<uint[]> callback = null)
		{
#if !MODDING
			var data = EncodeExportedObjects(node);
			ImportObjects(channelID, data, callback);
			data.Recycle();
#endif
		}

		/// <summary>
		/// Import previously exported objects in the specified channel.
		/// </summary>

		public void ImportObjects (int channelID, Buffer buffer, Action<uint[]> callback = null)
		{
#if !MODDING
			if (isConnected && buffer != null && buffer.size > 0)
			{
				++mRequestID;
				if (callback != null) mOnImport.Add(mRequestID, callback);

				var writer = BeginSend(Packet.RequestImport);
				writer.Write(mRequestID);
				writer.Write(channelID);
				writer.Write(buffer.buffer, buffer.position, buffer.size);
				EndSend();
			}
#endif
		}

#if !MODDING
		/// <summary>
		/// When a server exports objects, the result comes as a byte array, which is not very readable or modifiable.
		/// This function is used to convert the byte array into a structured DataNode format, which is much easier to edit.
		/// </summary>

		static DataNode DecodeExportedObjects (List<TNObject> objects, byte[] bytes)
		{
			var node = new DataNode();
#if W2
			try
#endif
			{
				var buffer = Buffer.Create();
				buffer.BeginWriting(false).Write(bytes);
				var reader = buffer.BeginReading();

				// Number of objects
				var count = reader.ReadInt32();

				for (int i = 0; i < count; ++i)
				{
					var obj = objects.buffer[i];
					reader.ReadInt32(); // Size of the data, we don't need it since we're parsing everything
					var rccID = reader.ReadByte();
					var funcName = (rccID == 0) ? reader.ReadString() : null;
					var prefab = reader.ReadString();
					var args = reader.ReadArray();
					var func = TNManager.GetRCC(rccID, funcName);

					var child = (rccID != 0) ? node.AddChild("RCC", rccID) : node.AddChild("RCC", funcName);
					child.AddChild("prefab", prefab);

					if (func != null)
					{
						var funcPars = func.parameters;
						var argLength = args.Length;

						if (funcPars.Length == argLength + 1)
						{
							var pn = child.AddChild("Args");
							for (int b = 0; b < argLength; ++b) pn.AddChild(funcPars[b + 1].Name, args[b]);
						}
#if UNITY_EDITOR
						else Debug.LogError("RCC " + rccID + " (" + funcName + ") has a different number of parameters than expected: " + funcPars.Length + " vs " + (args.Length + 1), obj);
#endif
					}
#if UNITY_EDITOR
					else Debug.LogError("Unable to find RCC " + rccID + " (" + funcName + ")", obj);
#endif
					var rfcs = reader.ReadInt32();
					if (rfcs > 0) child = child.AddChild("RFCs");

					for (int r = 0; r < rfcs; ++r)
					{
						uint objID;
						byte funcID;
						TNObject.DecodeUID(reader.ReadUInt32(), out objID, out funcID);
						funcName = (funcID == 0) ? reader.ReadString() : null;
						reader.ReadInt32(); // Size of the data, we don't need it since we're parsing everything
						var array = reader.ReadArray();
						var funcRef = (funcID == 0) ? obj.FindFunction(funcName) : obj.FindFunction(funcID);

						if (funcRef != null)
						{
							var pc = array.Length;

							if (funcRef.parameters.Length == pc)
							{
								var rfcNode = (funcID == 0) ? child.AddChild("RFC", funcName) : child.AddChild("RFC", funcID);
								for (int p = 0; p < pc; ++p) rfcNode.AddChild(funcRef.parameters[p].Name, array[p]);
							}
#if UNITY_EDITOR
							else Debug.LogError("RFC " + funcID + " (" + funcName + ") has a different number of parameters than expected: " + funcRef.parameters.Length + " vs " + pc, obj);
#endif
						}
#if UNITY_EDITOR
						else Debug.LogWarning("RFC " + funcID + " (" + funcName + ") can't be found", obj);
#endif
					}
				}

				buffer.Recycle();
				return node;
			}
#if W2
			catch (Exception ex)
			{
				TNManager.Log("ERROR: " + ex.Message + "\n" + ex.StackTrace);
				TNManager.SaveFile("Debug/" + TNManager.playerName + "_" + (TNManager.serverUptime / 1000) + ".txt", bytes);
				return node;
			}
#endif
		}

		/// <summary>
		/// The opposite of DecodeExportedObjects, encoding the DataNode-stored data into a binary format that can be sent back to the server.
		/// </summary>

		static Buffer EncodeExportedObjects (DataNode node)
		{
			var buffer = Buffer.Create();
			var writer = buffer.BeginWriting();

			// Number of objects
			writer.Write(node.children.size);

			for (int i = 0; i < node.children.size; ++i)
			{
				var child = node.children.buffer[i];
				var sizePos = buffer.position;

				if (child.value is string)
				{
					var s = (string)child.value;
					if (string.IsNullOrEmpty(s)) continue;

					writer.Write(0); // Size of the RCC's data -- set after writing it
					writer.Write((byte)0);
					writer.Write(s);
				}
				else
				{
					writer.Write(0); // Size of the RCC's data -- set after writing it
					writer.Write((byte)child.Get<int>());
				}

				writer.Write(child.GetChild<string>("prefab"));

				var args = child.GetChild("Args");
				var argCount = (args != null) ? args.children.size : 0;
				var array = new object[argCount];
				for (int b = 0; b < argCount; ++b) array[b] = args.children.buffer[b].value;
				writer.WriteArray(array);

				// Write down the size of the RCC
				var endPos = buffer.position;
				var size = endPos - sizePos;
				buffer.position = sizePos;
				writer.Write(size - 4);
				buffer.position = endPos;

				var rfcs = child.GetChild("RFCs");
				var rfcCount = (rfcs != null) ? rfcs.children.size : 0;
				writer.Write(rfcCount);

				if (rfcCount > 0)
				{
					for (int b = 0; b < rfcs.children.size; ++b)
					{
						var rfc = rfcs.children.buffer[b];

						if (rfc.value is string)
						{
							var s = (string)rfc.value;
							if (string.IsNullOrEmpty(s)) continue;
							writer.Write((uint)0);
							writer.Write(s);
						}
						else writer.Write(TNObject.GetUID(0, (byte)rfc.Get<int>()));

						array = new object[rfc.children.size];
						for (int c = 0; c < rfc.children.size; ++c) array[c] = rfc.children.buffer[c].value;

						var rfcPos = buffer.position;
						writer.Write(0); // Size of the array -- set after writing the array
						writer.WriteArray(array);

						// Write down the size of the RFC
						endPos = buffer.position;
						size = endPos - rfcPos;
						buffer.position = rfcPos;
						writer.Write(size - 4);
						buffer.position = endPos;
					}
				}
			}

			buffer.EndWriting();
			return buffer;
		}
#endif
	}
}
