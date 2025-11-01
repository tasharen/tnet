//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2025 Tasharen Entertainment Inc
//-------------------------------------------------

//#define COUNT_PACKETS

// If you want to see exceptions instead of error messages, comment this out
#define SAFE_EXCEPTIONS

using System.IO;
using UnityEngine;
using System.Net;
using System.Reflection;
using System;

namespace TNet
{
	/// <summary>
	/// Tasharen Network Manager tailored for Unity.
	/// </summary>

	public class TNManager : MonoBehaviour
	{
		// Will be 'true' at play time unless the application is shutting down. 'false' at edit time.
		static bool isPlaying { get { return !mDestroyed && !mShuttingDown && Application.isPlaying; } }
		[NonSerialized] static bool mDestroyed = false;
		[NonSerialized] static bool mShuttingDown = false;

		#region Delegates
		/// <summary>
		/// Ping notification.
		/// </summary>

		static public TNEvents.OnPing onPing;

		/// <summary>
		/// Error notification.
		/// </summary>

		static public TNEvents.OnError onError;

		/// <summary>
		/// Connection attempt result indicating success or failure.
		/// </summary>

		static public TNEvents.OnConnect onConnect;

		/// <summary>
		/// Notification sent after the connection terminates for any reason.
		/// </summary>

		static public TNEvents.OnDisconnect onDisconnect;

		/// <summary>
		/// Notification sent when attempting to join a channel, indicating a success or failure.
		/// </summary>

		static public TNEvents.OnJoinChannel onJoinChannel;

		/// <summary>
		/// Notification sent when the channel gets updated.
		/// </summary>

		static public TNEvents.OnUpdateChannel onUpdateChannel;

		/// <summary>
		/// Notification sent when leaving a channel for any reason, including being disconnected.
		/// </summary>

		static public TNEvents.OnLeaveChannel onLeaveChannel;

		/// <summary>
		/// Notification sent when changing levels.
		/// </summary>

		static public TNEvents.OnLoadLevel onLoadLevel;

		/// <summary>
		/// Notification sent when a new player joins the channel.
		/// </summary>

		static public TNEvents.OnPlayerJoin onPlayerJoin;

		/// <summary>
		/// Notification sent when a player leaves the channel.
		/// </summary>

		static public TNEvents.OnPlayerLeave onPlayerLeave;

		/// <summary>
		/// Notification of some player changing their name.
		/// </summary>

		static public TNEvents.OnRenamePlayer onRenamePlayer { get { return mInstance ? mInstance.mClient.onRenamePlayer : null; } set { if (mInstance) mInstance.mClient.onRenamePlayer = value; } }

		/// <summary>
		/// Notification sent when the channel's host changes.
		/// </summary>

		static public TNEvents.OnHostChanged onHostChanged { get { return mInstance ? mInstance.mClient.onHostChanged : null; } set { if (mInstance) mInstance.mClient.onHostChanged = value; } }

		/// <summary>
		/// Notification sent when the server's data gets changed.
		/// </summary>

		static public TNEvents.OnSetServerData onSetServerData;

		/// <summary>
		/// Notification sent when the channel's data gets changed.
		/// </summary>

		static public TNEvents.OnSetChannelData onSetChannelData;

		/// <summary>
		/// Notification sent when player data gets changed.
		/// </summary>

		static public TNEvents.OnSetPlayerData onSetPlayerData;

		/// <summary>
		/// Callback triggered when the player gets verified as an administrator.
		/// </summary>

		static public TNEvents.OnSetAdmin onSetAdmin { get { return mInstance ? mInstance.mClient.onSetAdmin : null; } set { if (mInstance) mInstance.mClient.onSetAdmin = value; } }

		/// <summary>
		/// Callback triggered when a chat message arrives.
		/// </summary>

		static public TNEvents.OnChatPacket onChat { get { return mInstance ? mInstance.mClient.onChat : null; } set { if (mInstance) mInstance.mClient.onChat = value; } }
		#endregion

		/// <summary>
		/// Whether the application is currently paused.
		/// </summary>

		[NonSerialized] static public bool isPaused = false;

		/// <summary>
		/// If set to 'true', the list of custom creation functions will be rebuilt the next time it's accessed.
		/// </summary>

		[NonSerialized] static public bool rebuildMethodList = true;

		// Cached list of creation functions
		[NonSerialized] static System.Collections.Generic.Dictionary<int, CachedFunc> mDict0 = new System.Collections.Generic.Dictionary<int, CachedFunc>();
		[NonSerialized] static System.Collections.Generic.Dictionary<string, CachedFunc> mDict1 = new System.Collections.Generic.Dictionary<string, CachedFunc>();

		// Static player, here just for convenience so that GetPlayer() works the same even if instance is missing.
#if UNITY_EDITOR
		[NonSerialized] static Player mPlayer = new Player("Editor", 1);
#else
		[NonSerialized] static Player mPlayer = new Player("Guest", 1);
#endif
		// Player list that will contain only the player in it. Here for the same reason as 'mPlayer'.
		[NonSerialized] static List<Player> mPlayers;

		// Instance pointer
		[NonSerialized] static TNManager mInstance;

		// Used to stop processing incoming packets after a delayed disconnect operation
		[NonSerialized] bool mDelayedDisconnect = false;

		/// <summary>
		/// Delayed disconnect had been initiated.
		/// </summary>

		static public bool isDisconnecting { get { return mInstance != null && mInstance.mDelayedDisconnect; } }

		/// <summary>
		/// Object owner is only valid during object creation. In most cases you will want to use tno.owner.
		/// </summary>

		static internal Player currentObjectOwner = null;

		/// <summary>
		/// List of objects that can be instantiated by the network.
		/// </summary>

		public GameObject[] objects;

		// Network client
		[NonSerialized] GameClient mClient = new GameClient();
		[NonSerialized] List<int> mLoadingLevel = new List<int>();

		/// <summary>
		/// Custom protocol, if used.
		/// </summary>

		static public IConnection custom
		{
			get
			{
				return (mInstance != null) ? mInstance.mClient.custom : null;
			}
			set
			{
				var inst = instance;

				if (inst != null)
				{
					inst.mClient.custom = value;

					if (value != null)
					{
						client.stage = TcpProtocol.Stage.Verifying;

						var b = CreatePacket(Packet.RequestID);
						var w = b.writer;
						w.Write(Player.version);
						w.Write(mPlayer.name);
						w.Write(mPlayer.dataNode);
						SendPacket(b);
					}
				}
			}
		}

		/// <summary>
		/// Whether the player has verified himself as an administrator.
		/// </summary>

		static public bool isAdmin { get { return isConnected && mInstance.mClient.isAdmin; } }

		/// <summary>
		/// Set administrator privileges. Note that failing the password test will cause a disconnect.
		/// </summary>

		static public void SetAdmin (string passKey) { if (mInstance) mInstance.mClient.SetAdmin(passKey); }

		/// <summary>
		/// Add a new pass key to the admin file.
		/// </summary>

		static public void AddAdmin (string passKey)
		{
#if !MODDING
			if (isAdmin)
			{
				var b = CreatePacket(Packet.RequestCreateAdmin);
				b.writer.Write(passKey);
				SendPacket(b);
			}
#endif
		}

		/// <summary>
		/// Remove the specified pass key from the admin file.
		/// </summary>

		static public void RemoveAdmin (string passKey)
		{
#if !MODDING
			if (isAdmin)
			{
				var b = CreatePacket(Packet.RequestRemoveAdmin);
				b.writer.Write(passKey);
				SendPacket(b);
			}
#endif
		}

		/// <summary>
		/// Set a player alias. Player aliases can be used to store useful player-associated data such as Steam usernames,
		/// database IDs, or other unique identifiers. Aliases will show up in TNet's log and can also be banned by on
		/// the server side. When a player is banned, all their aliases are banned as well, so be careful to make sure
		/// that they are indeed unique. All aliases are visible via TNet.Player.aliases list of each player.
		/// </summary>

		static public void SetAlias (string alias)
		{
			if (mInstance != null && mInstance.mClient.isConnected) mInstance.mClient.SetAlias(alias);
			else GameClient.AddAlias(alias);
		}

		/// <summary>
		/// TNet Client used for communication.
		/// </summary>

		static public GameClient client
		{
			get
			{
				return mInstance != null ? mInstance.mClient : (!mDestroyed && !mShuttingDown ? instance.mClient : null);
			}
		}

		/// <summary>
		/// Whether we're currently connected.
		/// </summary>

		static public bool isConnected { get { return mInstance != null && mInstance.mClient.isConnected; } }

		/// <summary>
		/// How many packets were sent in the last second.
		/// </summary>

		static public int sentPackets { get { return mInstance != null ? mInstance.mClient.sentPackets : 0; } }

		/// <summary>
		/// How many bytes were sent in the last second.
		/// </summary>

		static public int sentBytes { get { return mInstance != null ? mInstance.mClient.sentBytes : 0; } }

		/// <summary>
		/// How many packets have been received in the last second.
		/// </summary>

		static public int receivedPackets { get { return mInstance != null ? mInstance.mClient.receivedPackets : 0; } }

		/// <summary>
		/// How many bytes have been received in the last second.
		/// </summary>

		static public int receivedBytes { get { return mInstance != null ? mInstance.mClient.receivedBytes : 0; } }

		/// <summary>
		/// Immediately reset the packet count. Calling this isn't necessary as they get updated once per second anyway.
		/// </summary>

		static public void ResetPacketCount () { if (mInstance != null) mInstance.mClient.ResetPacketCount(); }

		/// <summary>
		/// Whether we are currently in the process of joining a channel.
		/// To find out whether we are joining a specific channel, use the "IsJoiningChannel(id)" function.
		/// </summary>

		static public bool isJoiningChannel { get { return IsJoiningChannel(-1); } }

		/// <summary>
		/// Whether we are currently trying to join the specified channel.
		/// </summary>

		static public IsJoiningChannelFunc IsJoiningChannel = IsJoiningChannelDefault;

		/// <summary>
		/// Default function to check whether the TNet is currently joining a channel.
		/// </summary>

		static public bool IsJoiningChannelDefault (int channelID)
		{
			if (!isConnected) return false;

			if (channelID < 0)
			{
				return (mInstance.mLoadingLevel.size != 0 || mInstance.mClient.isJoiningChannel);
			}
			return mInstance.mLoadingLevel.Contains(channelID) || mInstance.mClient.IsJoiningChannel(channelID);
		}

		public delegate bool IsJoiningChannelFunc (int channelID);

