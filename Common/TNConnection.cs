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
	static BetterList<Buffer> mPool = new BetterList<Buffer>();

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
	/// Create a new buffer, reusing an old one if possible.
	/// </summary>

	static public Buffer CreateBuffer ()
	{
		Buffer b = null;

		if (mPool.size == 0)
		{
			b = new Buffer();
		}
		else
		{
			lock (mPool)
			{
				if (mPool.size != 0) b = mPool.Pop();
				else b = new Buffer();
			}
		}
		return b;
	}

	/// <summary>
	/// Release the specified buffer into the reusable pool.
	/// </summary>

	static public void ReleaseBuffer (Buffer b)
	{
		lock (mPool)
		{
			b.Clear();
			mPool.Add(b);
		}
	}

	/// <summary>
	/// Disconnect the player, freeing all resources.
	/// </summary>

	public void Disconnect ()
	{
		if (socket != null)
		{
			// Send a zero, indicating that the connection should now be severed
			if (socket.Connected) socket.Send(new byte[] { 0, 0, 0, 0 });
			Close(true);
		}
	}

	/// <summary>
	/// Close the connection.
	/// </summary>

	protected void Close (bool notify)
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
				Console.WriteLine(ex.Message);
			}
			socket = null;

			if (notify)
			{
				Buffer buff = CreateBuffer();
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
				Console.WriteLine(ex.Message);
			}
			socket = null;
		}

		lock (mPool)
		{
			while (mIn.Count != 0) mPool.Add(mIn.Dequeue());
			while (mOut.Count != 0) mPool.Add(mOut.Dequeue());
		}
	}

	/// <summary>
	/// Add an error packet to the incoming queue.
	/// </summary>

	protected void Error (string error)
	{
		Buffer buff = CreateBuffer();
		BinaryWriter writer = buff.BeginWriting(false);
		writer.Write((byte)Packet.Error);
		writer.Write(error);
		lock (mIn) mIn.Enqueue(buff);
	}

	/// <summary>
	/// Send the specified packet.
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
						buffer.size, SocketFlags.None, OnSend, null);
				}
			}
		}
		else if (buffer.MarkAsUnused())
		{
			Connection.ReleaseBuffer(buffer);
		}
	}

	/// <summary>
	/// Send data one packet at a time.
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

		if (bytes > 0)
		{
			Console.WriteLine("...sent " + bytes + " bytes");

			lock (mOut)
			{
				Buffer finished = mOut.Dequeue();

				// Recycle this buffer if it's no longer in use
				if (finished.MarkAsUnused()) Connection.ReleaseBuffer(finished);

				// If there is another packet to send out, let's send it
				Buffer next = (mOut.Count == 0) ? null : mOut.Peek();
				if (next != null) socket.BeginSend(next.buffer, next.position, next.size,
					SocketFlags.None, OnSend, null);
			}
		}
		else Close(true);
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
				mReceiveBuffer = Connection.CreateBuffer();
				mReceiveBuffer.MarkAsUsed();
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
					mReceiveBuffer.BeginReading();
					mReceiveBuffer.position = mOffset + 4;

					// This packet is now ready to be processed
					lock (mIn) mIn.Enqueue(mReceiveBuffer);
					mReceiveBuffer = null;
					mExpected = 0;
					mOffset = 0;
					break;
				}
				else if (available > mExpected)
				{
					// Skip the size
					mOffset += 4;

					// There is more than one packet. Extract this packet.
					Buffer temp = Connection.CreateBuffer();
					temp.MarkAsUsed();
					temp.BeginWriting(false).Write(mReceiveBuffer.buffer, mOffset, mExpected);
					Console.WriteLine("Added packet of size " + mExpected);
					lock (mIn) mIn.Enqueue(temp);

					// Skip this packet
					available -= mExpected;
					mOffset += mExpected;
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