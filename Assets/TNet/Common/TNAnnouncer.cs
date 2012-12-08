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
	EndPoint mAddress = new IPEndPoint(IPAddress.Any, 0);
	Buffer mBuffer;

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
				mReceiver.BeginReceiveFrom(mTemp, 0, mTemp.Length, SocketFlags.None, ref mAddress, OnReceive, null);
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
	}

	/// <summary>
	/// Receive incoming data.
	/// </summary>

	void OnReceive (IAsyncResult result)
	{
		int bytes = 0;

		try
		{
			bytes = mReceiver.EndReceiveFrom(result, ref mAddress);
			//UnityEngine.Debug.Log(((IPEndPoint)mAddress).Address.ToString());
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

			IPEndPoint ip = (IPEndPoint)mAddress;
			writer.Write(ip.Address.ToString() + ":" + ip.Port);
			writer.Write(mTemp, 0, bytes);
			buffer.BeginReading();
			lock (mBuffers) mBuffers.Enqueue(buffer);

			// Queue up the next receive operation
			mReceiver.BeginReceiveFrom(mTemp, 0, mTemp.Length, SocketFlags.None, ref mAddress, OnReceive, null);
		}
	}

	/// <summary>
	/// Extract the first incoming packet.
	/// </summary>

	public Buffer ReceivePacket (out string address)
	{
		if (mBuffers.Count != 0)
		{
			Buffer buffer;
			lock (mBuffers) buffer = mBuffers.Dequeue();

			if (buffer != null)
			{
				BinaryReader reader = buffer.BeginReading();
				address = reader.ReadString();
				reader.ReadInt32(); // Skip past the packet's size
				return buffer;
			}
		}
		address = null;
		return null;
	}

	/// <summary>
	/// Begin the broadcast operation.
	/// </summary>

	public BinaryWriter BeginSend (Packet packet)
	{
		mBuffer = Buffer.Create();
		return mBuffer.BeginPacket(packet);
	}

	/// <summary>
	/// Finish the broadcast operation and send the accumulated buffer to the entire LAN.
	/// </summary>

	public void EndSend (int port)
	{
		if (mBuffer != null)
		{
			mBuffer.EndPacket();

			if (mSender == null)
			{
				mSender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
				mSender.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
			}
			mSender.SendTo(mBuffer.buffer, mBuffer.position, mBuffer.size, SocketFlags.None, new IPEndPoint(IPAddress.Broadcast, port));
			mBuffer.Recycle();
			mBuffer = null;
		}
	}

	/// <summary>
	/// Send the specified buffer to the entire LAN.
	/// </summary>

	public void Send (int port, Buffer buffer)
	{
		mSender.SendTo(buffer.buffer, buffer.position, buffer.size, SocketFlags.None, new IPEndPoint(IPAddress.Broadcast, port));
	}
}
}