		/// <summary>
		/// Whether we are currently trying to establish a new connection.
		/// </summary>

		static public bool isTryingToConnect { get { return mInstance != null && mInstance.mClient.isTryingToConnect; } }

		/// <summary>
		/// Whether we're currently hosting. Note that this should only be used if the player is in only one channel.
		/// </summary>

		[System.Obsolete("Use IsHosting(channelID)")]
		static public bool isHosting
		{
			get
			{
#if UNITY_EDITOR
				if (channels.size > 1)
				{
					Debug.LogWarning("Use TNManager.IsHosting(channelID) instead, as the player is in more than one channel.\nUndefined results will occur if you keep using TNManager.isHosting.");
				}
#endif
				return GetHost(lastChannelID) == player;
			}
		}

		/// <summary>
		/// Whether the player is hosting this channel.
		/// </summary>

		static public bool IsHosting (int channelID) { return GetHost(channelID) == player; }

		/// <summary>
		/// Whether we're currently in any channel. To find out if we are in a specific channel, use TNManager.IsInChannel(id).
		/// </summary>

		static public bool isInChannel
		{
			get
			{
				return !isJoiningChannel && mInstance != null && mInstance.mClient != null && (mInstance.mClient.isConnected && mInstance.mClient.isInChannel);
			}
		}

		/// <summary>
		/// You can pause TNManager's message processing if you like.
		/// This happens automatically when a scene is being loaded.
		/// </summary>

		static public bool isActive
		{
			get
			{
				return mInstance != null && mInstance.mClient.isActive;
			}
			set
			{
				if (mInstance != null) mInstance.mClient.isActive = value;
			}
		}

		/// <summary>
		/// Enable or disable the Nagle's buffering algorithm (aka NO_DELAY flag).
		/// Enabling this flag will improve latency at the cost of increased bandwidth.
		/// http://en.wikipedia.org/wiki/Nagle's_algorithm
		/// </summary>

		static public bool noDelay { get { return mInstance != null && mInstance.mClient.noDelay; } set { if (mInstance != null) mInstance.mClient.noDelay = value; } }

		/// <summary>
		/// Current ping to the server. This value is for a round-trip packet, so may be double of what you may expect.
		/// Also note that this value will most certainly be affected by the FPS, so with 60 FPS there will be a 16.7 ms delay due to the packet processing time.
		/// </summary>

		static public int ping { get { return mInstance != null ? mInstance.mClient.ping : 0; } }

		/// <summary>
		/// Size of the packets that are currently queued up for processing, in bytes.
		/// </summary>

		static public int incomingQueueSize { get { return mInstance != null ? mInstance.mClient.protocol.incomingQueueSize : 0; } }

		/// <summary>
		/// Number of bytes available in the incoming buffer that have not yet been processed.
		/// </summary>

		static public int availablePacketSize { get { return mInstance != null ? mInstance.mClient.protocol.availablePacketSize : 0; } }

		/// <summary>
		/// Number of bytes expected before the incoming packet can be processed.
		/// </summary>

		static public int incomingPacketSize { get { return mInstance != null ? mInstance.mClient.protocol.incomingPacketSize : 0; } }

		/// <summary>
		/// Whether we can use unreliable packets (UDP) to communicate with the server.
		/// </summary>

		static public bool canUseUDP { get { return mInstance != null && mInstance.mClient.canUseUDP; } }

		/// <summary>
		/// Listening port for incoming UDP packets. Set via TNManager.StartUDP().
		/// </summary>

		static public int listeningPort { get { return mInstance != null ? mInstance.mClient.listeningPort : 0; } }

		/// <summary>
		/// Time on the server in seconds.
		/// </summary>

		static public double serverTime { get { return serverTimeMS * 0.001; } }

		/// <summary>
		/// Current time on the server in milliseconds.
		/// </summary>

		static public long serverTimeMS { get { return (mInstance != null && mInstance.mClient.isConnected) ? mInstance.mClient.serverTime : (DateTime.UtcNow.Ticks / 10000); } }

		/// <summary>
		/// Server's uptime in milliseconds.
		/// </summary>

		static public long serverUptimeMS { get { return (mInstance != null && mInstance.mClient.isConnected) ? mInstance.mClient.serverUptime : (DateTime.UtcNow.Ticks / 10000) - mStartTime; } }
		static readonly long mStartTime = (DateTime.UtcNow.Ticks / 10000);

		/// <summary>
		/// Time elapsed either since the server has been started up in seconds, or (if not connected) -- time since TNManager was first used.
		/// It's a more precise version of Unity's Time.time, since it takes a mere 3 hours for float precision to start dropping milliseconds.
		/// It has an added benefit of usable from any thread, unlike Unity's Time.time (which must be used from the main thread).
		/// </summary>

		static public double serverUptime { get { return serverUptimeMS * 0.001; } }

		[Obsolete("Use TNManager.serverUptime instead for clarity")]
		static public double time => serverUptime;

		/// <summary>
		/// Player's /played time in seconds. This value is automatically tracked by the server and is saved as a part of the player file.
		/// </summary>

		static public long playedTime
		{
			get
			{
				if (player == null || player.dataNode == null) return 0;
				var server = player.dataNode.GetChild("Server");
				if (server == null) return 0;

				var time = serverTimeMS;
				var played = server.GetChild<long>("playedTime", 0);
				var save = server.GetChild<long>("lastSave", time);
				var elapsed = time - save;
				return (elapsed + played) / 1000;
			}
		}

		/// <summary>
		/// Forward, Create and Destroy type packets write down their source.
		/// If the packet was sent by the server instead of another player, the ID will be 0.
		/// </summary>

		static public int packetSourceID
		{
			get
			{
				if (mInstance == null) return playerID;
				return mInstance.mClient.packetSourceID;
			}
			set
			{
				if (mInstance != null) mInstance.mClient.packetSourceID = value;
			}
		}

		/// <summary>
		/// Forward, Create and Destroy type packets write down their source.
		/// </summary>

		static public Player packetSourcePlayer
		{
			get
			{
				if (mInstance == null) return player;
				if (mInstance.mClient.packetSourceID < 1) return null;
				return GetPlayer(mInstance.mClient.packetSourceID);
			}
		}

		/// <summary>
		/// Address from which the packet was received. Only available during packet processing callbacks.
		/// If null, then the packet arrived via the active connection (TCP).
		/// If the return value is not null, then the last packet arrived via UDP.
		/// </summary>

		static public IPEndPoint packetSourceIP { get { return (mInstance != null) ? mInstance.mClient.packetSourceIP : null; } }

		/// <summary>
		/// TCP end point, available only if we're actually connected to the server.
		/// </summary>

		static public IPEndPoint tcpEndPoint { get { return (mInstance != null) ? mInstance.mClient.tcpEndPoint : null; } }

		/// <summary>
		/// Whether the specified channel is currently locked.
		/// </summary>

		static public bool IsChannelLocked (int channelID)
		{
			if (mInstance != null && mInstance.mClient != null)
			{
				var ch = mInstance.mClient.GetChannel(channelID);
				return (ch != null && ch.isLocked);
			}
			return false;
		}

		/// <summary>
		/// Whether the specified channel is closed.
		/// </summary>

		static public bool IsChannelClosed (int channelID)
		{
			if (mInstance != null && mInstance.mClient != null)
			{
				var ch = mInstance.mClient.GetChannel(channelID);
				return (ch != null && ch.isClosed);
			}
			return false;
		}

		/// <summary>
		/// ID of the channel the player is in. This is not a reliable way of retrieving a channel, and is only meant
		/// to be used from inside RCC functions when the object's channel is required.
		/// </summary>

		[System.NonSerialized]
		static public int lastChannelID = 0;

		/// <summary>
		/// Dummy channel list, used for backwards compatibility when using offline mode.
		/// </summary>

		[System.NonSerialized]
		static List<Channel> mDummyCL = new List<Channel>();

		/// <summary>
		/// List of channels the player is currently in.
		/// </summary>

		static public List<Channel> channels
		{
			get
			{
				if (isConnected) return mInstance.mClient.channels;
				return mDummyCL;
			}
		}

		/// <summary>
		/// Check to see if we are currently in the specified channel.
		/// </summary>

		static public bool IsInChannel (int channelID, bool isNotLeaving = false)
		{
			if (isConnected) return mInstance.mClient.IsInChannel(channelID);

			for (int i = 0; i < mDummyCL.size; ++i)
			{
				var channel = mDummyCL.buffer[i];
				if (channel.id == channelID) return !isNotLeaving || !channel.isLeaving;
			}
			return false;
		}

		/// <summary>
		/// Check to see if the specified player is present in the chosen channel.
		/// </summary>

		static public bool IsPlayerInChannel (Player p, int channelID)
		{
			if (p == player) return IsInChannel(channelID);
			var channel = GetChannel(channelID);
			if (channel != null) for (int i = 0; i < channel.players.size; ++i) if (channel.players.buffer[i] == p) return true;
			return false;
		}

		/// <summary>
		/// Check to see if the specified player is present in the chosen channel.
		/// </summary>

		static public bool IsPlayerInChannel (int playerID, int channelID)
		{
			if (playerID == TNManager.playerID) return IsInChannel(channelID);
			var channel = GetChannel(channelID);
			if (channel != null) for (int i = 0; i < channel.players.size; ++i) if (channel.players.buffer[i].id == playerID) return true;
			return false;
		}

		/// <summary>
		/// Get the player hosting the specified channel. Only works for the channels the player is in.
		/// </summary>

		static public Player GetHost (int channelID)
		{
			if (!isConnected) return mPlayer;
			return mInstance.mClient.GetHost(channelID);
		}

		/// <summary>
		/// The player's unique identifier.
		/// </summary>

		static public int playerID { get { return isConnected ? mInstance.mClient.playerID : mPlayer.id; } }

		/// <summary>
		/// Get or set the player's name as everyone sees him on the network.
		/// </summary>

		static public string playerName
		{
			get
			{
				return isConnected ? mInstance.mClient.playerName : mPlayer.name;
			}
			set
			{
				if (playerName != value)
				{
					mPlayer.name = value;
					if (mInstance && mInstance.mClient != null) mInstance.mClient.playerName = value;
				}
			}
		}

		/// <summary>
		/// Get or set the player's data, synchronizing it with the server.
		/// To change the data, use TNManager.SetPlayerData instead of changing the content directly.
		/// </summary>

		static public DataNode playerData
		{
			get
			{
				return isConnected ? mInstance.mClient.playerData : mPlayer.dataNode;
			}
			set
			{
				mPlayer.dataNode = value;
				if (isConnected) mInstance.mClient.SetPlayerData("", value);
			}
		}

