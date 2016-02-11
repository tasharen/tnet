//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2016 Tasharen Entertainment
//---------------------------------------------

using UnityEngine;
using System.Reflection;
using System.Net;
using System.IO;

namespace TNet
{
/// <summary>
/// Container class with all the possible notification delegates used by TNet's GameClient.
/// </summary>

public class TNEvents
{
	/// <summary>
	/// Ping notification.
	/// </summary>

	public OnPing onPing;
	public delegate void OnPing (IPEndPoint ip, int ping);

	/// <summary>
	/// Error notification.
	/// </summary>

	public OnError onError;
	public delegate void OnError (string msg);

	/// <summary>
	/// Connection attempt result indicating success or failure.
	/// </summary>

	public OnConnect onConnect;
	public delegate void OnConnect (bool success, string message);

	/// <summary>
	/// Notification sent after the connection terminates for any reason.
	/// </summary>

	public OnDisconnect onDisconnect;
	public delegate void OnDisconnect ();

	/// <summary>
	/// Notification sent when attempting to join a channel, indicating a success or failure.
	/// </summary>

	public OnJoinChannel onJoinChannel;
	public delegate void OnJoinChannel (int channelID, bool success, string message);

	/// <summary>
	/// Notification sent when leaving a channel.
	/// Also sent just before a disconnect (if inside a channel when it happens).
	/// </summary>

	public OnLeaveChannel onLeaveChannel;
	public delegate void OnLeaveChannel (int channelID);

	/// <summary>
	/// Notification sent when changing levels.
	/// </summary>

	public OnLoadLevel onLoadLevel;
	public delegate void OnLoadLevel (int channelID, string levelName);

	/// <summary>
	/// Notification sent when a new player joins the channel.
	/// </summary>

	public OnPlayerJoin onPlayerJoin;
	public delegate void OnPlayerJoin (int channelID, Player p);

	/// <summary>
	/// Notification sent when a player leaves the channel.
	/// </summary>

	public OnPlayerLeave onPlayerLeave;
	public delegate void OnPlayerLeave (int channelID, Player p);

	/// <summary>
	/// Notification of some player changing their name.
	/// </summary>

	public OnRenamePlayer onRenamePlayer;
	public delegate void OnRenamePlayer (Player p, string previous);

	/// <summary>
	/// Notification sent when the channel's host changes.
	/// </summary>

	public OnHostChanged onHostChanged;
	public delegate void OnHostChanged (Channel ch);

	/// <summary>
	/// Notification sent when the server's data gets changed.
	/// </summary>

	public OnSetServerData onSetServerData;
	public delegate void OnSetServerData (string path, DataNode node);

	/// <summary>
	/// Notification sent when the channel's data gets changed.
	/// </summary>

	public OnSetChannelData onSetChannelData;
	public delegate void OnSetChannelData (Channel ch, string path, DataNode node);

	/// <summary>
	/// Notification sent when player data gets changed.
	/// </summary>

	public OnSetPlayerData onSetPlayerData;
	public delegate void OnSetPlayerData (Player p, string path, DataNode node);

	/// <summary>
	/// Notification of a new object being created.
	/// </summary>

	public OnCreate onCreate;
	public delegate void OnCreate (int channelID, int creator, uint objID, BinaryReader reader);

	/// <summary>
	/// Notification of the specified object being destroyed.
	/// </summary>

	public OnDestroy onDestroy;
	public delegate void OnDestroy (int channelID, uint objID);

	/// <summary>
	/// Notification of the specified object being transferred to another channel.
	/// </summary>

	public OnTransfer onTransfer;
	public delegate void OnTransfer (int oldChannelID, int newChannelID, uint oldObjectID, uint newObjectID);

	/// <summary>
	/// Callback triggered when the channel becomes locked or unlocked.
	/// </summary>

	public OnLockChannel onLockChannel;
	public delegate void OnLockChannel (int channelID, bool locked);

	/// <summary>
	/// Callback triggered when the player gets verified as an administrator.
	/// </summary>

	public OnSetAdmin onSetAdmin;
	public delegate void OnSetAdmin (Player p);

	/// <summary>
	/// Notification of a client packet arriving.
	/// </summary>

	public OnForwardedPacket onForwardedPacket;
	public delegate void OnForwardedPacket (int channelID, BinaryReader reader);
}

/// <summary>
/// Convenience class that you can inherit from that implements all of TNet's common notifications for user
/// convenience. Note that this script should ideally only be used on a manager class that needs to know
/// about all these events. If your class only needs a few of these events, consider simply subscribing
/// to them directly using the same += and -= logic in OnEnable/OnDisable.
/// </summary>

public abstract class NetworkEventReceiver : MonoBehaviour
{
	protected virtual void OnError (string msg) { }
	protected virtual void OnConnect (bool success, string msg) { }
	protected virtual void OnDisconnect () { }
	protected virtual void OnJoinChannel (int channelID, bool success, string message) { }
	protected virtual void OnLeaveChannel (int channelID) { }
	protected virtual void OnPlayerJoin (int channelID, Player p) { }
	protected virtual void OnPlayerLeave (int channelID, Player p) { }
	protected virtual void OnRenamePlayer (Player p, string previous) { }
	protected virtual void OnSetServerData (string path, DataNode node) { }
	protected virtual void OnSetChannelData (Channel ch, string path, DataNode node) { }
	protected virtual void OnSetPlayerData (Player p, string path, DataNode node) { }

	protected virtual void OnEnable ()
	{
		TNManager.onError += OnError;
		TNManager.onConnect += OnConnect;
		TNManager.onDisconnect += OnDisconnect;
		TNManager.onJoinChannel += OnJoinChannel;
		TNManager.onLeaveChannel += OnLeaveChannel;
		TNManager.onPlayerJoin += OnPlayerJoin;
		TNManager.onPlayerLeave += OnPlayerLeave;
		TNManager.onRenamePlayer += OnRenamePlayer;
		TNManager.onSetServerData += OnSetServerData;
		TNManager.onSetChannelData += OnSetChannelData;
		TNManager.onSetPlayerData += OnSetPlayerData;
	}

	protected virtual void OnDisable ()
	{
		TNManager.onError -= OnError;
		TNManager.onConnect -= OnConnect;
		TNManager.onDisconnect -= OnDisconnect;
		TNManager.onJoinChannel -= OnJoinChannel;
		TNManager.onLeaveChannel -= OnLeaveChannel;
		TNManager.onPlayerJoin -= OnPlayerJoin;
		TNManager.onPlayerLeave -= OnPlayerLeave;
		TNManager.onRenamePlayer -= OnRenamePlayer;
		TNManager.onSetServerData -= OnSetServerData;
		TNManager.onSetChannelData -= OnSetChannelData;
		TNManager.onSetPlayerData -= OnSetPlayerData;
	}
}
}
