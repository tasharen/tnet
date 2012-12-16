//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;

namespace TNet
{
/// <summary>
/// UDP class makes it possible to broadcast messages to players on the same network prior to establishing a connection.
/// </summary>

public class UdpProtocol
{
	// Port used to listen and socket used to send and receive
	int mPort = 0;
	Socket mSocket;

	// Optional socket used to broadcast (created on demand)
	Socket mBroadcaster;

	// Buffer used for receiving incoming data
	byte[] mTemp = new byte[8192];

	// Datagram that's currently used to receive incoming data
	Datagram mInDatagram;

	// Cached broadcast end-point
	IPEndPoint mBroadcastIP = new IPEndPoint(IPAddress.Broadcast, 0);

	// Incoming message queue
	protected Queue<Datagram> mIn = new Queue<Datagram>();
	protected Queue<Datagram> mOut = new Queue<Datagram>();

	/// <summary>
	/// Whether we can send or receive through the UDP socket.
	/// </summary>

	public bool isActive { get { return mSocket != null; } }

	/// <summary>
	/// Port used for listening.
	/// </summary>

	public int listenerPort { get { return mPort; } }

	/// <summary>
	/// Stop listening for incoming packets.
	/// </summary>

	public void Stop ()
	{
		if (mInDatagram != null)
		{
			mInDatagram.Recycle();
			mInDatagram = null;
		}

		if (mSocket != null)
		{
			mSocket.Close();
			mSocket = null;
		}

		if (mBroadcaster != null)
		{
			mBroadcaster.Close();
			mBroadcaster = null;
		}
		Buffer.Recycle(mIn);
		Buffer.Recycle(mOut);
	}

	/// <summary>
	/// Start listening for incoming messages on the specified port.
	/// </summary>

