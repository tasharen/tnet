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

public class TcpProtocol : Player
{
	/// <summary>
	/// IP address of the target we're connected to.
	/// </summary>

	public string address;

	/// <summary>
	/// Timestamp of when we received the last message.
	/// </summary>

	public long timestamp = 0;

	public delegate void OnConnect (bool success);
	OnConnect mOnConnect;
	Socket mSocket;

	// Incoming and outgoing queues
	Queue<Buffer> mIn = new Queue<Buffer>();
	Queue<Buffer> mOut = new Queue<Buffer>();

	// Buffer used for receiving incoming data
	byte[] mTemp = new byte[8192];

	// Current incoming buffer
	Buffer mReceiveBuffer;
	int mExpected = 0;
	int mOffset = 0;
	bool mImproveLatency = false;
	
	// Static as it's temporary
	static Buffer mBuffer;

	/// <summary>
	/// Socket that is used for communication.
	/// </summary>
	/// 
	public Socket socket
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
	/// Whether the connection is currently active.
	/// </summary>

	public bool isConnected { get { return mSocket != null && mSocket.Connected; } }

	/// <summary>
	/// Disconnect the player, freeing all resources.
	/// </summary>

	public void Disconnect () { if (mSocket != null) Close(mSocket.Connected); }

	/// <summary>
	/// Try to establish a connection with the specified address.
	/// </summary>

	public void Connect (string addr, int port, OnConnect callback)
	{
		Disconnect();
		mOnConnect = callback;

		IPAddress destination = null;

		if (!IPAddress.TryParse(addr, out destination))
		{
			IPAddress[] ips = Dns.GetHostAddresses(addr);
			if (ips.Length > 0) destination = ips[0];
		}

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
			mOnConnect(false);
			Error(ex.Message);
			Close(false);
			return;
		}

		SetSocketOptions();
		mOnConnect(true);
		StartReceiving();
	}

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
	/// Begin sending a new packet to the server.
	/// </summary>

	public BinaryWriter BeginSend (Packet type)
	{
		mBuffer = Buffer.Create(false);
		return mBuffer.BeginPacket(type);
	}

	/// <summary>
	/// Begin sending a new packet to the server.
	/// </summary>

	public BinaryWriter BeginSend (byte packetID)
	{
		mBuffer = Buffer.Create(false);
		return mBuffer.BeginPacket(packetID);
	}

	/// <summary>
	/// Send the outgoing buffer.
	/// </summary>

	public void EndSend ()
	{
		mBuffer.EndPacket();
		SendPacket(mBuffer);
		mBuffer = null;
	}

	/// <summary>
	/// Close the connection.
	/// </summary>

	public void Close (bool notify)
	{
		if (mReceiveBuffer != null)
		{
			mReceiveBuffer.Recycle();
			mReceiveBuffer = null;
		}

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

	public void Release ()
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

		Buffer.Recycle(mIn);
		Buffer.Recycle(mOut);
	}

	/// <summary>
	/// Add an error packet to the incoming queue.
	/// </summary>

	public void Error (string error)
	{
		Buffer buff = Buffer.Create();
		BinaryWriter writer = buff.BeginWriting(false);
		writer.Write((byte)Packet.Error);
		writer.Write(error);
		lock (mIn) mIn.Enqueue(buff);
	}

	/// <summary>
	/// Send the specified packet. Marks the buffer as used.
	/// </summary>

	public void SendPacket (Buffer buffer)
	{
		buffer.MarkAsUsed();

		if (mSocket != null && mSocket.Connected)
		{
			buffer.BeginReading();

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

	public void StartReceiving ()
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
	/// Extract the first incoming packet.
	/// </summary>

	public Buffer ReceivePacket ()
	{
		if (mIn.Count == 0) return null;
		lock (mIn) return mIn.Dequeue();
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
			if (mReceiveBuffer == null)
			{
				// Create a new packet buffer
				mReceiveBuffer = Buffer.Create();
				mReceiveBuffer.BeginWriting(false).Write(mTemp, 0, bytes);
			}
			else
			{
				// Append this data to the end of the last used buffer
				mReceiveBuffer.BeginWriting(true).Write(mTemp, 0, bytes);
			}

			for (int available = mReceiveBuffer.size - mOffset; available >= 4; )
			{
				// Figure out the expected size of the packet
				if (mExpected == 0)
				{
					mExpected = mReceiveBuffer.PeekInt(mOffset);
					if (mExpected == -1) break;
					
					// 0 indicates a closed connection
					if (mExpected == 0)
					{
						Close(true);
						return;
					}
				}

				// The first 4 bytes of any packet always contain the number of bytes in that packet
				available -= 4;

				// If the entire packet is present
				if (available == mExpected)
				{
					// Reset the position to the beginning of the packet
					mReceiveBuffer.BeginReading(mOffset += 4);

					// This packet is now ready to be processed
					lock (mIn) mIn.Enqueue(mReceiveBuffer);
					
					mReceiveBuffer = null;
					mExpected = 0;
					mOffset = 0;
					break;
				}
				else if (available > mExpected)
				{
					// There is more than one packet. Extract this packet fully.
					int realSize = mExpected + 4;
					Buffer temp = Buffer.Create();
					temp.BeginWriting(false).Write(mReceiveBuffer.buffer, mOffset, realSize);

					// Reset the position to the beginning of the packet
					temp.BeginReading(4);

					// This packet is now ready to be processed
					lock (mIn) mIn.Enqueue(temp);

					// Skip this packet
					available -= mExpected;
					mOffset += realSize;
					mExpected = 0;
				}
				else break;
			}
			// Queue up the next read operation
			mSocket.BeginReceive(mTemp, 0, mTemp.Length, SocketFlags.None, OnReceive, null);
		}
		else Close(true);
	}
}
}