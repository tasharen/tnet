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
	public enum Protocol
	{
		Udp,
		Tcp,
	}

	// List of servers that's currently being updated
	ServerList mList = new ServerList();
	UdpProtocol mUdp;
	List<TcpProtocol> mTcp = new List<TcpProtocol>();
	TcpListener mListener;
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
	/// Protocol that should be used.
	/// </summary>

	public Protocol protocol = Protocol.Udp;

	/// <summary>
	/// Whether the server is active.
	/// </summary>

	public bool isActive { get { return (mUdp != null && mUdp.isActive) || (mListener != null); } }

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

		if (protocol == Protocol.Udp)
		{
			if (!mUdp.Start(listenPort)) return false;
			mBroadcastPort = (ushort)broadcastPort;
		}
		else
		{
			try
			{
				mListener = new TcpListener(IPAddress.Any, listenPort);
				mListener.Start(50);
			}
			catch (System.Exception)
			{
				return false;
			}
		}
		mThread = new Thread(ThreadFunction);
		mThread.Start();
		return true;
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
		
		if (mUdp != null)
		{
			mUdp.Stop();
			mUdp = null;
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

			// Cleanup a list of servers by removing expired entries
			if (mList.Cleanup(mTime)) mListIsDirty = true;

			// Accept incoming connections
			while (mListener != null && mListener.Pending())
			{
				TcpProtocol tc = new TcpProtocol();
				tc.StartReceiving(mListener.AcceptSocket());
				mTcp.Add(tc);
			}

			Buffer buffer;
			IPEndPoint ip;

			// Process incoming UDP packets
			while (mUdp != null && mUdp.ReceivePacket(out buffer, out ip))
			{
				try { ProcessPacket(buffer, null, ip); }
				catch (System.Exception) { }
				
				if (buffer != null)
				{
					buffer.Recycle();
					buffer = null;
				}
			}

			// Process incoming TCP packets
			for (int i = 0; i < mTcp.size; ++i)
			{
				TcpProtocol tc = mTcp[i];

				while (tc.ReceivePacket(out buffer))
				{
					try
					{
						if (!ProcessPacket(buffer, tc, null))
							tc.Disconnect();
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

			// If the list has changed, broadcast the updated list to the network
			if (mListIsDirty && mBroadcastPort != 0)
			{
				mListIsDirty = false;
				mList.WriteTo(BeginSend(Packet.ResponseServerList), localServer);
				EndSend();
			}
			Thread.Sleep(1);
		}
	}

	/// <summary>
	/// Process an incoming packet.
	/// </summary>

	bool ProcessPacket (Buffer buffer, TcpProtocol tc, IPEndPoint ip)
	{
		BinaryReader reader = buffer.BeginReading();
		Packet request = (Packet)reader.ReadByte();

		// TCP connections must be verified first to ensure that they are using the correct protocol
		if (tc != null && tc.stage == TcpProtocol.Stage.Verifying)
		{
			if (request == Packet.RequestID)
			{
				int clientVersion = reader.ReadInt32();
				tc.name = reader.ReadString();

				// Version matches? Connection is now verified.
				if (clientVersion == TcpPlayer.version)
					tc.stage = TcpProtocol.Stage.Connected;

				// Send the player their ID
				BinaryWriter writer = BeginSend(Packet.ResponseID);
				writer.Write(TcpPlayer.version);
				writer.Write(0);
				EndSend(tc, null);

				// If the version matches, move on to the next packet
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
				mList.Add(name, count, new IPEndPoint(ip.Address, port), mTime);
				return true;
			}
			case Packet.RequestRemoveServer:
			{
				if (reader.ReadUInt16() != GameServer.gameID) return false;
				ushort port = reader.ReadUInt16();
				mList.Remove(new IPEndPoint(ip.Address, port));
				return true;
			}
			case Packet.RequestServerList:
			{
				if (reader.ReadUInt16() != GameServer.gameID) return false;
				mList.WriteTo(BeginSend(Packet.ResponseServerList), localServer);
				EndSend(tc, ip);
				return true;
			}
			case Packet.Empty:
			{
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

	void EndSend (TcpProtocol tc, IPEndPoint ip)
	{
		mBuffer.EndTcpPacket();
		if (tc != null) tc.SendTcpPacket(mBuffer);
		else if (ip != null) mUdp.Send(mBuffer, ip);
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
