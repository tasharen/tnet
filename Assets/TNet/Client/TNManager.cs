//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using System.IO;
using UnityEngine;
using System.Net;
using System.Reflection;
using UnityTools = TNet.UnityTools;
using System;

namespace TNet
{
/// <summary>
/// Tasharen Network Manager tailored for Unity.
/// </summary>

public class TNManager : MonoBehaviour
{
	// Will be 'true' at play time unless the application is shutting down. 'false' at edit time.
	static bool isPlaying { get { return !mDestroyed && Application.isPlaying; } }
	static bool mDestroyed = false;

#region Delegates
	/// <summary>
	/// Ping notification.
	/// </summary>

	static public TNEvents.OnPing onPing { get { return isPlaying ? instance.mClient.onPing : null; } set { if (isPlaying) instance.mClient.onPing = value; } }

	/// <summary>
	/// Error notification.
	/// </summary>

	static public TNEvents.OnError onError { get { return isPlaying ? instance.mClient.onError : null; } set { if (isPlaying) instance.mClient.onError = value; } }

	/// <summary>
	/// Connection attempt result indicating success or failure.
	/// </summary>

	static public TNEvents.OnConnect onConnect { get { return isPlaying ? instance.mClient.onConnect : null; } set { if (isPlaying) instance.mClient.onConnect = value; } }

	/// <summary>
	/// Notification sent after the connection terminates for any reason.
	/// </summary>

	static public TNEvents.OnDisconnect onDisconnect { get { return isPlaying ? instance.mClient.onDisconnect : null; } set { if (isPlaying) instance.mClient.onDisconnect = value; } }

	/// <summary>
	/// Notification sent when attempting to join a channel, indicating a success or failure.
	/// </summary>

	static public TNEvents.OnJoinChannel onJoinChannel { get { return isPlaying ? instance.mClient.onJoinChannel : null; } set { if (isPlaying) instance.mClient.onJoinChannel = value; } }

	/// <summary>
	/// Notification sent when leaving a channel for any reason, including being disconnected.
	/// </summary>

	static public TNEvents.OnLeaveChannel onLeaveChannel { get { return isPlaying ? instance.mClient.onLeaveChannel : null; } set { if (isPlaying) instance.mClient.onLeaveChannel = value; } }

	/// <summary>
	/// Notification sent when changing levels.
	/// </summary>

	static public TNEvents.OnLoadLevel onLoadLevel { get { return isPlaying ? instance.mClient.onLoadLevel : null; } set { if (isPlaying) instance.mClient.onLoadLevel = value; } }

	/// <summary>
	/// Notification sent when a new player joins the channel.
	/// </summary>

	static public TNEvents.OnPlayerJoin onPlayerJoin { get { return isPlaying ? instance.mClient.onPlayerJoin : null; } set { if (isPlaying) instance.mClient.onPlayerJoin = value; } }

	/// <summary>
	/// Notification sent when a player leaves the channel.
	/// </summary>

	static public TNEvents.OnPlayerLeave onPlayerLeave { get { return isPlaying ? instance.mClient.onPlayerLeave : null; } set { if (isPlaying) instance.mClient.onPlayerLeave = value; } }

	/// <summary>
	/// Notification of some player changing their name.
	/// </summary>

	static public TNEvents.OnRenamePlayer onRenamePlayer { get { return isPlaying ? instance.mClient.onRenamePlayer : null; } set { if (isPlaying) instance.mClient.onRenamePlayer = value; } }

	/// <summary>
	/// Notification sent when the channel's host changes.
	/// </summary>

	static public TNEvents.OnHostChanged onHostChanged { get { return isPlaying ? instance.mClient.onHostChanged : null; } set { if (isPlaying) instance.mClient.onHostChanged = value; } }

	/// <summary>
	/// Notification sent when the server's data gets changed.
	/// </summary>

	static public TNEvents.OnSetServerData onSetServerData { get { return isPlaying ? instance.mClient.onSetServerData : null; } set { if (isPlaying) instance.mClient.onSetServerData = value; } }

	/// <summary>
	/// Notification sent when the channel's data gets changed.
	/// </summary>

	static public TNEvents.OnSetChannelData onSetChannelData { get { return isPlaying ? instance.mClient.onSetChannelData : null; } set { if (isPlaying) instance.mClient.onSetChannelData = value; } }

	/// <summary>
	/// Notification sent when player data gets changed.
	/// </summary>

	static public TNEvents.OnSetPlayerData onSetPlayerData { get { return isPlaying ? instance.mClient.onSetPlayerData : null; } set { if (isPlaying) instance.mClient.onSetPlayerData = value; } }

	/// <summary>
	/// Callback triggered when the channel becomes locked or unlocked.
	/// </summary>

	static public TNEvents.OnLockChannel onLockChannel { get { return isPlaying ? instance.mClient.onLockChannel : null; } set { if (isPlaying) instance.mClient.onLockChannel = value; } }

	/// <summary>
	/// Callback triggered when the player gets verified as an administrator.
	/// </summary>

