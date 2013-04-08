//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2013 Tasharen Entertainment
//---------------------------------------------

using UnityEngine;
using System.Net;
using TNet;

/// <summary>
/// Server Lobby Client is an abstract class designed to communicate with the Lobby Server.
/// You should instantiate protocol-specific versions: TNTcpLobbyClient or TNUdpLobbyClient,
/// and you should only have one of them active at a time, not both.
/// </summary>

public abstract class TNLobbyClient : MonoBehaviour
{
	public delegate void OnListChange ();

	/// <summary>
	/// List of known servers.
	/// </summary>

	static public ServerList knownServers = new ServerList();

	/// <summary>
	/// Callback that will be triggered every time the server list changes.
	/// </summary>

	static public OnListChange onChange;

	/// <summary>
	/// Whether some lobby client is currently active.
	/// </summary>

	static public bool isActive = false;

	/// <summary>
	/// Clear the list of known servers when the component is disabled.
	/// </summary>

	protected virtual void OnDisable () { knownServers.Clear(); }
}
