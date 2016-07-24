//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using System.Net;
using System.IO;
using System.Collections;
using System.Threading;
using UnityEngine;

namespace TNet
{
/// <summary>
/// TCP-based lobby client, designed to communicate with the TcpLobbyServer.
/// </summary>

public class TNTcpLobbyClient : TNLobbyClient
{
	TcpProtocol mTcp = new TcpProtocol();
	long mNextConnect = 0;
	IPEndPoint mRemoteAddress;

	void Start ()
	{
		if (mRemoteAddress == null)
		{
			mRemoteAddress = string.IsNullOrEmpty(remoteAddress) ?
				new IPEndPoint(IPAddress.Broadcast, remotePort) :
				Tools.ResolveEndPoint(remoteAddress, remotePort);

			if (mRemoteAddress == null)
				mTcp.RespondWithError("Invalid address: " + remoteAddress + ":" + remotePort);
		}
	}

	protected override void OnDisable ()
	{
		isActive = false;
		mTcp.Disconnect();
		base.OnDisable();
		if (onChange != null) onChange();
	}

	/// <summary>
	/// Keep receiving incoming packets.
	/// </summary>

	void Update ()
	{
		Buffer buffer;
		bool changed = false;
		long time = System.DateTime.UtcNow.Ticks / 10000;

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

					if (response == Packet.ResponseID)
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
						errorString = "";
					}
					else if (response == Packet.ResponseServerList)
					{
						lock (knownServers.list) knownServers.list.Clear();
						knownServers.ReadFrom(reader, time);
						changed = true;
						errorString = "";
					}
					else if (response == Packet.Error)
					{
						errorString = reader.ReadString();
						Debug.LogWarning(errorString);
						changed = true;
					}
				}
				catch (System.Exception ex)
				{
					errorString = ex.Message;
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
}
