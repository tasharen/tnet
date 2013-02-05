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
/// UDP-based discovery client, designed to communicate with the UdpDiscoveryServer.
/// </summary>

public class TNUdpDiscoveryClient : TNDiscoveryClient
{
	UdpProtocol mUdp;
	Buffer mRequest;
	IPEndPoint mTarget;
	long mNextSend = 0;

	void Start ()
	{
		if (!string.IsNullOrEmpty(address))
		{
			mTarget = Player.ResolveEndPoint(address, port);

			if (mTarget == null)
			{
				Debug.LogError("Invalid address: " + address + ":" + port);
				return;
			}

			// Server list request -- we'll be using it a lot, so just create it once
			mRequest = Buffer.Create();
			mRequest.BeginTcpPacket(Packet.RequestServerList).Write(1);
			mRequest.EndTcpPacket();

			mUdp = new UdpProtocol();
			mUdp.Start(0);
		}
	}

	void OnDestroy ()
	{
		if (mUdp != null)
		{
			mUdp.Stop();
			mUdp = null;
		}
		
		knownServers.Clear();
		onChange = null;
		if (mRequest != null) mRequest.Recycle();
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
						mNextSend = time + 3000;
						knownServers.ReadFrom(reader, ip, time);
						changed = true;
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
			mUdp.Send(mRequest, mTarget);
		}
	}
}
