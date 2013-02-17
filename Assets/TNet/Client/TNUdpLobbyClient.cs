//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

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
	UdpProtocol mUdp;
	Buffer mRequest;
	long mNextSend = 0;

	void Start ()
	{
		mUdp = new UdpProtocol();

		// Server list request -- we'll be using it a lot, so just create it once
		isActive = false;
		mRequest = Buffer.Create();
		mRequest.BeginPacket(Packet.RequestServerList).Write(GameServer.gameID);
		mRequest.EndPacket();

		// Twice just in case the first try falls on a taken port
		if (!mUdp.Start(10000 + (int)(System.DateTime.Now.Ticks % 50000)))
			 mUdp.Start(10000 + (int)(System.DateTime.Now.Ticks % 50000));
	}

	void OnDestroy ()
	{
		if (mUdp != null)
		{
			mUdp.Stop();
			mUdp = null;
			knownServers.Clear();
			onChange = null;
		}

		isActive = false;

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
					Debug.Log(response);

					if (response == Packet.ResponseServerList)
					{
						isActive = true;
						mNextSend = time + 3000;
						knownServers.ReadFrom(reader, time);
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

		// Trigger the listener callback
		if (changed && onChange != null) onChange();

		// Send out the update request
		if (mNextSend < time && mUdp != null)
		{
			mNextSend = time + 3000;
			mUdp.Send(mRequest, mRemoteAddress);
			Debug.Log(mRemoteAddress.ToString());
		}
	}
}