	public bool Start (int port)
	{
		Stop();

		mPort = port;
		mSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		mSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
			
		if (port != 0)
		{
			mInDatagram = Datagram.Create();
			EndPoint ep = mInDatagram.endPoint;

			try
			{
				mSocket.Bind(new IPEndPoint(IPAddress.Any, mPort));
				mSocket.BeginReceiveFrom(mTemp, 0, mTemp.Length, SocketFlags.None, ref ep, OnReceive, null);
			}
#if UNITY_EDITOR
			catch (System.Exception ex)
			{
				UnityEngine.Debug.LogError(ex.Message);
#else
			catch (System.Exception)
			{
#endif
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// Receive incoming data.
	/// </summary>

	void OnReceive (IAsyncResult result)
	{
		// NOTE: I can set this to 'null'. Apparently the value does NOT get updated, but re-assigned.
		// I wonder what happens to the previous data. Garbage collection? Ugh...
		EndPoint ep = mInDatagram.endPoint;
		int bytes = 0;

		try
		{
			bytes = mSocket.EndReceiveFrom(result, ref ep);
		}
#if UNITY_EDITOR
		catch (System.Exception ex)
		{
			UnityEngine.Debug.LogWarning(ex.Message);
#else
		catch (System.Exception)
		{
#endif
			Stop();
			return;
		}

		if (bytes > 4)
		{
			// This datagram is now ready to be processed
			mInDatagram.buffer.BeginWriting(false).Write(mTemp, 0, bytes);
			mInDatagram.buffer.BeginReading(4);

			// See the note above. The 'endPoint', gets reassigned rather than updated.
			mInDatagram.endPoint = (IPEndPoint)ep;
			lock (mIn) mIn.Enqueue(mInDatagram);
			if (mSocket != null) mInDatagram = Datagram.Create();
		}

		// Queue up the next receive operation
		if (mSocket != null)
		{
			ep = mInDatagram.endPoint;
			mSocket.BeginReceiveFrom(mTemp, 0, mTemp.Length, SocketFlags.None, ref ep, OnReceive, null);
		}
	}

	/// <summary>
	/// Extract the first incoming packet.
	/// </summary>

	public Datagram ReceiveDatagram ()
	{
		if (mIn.Count != 0)
		{
			lock (mIn) return mIn.Dequeue();
		}
		return null;
	}

	/// <summary>
	/// Send an empty packet to the target destination.
	/// Can be used for NAT punch-through, or just to keep the connection alive.
	/// Empty packets are simply ignored.
	/// </summary>

	public void SendEmptyPacket (IPEndPoint ip)
	{
		Datagram dg = Datagram.Create();
		dg.endPoint = ip;
		dg.buffer.BeginTcpPacket(Packet.Empty);
		dg.buffer.EndTcpPacket();
		Send(dg);
	}

	/// <summary>
	/// Send the specified buffer to the entire LAN.
	/// </summary>

	public void Send (int port, Buffer buffer)
	{
		buffer.MarkAsUsed();
#if UNITY_WEBPLAYER
		UnityEngine.Debug.LogError("Sending broadcasts doesn't work in the Unity Web Player");
#else
		if (mBroadcaster == null)
		{
			mBroadcaster = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			mBroadcaster.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
		}
		mBroadcastIP.Port = port;
		mBroadcaster.SendTo(buffer.buffer, buffer.position, buffer.size, SocketFlags.None, mBroadcastIP);
#endif
		buffer.Recycle();
	}

	/// <summary>
	/// Send the specified datagram.
	/// </summary>

	public void Send (Datagram dg)
	{
		if (mSocket != null)
		{
			dg.buffer.BeginReading();

			lock (mOut)
			{
				mOut.Enqueue(dg);

				if (mOut.Count == 1)
				{
					// If it's the first datagram, begin the sending process
					mSocket.BeginSendTo(dg.buffer.buffer, dg.buffer.position, dg.buffer.size,
						SocketFlags.None, dg.endPoint, OnSend, dg);
				}
			}
		}
		else dg.Recycle();
	}

	/// <summary>
	/// Send completion callback. Recycles the datagram.
	/// </summary>

	void OnSend (IAsyncResult result)
	{
		int bytes;
		Datagram dg = (Datagram)result.AsyncState;

		try
		{
			bytes = mSocket.EndSendTo(result);
		}
#if UNITY_EDITOR
		catch (System.Exception ex)
		{
			UnityEngine.Debug.LogWarning(ex.Message);
#else
		catch (System.Exception)
		{
#endif
			Stop();
			return;
		}

		lock (mOut)
		{
			mOut.Dequeue();

			if (bytes > 0)
			{
				// Recycle this datagram as it's no longer in use
				dg.Recycle();

				// If there is another packet to send out, let's send it
				Datagram next = (mOut.Count == 0) ? null : mOut.Peek();
				if (next != null) mSocket.BeginSendTo(next.buffer.buffer, next.buffer.position, next.buffer.size,
					SocketFlags.None, next.endPoint, OnSend, next);
			}
			else dg.Recycle();
		}
	}

	/// <summary>
	/// Send the specified buffer to the target destination.
	/// </summary>

	public void Send (IPEndPoint target, Buffer buffer)
	{
		mSocket.SendTo(buffer.buffer, buffer.position, buffer.size, SocketFlags.None, target);
	}

	/// <summary>
	/// Send the specified buffer to the target destination.
	/// </summary>

	public void Send (string address, int port, Buffer buffer)
	{
		IPEndPoint target = new IPEndPoint(Player.ResolveAddress(address), port);
		mSocket.SendTo(buffer.buffer, buffer.position, buffer.size, SocketFlags.None, target);
	}

	/// <summary>
	/// Add an error packet to the incoming queue.
	/// </summary>

	public void Error (IPEndPoint ip, string error)
	{
		Datagram dg = Datagram.Create();
		dg.endPoint = ip;
		Error (dg, error);
	}

	/// <summary>
	/// Add an error packet to the incoming queue.
	/// </summary>

	void Error (Datagram dg, string error)
	{
		if (dg.buffer == null) dg.buffer = Buffer.Create();
		dg.buffer.BeginTcpPacket(Packet.Error).Write(error);
		dg.buffer.EndTcpPacketWithOffset(4);
		lock (mIn) mIn.Enqueue(dg);
	}
}
}