		/// <summary>
		/// List of other players in the same channel as the client. This list does not include TNManager.player.
		/// </summary>

		[Obsolete("Use TNManager.GetPlayers(channelID)")]
		static public List<Player> players { get { return GetPlayers(lastChannelID); } }

		/// <summary>
		/// Get a list of players under the specified channel.
		/// This will only work for channels the player has joined.
		/// The returned list will not include TNManager.player.
		/// </summary>

		static public List<Player> GetPlayers (int channelID)
		{
			if (isConnected)
			{
				var ch = mInstance.mClient.GetChannel(channelID);
				if (ch != null) return ch.players;
			}

			if (mPlayers == null) mPlayers = new List<Player>();
			return mPlayers;
		}

		/// <summary>
		/// Get the local player.
		/// </summary>

		static public Player player { get { return isConnected ? mInstance.mClient.player : mPlayer; } }

		/// <summary>
		/// Ensure that we have a TNManager to work with.
		/// </summary>

		static TNManager instance
		{
			get
			{
#if UNITY_EDITOR
				if (!Application.isPlaying) return mInstance;
#endif
				if (mInstance == null)
				{
					if (mShuttingDown) return null;
					var go = new GameObject("Network Manager");
					mInstance = go.AddComponent<TNManager>();
					mDestroyed = false;
				}
				return mInstance;
			}
		}

#if !MODDING
		/// <summary>
		/// If you want to do custom logic for when packets can be processed and when they can't be, overwrite this delegate.
		/// </summary>

		static public ProcessPacketsFunc ProcessPackets = delegate ()
		{
			if (mInstance != null && mInstance.mLoadingLevel.size == 0)
			{
#if UNITY_EDITOR && UNITY_5_5_OR_NEWER
				UnityEngine.Profiling.Profiler.BeginSample("TNet.GameClient.ProcessPackets()");
				mInstance.mClient.ProcessPackets();
				UnityEngine.Profiling.Profiler.EndSample();
#else
				mInstance.mClient.ProcessPackets();
#endif
#if UNITY_EDITOR
				if (sentPackets > 1000 && TNServerInstance.isActive)
				{
#if COUNT_PACKETS
					var sb = new System.Text.StringBuilder();
					sb.Append("[TNet] Packets in the last second: Sent: " + sentPackets + " (" + sentBytes.ToString("N0") + " bytes), received: " +
						receivedPackets + " (" + receivedBytes.ToString("N0") + " bytes)");

					foreach (var ent in TNObject.lastSentDictionary)
					{
						sb.Append("\n");
						sb.Append(ent.Key);
						sb.Append(" = ");
						sb.Append(ent.Value);
					}

					Debug.LogWarning(sb.ToString());
#else
					//Debug.LogWarning("[TNet] Packets in the last second:\nSent: " + sentPackets + " (" + sentBytes.ToString("N0") + " bytes), received: " +
					//	receivedPackets + " (" + receivedBytes.ToString("N0") + " bytes)");
#endif
				}
#endif
			}
		};

		public delegate void ProcessPacketsFunc ();
#endif

		/// <summary>
		/// Server configuration is set by administrators.
		/// Don't try to edit it. Use GetServerData and SetServerData functions instead.
		/// </summary>

		static public DataNode serverData
		{
			get
			{
				return ((mInstance != null) ? mInstance.mClient.serverData : null) ?? mDummyNode;
			}
			set
			{
				if (mInstance != null)
					mInstance.mClient.serverData = value;
			}
		}

		static DataNode mDummyNode = new DataNode("Version", Player.version);

		/// <summary>
		/// Retrieve the specified server option.
		/// </summary>

		static public DataNode GetServerData (string key) { return (mInstance != null) ? mInstance.mClient.GetServerData(key) : null; }

		/// <summary>
		/// Retrieve the specified server option.
		/// </summary>

		static public T GetServerData<T> (string key) { return (mInstance != null) ? mInstance.mClient.GetServerData<T>(key) : default(T); }

		/// <summary>
		/// Retrieve the specified server option.
		/// </summary>

		static public T GetServerData<T> (string key, T def) { return (mInstance != null) ? mInstance.mClient.GetServerData<T>(key, def) : def; }

		/// <summary>
		/// Set the specified server option using key = value notation.
		/// </summary>

		static public void SetServerData (string text)
		{
			if (!string.IsNullOrEmpty(text))
			{
				var parts = text.Split(new char[] { '=' }, 2);

				if (parts.Length == 2)
				{
					var key = parts[0].Trim();
					var val = parts[1].Trim();
					var node = new DataNode(key, val);
					if (node.ResolveValue()) SetServerData(node.name, node.value);
				}
				else Debug.LogWarning("Invalid syntax [" + text + "]. Expected [key = value].");
			}
		}

		/// <summary>
		/// Set the specified server option.
		/// </summary>

		static public void SetServerData (DataNode node) { if (mInstance != null && isAdmin) mInstance.mClient.SetServerData(node); }

		/// <summary>
		/// Set the specified server option.
		/// </summary>

		static public void SetServerData (string key, object val) { if (mInstance != null && isAdmin) mInstance.mClient.SetServerData(key, val); }

		/// <summary>
		/// Return a channel with the specified ID. This will only work as long as the player is in this channel.
		/// </summary>

		static public Channel GetChannel (int channelID) { return isConnected ? mInstance.mClient.GetChannel(channelID, false) : null; }

		/// <summary>
		/// Convenience method: Retrieve the specified channel option.
		/// </summary>

		[System.Obsolete("Use GetChannelData(channelID, key) instead")]
		static public DataNode GetChannelData (string key)
		{
			if (!isConnected) return null;
			var ch = GetChannel(lastChannelID);
			return (ch != null) ? ch.Get(key) : null;
		}

		/// <summary>
		/// Convenience method: Retrieve the specified channel option.
		/// </summary>

		[System.Obsolete("Use GetChannelData(channelID, key) instead")]
		static public T GetChannelData<T> (string key)
		{
			if (!isConnected) return default(T);
			var ch = GetChannel(lastChannelID);
			return (ch != null) ? ch.Get<T>(key) : default(T);
		}

		/// <summary>
		/// Convenience method: Retrieve the specified channel option.
		/// </summary>

		[System.Obsolete("Use GetChannelData(channelID, key) instead")]
		static public T GetChannelData<T> (string key, T def)
		{
			if (!isConnected) return def;
			var ch = GetChannel(lastChannelID);
			return (ch != null) ? ch.Get<T>(key) : def;
		}

		/// <summary>
		/// Convenience method: Retrieve the specified channel option.
		/// </summary>

		static public DataNode GetChannelData (int channelID, string key)
		{
			if (!isConnected) return null;
			var ch = GetChannel(channelID);
			return (ch != null) ? ch.Get(key) : null;
		}

		/// <summary>
		/// Convenience method: Retrieve the specified channel option.
		/// </summary>

		static public T GetChannelData<T> (int channelID, string key)
		{
			if (!isConnected) return default(T);
			var ch = GetChannel(channelID);
			return (ch != null) ? ch.Get<T>(key) : default(T);
		}

		/// <summary>
		/// Convenience method: Retrieve the specified channel option.
		/// </summary>

		static public T GetChannelData<T> (int channelID, string key, T def)
		{
			if (!isConnected) return def;
			var ch = GetChannel(channelID);
			return (ch != null) ? ch.Get<T>(key, def) : def;
		}

		/// <summary>
		/// Set the specified channel option.
		/// </summary>

		[System.Obsolete("Use SetChannelData(channelID, key, value) instead")]
		static public void SetChannelData (string key, object val) { if (isConnected && isInChannel) mInstance.mClient.SetChannelData(lastChannelID, key, val); }

		/// <summary>
		/// Set the specified channel option.
		/// </summary>

		static public void SetChannelData (int channelID, string key, object val) { if (isConnected) mInstance.mClient.SetChannelData(channelID, key, val); }

		/// <summary>
		/// Get the player associated with the specified ID.
		/// </summary>

		static public Player GetPlayer (int id)
		{
			if (id == 0) return null;
			if (id == playerID) return player;
			if (isConnected) return mInstance.mClient.GetPlayer(id);
			return null;
		}

		/// <summary>
		/// Get the player associated with the specified name.
		/// </summary>

		static public Player GetPlayer (string name)
		{
			if (name == playerName) return player;
			if (isConnected) return mInstance.mClient.GetPlayer(name);
			return null;
		}

		/// <summary>
		/// Find the player associated with the specified partial name. This function is not case sensitive, so calling it with "Ren" will return "Aren Mook" as a possible choice.
		/// </summary>

		static public Player FindPlayer (string name, int channelID = 0)
		{
			var exact = GetPlayer(name);
			if (exact != null) return exact;

			var list = GetPlayers(channelID);

			for (int i = 0; i < list.size; ++i)
			{
				var p = list.buffer[i];
				if (p.name.IndexOf(name, System.StringComparison.CurrentCultureIgnoreCase) != -1) return p;
			}
			return null;
		}

		/// <summary>
		/// Get our player's data.
		/// </summary>

		static public DataNode GetPlayerData (string path) { return player.Get(path); }

		/// <summary>
		/// Convenience method: Get the specified value from our player.
		/// </summary>

		static public T GetPlayerData<T> (string path) { return player.Get<T>(path); }

		/// <summary>
		/// Convenience method: Get the specified value from our player.
		/// </summary>

		static public T GetPlayerData<T> (string path, T defaultVal) { return player.Get<T>(path, defaultVal); }

		/// <summary>
		/// Set the specified value on our player.
		/// </summary>

		static public void SetPlayerData (string path, object val)
		{
			if (isConnected)
			{
				mInstance.mClient.SetPlayerData(path, val);
			}
			else if (val != null)
			{
				// This forces the Serialize/Deserialize to be called, same as playing online would.
				// This ensures the same behaviour, with the Deserialize always called, online and offline.
				if (val is IBinarySerializable)
				{
					var o = (val as IBinarySerializable);
					var buffer = Buffer.Create();
					o.Serialize(buffer.BeginWriting());
					o = (IBinarySerializable)val.GetType().Create();
					o.Deserialize(buffer.BeginReading());
					player.Set(path, o);
					buffer.Recycle();
				}
				else if (val is IDataNodeSerializable)
				{
					var o = (val as IDataNodeSerializable);
					var dn = new DataNode("DN");
					o.Serialize(dn);
					o = (IDataNodeSerializable)val.GetType().Create();
					o.Deserialize(dn);
					player.Set(path, o);
				}
				else player.Set(path, val);
			}
			else player.Set(path, val);
		}

