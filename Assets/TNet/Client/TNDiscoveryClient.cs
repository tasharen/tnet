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
/// Server Discovery Client can query a remote server for a list of active game servers.
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

		// Send out the update request
		if (mNextSend < time && mUdp != null)
		{
			mNextSend = time + 3000;
			mUdp.Send(mRequest, mTarget);
		}
	}
}
