//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using System;
using System.IO;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Net;

namespace TNet
{
/// <summary>
/// Common network communication-based logic: sending and receiving of data via TCP.
/// </summary>

public class TcpProtocol : ConnectedProtocol
{
	Socket mSocket;
	bool mImproveLatency = false;

	/// <summary>
	/// Socket that is used for communication.
	/// </summary>

	public override Socket socket
	{
		get
		{
			return mSocket;
		}
		set
		{
			if (mSocket != value)
			{
				Disconnect();
				mSocket = value;
				SetSocketOptions();
			}
		}
	}

	/// <summary>
	/// Enable or disable the Nagle's buffering algorithm (aka NO_DELAY flag).
	/// Enabling this flag will improve latency at the cost of increased bandwidth.
	/// http://en.wikipedia.org/wiki/Nagle's_algorithm
	/// </summary>

	public bool improveLatency
	{
		get
		{
			return mImproveLatency;
		}
		set
		{
			if (mImproveLatency != value)
			{
				mImproveLatency = value;
				SetSocketOptions();
			}
		}
	}

	/// <summary>
	/// Try to establish a connection with the specified address.
	/// </summary>

	public override void Connect (string addr, int port)
	{
		Disconnect();

		IPAddress destination = null;

		if (!IPAddress.TryParse(addr, out destination))
		{
			IPAddress[] ips = Dns.GetHostAddresses(addr);
			if (ips.Length > 0) destination = ips[0];
		}

		stage = Stage.Connecting;
		address = addr + ":" + port;
		mSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		mSocket.BeginConnect(destination, port, OnConnectResult, mSocket);
	}

	/// <summary>
	/// Connection attempt result.
	/// </summary>

	void OnConnectResult (IAsyncResult result)
	{
		Socket sock = (Socket)result.AsyncState;

		try
		{
			sock.EndConnect(result);
		}
		catch (System.Exception ex)
		{
			Error(ex.Message);
			SendVerification(false);
			return;
		}

		SetSocketOptions();
		SendVerification(true);
		StartReceiving();
	}

	/// <summary>
	/// Disconnect the player, freeing all resources.
	/// </summary>

	public override void Disconnect () { if (mSocket != null) Close(mSocket.Connected); }

	/// <summary>
	/// Set socket options to the currently saved values.
	/// </summary>

	void SetSocketOptions ()
	{
		if (mSocket != null)
		{
			mSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, mImproveLatency);
		}
	}

	/// <summary>
	/// Close the connection.
	/// </summary>

	public override void Close (bool notify)
	{
		base.Close(notify);

		if (mSocket != null)
		{
			try
			{
				if (mSocket.Connected) mSocket.Shutdown(SocketShutdown.Both);
				mSocket.Close();
			}
			catch (System.Exception ex)
			{
				Error(ex.Message);
			}
			mSocket = null;

			if (notify)
			{
				Buffer buff = Buffer.Create();
				buff.BeginWriting(false).Write((byte)Packet.Disconnect);
				lock (mIn) mIn.Enqueue(buff);
			}
		}
	}

	/// <summary>
	/// Release the buffers.
	/// </summary>

	public override void Release ()
	{
		if (mSocket != null)
		{
			try
			{
				if (mSocket.Connected) mSocket.Shutdown(SocketShutdown.Both);
				mSocket.Close();
			}
			catch (System.Exception ex)
			{
				Error(ex.Message);
			}
			mSocket = null;
		}

		base.Release();
	}

	/// <summary>
	/// Send the specified packet. Marks the buffer as used.
	/// </summary>

	public override void SendPacket (Buffer buffer, bool immediate)
	{
		buffer.MarkAsUsed();

		if (mSocket != null && mSocket.Connected)
		{
			buffer.BeginReading();

			if (immediate)
			{
				mSocket.Send(buffer.buffer, buffer.position, buffer.size, SocketFlags.None);
				buffer.Recycle();
			}
			else
			{
				lock (mOut)
				{
					mOut.Enqueue(buffer);

					if (mOut.Count == 1)
					{
						// If it's the first packet, let's begin the send process
						mSocket.BeginSend(buffer.buffer, buffer.position,
							buffer.size, SocketFlags.None, OnSend, buffer);
					}
				}
			}
		}
		else buffer.Recycle();
	}

	/// <summary>
	/// Send completion callback. Recycles the buffer.
	/// </summary>

	void OnSend (IAsyncResult result)
	{
		int bytes;
		
		try
		{
			bytes = (mSocket.SocketType == SocketType.Dgram) ? mSocket.EndSendTo(result) : mSocket.EndSend(result);
		}
		catch (System.NullReferenceException)
		{
			Close(true);
			return;
		}
		catch (System.Exception)
		{
			bytes = 0;
			Close(true);
			return;
		}

		lock (mOut)
		{
			// Recycle this buffer as it's no longer in use
			mOut.Dequeue().Recycle();

			if (bytes > 0)
			{
				// If there is another packet to send out, let's send it
				Buffer next = (mOut.Count == 0) ? null : mOut.Peek();
				if (next != null) mSocket.BeginSend(next.buffer, next.position, next.size,
					SocketFlags.None, OnSend, next);
			}
			else Close(true);
		}
	}

	/// <summary>
	/// Start receiving incoming messages.
	/// </summary>

	public override void StartReceiving ()
	{
		if (mSocket != null && mSocket.Connected)
		{
			// Save the timestamp
			timestamp = DateTime.Now.Ticks / 10000;

			// Save the address
			address = ((IPEndPoint)mSocket.RemoteEndPoint).ToString();

			// Queue up the read operation
			mSocket.BeginReceive(mTemp, 0, mTemp.Length, SocketFlags.None, OnReceive, null);
		}
	}

	/// <summary>
	/// Receive incoming data.
	/// </summary>

	void OnReceive (IAsyncResult result)
	{
		int bytes = 0;

		try
		{
			bytes = mSocket.EndReceive(result);
		}
		catch (System.Exception)
		{
			Close(true);
			return;
		}
		timestamp = DateTime.Now.Ticks / 10000;

		if (bytes > 0)
		{
			// Process the data
			OnReceive(bytes);

			// Queue up the next read operation
			mSocket.BeginReceive(mTemp, 0, mTemp.Length, SocketFlags.None, OnReceive, null);
		}
		else Close(true);
	}
}
}