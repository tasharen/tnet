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
/// Discovery server is an optional UDP-based listener that makes it possible for servers to
/// register themselves with a central location for easy discovery by clients.
/// </summary>

public class DiscoveryServer
{
	// List of servers that's currently being updated
	ServerList mList = new ServerList();
	UdpProtocol mUdp = new UdpProtocol();
	Thread mThread;
	long mTime = 0;
	bool mListIsDirty = false;
	Buffer mBuffer;
	ushort mBroadcastPort = 0;

	/// <summary>
	/// Local server, if we're hosting any.
	/// </summary>

	public GameServer localServer;

	/// <summary>
	/// Whether the server is active.
	/// </summary>

	public bool isActive { get { return mUdp.isActive; } }

	/// <summary>
	/// Port used to listen for incoming packets.
	/// </summary>

	public int port { get { return mUdp.isActive ? mUdp.listeningPort : 0; } }

	/// <summary>
	/// Start listening for incoming UDP packets on the specified listener port.
	/// </summary>

	public bool Start (int listenPort) { return Start(listenPort, 0); }

	/// <summary>
	/// Start listening for incoming UDP packets on the specified listener port, and automatically
	/// broadcast list changes to the entire LAN to the specified Broadcast Port.
	/// </summary>

	public bool Start (int listenPort, int broadcastPort)
	{
		Stop();
		
		if (mUdp.Start(listenPort))
		{
			mBroadcastPort = (ushort)broadcastPort;
			mThread = new Thread(ThreadFunction);
			mThread.Start();
			return true;
		}
		return false;
	}

	/// <summary>
	/// Stop listening for incoming packets.
	/// </summary>

	public void Stop ()
	{
		if (mThread != null)
		{
			mThread.Abort();
			mThread = null;
		}
		mUdp.Stop();
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

			// Cleanup a list of servers by removing expired entries
			if (mList.Cleanup(mTime)) mListIsDirty = true;

			Buffer buffer;
			IPEndPoint ip;

			// Receive and process UDP packets one at a time
			while (mUdp.ReceivePacket(out buffer, out ip))
			{
				if (buffer.size > 0)
				{
					try { ProcessPacket(buffer, ip); }
					catch (System.Exception) { }
				}
				buffer.Recycle();
			}

			// If the list has changed, broadcast the updated list to the network
			if (mListIsDirty && mBroadcastPort != 0)
			{
				mListIsDirty = false;
				mList.WriteTo(BeginSend(), localServer);
				EndSend();
			}
			Thread.Sleep(1);
		}
	}

	/// <summary>
	/// Process an incoming packet.
	/// </summary>

	bool ProcessPacket (Buffer buffer, IPEndPoint ip)
	{
		BinaryReader reader = buffer.BeginReading();

		// First byte should be the packet identifier
		Packet request = (Packet)reader.ReadByte();

		// The game ID must match
		if (reader.ReadUInt16() != GameServer.gameID) return false;

		switch (request)
		{
			case Packet.RequestAddServer:
			{
				string name = reader.ReadString();
				ushort port = reader.ReadUInt16();
				ushort count = reader.ReadUInt16();
				mList.Add(name, count, new IPEndPoint(ip.Address, port), mTime);
				return true;
			}
			case Packet.RequestRemoveServer:
			{
				ushort port = reader.ReadUInt16();
				mList.Remove(new IPEndPoint(ip.Address, port));
				return true;
			}
			case Packet.RequestListServers:
			{
				mList.WriteTo(BeginSend(), localServer);
				EndSend(ip);
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Start the sending process.
	/// </summary>

	BinaryWriter BeginSend ()
	{
		mBuffer = Buffer.Create();
		BinaryWriter writer = mBuffer.BeginTcpPacket(Packet.ResponseListServers);
		return writer;
	}

	/// <summary>
	/// Send the outgoing buffer to the specified remote destination.
	/// </summary>

	void EndSend (IPEndPoint ip)
	{
		mBuffer.EndTcpPacket();
		mUdp.Send(mBuffer, ip);
		mBuffer.Recycle();
		mBuffer = null;
	}

	/// <summary>
	/// Broadcast this packet to LAN.
	/// </summary>

	void EndSend ()
	{
		mBuffer.EndTcpPacket();
		mUdp.Broadcast(mBuffer, mBroadcastPort);
		mBuffer.Recycle();
		mBuffer = null;
	}
}
}