	static public TNEvents.OnSetAdmin onSetAdmin { get { return isPlaying ? instance.mClient.onSetAdmin : null; } set { if (isPlaying) instance.mClient.onSetAdmin = value; } }
#endregion

	/// <summary>
	/// Whether the application is currently paused.
	/// </summary>

	static public bool isPaused = false;

	/// <summary>
	/// If set to 'true', the list of custom creation functions will be rebuilt the next time it's accessed.
	/// </summary>

	static public bool rebuildMethodList = true;

	// Cached list of creation functions
	static System.Collections.Generic.Dictionary<int, CachedFunc> mDict0 = new System.Collections.Generic.Dictionary<int, CachedFunc>();
	static System.Collections.Generic.Dictionary<string, CachedFunc> mDict1 = new System.Collections.Generic.Dictionary<string, CachedFunc>();

	// Static player, here just for convenience so that GetPlayer() works the same even if instance is missing.
#if UNITY_EDITOR
	static Player mPlayer = new Player("Editor");
#else
	static Player mPlayer = new Player("Guest");
#endif

	// Player list that will contain only the player in it. Here for the same reason as 'mPlayer'.
	static List<Player> mPlayers;

	// Instance pointer
	static TNManager mInstance;

	/// <summary>
	/// Object owner is only valid during object creation. In most cases you will want to use tno.owner.
	/// </summary>

	static internal Player currentObjectOwner = null;

	/// <summary>
	/// List of objects that can be instantiated by the network.
	/// </summary>

	public GameObject[] objects;

	// Network client
	[System.NonSerialized] GameClient mClient = new GameClient();
	[System.NonSerialized] List<int> mLoadingLevel = new List<int>();

	/// <summary>
	/// Whether the player has verified himself as an administrator.
	/// </summary>

	static public bool isAdmin { get { return (mInstance == null || !mInstance.mClient.isConnected || mInstance.mClient.isAdmin); } }

	/// <summary>
	/// Set administrator privileges. Note that failing the password test will cause a disconnect.
	/// </summary>

	static public void SetAdmin (string passKey) { if (mInstance) mInstance.mClient.SetAdmin(passKey); }

	/// <summary>
	/// Add a new pass key to the admin file.
	/// </summary>

	static public void AddAdmin (string passKey)
	{
		if (TNManager.isAdmin)
		{
			TNManager.BeginSend(Packet.RequestCreateAdmin).Write(passKey);
			TNManager.EndSend();
		}
	}

	/// <summary>
	/// Remove the specified pass key from the admin file.
	/// </summary>

	static public void RemoveAdmin (string passKey)
	{
		if (TNManager.isAdmin)
		{
			TNManager.BeginSend(Packet.RequestRemoveAdmin).Write(passKey);
			TNManager.EndSend();
		}
	}

	/// <summary>
	/// Set a player alias. Player aliases can be used to store useful player-associated data such as Steam usernames,
	/// database IDs, or other unique identifiers. Aliases will show up in TNet's log and can also be banned by on
	/// the server side. When a player is banned, all their aliases are banned as well, so be careful to make sure
	/// that they are indeed unique. All aliases are visible via TNet.Player.aliases list of each player.
	/// </summary>

	static public void SetAlias (string alias)
	{
		if (mInstance)
		{
			BeginSend(Packet.RequestSetAlias).Write(alias);
			EndSend();
		}
	}

	/// <summary>
	/// TNet Client used for communication.
	/// </summary>

	static public GameClient client
	{
		get
		{
			return mInstance != null ? mInstance.mClient : (mDestroyed ? null : instance.mClient);
		}
	}

	/// <summary>
	/// Whether we're currently connected.
	/// </summary>

	static public bool isConnected { get { return mInstance != null && mInstance.mClient.isConnected; } }

	/// <summary>
	/// Whether we are currently in the process of joining a channel.
	/// To find out whether we are joining a specific channel, use the "IsJoiningChannel(id)" function.
	/// </summary>

	static public bool isJoiningChannel { get { return IsJoiningChannel(-1); } }

	/// <summary>
	/// Whether we are currently trying to join the specified channel.
	/// </summary>

	static public IsJoiningChannelFunc IsJoiningChannel = delegate(int channelID)
	{
		if (mInstance == null) return false;
		
		if (channelID < 0)
		{
			return (mInstance.mLoadingLevel.size != 0 || mInstance.mClient.isJoiningChannel);
		}
		return mInstance.mLoadingLevel.Contains(channelID) || mInstance.mClient.IsJoiningChannel(channelID);
	};

	public delegate bool IsJoiningChannelFunc (int channelID);

	/// <summary>
	/// Whether we are currently trying to establish a new connection.
	/// </summary>

	static public bool isTryingToConnect { get { return mInstance != null && mInstance.mClient.isTryingToConnect; } }

	/// <summary>
	/// Whether we're currently hosting.
	/// </summary>

	static public bool isHosting { get { return GetHost(lastChannelID) == player; } }