		/// <summary>
		/// Set the specified value on our player using key = value notation.
		/// </summary>

		static public void SetPlayerData (string text)
		{
			if (!string.IsNullOrEmpty(text))
			{
				string[] parts = text.Split(new char[] { '=' }, 2);

				if (parts.Length == 2)
				{
					string key = parts[0].Trim();
					string val = parts[1].Trim();
					DataNode node = new DataNode(key, val);
					if (node.ResolveValue()) SetPlayerData(node.name, node.value);
				}
				else Debug.LogWarning("Invalid syntax [" + text + "]. Expected [key = value].");
			}
		}

		/// <summary>
		/// Get a list of channels from the server.
		/// </summary>

		static public void GetChannelList (GameClient.OnGetChannels callback) { if (mInstance != null) mInstance.mClient.GetChannelList(callback); }

		/// <summary>
		/// Set the following function to handle this type of packets.
		/// </summary>

		static public void SetPacketHandler (byte packetID, GameClient.OnPacket callback)
		{
			if (!mShuttingDown && !mDestroyed && Application.isPlaying)
				instance.mClient.packetHandlers[packetID] = callback;
		}

		/// <summary>
		/// Set the following function to handle this type of packets.
		/// </summary>

		static public void SetPacketHandler (Packet packet, GameClient.OnPacket callback)
		{
			if (!mShuttingDown && !mDestroyed && Application.isPlaying)
				instance.mClient.packetHandlers[(byte)packet] = callback;
		}

		/// <summary>
		/// Start listening for incoming UDP packets on the specified port.
		/// </summary>

		static public bool StartUDP (int udpPort) { return instance.mClient.StartUDP(udpPort); }

		/// <summary>
		/// Stop listening to incoming UDP packets.
		/// </summary>

		static public void StopUDP () { if (mInstance != null) mInstance.mClient.StopUDP(); }

		/// <summary>
		/// Send a remote ping request to the specified TNet server.
		/// </summary>

		static public void Ping (IPEndPoint udpEndPoint, TNEvents.OnPing callback) { instance.mClient.Ping(udpEndPoint, callback); }

		/// <summary>
		/// Connect to a local server.
		/// </summary>

		static public void Connect ()
		{
			if (TNServerInstance.isActive)
			{
				Connect(Tools.localAddress.ToString(), TNServerInstance.listeningPort);
			}
			else Debug.LogError("Expecting an address to connect to or a local server to be started first.");
		}

		/// <summary>
		/// Connect to the specified destination with the address and port specified as "255.255.255.255:255".
		/// </summary>

		static public void Connect (string address)
		{
			if (isTryingToConnect) return;
			string[] split = address.Split(new char[] { ':' });
#if W2
			int port = 5137;
#else
			int port = 5127;
#endif
			if (split.Length == 2 && int.TryParse(split[1], out port)) Connect(split[0], port);
			else Connect(address, port);
		}

		/// <summary>
		/// Connect to the specified destination.
		/// </summary>

		static public void Connect (string address, int port)
		{
#if !MODDING
			if (!instance.mClient.isTryingToConnect && !instance.mClient.isConnected)
			{
				mInstance.CancelInvoke("DisconnectDelayed");
				mInstance.mDelayedDisconnect = false;
				mInstance.mClient.playerName = mPlayer.name;
				mInstance.mClient.playerData = (mPlayer.dataNode != null) ? mPlayer.dataNode.Clone() : null;

				if (TNServerInstance.isLocal)
				{
					instance.mClient.Connect(TNServerInstance.game);
				}
				else
				{
					Application.runInBackground = true;
					var ip = Tools.ResolveEndPoint(address, port);

					if (ip == null)
					{
						if (onConnect != null) onConnect(false, "Unable to resolve [" + address + "]");
					}
					else instance.mClient.Connect(ip, null);
				}
			}
#endif
		}

		/// <summary>
		/// Connect to the specified remote destination.
		/// </summary>

		static public void Connect (IPEndPoint externalIP, IPEndPoint internalIP)
		{
#if !MODDING
			if (!instance.mClient.isTryingToConnect && !instance.mClient.isConnected)
			{
				mInstance.CancelInvoke("DisconnectDelayed");
				mInstance.mDelayedDisconnect = false;
				mInstance.mClient.playerName = mPlayer.name;
				mInstance.mClient.playerData = (mPlayer.dataNode != null) ? mPlayer.dataNode.Clone() : null;
				mInstance.mClient.Connect(externalIP, internalIP);
			}
#endif
		}

		/// <summary>
		/// Disconnect from the specified destination.
		/// </summary>

		static public void Disconnect () { if (mInstance != null) mInstance.mClient.Disconnect(); }

		/// <summary>
		/// Disconnect after a specified delay in seconds.
		/// </summary>

		static public void Disconnect (float delay)
		{
			if (mInstance != null && !mInstance.mDelayedDisconnect)
			{
				mInstance.mDelayedDisconnect = true;
				mInstance.Invoke("DisconnectDelayed", delay);
			}
		}

		/// <summary>
		/// Log an error, complete with a stack trace, and disconnect from the server.
		/// </summary>

		static public void DisconnectWithException (string text)
		{
			try
			{
				throw new Exception(text);
			}
			catch (Exception ex)
			{
				Log("ERROR: " + ex.Message + "\n" + ex.StackTrace);
				Disconnect(1f);
			}
		}

		protected void DisconnectDelayed () { Disconnect(); }

		/// <summary>
		/// Join the specified channel.
		/// </summary>
		/// <param name="channelID">ID of the channel. Every player joining this channel will see one another.</param>
		/// <param name="persistent">Whether the channel will remain active even when the last player leaves.</param>

		static public void JoinChannel (int channelID, bool persistent = false, bool leaveCurrentChannel = false, TNEvents.OnJoinChannel onJoin = null)
		{
			JoinChannel(channelID, null, persistent, 65535, null, leaveCurrentChannel, onJoin);
		}

		/// <summary>
		/// Load the chosen scene. This delegate is called when it's time to load the specified scene. Don't try to call it yourself. Use TNManager.LoadLevel instead.
		/// </summary>

		static public LoadSceneFunc onLoadScene = delegate (string levelName)
		{
			if (!string.IsNullOrEmpty(levelName)) UnityTools.LoadScene(levelName);
		};

		/// <summary>
		/// Load the chosen scene asynchronously. This delegate is called when it's time to load the specified scene. Don't try to call it yourself. Use TNManager.LoadLevel instead.
		/// </summary>

		static public LoadSceneAsyncFunc onLoadSceneAsync = delegate (string levelName)
		{
			if (!string.IsNullOrEmpty(levelName)) return UnityTools.LoadSceneAsync(levelName);
			return null;
		};

		public delegate void LoadSceneFunc (string levelName);
		public delegate AsyncOperation LoadSceneAsyncFunc (string levelName);

		/// <summary>
		/// Join the specified channel. This channel will be marked as persistent, meaning it will
		/// stay open even when the last player leaves, unless explicitly closed first.
		/// </summary>
		/// <param name="channelID">ID of the channel. Every player joining this channel will see one another.</param>
		/// <param name="levelName">Level that will be loaded first.</param>

		static public void JoinChannel (int channelID, string levelName, bool persistent, bool leaveCurrentChannel = true, TNEvents.OnJoinChannel onJoin = null)
		{
			if (!IsInChannel(channelID))
			{
				if (leaveCurrentChannel) LeaveAllChannels();

				if (isConnected)
				{
					if (onJoin != null)
					{
						if (mJoinCallbacks == null) mJoinCallbacks = new System.Collections.Generic.Dictionary<int, TNEvents.OnJoinChannel>();
						mJoinCallbacks[channelID] = onJoin;
					}

					mInstance.mClient.JoinChannel(channelID, levelName, persistent, 65535, null);
				}
				else
				{
					var ch = new Channel();
					ch.id = channelID;
					ch.host = player;
					mDummyCL.Add(ch);
					lastChannelID = channelID;
					onLoadScene(levelName);
					if (onJoin != null) onJoin(channelID, true, null);
				}
			}
		}

		[NonSerialized] static System.Collections.Generic.Dictionary<int, TNEvents.OnJoinChannel> mJoinCallbacks;

		/// <summary>
		/// Join the specified channel.
		/// </summary>
		/// <param name="channelID">ID of the channel. Every player joining this channel will see one another.</param>
		/// <param name="levelName">Level that will be loaded first.</param>
		/// <param name="persistent">Whether the channel will remain active even when the last player leaves.</param>
		/// <param name="playerLimit">Maximum number of players that can be in this channel at once.</param>
		/// <param name="password">Password for the channel. First player sets the password.</param>

		static public void JoinChannel (int channelID, string levelName, bool persistent, int playerLimit, string password,
			bool leaveCurrentChannel = true, TNEvents.OnJoinChannel onJoin = null)
		{
			if (!IsInChannel(channelID))
			{
				if (leaveCurrentChannel) LeaveAllChannels();

				if (isConnected)
				{
					if (onJoin != null)
					{
						if (mJoinCallbacks == null) mJoinCallbacks = new System.Collections.Generic.Dictionary<int, TNEvents.OnJoinChannel>();
						mJoinCallbacks[channelID] = onJoin;
					}

					mInstance.mClient.JoinChannel(channelID, levelName, persistent, playerLimit, password);
				}
				else
				{
					var ch = new Channel();
					ch.id = channelID;
					ch.host = player;
					mDummyCL.Add(ch);
					lastChannelID = channelID;
					if (onLoadScene != null) onLoadScene(levelName);
					if (onJoinChannel != null) onJoinChannel(channelID, true, null);
					if (onJoin != null) onJoin(channelID, true, null);
				}
			}
		}

		/// <summary>
		/// Join a random open game channel or create a new one. Guaranteed to load the specified level.
		/// </summary>
		/// <param name="levelName">Level that will be loaded first.</param>
		/// <param name="persistent">Whether the channel will remain active even when the last player leaves.</param>
		/// <param name="playerLimit">Maximum number of players that can be in this channel at once.</param>
		/// <param name="password">Password for the channel. First player sets the password.</param>

		static public void JoinRandomChannel (string levelName, bool persistent, int playerLimit, string password, bool leaveCurrentChannel = true)
		{
			if (leaveCurrentChannel) LeaveAllChannels();
			if (isConnected) mInstance.mClient.JoinChannel(-2, levelName, persistent, playerLimit, password);
			else JoinChannel(UnityEngine.Random.Range(1000, 100000), levelName, persistent, playerLimit, password, false);
		}

