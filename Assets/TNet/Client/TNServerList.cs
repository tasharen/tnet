//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using UnityEngine;
using TNet;
using System.IO;
using System.Collections;

/// <summary>
/// Example script showing how to use the BroadcastToLAN functionality to share a list of servers.
/// </summary>

[AddComponentMenu("TNet/Server List")]
public class TNServerList : TNBehaviour
{
	public class Entry
	{
		public string address;
		public string name;
		public float expiration = 0f;
	}

	static public List<Entry> list = new List<Entry>();

	/// <summary>
	/// Port used for broadcasts.
	/// </summary>

	public int broadcastPort = 5128;

	/// <summary>
	/// The network manager won't listen for broadcasts unless they get explicitly enabled.
	/// </summary>

	void Start ()
	{
		// Start listening for packets broadcast on the specified port
		TNManager.Start(broadcastPort);

		// Start a pair of co-routines that will be updating the list
		StartCoroutine(RemoveExpiredEntries());
		StartCoroutine(BroadcastServerInfo());
	}

	/// <summary>
	/// Helper function that retrieves an existing entry or creates a new one.
	/// </summary>

	Entry Get (string address)
	{
		for (int i = 0; i < list.size; ++i)
		{
			Entry ent = list[i];
			if (ent.address == address) return ent;
		}
		Entry e = new Entry();
		e.address = address;
		list.Add(e);
		return e;
	}

	/// <summary>
	/// If a server hasn't been updated in a while, remove it from the list.
	/// </summary>

	IEnumerator RemoveExpiredEntries ()
	{
		for (; ; )
		{
			float time = Time.time;

			for (int i = list.size; i > 0; )
			{
				Entry ent = list[--i];
				if (ent.expiration < time) list.RemoveAt(i);
			}
			yield return new WaitForSeconds(1f);
		}
	}

	/// <summary>
	/// If we are running a server, broadcast its information to the rest of the LAN.
	/// </summary>

	IEnumerator BroadcastServerInfo ()
	{
		for (; ; )
		{
			if (TNServerInstance.isListening)
			{
				tno.BroadcastToLAN(broadcastPort, "OnServerInfo", TNServerInstance.serverName, TNServerInstance.listeningPort);
			}
			yield return new WaitForSeconds(3f);
		}
	}

	/// <summary>
	/// Remote function call received via a LAN broadcast.
	/// </summary>

	[RFC]
	void OnServerInfo (string serverName, int port)
	{
		if (TNManager.lastAddress != null)
		{
			// The port where this packet came from is not very useful as it's that of the UDP socket.
			string address = TNManager.lastAddress.Split(new char[] { ':' })[0] + ":" + port;

			Entry ent = Get(address);
			ent.name = serverName;
			ent.expiration = Time.time + 10f;
		}
	}
}