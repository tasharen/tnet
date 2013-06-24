//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2013 Tasharen Entertainment
//---------------------------------------------

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
	int mPort = -1;
	Socket mSocket;

	// Buffer used for receiving incoming data
	byte[] mTemp = new byte[8192];

	// Buffer that's currently used to receive incoming data
	EndPoint mEndPoint = new IPEndPoint(IPAddress.Any, 0);

#if !UNITY_WEBPLAYER
	// Cached broadcast end-point
	IPEndPoint mBroadcastIP = new IPEndPoint(IPAddress.Broadcast, 0);
#endif

	// Incoming message queue
	protected Queue<Datagram> mIn = new Queue<Datagram>();
	protected Queue<Datagram> mOut = new Queue<Datagram>();

	/// <summary>
	/// Whether we can send or receive through the UDP socket.
	/// </summary>

	public bool isActive { get { return mPort != -1; } }

	/// <summary>
	/// Port used for listening.
	/// </summary>

	public int listeningPort { get { return mPort > 0 ? mPort : 0; } }

	/// <summary>
	/// Start UDP, but don't bind it to a specific port. This means we will be able to send, but not receive.
	/// </summary>

	public bool Start () { return Start(0); }

	/// <summary>
	/// Start listening for incoming messages on the specified port.
	/// </summary>

#if UNITY_FLASH || UNITY_WEBPLAYER
	// UDP is not supported by Flash.
	public bool Start (int port)
	{
#if UNITY_EDITOR
		UnityEngine.Debug.LogWarning("Unity doesn't support UDP on Flash or Web Player targets");
#endif
		return false;
	}
#else
	public bool Start (int port)
	{
		Stop();

		mPort = port;
		mSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
#if !UNITY_WEBPLAYER
		// Web player doesn't seem to support broadcasts
		mSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
#endif
		// Port zero means we will be able to send, but not receive
		if (mPort == 0) return true;

		try
		{
			mSocket.Bind(new IPEndPoint(IPAddress.Any, mPort));
			mSocket.BeginReceiveFrom(mTemp, 0, mTemp.Length, SocketFlags.None, ref mEndPoint, OnReceive, null);
		}
#if UNITY_EDITOR
		catch (System.Exception ex) { UnityEngine.Debug.LogError("Udp.Start: " + ex.Message); Stop(); return false; }
#elif DEBUG
		catch (System.Exception ex) { Console.WriteLine("Udp.Start: " + ex.Message); Stop(); return false; }
#else
		catch (System.Exception) { Stop(); return false; }
#endif
		return true;
	}