	/// <summary>
	/// Whether we're currently in any channel. To find out if we are in a specific channel, use TNManager.IsInChannel(id).
	/// </summary>

	static public bool isInChannel
	{
		get
		{
			return !isJoiningChannel && (mInstance == null || mInstance.mClient == null ||
				(mInstance.mClient.isConnected && mInstance.mClient.isInChannel));
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
	/// Current ping to the server.
	/// </summary>

	static public int ping { get { return mInstance != null ? mInstance.mClient.ping : 0; } }

	/// <summary>
	/// Whether we can use unreliable packets (UDP) to communicate with the server.
	/// </summary>

	static public bool canUseUDP { get { return mInstance != null && mInstance.mClient.canUseUDP; } }

	/// <summary>
	/// Listening port for incoming UDP packets. Set via TNManager.StartUDP().
	/// </summary>

	static public int listeningPort { get { return mInstance != null ? mInstance.mClient.listeningPort : 0; } }

	/// <summary>
	/// Current time on the server in milliseconds.
	/// </summary>

	static public long serverTime { get { return (mInstance != null) ? mInstance.mClient.serverTime : (System.DateTime.UtcNow.Ticks / 10000); } }

	/// <summary>
	/// Forward and Create type packets write down their source.
	/// If the packet was sent by the server instead of another player, the ID will be 0.
	/// </summary>

	static public int packetSourceID { get { return (mInstance != null) ? mInstance.mClient.packetSourceID : 0; } }

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
			Channel ch = mInstance.mClient.GetChannel(channelID);
			return (ch != null && ch.isLocked);
		}
		return false;
	}

	/// <summary>
	/// ID of the channel the player is in.
	/// Note that if used while the player is in more than one channel, a warning will be shown.
	/// </summary>

	static public int lastChannelID = 0;

	static List<Channel> mDummyCL = new List<Channel>();

	/// <summary>
	/// List of channels the player is currently in.
	/// </summary>

	static public List<Channel> channels
	{
		get
		{
			if (isConnected) return mInstance.mClient.channels;
			mDummyCL.Clear();
			return mDummyCL;
		}
	}

	/// <summary>
	/// Check to see if we are currently in the specified channel.
	/// </summary>

	static public bool IsInChannel (int channelID) { return isConnected && mInstance.mClient.IsInChannel(channelID); }

	/// <summary>
	/// Get the player hosting the specified channel. Only works for the channels the player is in.
	/// </summary>

