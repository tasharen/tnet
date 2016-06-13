//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using System;
using System.IO;
using System.Net;
using System.Threading;

namespace TNet
{
/// <summary>
/// TCP-based lobby server link. Designed to communicate with a remote TcpLobbyServer.
/// You can use this class to register your game server with a remote lobby server.
/// </summary>

public class TcpLobbyServerLink : LobbyServerLink
{
	TcpProtocol mTcp;
	IPEndPoint mRemoteAddress;
	long mNextConnect = 0;
	bool mWasConnected = false;
	long mTimeDifference = 0;
	bool mUpdateNeeded = false;
	long mNextSend = 0;

	/// <summary>
	/// Create a new link to a remote lobby server.
	/// </summary>

	public TcpLobbyServerLink (IPEndPoint address) : base(null) { mRemoteAddress = address; }

	/// <summary>
	/// Whether the link is currently active.
	/// </summary>

	public override bool isActive { get { return mTcp.isConnected; } }

	/// <summary>
	/// Current time on the server.
	/// </summary>

	public long serverTime { get { return mTimeDifference + (System.DateTime.UtcNow.Ticks / 10000); } }

	/// <summary>
	/// Start the lobby server link.
	/// </summary>

	public override void Start ()
	{
		base.Start();

		if (mTcp == null)
		{
			mTcp = new TcpProtocol();
			mTcp.name = "Link";
		}
		mNextConnect = 0;
	}

	/// <summary>
	/// Send a server update.
	/// </summary>

	public override void SendUpdate (GameServer server)
	{
		if (!mShutdown)
		{
			mGameServer = server;
			mUpdateNeeded = true;

			if (mThread == null)
			{
				mThread = new Thread(ThreadFunction);
				mThread.Start();
			}
		}
	}

	/// <summary>
	/// Send periodic updates.
	/// </summary>

	void ThreadFunction()
	{
		mInternal = new IPEndPoint(Tools.localAddress, mGameServer.tcpPort);
		mExternal = new IPEndPoint(Tools.externalAddress, mGameServer.tcpPort);

		for (; ; )
		{
			long time = DateTime.UtcNow.Ticks / 10000;

			if (mShutdown)
			{
				mTcp.Disconnect();
				mThread = null;
#if STANDALONE
				Tools.Print("TcpLobbyLink shut down");
#endif
				break;
			}

#if !STANDALONE
			if (TNManager.isPaused)
			{
				Thread.Sleep(500);
				continue;
			}
#endif
			Buffer buffer;

			// Try to establish a connection
			if (mGameServer != null && !mTcp.isConnected && !mTcp.isTryingToConnect && mNextConnect < time)
			{
#if STANDALONE
				Tools.Print("TcpLobbyLink is connecting to " + mRemoteAddress + "...");
#endif
				mUpdateNeeded = true;
				mNextConnect = time + 15000;
				mTcp.Connect(mRemoteAddress);
			}
			
			while (mTcp.ReceivePacket(out buffer))
			{
				BinaryReader reader = buffer.BeginReading();
				Packet response = (Packet)reader.ReadByte();

				if (mTcp.stage == TcpProtocol.Stage.Verifying)
				{
					if (mTcp.VerifyResponseID(response, reader))
					{
						mTimeDifference = reader.ReadInt64() - (System.DateTime.UtcNow.Ticks / 10000);
						mWasConnected = true;
#if STANDALONE
						Tools.Print("TcpLobbyLink connection established");
#endif
					}
					else
					{
#if STANDALONE
						Tools.Print("TcpLobbyLink Error: Protocol version mismatch");
#endif
						mThread = null;
						return;
					}
				}
				else if (response == Packet.RequestPing) {}
#if STANDALONE
				else if (response == Packet.Error) Tools.Print("TcpLobbyLink Error: " + reader.ReadString());
				else if (response == Packet.Disconnect) Tools.Print("TcpLobbyLink disconnected");
				else Tools.Print("TcpLobbyLink can't handle this packet: " + response);
#endif
				buffer.Recycle();
			}


			// Automatically try to re-establish a connection on disconnect
			if (mWasConnected && !mTcp.isConnected && !mTcp.isTryingToConnect)
			{
				mNextConnect = time + 5000;
				mWasConnected = false;
			}
			else if (mGameServer != null && mTcp.isConnected && (mUpdateNeeded || mNextSend < time))
			{
				mUpdateNeeded = false;
				mNextSend = time + 5000;

				Buffer buff = Buffer.Create();
				BinaryWriter writer = buff.BeginPacket(Packet.RequestAddServer);
				writer.Write(GameServer.gameID);
				writer.Write(mGameServer.name);
				writer.Write((short)mGameServer.playerCount);
				Tools.Serialize(writer, mInternal);
				Tools.Serialize(writer, mExternal);
				buff.EndPacket();
				mTcp.SendTcpPacket(buff);
				buff.Recycle();
			}

			try { Thread.Sleep(10); }
			catch (System.Threading.ThreadInterruptedException) { return; }
		}
	}
}
}
