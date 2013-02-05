//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using System;
using System.IO;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;
using System.Net;

namespace TNet
{
/// <summary>
/// Optional TCP-based listener that makes it possible for servers to
/// register themselves with a central location for easy discovery by clients.
/// </summary>

public class TcpDiscoveryServer : DiscoveryServer
{
	// List of servers that's currently being updated
	ServerList mList = new ServerList();
	List<TcpProtocol> mTcp = new List<TcpProtocol>();
	TcpListener mListener;
	int mPort = 0;
	Thread mThread;
	bool mListIsDirty = false;
	long mTime = 0;

	/// <summary>
	/// If the number of simultaneous connected clients exceeds this number,
	/// server updates will no longer be instant, but rather delayed instead.
	/// </summary>

	public int instantUpdatesClientLimit = 50;

	/// <summary>
	/// Port used to listen for incoming packets.
	/// </summary>

	public override int port { get { return mPort; } }

	/// <summary>
	/// Whether the server is active.
	/// </summary>

	public override bool isActive { get { return (mListener != null); } }

	/// <summary>
	/// Mark the list as having changed.
	/// </summary>

	public override void MarkAsDirty () { mListIsDirty = true; }

	/// <summary>
	/// Start listening for incoming connections.
	/// </summary>

	public override bool Start (int listenPort)
	{
		Stop();

		try
		{
			mListener = new TcpListener(IPAddress.Any, listenPort);
			mListener.Start(50);
			mPort = listenPort;
		}
		catch (System.Exception)
		{
			return false;
		}
		mThread = new Thread(ThreadFunction);
		mThread.Start();
		return true;
	}

	/// <summary>
	/// Stop listening for incoming packets.
	/// </summary>

	public override void Stop ()
	{
		if (mThread != null)
		{
			mThread.Abort();
			mThread = null;
		}

		if (mListener != null)
		{
			mListener.Stop();
			mListener = null;
		}
		mList.Clear();
	}

	/// <summary>
	/// Thread that will be processing incoming data.
	/// </summary>

	void ThreadFunction ()
	{
		for (; ; )
		{
			mTime = DateTime.Now.Ticks / 10000;

			// Accept incoming connections
			while (mListener != null && mListener.Pending())
			{
				TcpProtocol tc = new TcpProtocol();
				tc.StartReceiving(mListener.AcceptSocket());
				mTcp.Add(tc);
			}

			Buffer buffer = null;

			// Process incoming TCP packets
			for (int i = 0; i < mTcp.size; ++i)
			{
				TcpProtocol tc = mTcp[i];

				while (tc.ReceivePacket(out buffer))
				{
					try
					{
						if (!ProcessPacket(buffer, tc))
						{
							mList.Remove(new IPEndPoint(tc.tcpEndPoint.Address, port));
							tc.Disconnect();
						}
					}
					catch (System.Exception) { }

					if (buffer != null)
					{
						buffer.Recycle();
						buffer = null;
					}
				}
			}

			// Remove clients that have been disconnected
			for (int i = mTcp.size; i > 0; )
			{
				TcpProtocol tc = mTcp[--i];
				if (tc.stage == TcpProtocol.Stage.NotConnected)
					mTcp.RemoveAt(i);
			}

			// We only want to send instant updates if the number of players is under a specific threshold
			bool instantUpdates = mTcp.size < instantUpdatesClientLimit;

			// Send the server list to all connected clients
			for (int i = 0; i < mTcp.size; ++i)
			{
				TcpProtocol tc = mTcp[i];
				if (tc.stage != TcpProtocol.Stage.Connected) continue;

				if (instantUpdates)
				{
					// Was instant before as well
					if (tc.customTimestamp == 0)
					{
						// List hasn't changed -- do nothing
						if (!mListIsDirty) continue;
					}
					// Timestamp hasn't been reached yet
					else if (tc.customTimestamp > mTime) continue;
				}
				// Timestamp hasn't been reached yet
				else if (tc.customTimestamp > mTime) continue;

				// Create the server list packet
				if (buffer == null)
				{
					buffer = Buffer.Create();
					BinaryWriter writer = buffer.BeginTcpPacket(Packet.ResponseServerList);
					mList.WriteTo(writer);
					buffer.EndTcpPacket();
				}
				tc.SendTcpPacket(buffer);
				tc.customTimestamp = instantUpdates ? 0 : mTime + 4000;
			}

			if (buffer != null)
			{
				buffer.Recycle();
				buffer = null;
			}
			mListIsDirty = false;
			Thread.Sleep(instantUpdates ? 1 : 10);
		}
	}

	/// <summary>
	/// Process an incoming packet.
	/// </summary>

	bool ProcessPacket (Buffer buffer, TcpProtocol tc)
	{
		BinaryReader reader = buffer.BeginReading();
		Packet request = (Packet)reader.ReadByte();

		// TCP connections must be verified first to ensure that they are using the correct protocol
		if (tc.stage == TcpProtocol.Stage.Verifying)
		{
			if (request == Packet.RequestID)
			{
				int clientVersion = reader.ReadInt32();
				tc.name = reader.ReadString();

				// Version matches? Connection is now verified.
				if (clientVersion == TcpPlayer.version)
					tc.stage = TcpProtocol.Stage.Connected;

				// Send the player the server's protocol version
				BinaryWriter writer = tc.BeginSend(Packet.ResponseID);
				writer.Write(TcpPlayer.version);
				writer.Write(0);
				tc.EndSend();

				// The client version must match
				if (clientVersion == TcpPlayer.version) return true;
			}
#if STANDALONE
			Console.WriteLine(tc.address + " has failed the verification step");
#endif
			return false;
		}

		switch (request)
		{
			case Packet.RequestAddServer:
			{
				if (reader.ReadUInt16() != GameServer.gameID) return false;
				string name = reader.ReadString();
				ushort port = reader.ReadUInt16();
				ushort count = reader.ReadUInt16();
				mList.Add(name, count, new IPEndPoint(tc.tcpEndPoint.Address, port), mTime);
				mListIsDirty = true;
				return true;
			}
			case Packet.RequestRemoveServer:
			{
				if (reader.ReadUInt16() != GameServer.gameID) return false;
				ushort port = reader.ReadUInt16();
				mList.Remove(new IPEndPoint(tc.tcpEndPoint.Address, port));
				mListIsDirty = true;
				return true;
			}
		}
		return false;
	}
}
}
