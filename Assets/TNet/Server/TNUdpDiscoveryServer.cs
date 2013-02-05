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
/// Optional UDP-based listener that makes it possible for servers to
/// register themselves with a central location for easy discovery by clients.
/// </summary>

public class UdpDiscoveryServer : DiscoveryServer
{
	// List of servers that's currently being updated
	ServerList mList = new ServerList();
	UdpProtocol mUdp;
	Thread mThread;
	long mTime = 0;
	bool mListIsDirty = false;
	Buffer mBuffer;
	ushort mBroadcastPort = 0;

	/// <summary>
	/// Port used to listen for incoming packets.
	/// </summary>

	public override int port { get { return mUdp.isActive ? mUdp.listeningPort : 0; } }

	/// <summary>
	/// Whether the server is active.
	/// </summary>

	public override bool isActive { get { return (mUdp != null && mUdp.isActive); } }

	/// <summary>
	/// Mark the list as having changed.
	/// </summary>

	public override void MarkAsDirty () { mListIsDirty = true; }

	/// <summary>
	/// Start listening for incoming UDP packets on the specified listener port.
	/// </summary>

	public override bool Start (int listenPort) { return Start(listenPort, 0); }

	/// <summary>
	/// Start listening for incoming UDP packets on the specified listener port, and automatically
	/// broadcast list changes to the entire LAN to the specified Broadcast Port.
	/// </summary>

	public bool Start (int listenPort, int broadcastPort)
	{
		Stop();
		mUdp = new UdpProtocol();
		if (!mUdp.Start(listenPort)) return false;
		mBroadcastPort = (ushort)broadcastPort;
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
		
		if (mUdp != null)
		{
			mUdp.Stop();
			mUdp = null;
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

			// Cleanup a list of servers by removing expired entries
			if (mList.Cleanup(mTime)) mListIsDirty = true;

			Buffer buffer;
			IPEndPoint ip;

			// Process incoming UDP packets
			while (mUdp != null && mUdp.ReceivePacket(out buffer, out ip))
			{
				try { ProcessPacket(buffer, ip); }
				catch (System.Exception) { }
				
				if (buffer != null)
				{
					buffer.Recycle();
					buffer = null;
				}
			}

			// If the list has changed, broadcast the updated list to the network
			if (mListIsDirty && mBroadcastPort != 0)
			{
				mListIsDirty = false;
				mList.WriteTo(BeginSend(Packet.ResponseServerList));
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
		Packet request = (Packet)reader.ReadByte();

		switch (request)
		{
			case Packet.RequestAddServer:
			{
				if (reader.ReadUInt16() != GameServer.gameID) return false;
				string name = reader.ReadString();
				ushort port = reader.ReadUInt16();
				ushort count = reader.ReadUInt16();
				mList.Add(name, count, new IPEndPoint(ip.Address, port), mTime);
				mListIsDirty = true;
				return true;
			}
			case Packet.RequestRemoveServer:
			{
				if (reader.ReadUInt16() != GameServer.gameID) return false;
				ushort port = reader.ReadUInt16();
				mList.Remove(new IPEndPoint(ip.Address, port));
				mListIsDirty = true;
				return true;
			}
			case Packet.RequestServerList:
			{
				if (reader.ReadUInt16() != GameServer.gameID) return false;
				mList.WriteTo(BeginSend(Packet.ResponseServerList));
				EndSend(ip);
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Start the sending process.
	/// </summary>

	BinaryWriter BeginSend (Packet packet)
	{
		mBuffer = Buffer.Create();
		BinaryWriter writer = mBuffer.BeginTcpPacket(packet);
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
