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
/// Announcer class makes it possible to broadcast messages to players on the same network prior to establishing a connection.
/// TODO: Why not move this logic into client/server? Client will listen for broadcast ID packets. Server will send them.
/// </summary>

public class Announcer
{
	int mPort = 0;
	Socket mSocket;
	Buffer mReceiveBuffer;

	// Buffer used for receiving incoming data
	byte[] mTemp = new byte[8192];

	// Incoming message queue
	Queue<Buffer> mBuffers = new Queue<Buffer>();
	EndPoint mAddress = new IPEndPoint(IPAddress.Any, 0);

	/// <summary>
	/// Whether we can send or receive through the announcer.
	/// </summary>

	public bool isActive { get { return mSocket != null; } }

	/// <summary>
	/// Add an error packet to the incoming queue.
	/// </summary>

	void Error (string error)
	{
		Buffer buffer = Buffer.Create();
		BinaryWriter writer = buffer.BeginWriting(false);
		writer.Write((byte)Packet.Error);
		writer.Write(error);
		lock (mBuffers) mBuffers.Enqueue(buffer);
	}

	/// <summary>
	/// Start listening for incoming messages on the specified port.
	/// </summary>

	public bool Start (int port, bool canReceive)
	{
		Stop();

		mPort = port;
		mSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		mSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
			
		if (port != 0)
		{
			EndPoint endPoint = new IPEndPoint(IPAddress.Any, mPort);

			try
			{
				mSocket.Bind(endPoint);
				if (canReceive) mSocket.BeginReceiveFrom(mTemp, 0, mTemp.Length, SocketFlags.None, ref mAddress, OnReceive, null);
			}
			catch (System.Exception ex)
			{
				Error(ex.Message);
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
		if (mSocket != null)
		{
			mSocket.Close();
			mSocket = null;
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
			bytes = mSocket.EndReceiveFrom(result, ref mAddress);
		}
		catch (System.Exception)
		{
			Stop();
			return;
		}

		if (bytes > 0)
		{
			// Read the packet and skip past its size. Note that it would be safer to buffer it like TNet.Connection does,
			// but UDP packets are generally short and unimportant, so in this case we don't really care.

			Buffer buffer = Buffer.Create();
			buffer.BeginWriting(false).Write(mTemp, 0, bytes);
			buffer.BeginReading();
			buffer.position = 4;
			lock (mBuffers) mBuffers.Enqueue(buffer);

			// Queue up the next receive operation
			mSocket.BeginReceiveFrom(mTemp, 0, mTemp.Length, SocketFlags.None, ref mAddress, OnReceive, null);
		}
	}

	/// <summary>
	/// Extract the first incoming packet.
	/// </summary>

	public Buffer Receive ()
	{
		if (mBuffers.Count == 0) return null;
		lock (mBuffers) return mBuffers.Dequeue();
	}

	/// <summary>
	/// Send the specified packet to everyone listening to the specified port on the network.
	/// </summary>

	public void Broadcast (Buffer buffer, int port)
	{
		if (mSocket != null)
		{
			buffer.MarkAsUsed();
			buffer.BeginReading();
			mSocket.SendTo(buffer.buffer, buffer.position, buffer.size, SocketFlags.None, new IPEndPoint(IPAddress.Broadcast, port));
			buffer.MarkAsUnused();
		}
	}
}
}