		/// <summary>
		/// Create a new channel.
		/// </summary>
		/// <param name="levelName">Level that will be loaded first.</param>
		/// <param name="persistent">Whether the channel will remain active even when the last player leaves.</param>
		/// <param name="playerLimit">Maximum number of players that can be in this channel at once.</param>
		/// <param name="password">Password for the channel. First player sets the password.</param>

		static public void CreateChannel (string levelName, bool persistent, int playerLimit, string password, bool leaveCurrentChannel = true)
		{
			if (leaveCurrentChannel) LeaveAllChannels();
			if (isConnected) mInstance.mClient.JoinChannel(-1, levelName, persistent, playerLimit, password);
			else JoinChannel(UnityEngine.Random.Range(1000, 100000), levelName, persistent, playerLimit, password, false);
		}

		/// <summary>
		/// TNet 3.0 onwards makes it possible to join more than one channel at once.
		/// When the player is in more than one channel, commands need to specify which channel they are directed towards.
		/// </summary>

		[System.Diagnostics.DebuggerHidden]
		[System.Diagnostics.DebuggerStepThrough]
		static void ChannelCheck ()
		{
			if (channels.size > 1)
			{
				Debug.LogWarning("Currently in more than one channel! Specify which channel you want to work with.");
			}
		}

		/// <summary>
		/// Close the channel the player is in. New players will be prevented from joining.
		/// Once a channel has been closed, it cannot be re-opened.
		/// </summary>

		[System.Obsolete("Use CloseChannel(id)")]
		static public void CloseChannel () { CloseChannel(lastChannelID); }

		/// <summary>
		/// Close the channel the player is in. New players will be prevented from joining.
		/// Once a channel has been closed, it cannot be re-opened.
		/// </summary>

		static public void CloseChannel (int channelID) { if (mInstance != null) mInstance.mClient.CloseChannel(channelID); }

		/// <summary>
		/// Leave all of the channels we're currently in.
		/// </summary>

		static public void LeaveAllChannels ()
		{
			if (mInstance != null) mInstance.mClient.LeaveAllChannels();
			mDummyCL.Clear();
		}

		/// <summary>
		/// Leave the channel we're in.
		/// </summary>

		[System.Obsolete("Specify a channel ID")]
		static public void LeaveChannel () { LeaveChannel(lastChannelID); }

		/// <summary>
		/// Leave the channel we're in.
		/// </summary>

		static public void LeaveChannel (int channelID)
		{
			if (isConnected)
			{
#if SIGHTSEER && UNITY_EDITOR
				if (WorldGenerator.instanceID == channelID) UnityEngine.Debug.Log("LeaveChannel");
#endif
				mInstance.mClient.LeaveChannel(channelID);
			}
			else
			{
				for (int i = 0; i < mDummyCL.size; ++i)
				{
					if (mDummyCL.buffer[i].id == channelID)
					{
						mDummyCL.RemoveAt(i);
						return;
					}
				}
			}
		}

		/// <summary>
		/// Delete the specified channel.
		/// </summary>

		static public void DeleteChannel (int id, bool disconnect) { if (mInstance != null) mInstance.mClient.DeleteChannel(id, disconnect); }

		/// <summary>
		/// Change the maximum number of players that can join the channel the player is currently in.
		/// </summary>

		[System.Obsolete("Use SetPlayerLimit(channel, max)")]
		static public void SetPlayerLimit (int max) { SetPlayerLimit(lastChannelID, max); }

		/// <summary>
		/// Change the maximum number of players that can join the channel the player is currently in.
		/// </summary>

		static public void SetPlayerLimit (int channelID, int max) { if (mInstance != null) mInstance.mClient.SetPlayerLimit(channelID, max); }

		[System.Obsolete("Use TNManager.LoadScene(channel, name) instead")]
		static public void LoadLevel (string levelName) { LoadScene(lastChannelID, levelName); }

		[System.Obsolete("Function renamed to TNManager.LoadScene(channel, name) for clarity")]
		static public void LoadLevel (int channelID, string levelName) { LoadScene(channelID, levelName); }

		/// <summary>
		/// Load the specified scene on all clients.
		/// </summary>

		static public void LoadScene (int channelID, string levelName) { if (!mInstance.mClient.LoadLevel(channelID, levelName)) onLoadScene(levelName); }

		/// <summary>
		/// Save the specified file on the server.
		/// </summary>

		static public void SaveFile (string filename, byte[] data)
		{
			if (isConnected)
			{
				mInstance.mClient.SaveFile(filename, data);
			}
			else
			{
				try
				{
					Tools.WriteFile(filename, data);
				}
				catch (System.Exception ex)
				{
					Debug.LogError(ex.Message + " (" + filename + ")");
				}
			}
		}

		/// <summary>
		/// Load the specified file residing on the server.
		/// </summary>

		static public void LoadFile (string filename, GameClient.OnLoadFile callback)
		{
			if (callback != null)
			{
				if (isConnected)
				{
					mInstance.mClient.LoadFile(filename, callback);
				}
				else callback(filename, Tools.ReadFile(filename));
			}
		}

		/// <summary>
		/// Specify where the player's data should be saved. You only need to call this function once and TNet
		/// will automatically save the player file for you every time you use TNManager.SetPlayerData afterwards.
		/// </summary>

		static public void SetPlayerSave (string filename, DataNode.SaveType type = DataNode.SaveType.Binary, int hash = 0)
		{
#if !MODDING
			if (isConnected)
			{
				var b = CreatePacket(Packet.RequestSetPlayerSave);
				var writer = b.writer;
				writer.Write(filename);
				writer.Write((byte)type);
				writer.Write(hash);
				SendPacket(b);
			}
			else
			{
				playerData = DataNode.Read(filename);
				if (onSetPlayerData != null) onSetPlayerData(player, "", playerData);
			}
#endif
		}

		/// <summary>
		/// Delete the specified file on the server.
		/// </summary>

		static public void DeleteFile (string filename)
		{
#if !MODDING
			if (isConnected)
			{
				mInstance.mClient.DeleteFile(filename);
			}
			else
			{
				try
				{
					Tools.DeleteFile(filename);
				}
				catch (Exception ex)
				{
					Debug.LogError(ex.Message + " (" + filename + ")");
				}
			}
#endif
		}

		/// <summary>
		/// Change the hosting player.
		/// </summary>

		static public void SetHost (Player player) { SetHost(lastChannelID, player); }

		/// <summary>
		/// Change the hosting player.
		/// </summary>

		static public void SetHost (int channelID, Player player)
		{
			if (mInstance != null) mInstance.mClient.SetHost(channelID, player);
		}

		/// <summary>
		/// Set the timeout for the player. By default it's 10 seconds. If you know you are about to load a large level,
		/// and it's going to take, say 60 seconds, set this timeout to 120 seconds just to be safe. When the level
		/// finishes loading, change this back to 10 seconds so that dropped connections gets detected correctly.
		/// </summary>

		static public void SetTimeout (int seconds)
		{
			if (mInstance != null) mInstance.mClient.SetTimeout(seconds);
		}

		/// <summary>
		/// Lock the channel the player is currently in.
		/// </summary>

		[System.Obsolete("Use LockChannel(id, locked)")]
		static public void LockChannel (bool locked) { LockChannel(lastChannelID, locked); }

		/// <summary>
		/// Lock the specified channel, preventing all future persistent RFCs from being saved.
		/// </summary>

		static public void LockChannel (int channelID, bool locked)
		{
#if !MODDING
			if (mInstance != null && isAdmin)
			{
				var b = CreatePacket(Packet.RequestLockChannel);
				var writer = b.writer;
				writer.Write(channelID);
				writer.Write(locked);
				SendPacket(b, channelID);
			}
#endif
		}

		[System.Obsolete("You need to specify the channel ID as the first parameter")]
		static public void Instantiate (int rccID, string path, bool persistent, params object[] objs)
		{
			Instantiate(lastChannelID, rccID, null, path, persistent, objs);
		}

		[System.Obsolete("You need to specify the channel ID as the first parameter")]
		static public void Instantiate (string funcName, string path, bool persistent, params object[] objs)
		{
			Instantiate(lastChannelID, 0, funcName, path, persistent, objs);
		}

		/// <summary>
		/// Create a packet that will send a custom object creation call.
		/// Instantiate a new game object in the specified channel on all connected players.
		/// </summary>

		static public void Instantiate (int channelID, int rccID, string path, bool persistent, params object[] objs)
		{
			Instantiate(channelID, rccID, null, path, persistent, objs);
		}

		/// <summary>
		/// Create a packet that will send a custom object creation call.
		/// Instantiate a new game object in the specified channel on all connected players.
		/// </summary>

		static public void Instantiate (int channelID, string funcName, string path, bool persistent, params object[] objs)
		{
			Instantiate(channelID, 0, funcName, path, persistent, objs);
		}

		/// <summary>
		/// TNObject's ID, set right before calling the RCC function -- just in case you want to use it as a random seed.
		/// </summary>

		[System.NonSerialized]
		static public uint currentRccObjectID = 0;

		/// <summary>
		/// Create a packet that will send a custom object creation call.
		/// Instantiate a new game object in the specified channel on all connected players.
		/// </summary>

		static internal void Instantiate (int channelID, int rccID, string funcName, string path, bool persistent, params object[] objs)
		{
			if (path == null) path = "";

			lastChannelID = channelID;
			var go = UnityTools.LoadPrefab(path) ?? UnityTools.GetDummyObject();

			if (go != null && instance != null)
			{
				var func = GetRCC(rccID, funcName);

				if (func == null)
				{
					Debug.LogError("Unable to locate RCC " + rccID + " " + funcName);
				}
#if !MODDING
				else if (isConnected)
				{
					if (IsJoiningChannel(channelID))
					{
#if UNITY_EDITOR
						Debug.LogWarning("Trying to create an object while switching scenes. Call will be ignored.");
#endif
						return;
					}

					if (IsChannelLocked(channelID))
					{
#if UNITY_EDITOR
						Debug.LogWarning("Trying to create an object in a locked channel. Call will be ignored.");
#endif
						return;
					}

					if (!IsInChannel(channelID))
					{
#if UNITY_EDITOR
						Debug.LogWarning("Must join the channel #" + channelID + " first before calling instantiating objects.");
#endif
						return;
					}

					var b = CreatePacket(Packet.RequestCreateObject);
					var writer = b.writer;
					writer.Write(playerID);
					writer.Write(channelID);
					writer.Write(persistent ? (byte)1 : (byte)2);

					if (rccID > 0 && rccID < 256)
					{
						writer.Write((byte)rccID);
					}
					else
					{
						writer.Write((byte)0);
						writer.Write(funcName);
					}

					writer.Write(path);
					writer.WriteArray(objs);
					SendPacket(b, channelID);
				}
#endif
				else
				{
					// Offline mode
					currentRccObjectID = TNObject.GetUniqueID(true);
					objs = BinaryExtensions.CombineArrays(go, objs);
					go = func.Execute(objs) as GameObject;
					UnityTools.Clear(objs);

					if (go != null)
					{
						TNObject tno;
						if (!go.TryGetComponent(out tno)) tno = go.AddComponent<TNObject>();
						tno.id = currentRccObjectID;
						tno.channelID = channelID;
						go.SetActive(true);
						tno.Register();
					}
				}
			}
#if UNITY_EDITOR
			else Debug.LogError("Unable to load " + path);
#endif
		}

