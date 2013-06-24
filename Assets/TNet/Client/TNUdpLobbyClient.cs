//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2013 Tasharen Entertainment
//---------------------------------------------

using System.Net;
using System.IO;
using System.Collections;
using System.Threading;
using UnityEngine;
using TNet;

/// <summary>
/// UDP-based lobby client, designed to communicate with the UdpLobbyServer.
/// </summary>

public class TNUdpLobbyClient : TNLobbyClient
{
	/// <summary>
	/// Public address for the lobby client server's location.
	/// </summary>

	public string remoteAddress;

	/// <summary>
	/// Lobby server's port.
	/// </summary>

	public int remotePort = 5129;

	UdpProtocol mUdp = new UdpProtocol();
	Buffer mRequest;
	long mNextSend = 0;
	IPEndPoint mRemoteAddress;

	void Awake ()
	{
#if UNITY_FLASH || UNITY_WEBPLAYER
#if UNITY_EDITOR
		Debug.LogWarning("UDP is not supported on Flash and Web Player platforms. UDP-based server discovery will not function.", this);
#endif
		enabled = false;
#endif
	}

	void OnEnable()
	{
		if (mRequest == null)
		{
			mRequest = Buffer.Create();
			mRequest.BeginPacket(Packet.RequestServerList).Write(GameServer.gameID);
			mRequest.EndPacket();
		}

		if (mRemoteAddress == null)
		{
			if (string.IsNullOrEmpty(remoteAddress))
			{
				mRemoteAddress = new IPEndPoint(IPAddress.Broadcast, remotePort);
			}
			else
			{
				mRemoteAddress = Tools.ResolveEndPoint(remoteAddress, remotePort);
			}

			if (mRemoteAddress == null)
			{
				mUdp.Error(new IPEndPoint(IPAddress.Loopback, mUdp.listeningPort), "Invalid address: " + remoteAddress + ":" + remotePort);
			}
		}

		// Twice just in case the first try falls on a taken port
		if (!mUdp.Start(Tools.randomPort)) mUdp.Start(Tools.randomPort);
	}

	protected override void OnDisable ()
	{
		isActive = false;
		mUdp.Stop();
		knownServers.Clear();
		if (onChange != null) onChange();

		if (mRequest != null)
		{
			mRequest.Recycle();
			mRequest = null;
		}
	}

	/// <summary>
	/// Keep receiving incoming packets.
	/// </summary>

	void Update ()
	{
		Buffer buffer;
		IPEndPoint ip;
		bool changed = false;
		long time = System.DateTime.Now.Ticks / 10000;

		// Receive and process UDP packets one at a time
		while (mUdp != null && mUdp.ReceivePacket(out buffer, out ip))
		{
			if (buffer.size > 0)
			{
				try
				{
					BinaryReader reader = buffer.BeginReading();
					Packet response = (Packet)reader.ReadByte();

					if (response == Packet.ResponseServerList)
					{
						isActive = true;
						mNextSend = time + 3000;
						knownServers.ReadFrom(reader, time);
						knownServers.Cleanup(time);
						changed = true;
					}
					else if (response == Packet.Error)
					{
						Debug.LogWarning(reader.ReadString());
					}
				}
				catch (System.Exception) { }
			}
			buffer.Recycle();
		}

		// Clean up old servers
		if (knownServers.Cleanup(time))
			changed = true;

		// Trigger the listener callback
		if (changed && onChange != null)
		{
			onChange();
		}
		else if (mNextSend < time && mUdp != null)
		{
			// Send out the update request
			mNextSend = time + 3000;
			mUdp.Send(mRequest, mRemoteAddress);
		}
	}
}
