//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using System;
using System.IO;
using System.Net;
using System.Threading;

namespace TNet
{
/// <summary>
/// TCP-based discovery server link. Designed to communicate with a remote TcpDiscoveryServer.
/// You can use this class to register your game server with a remote discovery server.
/// </summary>

public class TcpDiscoveryServerLink : DiscoveryServerLink
{
	TcpProtocol mTcp;
	GameServer mServer;
	Thread mThread;

	/// <summary>
	/// Whether the link is currently active.
	/// </summary>

	public override bool isActive { get { return mTcp.isConnected; } }

	/// <summary>
	/// Start the discovery server link.
	/// </summary>

	public override void Start ()
	{
		IPEndPoint ip = Player.ResolveEndPoint(address, port);

		if (ip != null)
		{
			if (mTcp == null) mTcp = new TcpProtocol();
			mTcp.Connect(ip);
		}
	}

	/// <summary>
	/// Stop informing the discovery server of any changes.
	/// </summary>

	public override void Stop ()
	{
		if (mThread != null)
		{
			mThread.Abort();
			mThread = null;
		}

		if (mTcp != null)
		{
			mTcp.Disconnect();
			mTcp = null;
		}
	}

	/// <summary>
	/// Send a server update.
	/// </summary>

	public override void Update (GameServer server)
	{
		mServer = server;

		if (mThread == null)
		{
			mThread = new Thread(ThreadFunction);
			mThread.Start();
		}
	}

	/// <summary>
	/// Send periodic updates.
	/// </summary>

	void ThreadFunction()
	{
		for (; ; )
		{
			Buffer buffer;
			
			while (mTcp.ReceivePacket(out buffer))
			{
				BinaryReader reader = buffer.BeginReading();
				Packet response = (Packet)reader.ReadByte();

				if (mTcp.stage == TcpProtocol.Stage.Verifying)
				{
					if (!mTcp.VerifyServerProtocol(response, reader))
					{
						mThread = null;
						return;
					}
				}
				else if (response == Packet.Error)
				{
					Console.WriteLine("TcpDiscoveryLink: " + reader.ReadString());
				}
				else
				{
					Console.WriteLine("TcpDiscoveryLink can't handle this packet: " + response);
				}
				buffer.Recycle();
			}

			if (mServer != null && mTcp.isConnected)
			{
				BinaryWriter writer = mTcp.BeginSend(Packet.RequestAddServer);
				writer.Write(GameServer.gameID);
				writer.Write(mServer.name);
				writer.Write((ushort)mServer.tcpPort);
				writer.Write((short)mServer.playerCount);
				mTcp.EndSend();
				mServer = null;
			}
			Thread.Sleep(10);
		}
	}
}
}
