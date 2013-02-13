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
/// register themselves with a central location for easy lobby by clients.
/// </summary>

public class TcpLobbyServer : LobbyServer
{
	// List of servers that's currently being updated
	ServerList mList = new ServerList();
	long mTime = 0;
	long mLastChange = 0;
	List<TcpProtocol> mTcp = new List<TcpProtocol>();
	TcpListener mListener;
	int mPort = 0;
	Thread mThread;
	bool mInstantUpdates = true;

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
	/// Start listening for incoming connections.
	/// Note that the broadcasting is not possible with TCP, so the broadcast port is ignored.
	/// </summary>

	public override bool Start (int listenPort, int broadcastPort)
	{
		Stop();

		try
		{
			mListener = new TcpListener(IPAddress.Any, listenPort);
			mListener.Start(50);
			mPort = listenPort;
		}
#if STANDALONE
		catch (System.Exception ex)
		{
			Console.WriteLine("ERROR: " + ex.Message);
			return false;
		}
		Console.WriteLine("TCP Lobby Server started on port " + listenPort);
#else
		catch (System.Exception) { return false; }
#endif
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
				tc.data = (long)(-1);
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
					try { if (!ProcessPacket(buffer, tc)) tc.Disconnect(); }
#if STANDALONE
					catch (System.Exception ex) { Console.WriteLine("ERROR: " + ex.Message); }
#else
					catch (System.Exception) {}
#endif
					if (buffer != null)
					{
						buffer.Recycle();
						buffer = null;
					}
				}
			}

			// We only want to send instant updates if the number of players is under a specific threshold
			if (mTcp.size > instantUpdatesClientLimit) mInstantUpdates = false;

			// Send the server list to all connected clients
			for (int i = 0; i < mTcp.size; ++i)
			{
				TcpProtocol tc = mTcp[i];
				long customTimestamp = (long)tc.data;

				// Timestamp of -1 means we don't want updates to be sent
				if (tc.stage != TcpProtocol.Stage.Connected || customTimestamp == -1) continue;

				// If timestamp was set then the list was already sent previously
				if (customTimestamp != 0)
				{
					// List hasn't changed -- do nothing
					if (customTimestamp >= mLastChange) continue;
					
					// Too many clients: we want the updates to be infrequent
					if (!mInstantUpdates && customTimestamp + 4000 > mTime) continue;
				}

				// Create the server list packet
				if (buffer == null)
				{
					buffer = Buffer.Create();
					BinaryWriter writer = buffer.BeginPacket(Packet.ResponseServerList);
					mList.WriteTo(writer);
					buffer.EndPacket();
				}
				tc.SendTcpPacket(buffer);
				tc.data = mTime;
			}

			if (buffer != null)
			{
				buffer.Recycle();
				buffer = null;
			}
			Thread.Sleep(1);
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
			if (tc.VerifyRequestID(request, reader, false)) return true;
#if STANDALONE
			Console.WriteLine(tc.address + " has failed the verification step");
#endif
			return false;
		}

		switch (request)
		{
			case Packet.RequestServerList:
			{
				if (reader.ReadUInt16() != GameServer.gameID) return false;
				tc.data = (long)0;
				return true;
			}
			case Packet.RequestAddServer:
			{
				if (reader.ReadUInt16() != GameServer.gameID) return false;
				ServerList.Entry ent = new ServerList.Entry();
				ent.ReadFrom(reader);

				if (ent.externalAddress.Address.Equals(IPAddress.None))
					ent.externalAddress = tc.tcpEndPoint;

				mList.Add(ent).data = tc;
				mLastChange = mTime;
#if STANDALONE
				Console.WriteLine(tc.address + " added a server (" + ent.internalAddress + ", " + ent.externalAddress + ")");
#endif
				return true;
			}
			case Packet.RequestRemoveServer:
			{
				if (reader.ReadUInt16() != GameServer.gameID) return false;
				IPEndPoint internalAddress, externalAddress;
				Tools.Serialize(reader, out internalAddress);
				Tools.Serialize(reader, out externalAddress);

				if (externalAddress.Address.Equals(IPAddress.None))
					externalAddress = tc.tcpEndPoint;

				RemoveServer(internalAddress, externalAddress);
#if STANDALONE
				Console.WriteLine(tc.address + " removed a server (" + internalAddress + ", " + externalAddress + ")");
#endif
				return true;
			}
			case Packet.Disconnect:
			{
				RemoveServer(tc);
				mTcp.Remove(tc);
				return true;
			}
		}
#if STANDALONE
		Console.WriteLine(tc.address + " sent a packet not handled by the lobby server: " + request);
#endif
		return false;
	}

	/// <summary>
	/// Remove all entries added by the specified client.
	/// </summary>

	void RemoveServer (Player player)
	{
		lock (mList.list)
		{
			for (int i = mList.list.size; i > 0; )
			{
				ServerList.Entry ent = mList.list[--i];

				if (ent.data == player)
				{
					mList.list.RemoveAt(i);
					mLastChange = mTime;
				}
			}
		}
	}

	/// <summary>
	/// Add a new server to the list.
	/// </summary>

	public override void AddServer (string name, int playerCount, IPEndPoint internalAddress, IPEndPoint externalAddress)
	{
		mList.Add(name, playerCount, internalAddress, externalAddress);
		mLastChange = mTime;
	}

	/// <summary>
	/// Remove an existing server from the list.
	/// </summary>

	public override void RemoveServer (IPEndPoint internalAddress, IPEndPoint externalAddress)
	{
		if (mList.Remove(internalAddress, externalAddress))
			mLastChange = mTime;
	}
}
}
