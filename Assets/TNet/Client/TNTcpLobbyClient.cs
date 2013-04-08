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
/// TCP-based lobby client, designed to communicate with the TcpLobbyServer.
/// </summary>

public class TNTcpLobbyClient : TNLobbyClient
{
	/// <summary>
	/// Public address for the lobby client server's location.
	/// </summary>

	public string remoteAddress;

	/// <summary>
	/// Lobby server's port.
	/// </summary>

	public int remotePort = 5129;

	TcpProtocol mTcp = new TcpProtocol();
	long mNextConnect = 0;
	IPEndPoint mRemoteAddress;

	void OnEnable ()
	{
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
				mTcp.Error("Invalid address: " + remoteAddress + ":" + remotePort);
			}
		}
	}

	protected override void OnDisable ()
	{
		isActive = false;
		mTcp.Disconnect();
		knownServers.Clear();
		if (onChange != null) onChange();
	}

	/// <summary>
	/// Keep receiving incoming packets.
	/// </summary>

	void Update ()
	{
		Buffer buffer;
		bool changed = false;
		long time = System.DateTime.Now.Ticks / 10000;

		// Automatically try to connect and reconnect if not connected
		if (mRemoteAddress != null && mTcp.stage == TcpProtocol.Stage.NotConnected && mNextConnect < time)
		{
			mNextConnect = time + 5000;
			mTcp.Connect(mRemoteAddress);
		}

		// TCP-based lobby
		while (mTcp.ReceivePacket(out buffer))
		{
			if (buffer.size > 0)
			{
				try
				{
					BinaryReader reader = buffer.BeginReading();
					Packet response = (Packet)reader.ReadByte();

					if (mTcp.stage == TcpProtocol.Stage.Verifying)
					{
						if (mTcp.VerifyResponseID(response, reader))
						{
							isActive = true;

							// Request the server list -- with TCP this only needs to be done once
							mTcp.BeginSend(Packet.RequestServerList).Write(GameServer.gameID);
							mTcp.EndSend();
						}
					}
					else if (response == Packet.Disconnect)
					{
						knownServers.Clear();
						isActive = false;
						changed = true;
					}
					else if (response == Packet.ResponseServerList)
					{
						knownServers.ReadFrom(reader, time);
						changed = true;
					}
					else if (response == Packet.Error)
					{
#if UNITY_EDITOR
						Debug.LogWarning(reader.ReadString());
#endif
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
