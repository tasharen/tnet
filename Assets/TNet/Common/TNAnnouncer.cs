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
/// UDP class makes it possible to broadcast messages to players on the same network prior to establishing a connection.
/// </summary>

public class UdpTool
{
	int mPort = 0;
	Socket mSender;
	Socket mReceiver;
	Buffer mReceiveBuffer;

	// Buffer used for receiving incoming data
	byte[] mTemp = new byte[8192];

	// Incoming message queue
	Queue<Buffer> mBuffers = new Queue<Buffer>();
	Queue<string> mAddresses = new Queue<string>();
	EndPoint mEndPoint = new IPEndPoint(IPAddress.Any, 0);

	/// <summary>
	/// Whether we can send or receive through the announcer.
	/// </summary>

	public bool isActive { get { return mReceiver != null; } }

	/// <summary>
	/// Start listening for incoming messages on the specified port.
	/// </summary>

	public bool Start (int port)
	{
		Stop();

		mPort = port;
		mReceiver = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		mReceiver.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
			
		if (port != 0)
		{
			EndPoint endPoint = new IPEndPoint(IPAddress.Any, mPort);

			try
			{
				mReceiver.Bind(endPoint);
				mReceiver.BeginReceiveFrom(mTemp, 0, mTemp.Length, SocketFlags.None, ref mEndPoint, OnReceive, null);
			}
			catch (System.Exception ex)
			{
				UnityEngine.Debug.Log(ex.Message);
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// Stop listening for broadcasts.
	/// </summary>

	public void Stop ()
	{
		if (mReceiver != null)
		{
			mReceiver.Close();
			mReceiver = null;
		}
		Buffer.Recycle(mBuffers);
		mAddresses.Clear();
	}

	/// <summary>
	/// Receive incoming data.
	/// </summary>

	void OnReceive (IAsyncResult result)
	{
		int bytes = 0;

		try
		{
			bytes = mReceiver.EndReceiveFrom(result, ref mEndPoint);
		}
		catch (System.Exception)
		{
			Stop();
			return;
		}

		if (bytes > 4)
		{
			// Read the packet. UDP packets always arrive whole. They don't get fragmented like TCP.
			Buffer buffer = Buffer.Create();
			BinaryWriter writer = buffer.BeginWriting(false);

			IPEndPoint ip = (IPEndPoint)mEndPoint;
			writer.Write(mTemp, 0, bytes);
			buffer.BeginReading(4);
			
			lock (mBuffers)
			{
				mBuffers.Enqueue(buffer);
				mAddresses.Enqueue(ip.Address.ToString() + ":" + ip.Port);
			}

			// Queue up the next receive operation
			mReceiver.BeginReceiveFrom(mTemp, 0, mTemp.Length, SocketFlags.None, ref mEndPoint, OnReceive, null);
		}
	}

	/// <summary>
	/// Extract the first incoming packet.
	/// </summary>

	public Buffer ReceivePacket (out string address)
	{
		if (mBuffers.Count != 0)
		{
			lock (mBuffers)
			{
				address = mAddresses.Dequeue();
				return mBuffers.Dequeue();
			}
		}
		address = null;
		return null;
	}

	/// <summary>
	/// Send the specified buffer to the entire LAN.
	/// </summary>

	public void Send (int port, Buffer buffer)
	{
		if (mSender == null)
		{
			mSender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			mSender.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
		}
		mSender.SendTo(buffer.buffer, buffer.position, buffer.size, SocketFlags.None, new IPEndPoint(IPAddress.Broadcast, port));
	}
}
}