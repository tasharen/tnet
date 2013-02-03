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
/// Server list is an optional client component that listens for incoming server list packets.
/// You can use it as-is in your game. Just specify where your discovery server is located,
/// and you will be able to use TNDiscoveryClient.knownServers.
/// </summary>

public class TNDiscoveryClient : MonoBehaviour
{
	public delegate void OnListChanged ();

	public enum Protocol
	{
		Tcp,
		Udp,
	}

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

	/// <summary>
	/// Protocol that should be used for communication.
	/// </summary>

	public Protocol protocol = Protocol.Udp;

	UdpProtocol mUdp;
	TcpProtocol mTcp;
	Buffer mRequest;
	IPEndPoint mTarget;
	long mNextSend = 0;
	bool mWasConnected = false;

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
			mRequest.BeginTcpPacket(Packet.RequestListServers).Write(1);
			mRequest.EndTcpPacket();

			if (protocol == Protocol.Udp)
			{
				mUdp = new UdpProtocol();
				mUdp.Start(0);
			}
			else
			{
				mTcp = new TcpProtocol();
				mTcp.Connect(mTarget);
			}
		}
	}

	void OnDestroy ()
	{
		if (mUdp != null)
		{
			mUdp.Stop();
			mUdp = null;
		}
		
		if (mTcp != null)
		{
			mTcp.Disconnect();
			mTcp = null;
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

					if (response == Packet.ResponseListServers)
					{
						knownServers.ReadFrom(reader, ip, time);
						changed = true;
					}
				}
				catch (System.Exception) { }
			}
			buffer.Recycle();
		}

		// TCP-based discovery
		while (mTcp != null && mTcp.ReceivePacket(out buffer))
		{
			if (buffer.size > 0)
			{
				try
				{
					BinaryReader reader = buffer.BeginReading();
					Packet response = (Packet)reader.ReadByte();

					if (response == Packet.ResponseListServers)
					{
						knownServers.ReadFrom(reader, mTcp.tcpEndPoint, time);
						changed = true;
					}
				}
				catch (System.Exception) { mTcp.Close(false); }
			}
		}

		// Trigger the listener callback
		if (changed && onChange != null) onChange();

		// Send out the update request
		if (mNextSend < time)
		{
			if (mUdp != null)
			{
				Debug.Log("Sending");
				mNextSend = time + 3000;
				mUdp.Send(mRequest, mTarget);
			}
			else if (mTcp != null)
			{
				if (mTcp.isConnected)
				{
					Debug.Log("Sending");
					mWasConnected = true;
					mNextSend = time + 4000;
					mTcp.SendTcpPacket(mRequest);
				}
				else if (mWasConnected)
				{
					// Automatically try to reconnect
					mTcp.Connect(mTarget);
				}
			}
		}
	}
}
