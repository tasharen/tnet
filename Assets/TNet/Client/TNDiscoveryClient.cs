//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using UnityEngine;
using TNet;
using System.Net;
using System.IO;
using System.Collections;

/// <summary>
/// Server list is an optional client component that listens for incoming server list packets.
/// You can use it as-is in your game. Just specify where your discovery server is located,
/// and you will be able to use TNDiscoveryClient.knownServers.
/// </summary>

[RequireComponent(typeof(TNManager))]
[AddComponentMenu("TNet/Discovery Client")]
public class TNDiscoveryClient : MonoBehaviour
{
	static ServerList mList = new ServerList();

	/// <summary>
	/// List of known servers.
	/// </summary>

	static public List<ServerList.Entry> knownServers { get { return mList.list; } }

	/// <summary>
	/// Discovery server address if any. If none is specified, network broadcasts are used instead.
	/// </summary>

	public string discoveryAddress;

	/// <summary>
	/// Port for the discovery server.
	/// </summary>

	public int discoveryServerPort = 5129;

	bool mHandling = false;
	IPEndPoint mIp;
	float mNextRequest = 0f;

	/// <summary>
	/// We want to handle the server list response packet.
	/// </summary>

	void Start ()
	{
		mHandling = true;
		TNManager.SetPacketHandler(Packet.ResponseListServers, OnServerList);
		StartCoroutine(Cleanup());
		StartCoroutine(PeriodicRequest());
	}

	/// <summary>
	/// Custom packet handler for the Server List Response.
	/// </summary>

	void OnServerList (Packet response, BinaryReader reader, IPEndPoint source)
	{
		mNextRequest = Time.time + 3f;
		mList.ReadFrom(reader, source, System.DateTime.Now.Ticks / 10000);
	}

	/// <summary>
	/// Periodically clean up the list of known servers.
	/// </summary>

	IEnumerator Cleanup()
	{
		for (; ; )
		{
			mList.Cleanup(System.DateTime.Now.Ticks / 10000);
			yield return new WaitForSeconds(0.5f);
		}
	}

	/// <summary>
	/// Periodically request a new list of servers from the discovery server.
	/// </summary>

	IEnumerator PeriodicRequest ()
	{
		for (; ; )
		{
			if (mNextRequest < Time.time && !RequestUpdate()) break;
			yield return new WaitForSeconds(1f);
		}
	}

	/// <summary>
	/// Update the server list by requesting it from the server.
	/// </summary>

	public bool RequestUpdate ()
	{
		BinaryWriter writer = TNManager.BeginSend(Packet.RequestListServers);
		writer.Write(GameServer.gameID);

		if (string.IsNullOrEmpty(discoveryAddress))
		{
			TNManager.EndSend(discoveryServerPort);
		}
		else
		{
			if (mIp == null)
			{
				mIp = Player.ResolveEndPoint(discoveryAddress, discoveryServerPort);
			}
			if (mIp != null)
			{
				TNManager.EndSend(mIp);
			}
			else
			{
				Debug.LogError("Unable to resolve " + discoveryAddress, this);
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// If we've been handling the server list packets, clear the handler.
	/// </summary>

	void OnDestroy ()
	{
		if (mHandling)
		{
			TNManager.SetPacketHandler(Packet.ResponseListServers, null);
			mList.Clear();
		}
	}
}