	static public Player GetHost (int channelID)
	{
		if (mInstance == null) return mPlayer;
		return mInstance.mClient.GetHost(channelID) ?? mPlayer;
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
				if (isConnected) mInstance.mClient.playerName = value;
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
			Channel ch = mInstance.mClient.GetChannel(channelID);
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
				GameObject go = new GameObject("Network Manager");
				mInstance = go.AddComponent<TNManager>();
				mDestroyed = false;
			}
			return mInstance;
		}
	}

	/// <summary>
	/// If you want to do custom logic for when packets can be processed and when they can't be, overwrite this delegate.
	/// </summary>

	static public ProcessPacketsFunc ProcessPackets = delegate()
	{
		if (mInstance != null && mInstance.mLoadingLevel.size == 0)
			mInstance.mClient.ProcessPackets();
	};
	public delegate void ProcessPacketsFunc ();

	/// <summary>
	/// Server configuration is set by administrators.
	/// In most cases you should use GetServerData and SetServerData functions instead.
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
			string[] parts = text.Split(new char[] { '=' }, 2);

			if (parts.Length == 2)
			{
				string key = parts[0].Trim();
				string val = parts[1].Trim();
				DataNode node = new DataNode(key, val);
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

	static public DataNode GetChannelData (string key)
	{
		if (!isConnected) return null;
		var ch = GetChannel(lastChannelID);
		return (ch != null) ? ch.Get(key) : null;
	}

	/// <summary>
	/// Convenience method: Retrieve the specified channel option.
	/// </summary>

	static public T GetChannelData<T> (string key)
	{
		if (!isConnected) return default(T);
		var ch = GetChannel(lastChannelID);
		return (ch != null) ? ch.Get<T>(key) : default(T);
	}

	/// <summary>
	/// Convenience method: Retrieve the specified channel option.
	/// </summary>

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
		return (ch != null) ? ch.Get<T>(key) : def;
	}

	/// <summary>
	/// Set the specified channel option.
	/// </summary>

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
		if (isConnected) return mInstance.mClient.GetPlayer(id);
		if (id == mPlayer.id) return mPlayer;
		return null;
	}

	/// <summary>
	/// Get the player associated with the specified name.
	/// </summary>

	static public Player GetPlayer (string name)
	{
		if (isConnected) return mInstance.mClient.GetPlayer(name);
		if (name == playerName) return mPlayer;
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
		if (isConnected) mInstance.mClient.SetPlayerData(path, val);
		else mPlayer.Set(path, val);
	}

	/// <summary>
	/// Get a list of channels from the server.
	/// </summary>

	static public void GetChannelList (GameClient.OnGetChannels callback) { if (isConnected) mInstance.mClient.GetChannelList(callback); }

	/// <summary>
	/// Set the following function to handle this type of packets.
	/// </summary>

	static public void SetPacketHandler (byte packetID, GameClient.OnPacket callback)
	{
		if (!mDestroyed && Application.isPlaying)
			instance.mClient.packetHandlers[packetID] = callback;
	}

	/// <summary>
	/// Set the following function to handle this type of packets.
	/// </summary>

	static public void SetPacketHandler (Packet packet, GameClient.OnPacket callback)
	{
		if (!mDestroyed && Application.isPlaying)
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
		string[] split = address.Split(new char[] { ':' });
		int port = 5127;
		if (split.Length == 2 && int.TryParse(split[1], out port)) Connect(split[0], port);
		else Connect(address, port);
	}

	/// <summary>
	/// Connect to the specified destination.
	/// </summary>

	static public void Connect (string address, int port)
	{
		if (!instance.mClient.isTryingToConnect)
		{
			instance.mClient.playerName = mPlayer.name;
			instance.mClient.playerData = (mPlayer.dataNode != null) ? mPlayer.dataNode.Clone() : null;

			if (TNServerInstance.isLocal)
			{
				instance.mClient.Connect(TNServerInstance.game);
			}
			else
			{
				IPEndPoint ip = TNet.Tools.ResolveEndPoint(address, port);

				if (ip == null)
				{
					if (onConnect != null)
						onConnect(false, "Unable to resolve [" + address + "]");
				}
				else instance.mClient.Connect(ip, null);
			}
		}
		else Debug.LogWarning("Already connecting...");
	}

	/// <summary>
	/// Connect to the specified remote destination.
	/// </summary>

	static public void Connect (IPEndPoint externalIP, IPEndPoint internalIP)
	{
		if (!instance.mClient.isTryingToConnect)
		{
			instance.mClient.Disconnect();
			instance.mClient.playerName = mPlayer.name;
			instance.mClient.playerData = (mPlayer.dataNode != null) ? mPlayer.dataNode.Clone() : null;
			instance.mClient.Connect(externalIP, internalIP);
		}
		else Debug.LogWarning("Already connecting...");
	}

	/// <summary>
	/// Disconnect from the specified destination.
	/// </summary>

	static public void Disconnect () { if (mInstance != null) mInstance.mClient.Disconnect(); }

	/// <summary>
	/// Join the specified channel.
	/// </summary>
	/// <param name="channelID">ID of the channel. Every player joining this channel will see one another.</param>
	/// <param name="persistent">Whether the channel will remain active even when the last player leaves.</param>

	static public void JoinChannel (int channelID, bool persistent = false, bool leaveCurrentChannel = false)
	{
		JoinChannel(channelID, null, persistent, int.MaxValue, null, leaveCurrentChannel);
	}

	/// <summary>
	/// Join the specified channel. This channel will be marked as persistent, meaning it will
	/// stay open even when the last player leaves, unless explicitly closed first.
	/// </summary>
	/// <param name="channelID">ID of the channel. Every player joining this channel will see one another.</param>
	/// <param name="levelName">Level that will be loaded first.</param>

	static public void JoinChannel (int channelID, string levelName, bool leaveCurrentChannel = true)
	{
		if (mInstance != null && TNManager.isConnected)
		{
			if (!IsInChannel(channelID))
			{
				if (leaveCurrentChannel) mInstance.mClient.LeaveAllChannels();
				mInstance.mClient.JoinChannel(channelID, levelName, false, 65535, null);
			}
		}
		else
		{
			TNManager.lastChannelID = channelID;

			if (!string.IsNullOrEmpty(levelName))
			{
#if UNITY_4_6 || UNITY_4_7 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2
				Application.LoadLevel(levelName);
#else
				UnityEngine.SceneManagement.SceneManager.LoadScene(levelName);
#endif
			}
		}
	}

	/// <summary>
	/// Join the specified channel.
	/// </summary>
	/// <param name="channelID">ID of the channel. Every player joining this channel will see one another.</param>
	/// <param name="levelName">Level that will be loaded first.</param>
	/// <param name="persistent">Whether the channel will remain active even when the last player leaves.</param>
	/// <param name="playerLimit">Maximum number of players that can be in this channel at once.</param>
	/// <param name="password">Password for the channel. First player sets the password.</param>

	static public void JoinChannel (int channelID, string levelName, bool persistent, int playerLimit, string password, bool leaveCurrentChannel = true)
	{
		if (mInstance != null && TNManager.isConnected)
		{
			if (leaveCurrentChannel) mInstance.mClient.LeaveAllChannels();
			mInstance.mClient.JoinChannel(channelID, levelName, persistent, playerLimit, password);
		}
		else
		{
			TNManager.lastChannelID = channelID;

			if (!string.IsNullOrEmpty(levelName))
			{
#if UNITY_4_6 || UNITY_4_7 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2
				Application.LoadLevel(levelName);
#else
				UnityEngine.SceneManagement.SceneManager.LoadScene(levelName);
#endif
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
		if (mInstance != null && TNManager.isConnected)
		{
			if (leaveCurrentChannel) mInstance.mClient.LeaveAllChannels();
			mInstance.mClient.JoinChannel(-2, levelName, persistent, playerLimit, password);
		}
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
		if (mInstance != null && TNManager.isConnected)
		{
			if (leaveCurrentChannel) mInstance.mClient.LeaveAllChannels();
			mInstance.mClient.JoinChannel(-1, levelName, persistent, playerLimit, password);
		}
		else
		{
#if UNITY_4_6 || UNITY_4_7 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2
			Application.LoadLevel(levelName);
#else
			UnityEngine.SceneManagement.SceneManager.LoadScene(levelName);
#endif
		}
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

	static public void CloseChannel () { CloseChannel(lastChannelID); }

	/// <summary>
	/// Close the channel the player is in. New players will be prevented from joining.
	/// Once a channel has been closed, it cannot be re-opened.
	/// </summary>

	static public void CloseChannel (int channelID) { if (mInstance != null) mInstance.mClient.CloseChannel(channelID); }

	/// <summary>
	/// Leave all of the channels we're currently in.
	/// </summary>

	static public void LeaveAllChannels () { if (mInstance != null) mInstance.mClient.LeaveAllChannels(); }

	/// <summary>
	/// Leave the channel we're in.
	/// </summary>

	static public void LeaveChannel () { LeaveChannel(lastChannelID); }

	/// <summary>
	/// Leave the channel we're in.
	/// </summary>

	static public void LeaveChannel (int channelID) { if (mInstance != null) mInstance.mClient.LeaveChannel(channelID); }

	/// <summary>
	/// Delete the specified channel.
	/// </summary>

	static public void DeleteChannel (int id, bool disconnect) { if (mInstance != null) mInstance.mClient.DeleteChannel(id, disconnect); }

	/// <summary>
	/// Change the maximum number of players that can join the channel the player is currently in.
	/// </summary>

	static public void SetPlayerLimit (int max) { SetPlayerLimit(lastChannelID, max); }

	/// <summary>
	/// Change the maximum number of players that can join the channel the player is currently in.
	/// </summary>

	static public void SetPlayerLimit (int channelID, int max) { if (mInstance != null) mInstance.mClient.SetPlayerLimit(channelID, max); }

	/// <summary>
	/// Load the specified level.
	/// </summary>

	static public void LoadLevel (string levelName) { LoadLevel(lastChannelID, levelName); }

	/// <summary>
	/// Load the specified level.
	/// </summary>

	static public void LoadLevel (int channelID, string levelName)
	{
		if (!mInstance.mClient.LoadLevel(channelID, levelName))
		{
#if UNITY_4_6 || UNITY_4_7 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2
			Application.LoadLevel(levelName);
#else
			UnityEngine.SceneManagement.SceneManager.LoadScene(levelName);
#endif
		}
	}

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

	static public void SetPlayerSave (string filename, DataNode.SaveType type = DataNode.SaveType.Binary)
	{
		if (isConnected)
		{
			var writer = BeginSend(Packet.RequestSetPlayerSave);
			writer.Write(filename);
			writer.Write((byte)type);
			EndSend();
		}
		else
		{
			playerData = DataNode.Read(filename);
			if (onSetPlayerData != null) onSetPlayerData(player, "", playerData);
		}
	}

	/// <summary>
	/// Delete the specified file on the server.
	/// </summary>

	static public void DeleteFile (string filename)
	{
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
			catch (System.Exception ex)
			{
				Debug.LogError(ex.Message + " (" + filename + ")");
			}
		}
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

	static public void LockChannel (bool locked) { LockChannel(lastChannelID, locked); }

	/// <summary>
	/// Lock the specified channel, preventing all future persistent RFCs from being saved.
	/// </summary>

	static public void LockChannel (int channelID, bool locked)
	{
		if (mInstance != null && isAdmin)
		{
			BinaryWriter writer = BeginSend(Packet.RequestLockChannel);
			writer.Write(channelID);
			writer.Write(locked);
			EndSend(channelID, true);
		}
	}

	/// <summary>
	/// Create a packet that will send a custom object creation call.
	/// Instantiate a new game object in the current channel on all connected players.
	/// </summary>

	static public void Instantiate (int rccID, string path, bool persistent, params object[] objs)
	{
		Instantiate(lastChannelID, rccID, null, path, persistent, objs);
	}

	/// <summary>
	/// Create a packet that will send a custom object creation call.
	/// Instantiate a new game object in the current channel on all connected players.
	/// </summary>

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
	/// Create a packet that will send a custom object creation call.
	/// Instantiate a new game object in the specified channel on all connected players.
	/// </summary>

	static internal void Instantiate (int channelID, int rccID, string funcName, string path, bool persistent, params object[] objs)
	{
		GameObject go = UnityTools.LoadPrefab(path) ?? UnityTools.GetDummyObject();

		if (go != null && instance != null)
		{
			CachedFunc func = GetRCC(rccID, funcName);

			if (TNManager.IsInChannel(channelID))
			{
				if (IsJoiningChannel(channelID))
				{
#if UNITY_EDITOR
					Debug.LogWarning("Trying to create an object while switching scenes. Call will be ignored.");
#endif
					return;
				}

				if (TNManager.IsChannelLocked(channelID))
				{
#if UNITY_EDITOR
					Debug.LogWarning("Trying to create an object in a locked channel. Call will be ignored.");
#endif
					return;
				}

				BinaryWriter writer = mInstance.mClient.BeginSend(Packet.RequestCreateObject);
				byte flag = GetFlag(go, persistent);
				writer.Write(playerID);
				writer.Write(channelID);
				writer.Write(flag);

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
				EndSend(channelID, true);
			}
			else
			{
				// Offline mode
				objs = BinaryExtensions.CombineArrays(go, objs);
				go = func.Execute(objs) as GameObject;
				UnityTools.Clear(objs);

				if (go != null)
				{
					go.SetActive(true);
					TNObject tno = go.GetComponent<TNObject>();

					if (tno != null)
					{
						if (++mObjID == 0) mObjID = 32768;
						tno.uid = mObjID;
					}
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

	static CachedFunc GetRCC (int rccID, string funcName)
	{
		CachedFunc func = null;

		if (rccID > 0 && rccID < 256 && !mDict0.TryGetValue(rccID, out func))
		{
			CacheRFCs();

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
					CacheRFCs();

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

	static uint mObjID = 32767;

	/// <summary>
	/// Automatically find and cache RFCs on all known MonoBehaviours.
	/// </summary>

	static void CacheRFCs ()
	{
		var mb = typeof(MonoBehaviour);
		var types = TypeExtensions.GetTypes();

		for (int i = 0; i < types.size; ++i)
		{
			var t = types[i];
			if (t.IsSubclassOf(mb)) AddRCCs(t);
		}
	}

	/// <summary>
	/// Add a new Remote Creation Call.
	/// </summary>

	static public void AddRCCs<T> () { AddRCCs(typeof(T)); }

	/// <summary>
	/// Add a new Remote Creation Call.
	/// </summary>

	static public void AddRCCs (System.Type type)
	{
		MethodInfo[] methods = type.GetMethods(
					BindingFlags.Public |
					BindingFlags.NonPublic |
					BindingFlags.Static);

		for (int b = 0, bmax = methods.Length; b < bmax; ++b)
		{
			MethodInfo method = methods[b];

			if (method.IsDefined(typeof(RCC), true))
			{
				RCC tnc = (RCC)method.GetCustomAttributes(typeof(RCC), true)[0];

				if (method.ReturnType == typeof(GameObject))
				{
					CachedFunc ent = new CachedFunc();
					ent.func = method;

					if (tnc.id > 0 && tnc.id < 256) mDict0[tnc.id] = ent;
					else mDict1[method.Name] = ent;
				}
				else Debug.LogError("RCC(" + tnc.id + ") function [" + method.Name + "] must return an instantiated GameObject");
			}
		}
	}

	/// <summary>
	/// Built-in Remote Creation Call.
	/// </summary>

	[RCC(1)] static GameObject OnCreate1 (GameObject go)
	{
		go = Instantiate(go) as GameObject;
		go.SetActive(true);
		return go;
	}

	/// <summary>
	/// Built-in Remote Creation Call.
	/// </summary>

	[RCC(2)] static GameObject OnCreate2 (GameObject go, Vector3 pos, Quaternion rot)
	{
		go = Instantiate(go, pos, rot) as GameObject;
		go.SetActive(true);
		return go;
	}

	/// <summary>
	/// Built-in Remote Creation Call.
	/// </summary>
	
	[RCC(3)] static GameObject OnCreate3 (GameObject go, Vector3 pos, Quaternion rot, Vector3 velocity, Vector3 angularVelocity)
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
		if (isConnected)
		{
			TNManager.BeginSend(Packet.ServerLog).Write(text);
			TNManager.EndSend();
		}
	}

	/// <summary>
	/// Begin sending a new packet to the server.
	/// </summary>

	static public BinaryWriter BeginSend (Packet type) { return instance.mClient.BeginSend(type); }

	/// <summary>
	/// Begin sending a new packet to the server.
	/// </summary>

	static public BinaryWriter BeginSend (byte packetID) { return instance.mClient.BeginSend(packetID); }

	/// <summary>
	/// Send the outgoing buffer. This should only be used for generic packets going straight to the server.
	/// Packets that are going to a channel should use EndSend(channelID, reliable) function instead.
	/// </summary>

	static public void EndSend () { mInstance.mClient.EndSend(); }

	/// <summary>
	/// Send the outgoing buffer.
	/// </summary>

	static public void EndSend (int channelID, bool reliable = true)
	{
		if (!IsJoiningChannel(channelID))
		{
			mInstance.mClient.EndSend(channelID, reliable);
		}
		else
		{
			mInstance.mClient.CancelSend();
#if UNITY_EDITOR
			Debug.LogWarning("Trying to send a packet while joining a channel. Ignored.");
#endif
		}
	}

#if UNITY_EDITOR
	[ContextMenu("Close channel")]
	void ForceCloseChannel ()
	{
		mInstance.mClient.BeginSend(Packet.RequestCloseChannel).Write(lastChannelID);
		mInstance.mClient.EndSend(true);
	}
#endif

	/// <summary>
	/// Broadcast the packet to everyone on the LAN.
	/// </summary>

	static public void EndSendToLAN (int port) { mInstance.mClient.EndSend(port); }

	/// <summary>
	/// Broadcast the packet to the specified endpoint via UDP.
	/// </summary>

	static public void EndSend (IPEndPoint target) { mInstance.mClient.EndSend(target); }

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

			AddRCCs<TNManager>();
			SetDefaultCallbacks();
			
#if UNITY_EDITOR
			List<IPAddress> ips = TNet.Tools.localAddresses;

			if (ips != null && ips.size > 0)
			{
				var ipv6 = (TNet.Tools.localAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6);
				string text = "[TNet] Local IPs: " + ips.size;

				for (int i = 0; i < ips.size; ++i)
				{
					var ip = ips[i];
					text += "\n  " + (i + 1) + ": " + ips[i];
					
					if (ip == TNet.Tools.localAddress)
					{
						text += " (LAN)";
					}
					else if (ipv6 && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
					{
						if (ip == TNet.Tools.externalAddress)
							text += " (WAN)";
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
		mClient.onDisconnect = delegate() { mLoadingLevel.Clear(); };
		mClient.onJoinChannel = delegate(int channelID, bool success, string message) { lastChannelID = channelID; };
		mClient.onLeaveChannel = delegate(int channelID)
		{
			if (lastChannelID == channelID)
			{
				List<Channel> chs = channels;
				lastChannelID = (chs != null && chs.size > 0) ? chs[0].id : 0;
			}
		};

		mClient.onLoadLevel = delegate(int channelID, string levelName)
		{
			lastChannelID = channelID;

			if (!string.IsNullOrEmpty(levelName))
			{
				mLoadingLevel.Add(channelID);
				StartCoroutine("LoadLevelCoroutine", new System.Collections.Generic.KeyValuePair<int, string>(channelID, levelName));
			}
		};

		mClient.onCreate = OnCreateObject;
		mClient.onDestroy = OnDestroyObject;
		mClient.onTransfer = OnTransferObject;
		mClient.onForwardedPacket = OnForwardedPacket;
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

	/// <summary>
	/// RequestCreate flag.
	/// 0 = Local-only object. Only echoed to other clients.
	/// 1 = Saved on the server, assigned a new owner when the existing owner leaves.
	/// 2 = Saved on the server, destroyed when the owner leaves.
	/// </summary>

	static byte GetFlag (GameObject go, bool persistent)
	{
		TNObject tno = go.GetComponent<TNObject>();
		if (tno == null) return 0;
		return persistent ? (byte)1 : (byte)2;
	}
	
	/// <summary>
	/// Notification of a new object being created.
	/// </summary>

	void OnCreateObject (int channelID, int creator, uint objectID, BinaryReader reader)
	{
		currentObjectOwner = GetPlayer(creator) ?? player;
		TNManager.lastChannelID = channelID;
		byte rccID = reader.ReadByte();
		string funcName = (rccID == 0) ? reader.ReadString() : null;
		CachedFunc func = GetRCC(rccID, funcName);

		// Load the object from the resources folder
		string prefab = reader.ReadString();
		GameObject go = UnityTools.LoadPrefab(prefab);

		if (go == null)
		{
			go = UnityTools.GetDummyObject();
#if UNITY_EDITOR
			Debug.LogError("[TNet] Unable to find prefab \"" + prefab + "\". Make sure it's in the Resources folder.");
#else
			Debug.LogError("[TNet] Unable to find prefab \"" + prefab + "\"");
#endif
		}

		if (func != null)
		{
			// Custom creation function
			object[] objs = reader.ReadArray(go);
			go = func.Execute(objs) as GameObject;
			UnityTools.Clear(objs);
		}
		// Fallback to a very basic function
		else go = OnCreate1(go);

		if (go != null)
		{
			go.SetActive(true);

			// If an object ID was requested, assign it to the TNObject
			if (objectID != 0)
			{
				TNObject obj = go.GetComponent<TNObject>();

				if (obj != null)
				{
					obj.channelID = channelID;
					obj.uid = objectID;
					obj.Register();
				}
			}
		}

		currentObjectOwner = null;
	}

	/// <summary>
	/// Notification of a network object being destroyed.
	/// </summary>

	void OnDestroyObject (int channelID, uint objID)
	{
		TNObject obj = TNObject.Find(channelID, objID);

		if (obj)
		{
			if (obj.onDestroy != null) obj.onDestroy();
			UnityEngine.Object.Destroy(obj.gameObject);
		}
	}

	/// <summary>
	/// Notification of a network object being transferred to another channel.
	/// </summary>

	void OnTransferObject (int oldChannelID, int newChannelID, uint oldObjectID, uint newObjectID)
	{
		if (IsInChannel(oldChannelID))
		{
			TNObject obj = TNObject.Find(oldChannelID, oldObjectID);
			if (obj) obj.FinalizeTransfer(newChannelID, newObjectID);
#if UNITY_EDITOR
			else Debug.LogWarning("Unable to find TNO #" + oldObjectID + " in channel " + oldChannelID);
#endif
		}
	}

	/// <summary>
	/// If custom functionality is needed, all unrecognized packets will arrive here.
	/// </summary>

	void OnForwardedPacket (int channelID, BinaryReader reader)
	{
		uint objID;
		byte funcID;
		TNObject.DecodeUID(reader.ReadUInt32(), out objID, out funcID);

		if (funcID == 0)
		{
			string funcName = "";

			try
			{
				funcName = reader.ReadString();
				TNObject.FindAndExecute(channelID, objID, funcName, reader.ReadArray());
			}
			catch (Exception ex)
			{
				Debug.LogError(objID + " " + funcID + " " + funcName + "\n" + ex.Message + "\n" + ex.StackTrace);
			}
		}
		else TNObject.FindAndExecute(channelID, objID, funcID, reader.ReadArray());
	}

	/// <summary>
	/// Process incoming packets in the update function.
	/// </summary>

	void Update () { ProcessPackets(); }

#endregion

	/// <summary>
	/// Load level coroutine handling asynchronous loading of levels.
	/// </summary>

	System.Collections.IEnumerator LoadLevelCoroutine (System.Collections.Generic.KeyValuePair<int, string> pair)
	{
		yield return null;
#if UNITY_4_6 || UNITY_4_7 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2
		loadLevelOperation = Application.LoadLevelAsync(pair.Value);
#else
		loadLevelOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(pair.Value);
#endif
		loadLevelOperation.allowSceneActivation = false;

		while (loadLevelOperation.progress < 0.9f)
			yield return null;

		loadLevelOperation.allowSceneActivation = true;
		yield return loadLevelOperation;

		loadLevelOperation = null;
		mLoadingLevel.Remove(pair.Key);
	}

	/// <summary>
	/// When a level is being loaded, this value will contain the async coroutine for the LoadLevel operation.
	/// You can yield on it if you need.
	/// </summary>

	static public AsyncOperation loadLevelOperation = null;

	void OnApplicationPause (bool paused) { isPaused = paused; }

	/// <summary>
	/// Add the specified packet to the receive queue. Useful for inserting messages to be processed by the network manager.
	/// </summary>

	static public void AddToReceiveQueue (TNet.Buffer buff)
	{
		if (mInstance != null && mInstance.mClient != null)
		{
			var queue = mInstance.mClient.receiveQueue;
			lock (queue) queue.Enqueue(buff);
		}
	}

	[System.Obsolete("Use TNManager.playerData")]
	static public DataNode playerDataNode { get { return playerData; } }

	[Obsolete("Use TNManager.serverData instead")]
	static public DataNode serverOptions { get { return serverData; } }

	[System.Obsolete("Use TNManager.SetServerData instead")]
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
	static internal void CreateEx (int rccID, bool persistent, string path, params object[] objs) { Instantiate(rccID, path, persistent, objs); }

	[Obsolete("You need to specify a channel ID to send the packet to: TNManager.EndSend(channelID, reliable);")]
	static public void EndSend (bool reliable)
	{
		if (!IsJoiningChannel(lastChannelID))
		{
			if (channels.size > 1)
				Debug.LogWarning("You need to specify which channel this packet should be going to");
			mInstance.mClient.EndSend(lastChannelID, reliable);
		}
		else
		{
			mInstance.mClient.CancelSend();
#if UNITY_EDITOR
			Debug.LogWarning("Trying to send a packet while joining a channel. Ignored.");
#endif
		}
	}

	[System.Obsolete("Use TNManager.GetServerData instead")]
	static public DataNode GetServerOption (string key) { return (mInstance != null) ? mInstance.mClient.GetServerData(key) : null; }

	[System.Obsolete("Use TNManager.GetServerData instead")]
	static public T GetServerOption<T> (string key) { return (mInstance != null) ? mInstance.mClient.GetServerData<T>(key) : default(T); }

	[System.Obsolete("Use TNManager.GetServerData instead")]
	static public T GetServerOption<T> (string key, T def) { return (mInstance != null) ? mInstance.mClient.GetServerData<T>(key, def) : def; }

	[System.Obsolete("Use gameObject.DestroySelf() instead")]
	static public void Destroy (GameObject go) { go.DestroySelf(); }
}
}