		/// <summary>
		/// Get the specified RCC.
		/// </summary>

		static public CachedFunc GetRCC (int rccID, string funcName)
		{
			CachedFunc func = null;

			if (rccID > 0 && rccID < 256 && !mDict0.TryGetValue(rccID, out func))
			{
				CacheRCCs();

				if (!mDict0.TryGetValue(rccID, out func))
				{
					mDict0[rccID] = null;
#if UNITY_EDITOR
					Debug.LogError("RCC(" + rccID + ")  was not found");
#endif
				}
			}

			if (func == null)
			{
				if (funcName != null)
				{
					if (!mDict1.TryGetValue(funcName, out func))
					{
						CacheRCCs();

						if (!mDict1.TryGetValue(funcName, out func))
						{
							mDict1[funcName] = null;
#if UNITY_EDITOR
							Debug.LogError("RCC(" + funcName + ") was not found");
#endif
						}
					}
				}
			}
			return func;
		}

		/// <summary>
		/// Automatically find and cache RCCs on all known MonoBehaviours.
		/// </summary>

		static void CacheRCCs ()
		{
			var mb = typeof(MonoBehaviour);
			var types = TypeExtensions.GetTypes();

			for (int i = 0; i < types.size; ++i)
			{
				var t = types.buffer[i];
				if (t.type.IsSubclassOf(mb)) AddRCCs(t.type);
			}
		}

		/// <summary>
		/// Add a new Remote Creation Call.
		/// </summary>

		static public void AddRCCs<T> () { AddRCCs(typeof(T)); }

		/// <summary>
		/// Add a new Remote Creation Call.
		/// </summary>

		static public void AddRCCs (Type type)
		{
			var cache = type.GetCache().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

			for (int b = 0, bmax = cache.Count; b < bmax; ++b)
			{
				var ci = cache.buffer[b];

				if (ci.method.IsDefined(typeof(RCC), true))
				{
					RCC tnc = (RCC)ci.method.GetCustomAttributes(typeof(RCC), true)[0];

					if (ci.method.ReturnType == typeof(GameObject))
					{
						CachedFunc ent = new CachedFunc();
						ent.mi = ci.method;

						if (tnc.id > 0 && tnc.id < 256) mDict0[tnc.id] = ent;
						else mDict1[ci.name] = ent;
					}
					else Debug.LogError("RCC(" + tnc.id + ") function [" + ci.name + "] must return an instantiated GameObject");
				}
			}
		}

		/// <summary>
		/// Built-in Remote Creation Call.
		/// </summary>

		[RCC(1)]
		static GameObject OnCreate1 (GameObject go)
		{
			go = Instantiate(go) as GameObject;
			go.SetActive(true);
			return go;
		}

		/// <summary>
		/// Built-in Remote Creation Call.
		/// </summary>

		[RCC(2)]
		static GameObject OnCreate2 (GameObject go, Vector3 pos, Quaternion rot)
		{
			go = Instantiate(go, pos, rot) as GameObject;
			go.SetActive(true);
			return go;
		}

		/// <summary>
		/// Built-in Remote Creation Call.
		/// </summary>

		[RCC(3)]
		static GameObject OnCreate3 (GameObject go, Vector3 pos, Quaternion rot, Vector3 velocity, Vector3 angularVelocity)
		{
			go = UnityTools.Instantiate(go, pos, rot, velocity, angularVelocity);
			go.SetActive(true);
			return go;
		}

		/// <summary>
		/// Write a server log entry.
		/// </summary>

		static public void Log (string text)
		{
#if UNITY_EDITOR
			if (!TNServerInstance.isActive) Debug.Log(text);
#endif
#if MODDING
			Debug.Log(text);
#else
			if (isConnected)
			{
				var b = CreatePacket(Packet.ServerLog);
				b.writer.Write(text);
				SendPacket(b);
			}
#endif
		}

		/// <summary>
		/// Create a new packet of specified type.
		/// </summary>

		static public Buffer CreatePacket (Packet type)  { return instance.mClient.CreatePacket(type); }

		/// <summary>
		/// Create a new packet of specified type.
		/// </summary>

		static public Buffer CreatePacket (byte type) { return instance.mClient.CreatePacket(type); }

		/// <summary>
		/// Send out the specified packet that was created with CreatePacket() earlier.
		/// </summary>

		static public void SendPacket (Buffer b, bool reliable = true, bool instant = false) { instance.mClient.SendPacket(b, reliable, instant); }

		/// <summary>
		/// Send the outgoing buffer.
		/// </summary>

		static public void SendPacket (Buffer b, int channelID, bool reliable = true, bool instant = false)
		{
			if (!IsJoiningChannel(channelID))
			{
				mInstance.mClient.SendPacket(b, reliable, instant);
			}
			else
			{
				b.EndPacket();
				b.Recycle();
#if UNITY_EDITOR
				Debug.LogWarning("Trying to send a packet to channel " + channelID + " while still joining it. This packet will be ignored. You should check [tno.canSend] before trying to send something.");
#endif
			}
		}

		/// <summary>
		/// Broadcast the packet to everyone on the LAN.
		/// </summary>

		static public void SendPacket (Buffer b, IPEndPoint target) { mInstance.mClient.SendPacket(b, target); }

		/// <summary>
		/// Broadcast the packet to everyone on the LAN.
		/// </summary>

		static public void BroadcastPacket (Buffer b, int port) { mInstance.mClient.BroadcastPacket(b, port); }

		/// <summary>
		/// Write the specified data into a local cache file belonging to connected server.
		/// </summary>

		static public bool WriteCache (string path, byte[] data, bool inMyDocuments = false)
		{
			if (isConnected)
			{
				IPEndPoint ip = tcpEndPoint;
				string addr = (ip != null) ? (ip.Address + ":" + ip.Port) : "127.0.0.1:5127";
				int code = addr.GetHashCode();
				if (code < 0) code = -code;
				return Tools.WriteFile("Temp/" + code + "/" + path, data, inMyDocuments, false);
			}
			return false;
		}

		/// <summary>
		/// Read the specified file from the cache belonging to the connected server.
		/// </summary>

		static public byte[] ReadCache (string path)
		{
			if (isConnected)
			{
				IPEndPoint ip = tcpEndPoint;
				string addr = (ip != null) ? (ip.Address + ":" + ip.Port) : "127.0.0.1:5127";
				int code = addr.GetHashCode();
				if (code < 0) code = -code;
				return Tools.ReadFile("Temp/" + code + "/" + path);
			}
			return null;
		}

		/// <summary>
		/// Export the specified objects from the server. The server will return the byte[] necessary to re-instantiate all of the specified objects and restore their state.
		/// </summary>

		static public void ExportObjects (List<TNObject> list, Action<byte[]> callback) { if (mInstance != null) mInstance.mClient.ExportObjects(list, callback); }

		/// <summary>
		/// Export the specified objects from the server. The server will return the DataNode necessary to re-instantiate all of the specified objects and restore their state.
		/// </summary>

		static public void ExportObjects (List<TNObject> list, Action<DataNode> callback) { if (mInstance != null) mInstance.mClient.ExportObjects(list, callback); }

		/// <summary>
		/// Import previously exported objects in the specified channel. The optional callback will contain the object IDs of instantiated objects.
		/// The callback will be called only after all objects have been instantiated and their RFCs have been called on all clients.
		/// </summary>

		static public void ImportObjects (int channelID, byte[] data, Action<uint[]> callback = null) { if (mInstance != null) mInstance.mClient.ImportObjects(channelID, data, callback); }

		/// <summary>
		/// Import previously exported objects in the specified channel. The optional callback will contain the object IDs of instantiated objects.
		/// The callback will be called only after all objects have been instantiated and their RFCs have been called on all clients.
		/// </summary>

		static public void ImportObjects (int channelID, Buffer data, Action<uint[]> callback = null) { if (mInstance != null) mInstance.mClient.ImportObjects(channelID, data, callback); }

		/// <summary>
		/// Import previously exported objects in the specified channel. The optional callback will contain the object IDs of instantiated objects.
		/// The callback will be called only after all objects have been instantiated and their RFCs have been called on all clients.
		/// </summary>

		static public void ImportObjects (int channelID, DataNode node, Action<uint[]> callback = null) { if (mInstance != null) mInstance.mClient.ImportObjects(channelID, node, callback); }

		#region MonoBehaviour and helper functions -- it's unlikely that you will need to modify these

		/// <summary>
		/// Ensure that there is only one instance of this class present.
		/// </summary>