#endif // UNITY_FLASH

	/// <summary>
	/// Stop listening for incoming packets.
	/// </summary>

	public void Stop ()
	{
		mPort = -1;

		if (mSocket != null)
		{
			mSocket.Close();
			mSocket = null;
		}

		Buffer.Recycle(mIn);
		Buffer.Recycle(mOut);
	}

	/// <summary>
	/// Receive incoming data.
	/// </summary>

	void OnReceive (IAsyncResult result)
	{
		if (!isActive) return;
		int bytes = 0;

		try
		{
			bytes = mSocket.EndReceiveFrom(result, ref mEndPoint);
		}
		catch (System.Exception ex)
		{
			Error(new IPEndPoint(Tools.localAddress, 0), ex.Message);
		}

		if (bytes > 4)
		{
			// This datagram is now ready to be processed
			Buffer buffer = Buffer.Create();
			buffer.BeginWriting(false).Write(mTemp, 0, bytes);
			buffer.BeginReading(4);

			// See the note above. The 'endPoint', gets reassigned rather than updated.
			Datagram dg = new Datagram();
			dg.buffer = buffer;
			dg.ip = (IPEndPoint)mEndPoint;
			lock (mIn) mIn.Enqueue(dg);
		}

		// Queue up the next receive operation
		if (mSocket != null)
		{
			mSocket.BeginReceiveFrom(mTemp, 0, mTemp.Length, SocketFlags.None, ref mEndPoint, OnReceive, null);
		}
	}

	/// <summary>
	/// Extract the first incoming packet.
	/// </summary>

	public bool ReceivePacket (out Buffer buffer, out IPEndPoint source)
	{
		if (mPort == 0)
		{
			Stop();
			throw new System.InvalidOperationException("You must specify a non-zero port to UdpProtocol.Start() before you can receive data.");
		}
		else if (mIn.Count != 0)
		{
			lock (mIn)
			{
				Datagram dg = mIn.Dequeue();
				buffer = dg.buffer;
				source = dg.ip;
				return true;
			}
		}
		buffer = null;
		source = null;
		return false;
	}

	/// <summary>
	/// Send an empty packet to the target destination.
	/// Can be used for NAT punch-through, or just to keep a UDP connection alive.
	/// Empty packets are simply ignored.
	/// </summary>

	public void SendEmptyPacket (IPEndPoint ip)
	{
		Buffer buffer = Buffer.Create(false);
		buffer.BeginPacket(Packet.Empty);
		buffer.EndPacket();
		Send(buffer, ip);
	}

	/// <summary>
	/// Send the specified buffer to the entire LAN.
	/// </summary>

	public void Broadcast (Buffer buffer, int port)
	{
		if (buffer != null)
		{
			buffer.MarkAsUsed();
#if UNITY_WEBPLAYER || UNITY_FLASH
#if UNITY_EDITOR
			UnityEngine.Debug.LogError("Sending broadcasts doesn't work in the Unity Web Player or Flash");
#endif
#else
			mBroadcastIP.Port = port;
			
			try
			{
				mSocket.SendTo(buffer.buffer, buffer.position, buffer.size, SocketFlags.None, mBroadcastIP);
			}
			catch (System.Exception ex)
			{
				Error(null, ex.Message);
			}
#endif
			buffer.Recycle();
		}
	}

	/// <summary>
	/// Send the specified datagram.
	/// </summary>

	public void Send (Buffer buffer, IPEndPoint ip)
	{
		if (ip.Address.Equals(IPAddress.Broadcast))
		{
			Broadcast(buffer, ip.Port);
			return;
		}

		buffer.MarkAsUsed();

		if (mSocket != null)
		{
			buffer.BeginReading();

			lock (mOut)
			{
				Datagram dg = new Datagram();
				dg.buffer = buffer;
				dg.ip = ip;
				mOut.Enqueue(dg);

				if (mOut.Count == 1)
				{
					// If it's the first datagram, begin the sending process
					mSocket.BeginSendTo(buffer.buffer, buffer.position, buffer.size,
						SocketFlags.None, ip, OnSend, null);
				}
			}
		}
		else
		{
			buffer.Recycle();
			throw new InvalidOperationException("The socket is null. Did you forget to call UdpProtocol.Start()?");
		}
	}

	/// <summary>
	/// Send completion callback. Recycles the datagram.
	/// </summary>

	void OnSend (IAsyncResult result)
	{
		if (!isActive) return;
		int bytes = 0;

		try
		{
			bytes = mSocket.EndSendTo(result);
		}
		catch (System.Exception ex)
		{
			bytes = 1;
#if STANDALONE
			Console.WriteLine(ex.Message);
#else
			UnityEngine.Debug.Log(ex.Message);
#endif
			//Error(new IPEndPoint(Tools.localAddress, 0), ex.Message);
		}

		lock (mOut)
		{
			mOut.Dequeue().buffer.Recycle();

			if (bytes > 0 && mSocket != null && mOut.Count != 0)
			{
				// If there is another packet to send out, let's send it
				Datagram dg = mOut.Peek();
				mSocket.BeginSendTo(dg.buffer.buffer, dg.buffer.position, dg.buffer.size,
					SocketFlags.None, dg.ip, OnSend, null);
			}
		}
	}

	/// <summary>
	/// Add an error packet to the incoming queue.
	/// </summary>

	public void Error (IPEndPoint ip, string error)
	{
		Buffer buffer = Buffer.Create();
		buffer.BeginPacket(Packet.Error).Write(error);
		buffer.EndTcpPacketWithOffset(4);

		Datagram dg = new Datagram();
		dg.buffer = buffer;
		dg.ip = ip;
		lock (mIn) mIn.Enqueue(dg);
	}
}
}
