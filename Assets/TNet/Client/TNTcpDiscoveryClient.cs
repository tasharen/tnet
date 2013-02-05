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
/// TCP-based discovery client, designed to communicate with the TcpDiscoveryServer.
/// </summary>

public class TNTcpDiscoveryClient : TNDiscoveryClient
{
	TcpProtocol mTcp;

	void Start ()
	{
		if (!string.IsNullOrEmpty(address))
		{
			IPEndPoint ip = Player.ResolveEndPoint(address, port);

			if (ip == null)
			{
				Debug.LogError("Invalid address: " + address + ":" + port);
				return;
			}

			mTcp = new TcpProtocol();
			mTcp.Connect(ip);
		}
	}

	void OnDestroy ()
	{
		if (mTcp != null)
		{
			mTcp.Disconnect();
			mTcp = null;
		}
		
		knownServers.Clear();
		onChange = null;
	}

	/// <summary>
	/// Keep receiving incoming packets.
	/// </summary>

	void Update ()
	{
		Buffer buffer;
		bool changed = false;
		long time = System.DateTime.Now.Ticks / 10000;

		// TCP-based discovery
		while (mTcp != null && mTcp.ReceivePacket(out buffer))
		{
			if (buffer.size > 0)
			{
				try
				{
					BinaryReader reader = buffer.BeginReading();
					Packet response = (Packet)reader.ReadByte();

					// The connection must be verified before it's usable
					if (response == Packet.ResponseID)
					{
						if (mTcp.stage == TcpProtocol.Stage.Verifying)
						{
							int serverVersion = reader.ReadInt32();

							if (!mTcp.VerifyVersion(serverVersion, reader.ReadInt32()))
							{
								Debug.LogError("Version mismatch. Server is running version " +
									serverVersion + ", while you have version " + Player.version);
							}
						}
					}
					else if (response == Packet.ResponseServerList)
					{
						knownServers.ReadFrom(reader, mTcp.tcpEndPoint, time);
						changed = true;
					}
				}
				catch (System.Exception ex)
				{
					Debug.LogWarning(ex.Message);
					mTcp.Close(false);
				}
			}
			buffer.Recycle();
		}

		// Trigger the listener callback
		if (changed && onChange != null) onChange();
	}
}