		void Awake ()
		{
			if (mInstance != null)
			{
				UnityEngine.Object.Destroy(gameObject);
			}
			else
			{
				mInstance = this;
				rebuildMethodList = true;
				DontDestroyOnLoad(gameObject);
#if FORCE_EN_US
				Tools.SetCurrentCultureToEnUS();
#endif
				AddRCCs<TNManager>();
				SetDefaultCallbacks();

#if UNITY_EDITOR
				var ips = Tools.localAddresses;

				if (ips != null && ips.size > 0)
				{
					var ipv6 = (Tools.localAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6);
					var text = "[TNet] Local IPs: " + ips.size;

					for (int i = 0; i < ips.size; ++i)
					{
						var ip = ips.buffer[i];
						text += "\n  " + (i + 1) + ": " + ips.buffer[i];

						if (ip == Tools.localAddress)
						{
							text += " (LAN)";
						}
						else if (ipv6 && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
						{
							if (ip == Tools.externalAddress) text += " (WAN)";
						}
					}
					Debug.Log(text);
				}
#endif
			}
		}

		/// <summary>
		/// Set the built-in delegates.
		/// </summary>

		void SetDefaultCallbacks ()
		{
#if SQUIRREL
			Debug.Log("TNManager.SetDefaultCallbacks()");
#endif
			mClient.onConnect = OnConnect;
			mClient.onDisconnect = OnDisconnect;
			mClient.onJoinChannel = OnJoinChannel;
			mClient.onLeaveChannel = OnLeaveChannel;
			mClient.onLoadLevel = OnLoadLevel;
			mClient.onCreate = OnCreateObject;
			mClient.onDestroy = OnDestroyObject;
			mClient.onTransfer = OnTransferObject;
			mClient.onChangeOwner = OnChangeOwner;
			mClient.onForwardedPacket = OnForwardedPacket;
			mClient.onPlayerJoin = OnPlayerJoin;
			mClient.onPlayerLeave = OnPlayerLeave;
			mClient.onSetServerData = OnSetServerData;
			mClient.onSetChannelData = OnSetChannelData;
			mClient.onSetPlayerData = OnSetPlayerData;
		}

		void OnConnect (bool success, string message)
		{
			if (onConnect != null) onConnect(success, message);
		}

		void OnPlayerJoin (int channelID, Player p)
		{
			if (onPlayerJoin != null) onPlayerJoin(channelID, p);
		}

		void OnPlayerLeave (int channelID, Player p)
		{
			TNObject.OnPlayerLeave(channelID, p);
			if (onPlayerLeave != null) onPlayerLeave(channelID, p);
		}

		void OnSetServerData (string path, DataNode node)
		{
			if (onSetServerData != null) onSetServerData(path, node);
		}

		void OnSetChannelData (Channel ch, string path, DataNode node)
		{
			if (onSetChannelData != null) onSetChannelData(ch, path, node);
		}

		void OnSetPlayerData (Player p, string path, DataNode node)
		{
			if (onSetPlayerData != null) onSetPlayerData(p, path, node);
		}

		/// <summary>
		/// Make sure we disconnect on exit.
		/// </summary>

		void OnDestroy ()
		{
			if (mInstance == this)
			{
				mDestroyed = true;
				if (isConnected) mClient.Disconnect();
				mClient.StopUDP();
				mInstance = null;
			}
		}

		/// <summary>
		/// Find the index of the specified game object.
		/// </summary>

		static public int IndexOf (GameObject go)
		{
			if (go != null && mInstance != null && mInstance.objects != null)
			{
				for (int i = 0, imax = mInstance.objects.Length; i < imax; ++i)
					if (mInstance.objects[i] == go) return i;

				Debug.LogError("[TNet] The game object was not found in the TNManager's list of objects. Did you forget to add it?", go);
			}
			return -1;
		}

		void OnJoinChannel (int channelID, bool success, string message)
		{
			lastChannelID = channelID;
#if SQUIRREL
			Debug.Log("TNManager.OnJoinChannel #" + channelID + " = " + success + " (" + message + ")");
#endif
			if (onJoinChannel != null) onJoinChannel(channelID, success, message);

			if (mJoinCallbacks != null)
			{
				TNEvents.OnJoinChannel onJoin;

				if (mJoinCallbacks.TryGetValue(channelID, out onJoin))
				{
#if SQUIRREL
					Debug.Log("Found a callback... executing...");
#endif
					onJoin(channelID, success, message);
					mJoinCallbacks.Remove(channelID);
				}
			}
		}

		void OnLeaveChannel (int channelID)
		{
			UnityEngine.Profiling.Profiler.BeginSample("CleanupChannelObjects");
			TNObject.CleanupChannelObjects(channelID);
			UnityEngine.Profiling.Profiler.EndSample();

			if (onLeaveChannel != null) onLeaveChannel(channelID);

			if (lastChannelID == channelID)
			{
				var chs = channels;

				for (int i = 0; i < chs.size; ++i)
				{
					var ch = chs.buffer[i];

					if (ch.id != lastChannelID)
					{
						lastChannelID = ch.id;
						return;
					}
				}

				lastChannelID = 0;
			}
		}

		void OnLoadLevel (int channelID, string levelName)
		{
			lastChannelID = channelID;
			TNObject.CleanupChannelObjects(channelID);
#if SQUIRREL
			Debug.Log("TNManager.OnLoadLevel #" + channelID + " = " + levelName);
#endif
			if (!string.IsNullOrEmpty(levelName))
			{
				mLoadingLevel.Add(channelID);
				StartCoroutine("LoadLevelCoroutine", new System.Collections.Generic.KeyValuePair<int, string>(channelID, levelName));
			}

			if (onLoadLevel != null) onLoadLevel(channelID, levelName);
		}

		void OnDisconnect ()
		{
			if (onDisconnect != null) onDisconnect();
			mLoadingLevel.Clear();
		}

		void OnCreateObject (int channelID, int creator, uint objectID, BinaryReader reader)
		{
			currentObjectOwner = GetPlayer(creator) ?? GetHost(channelID);

			lastChannelID = channelID;
			var rccID = reader.ReadByte();
			var funcName = (rccID == 0) ? reader.ReadString() : null;
			var func = GetRCC(rccID, funcName);

			// Load the object from the resources folder
			var prefab = reader.ReadString();
			var go = UnityTools.LoadPrefab(prefab);

			if (go == null)
			{
				go = UnityTools.GetDummyObject();

#if UNITY_EDITOR
				if (!string.IsNullOrEmpty(prefab)) Debug.LogError("[TNet] Unable to find prefab \"" + prefab + "\". Make sure it's in the Resources folder.");
#else
				if (!string.IsNullOrEmpty(prefab)) Debug.LogError("[TNet] Unable to find prefab \"" + prefab + "\"");
#endif
			}

			currentRccObjectID = objectID;

			if (func != null)
			{
				// Custom creation function
				try
				{
					var objs = reader.ReadArray(go);
					go = func.Execute(objs) as GameObject;
					UnityTools.Clear(objs);
				}
				catch (Exception ex)
				{
					var c = GetPlayer(creator);
					var srcName = (c != null) ? c.name : "unknown";
					Debug.LogError("[TNet] OnCreateObject failed: " + ex.Message + "\n" + ex.StackTrace +
						$"\n    {rccID} ({funcName}), prefab: {prefab}, src: {srcName}", go);

					if (GameClient.currentBuffer != null)
					{
						var byteCount = GameClient.currentBuffer.size;
						var fn = "Errors/error_" + serverTimeMS + ".packet";
						Tools.WriteFile(fn, GameClient.currentBuffer.buffer, true, false, byteCount);
					}
				}
			}
			// Fallback to a very basic function
			else go = OnCreate1(go);

			if (go != null)
			{
				// Network objects should only be destroyed when leaving their channel
				var t = go.transform;
				if (t.parent == null) DontDestroyOnLoad(go);

				// If an object ID was requested, assign it to the TNObject
				if (objectID != 0)
				{
					TNObject obj;
					if (!go.TryGetComponent(out obj)) obj = go.AddComponent<TNObject>();
					obj.channelID = channelID;
					obj.id = objectID;
					go.SetActive(true);
					obj.Register();
				}
				else
				{
#if UNITY_EDITOR
					Debug.LogWarning("Object ID is 0. Intentional?", go);
#endif
					go.SetActive(true);
				}
			}

			currentObjectOwner = null;
		}

		/// <summary>
		/// Notification of a network object being destroyed.
		/// </summary>

		void OnDestroyObject (int channelID, uint objID)
		{
			var obj = TNObject.Find(channelID, objID);
			if (obj) obj.OnDestroyPacket();
		}

		/// <summary>
		/// Notification of a network object being transferred to another channel.
		/// </summary>

		void OnTransferObject (int oldChannelID, int newChannelID, uint oldObjectID, uint newObjectID)
		{
			if (IsInChannel(oldChannelID))
			{
				var obj = TNObject.Find(oldChannelID, oldObjectID);
				if (obj) obj.FinalizeTransfer(newChannelID, newObjectID);
#if UNITY_EDITOR
				else Debug.LogWarning("Unable to find TNO #" + oldObjectID + " in channel " + oldChannelID);
#endif
			}
		}

		/// <summary>
		/// Notification of the object's owner being changed.
		/// </summary>

		void OnChangeOwner (int channelID, uint objectID, Player p)
		{
			var obj = TNObject.Find(channelID, objectID);
			if (obj != null) obj.OnChangeOwnerPacket(p);
		}

		void OnApplicationQuit () { mShuttingDown = true; }

		/// <summary>
		/// If custom functionality is needed, all unrecognized packets will arrive here.
		/// </summary>

		void OnForwardedPacket (int channelID, BinaryReader reader)
		{
			uint objID;
			byte funcID;
			var start = reader.BaseStream.Position;
			TNObject.DecodeUID(reader.ReadUInt32(), out objID, out funcID);

			if (funcID == 0)
			{
				string funcName = "";

#if SAFE_EXCEPTIONS
				try
#endif
				{
					funcName = reader.ReadString();
					UnityEngine.Profiling.Profiler.BeginSample(funcName);
					var array = reader.ReadArray();
					var result = TNObject.FindAndExecute(channelID, objID, funcName, array);
					UnityEngine.Profiling.Profiler.EndSample();

					if (result != null && !result.success)
					{
						var last = reader.BaseStream.Position;
						var fn = "Errors/Error_" + funcName + "_" + start + ".dat";

						reader.BaseStream.Seek(0, SeekOrigin.End);
						var len = reader.BaseStream.Position;
						reader.BaseStream.Seek(0, SeekOrigin.Begin);
						var bytes = reader.ReadBytes((int)len);

						reader.BaseStream.Position = last < len ? last : len;

						Tools.WriteFile(fn, bytes, true);
						Debug.Log("Packet saved as " + fn);
					}
				}
#if SAFE_EXCEPTIONS
				catch (Exception ex)
				{
					Debug.LogError(objID + " " + funcID + " " + funcName + "\n" + ex.Message + "\n" + ex.StackTrace, TNObject.Find(((ulong)channelID << 32) | objID));
				}
#endif
			}
			else
			{
#if SAFE_EXCEPTIONS
				try
#endif
				{
					var arr = reader.ReadArray();
					TNObject.FindAndExecute(channelID, objID, funcID, arr);
				}
#if SAFE_EXCEPTIONS
				catch (Exception ex)
				{
					Debug.LogError(objID + " " + funcID + "\n" + ex.Message + "\n" + ex.StackTrace, TNObject.Find(((ulong)channelID << 32) | objID));
				}
#endif
			}
		}

		/// <summary>
		/// Called from TNManager's Update() function.
		/// </summary>

		[System.NonSerialized] static public Action onUpdate;

		/// <summary>
		/// Process incoming packets in the update function.
		/// </summary>

		void Update ()
		{
			if (onUpdate != null) onUpdate();
#if !MODDING
			if (!mDelayedDisconnect) ProcessPackets();
#endif
		}

#endregion

		/// <summary>
		/// Load level coroutine handling asynchronous loading of levels.
		/// </summary>

		System.Collections.IEnumerator LoadLevelCoroutine (System.Collections.Generic.KeyValuePair<int, string> pair)
		{
			yield return null;

			loadLevelOperation = onLoadSceneAsync(pair.Value);

			if (loadLevelOperation != null)
			{
				loadLevelOperation.allowSceneActivation = false;

				while (loadLevelOperation.progress < 0.9f)
					yield return null;

				loadLevelOperation.allowSceneActivation = true;
				yield return loadLevelOperation;

				loadLevelOperation = null;
			}

			mLoadingLevel.Remove(pair.Key);
		}

		/// <summary>
		/// When a level is being loaded, this value will contain the async coroutine for the LoadLevel operation.
		/// You can yield on it if you need.
		/// </summary>

		static public AsyncOperation loadLevelOperation = null;

		void OnApplicationPause (bool paused) { isPaused = paused; }

		/// <summary>
		/// Send a chat message to everyone on the server. If you want custom parameters or options, such as sending packets to a specific group of players,
		/// it's best to make a custom RFC on a persistent "global chat" game object instead.
		/// </summary>

		static public void SendChat (string text) { if (mInstance != null) mInstance.mClient.SendChat(text); }

		/// <summary>
		/// Bounces a packet from the server, then executes the chosen callback. This is useful in case you create a send a bunch of data to the server,
		/// then wait for it to get forwarded to all clients before proceeding with the rest of your code. Example usage:
		/// 
		///		TNManager.SendCallback(delegate ()
		///		{
		///			// Everything here will be executed after the packet comes back from the server.
		///			// Note that this code will only execute on the client that made the call, as it's not an RFC.
		///		});
		/// </summary>

		static public void SendCallback (Action callback)
		{
			if (isConnected) mInstance.mClient.SendCallback(callback);
			else if (callback != null) callback();
		}

		/// <summary>
		/// Bounces a packet from the server, then executes the chosen callback. This is useful in case you create a send a bunch of data to the server,
		/// then wait for it to get forwarded to all clients before proceeding with the rest of your code. Example usage:
		/// 
		///		TNManager.SendCallback(delegate ()
		///		{
		///			// Everything here will be executed after the packet comes back from the server.
		///			// You can use this to effectively ping servers without connecting to them first, provided both parties have UDP enabled.
		///		}, ip);
		/// </summary>

		static public void SendCallback (Action callback, IPEndPoint ip) { if (isPlaying) client.SendCallback(callback, ip); }

		/// <summary>
		/// This can be used in a yield statement in order to wait for all previously called packets to travel to the server and get a response back.
		/// This is useful in case you create a bunch of objects, then want to wait (yield) for these objects to be fully instantiated before proceeding with your code.
		/// Example usage:
		/// 
		/// IEnumerator Start ()
		/// {
		///		// Do object instantiation RCC or RFC calls here
		///		
		///		// Wait for all of the above calls to finish being forwarded to all clients
		///		yield return TNManager.WaitForBounceBack();
		///		
		///		// Now you can proceed with the rest of your code which guarantees to execute after all RCC and RFCs have arrived.
		///	}
		/// </summary>

		static public System.Collections.IEnumerator WaitForBounceBack ()
		{
			var done = false;
			SendCallback(delegate () { done = true; });
			while (!done) yield return null;
		}

#region Obsolete calls, kept for backwards compatibility
		[Obsolete("Renamed to SendChat")]
		static public void SendGlobalChat (string text) { if (mInstance != null) mInstance.mClient.SendChat(text); }

		/// <summary>
		/// Send a private chat message.
		/// </summary>

		static public void SendPM (Player p, string text) { if (mInstance != null) mInstance.mClient.SendChat(text, p); }

		[Obsolete("Use TNManager.playerData")]
		static public DataNode playerDataNode { get { return playerData; } }

		//[Obsolete("Use TNManager.serverData instead")]
		//static public DataNode serverOptions { get { return serverData; } }

		[Obsolete("Use TNManager.SetServerData instead")]
		static public void SetServerOption (string text) { SetServerData(text); }

		[Obsolete("Use TNManager.SetServerData instead")]
		static public void SetServerOption (string key, object val) { SetServerData(key, val); }

		[Obsolete("Use TNManager.SetServerData(key, value) instead")]
		static public void SetServerOption (DataNode node) { SetServerData(node.name, node.value); }

		[Obsolete("Use TNManager.SetChannelData(key, value) instead")]
		static public void SetChannelOption (DataNode node) { SetChannelData(node.name, node.value); }

		[Obsolete("Use TNManager.packetSourceIP or TNManager.packetSourceID instead")]
		static public IPEndPoint packetSource { get { return (mInstance != null) ? mInstance.mClient.packetSourceIP : null; } }

		[Obsolete("It's now possible to be in more than one channel at once. Use TNManager.IsChannelLocked(channelID) instead.")]
		static public bool isChannelLocked { get { return IsChannelLocked(lastChannelID); } }

		[Obsolete("Use TNManager.GetChannelData(id, data) and TNManager.SetChannelData(id, data) instead")]
		static public string channelData
		{
			get
			{
				Channel ch = GetChannel(lastChannelID);
				return (ch != null) ? ch.Get<string>("channelData") : null;
			}
			set
			{
				SetChannelData("channelData", value);
			}
		}

		[Obsolete("All TNObjects have channel IDs associated with them -- use them instead.")]
		static public int channelID { get { return lastChannelID; } }

		[Obsolete("It's now possible to be in more than one channel at once. Use TNManager.GetHost(channelID) instead.")]
		static public int hostID { get { return GetHost(lastChannelID).id; } }

		[Obsolete("You should create a custom RCC and use TNManager.Instantiate instead of using this function")]
		static internal void Create (string path, bool persistent = true) { Instantiate(lastChannelID, 1, null, path, persistent); }

		[Obsolete("You should create a custom RCC and use TNManager.Instantiate instead of using this function")]
		static internal void Create (string path, Vector3 pos, Quaternion rot, bool persistent = true) { Instantiate(lastChannelID, 2, null, path, persistent, pos, rot); }

		[Obsolete("You should create a custom RCC and use TNManager.Instantiate instead of using this function")]
		static internal void Create (string path, Vector3 pos, Quaternion rot, Vector3 vel, Vector3 angVel, bool persistent = true) { Instantiate(lastChannelID, 3, null, path, persistent, pos, rot, vel, angVel); }

		[Obsolete("You should create a custom RCC and use TNManager.Instantiate instead of using this function")]
		static internal void CreateEx (int rccID, bool persistent, string path, params object[] objs) { Instantiate(lastChannelID, rccID, path, persistent, objs); }

		[NonSerialized] static Buffer mBuffer;

		/// <summary>
		/// Begin sending a new packet to the server.
		/// </summary>

		[Obsolete("Use CreatePacket/SendPacket instead as they are thread-safe")]
		static public BinaryWriter BeginSend (Packet type) { return BeginSend((byte)type); }

		/// <summary>
		/// Begin sending a new packet to the server.
		/// </summary>

		[Obsolete("Use CreatePacket/SendPacket instead as they are thread-safe")]
		static public BinaryWriter BeginSend (byte packetID)
		{
			Debug.Assert(mBuffer == null, "BeginSend is not thread safe. Use CreatePacket/SendPacket instead");
			mBuffer = instance.mClient.CreatePacket(packetID);
			return mBuffer.BeginPacket(packetID);
		}

		/// <summary>
		/// Send the outgoing buffer. This should only be used for generic packets going straight to the server.
		/// Packets that are going to a channel should use EndSend(channelID, reliable) function instead.
		/// </summary>

		[Obsolete("Use CreatePacket/SendPacket instead as they are thread-safe")]
		static public void EndSend ()
		{
			if (mBuffer != null)
			{
				SendPacket(mBuffer);
				mBuffer = null;
			}
		}

		/// <summary>
		/// Send the outgoing buffer.
		/// </summary>

		[Obsolete("Use CreatePacket/SendPacket instead as they are thread-safe")]
		static public void EndSend (int channelID, bool reliable = true)
		{
			if (mBuffer != null)
			{
				SendPacket(mBuffer, channelID, reliable);
				mBuffer = null;
			}
		}

		/// <summary>
		/// Broadcast the packet to everyone on the LAN.
		/// </summary>

		[Obsolete("Use CreatePacket/SendPacket instead as they are thread-safe")]
		static public void EndSendToLAN (int port)
		{
			if (mBuffer != null)
			{
				SendPacket(mBuffer, port);
				mBuffer = null;
			}
		}

		/// <summary>
		/// Broadcast the packet to the specified endpoint via UDP.
		/// </summary>

		[Obsolete("Use CreatePacket/SendPacket instead as they are thread-safe")]
		static public void EndSend (IPEndPoint target)
		{
			if (mBuffer != null)
			{
				SendPacket(mBuffer, target);
				mBuffer = null;
			}
		}

		[Obsolete("You need to specify a channel ID to send the packet to: TNManager.EndSend(channelID, reliable);")]
		static public void EndSend (bool reliable)
		{
			if (channels.size > 1) Debug.LogWarning("You need to specify which channel this packet should be going to");
			SendPacket(mBuffer, lastChannelID, reliable);
			mBuffer = null;
		}

		[System.Obsolete("Use TNManager.GetServerData instead")]
		static public DataNode GetServerOption (string key) { return (mInstance != null) ? mInstance.mClient.GetServerData(key) : null; }

		[System.Obsolete("Use TNManager.GetServerData instead")]
		static public T GetServerOption<T> (string key) { return (mInstance != null) ? mInstance.mClient.GetServerData<T>(key) : default(T); }

		[System.Obsolete("Use TNManager.GetServerData instead")]
		static public T GetServerOption<T> (string key, T def) { return (mInstance != null) ? mInstance.mClient.GetServerData<T>(key, def) : def; }

		[System.Obsolete("Use gameObject.DestroySelf() instead")]
		static public void Destroy (GameObject go) { go.DestroySelf(); }
#endregion
	}
}
