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
/// Common network communication-based logic: sending and receiving of data.
/// </summary>

public class Connection
{
	/// <summary>
	/// Socket that is used for communication.
	/// </summary>

	public Socket socket;

	/// <summary>
	/// IP address of the target we're connected to.
	/// </summary>

	public string address;

	/// <summary>
	/// Timestamp of when we received the last message.
	/// </summary>

	public long timestamp = 0;

	// Incoming and outgoing queues
	Queue<Buffer> mIn = new Queue<Buffer>();
	Queue<Buffer> mOut = new Queue<Buffer>();

	// Buffer used for receiving incoming data
	byte[] mTemp = new byte[8192];

	// Current incoming buffer
	Buffer mReceiveBuffer;
	int mExpected = 0;
	int mOffset = 0;

	/// <summary>
	/// Whether the connection is currently active.
	/// </summary>

	public bool isConnected { get { return socket != null && socket.Connected; } }

	/// <summary>
	/// Disconnect the player, freeing all resources.
	/// </summary>

	public void Disconnect () { if (socket != null) Close(true); }

	/// <summary>
	/// Close the connection.
	/// </summary>

	protected void Close (bool notify)
	{
		if (mReceiveBuffer != null)
		{
			mReceiveBuffer.Recycle();
			mReceiveBuffer = null;
		}

		if (socket != null)
		{
			try
			{
				if (socket.Connected) socket.Shutdown(SocketShutdown.Both);
				socket.Close();
			}
			catch (System.Exception ex)
			{
				Error(ex.Message);
			}
			socket = null;

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
		if (socket != null)
		{
			try
			{
				if (socket.Connected) socket.Shutdown(SocketShutdown.Both);
				socket.Close();
			}
			catch (System.Exception ex)
			{
				Error(ex.Message);
			}
			socket = null;
		}

		Buffer.Recycle(mIn);
		Buffer.Recycle(mOut);
	}

	/// <summary>
	/// Add an error packet to the incoming queue.
	/// </summary>

	protected void Error (string error)
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

		if (socket != null && socket.Connected)
		{
			buffer.BeginReading();

			lock (mOut)
			{
				mOut.Enqueue(buffer);

				if (mOut.Count == 1)
				{
					// If it's the first packet, let's begin the send process
					socket.BeginSend(buffer.buffer, buffer.position,
						buffer.size, SocketFlags.None, OnSend, buffer);
				}
			}
		}
		else buffer.Recycle();
	}

	/// <summary>
	/// Send completion callback. Recycles the buffer.
	/// </summary>

	protected void OnSend (IAsyncResult result)
	{
		int bytes;
		
		try
		{
			bytes = socket.EndSend(result);
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

		//Console.WriteLine("...sent " + bytes + " bytes");

		lock (mOut)
		{
			// Recycle this buffer as it's no longer in use
			mOut.Dequeue().Recycle();

			if (bytes > 0)
			{
				// If there is another packet to send out, let's send it
				Buffer next = (mOut.Count == 0) ? null : mOut.Peek();
				if (next != null) socket.BeginSend(next.buffer, next.position, next.size,
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
		if (socket != null && socket.Connected)
		{
			// Save the timestamp
			timestamp = DateTime.Now.Ticks / 10000;

			// Save the address
			address = ((IPEndPoint)socket.RemoteEndPoint).ToString();

			// Queue up the read operation
			socket.BeginReceive(mTemp, 0, mTemp.Length, SocketFlags.None, OnReceive, null);
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
			bytes = socket.EndReceive(result);
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
					mReceiveBuffer.BeginReading(mOffset + 4);

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
					int realPacketSize = mExpected + 4;
					Buffer temp = Buffer.Create();
					temp.BeginWriting(false).Write(mReceiveBuffer.buffer, mOffset, realPacketSize);

					// Reset the position to the beginning of the packet
					temp.BeginReading(4);

					// This packet is now ready to be processed
					lock (mIn) mIn.Enqueue(temp);

					// Skip this packet
					available -= mExpected;
					mOffset += realPacketSize;
					mExpected = 0;
				}
				else break;
			}
			// Queue up the next read operation
			socket.BeginReceive(mTemp, 0, mTemp.Length, SocketFlags.None, OnReceive, null);
		}
		else Close(true);
	}
}
}