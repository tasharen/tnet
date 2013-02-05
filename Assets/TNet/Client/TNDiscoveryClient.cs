//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using UnityEngine;
using TNet;

/// <summary>
/// Server Discovery Client is an abstract class designed to communicate with the Discovery Server.
/// You should instantiate protocol-specific versions: TNTcpDiscoveryClient or TNUdpDiscoveryClient.
/// </summary>

public abstract class TNDiscoveryClient : MonoBehaviour
{
	public delegate void OnListChanged ();

	/// <summary>
	/// List of known servers.
	/// </summary>

	static public ServerList knownServers = new ServerList();

	/// <summary>
	/// Callback that will be triggered every time the server list changes.
	/// </summary>

	static public OnListChanged onChange;

	/// <summary>
	/// Public address for the discovery client server's location.
	/// </summary>

	public string address;

	/// <summary>
	/// Discovery server's port.
	/// </summary>

	public int port = 5129;
}
