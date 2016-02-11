//--------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//--------------------------------------------------

using UnityEngine;

namespace TNet
{
/// <summary>
/// Convenience class that you can inherit from that implements all of TNet's common notifications for user
/// convenience. Note that this script should ideally only be used on a manager class that needs to know
/// about all these events. If your class only needs a few of these events, consider simply subscribing
/// to them directly using the same += and -= logic in OnEnable/OnDisable.
/// </summary>

public abstract class TNEventReceiver : MonoBehaviour
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
