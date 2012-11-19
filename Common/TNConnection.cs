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
	protected Queue<Buffer> mIn = new Queue<Buffer>();
	protected Queue<Buffer> mOut = new Queue<Buffer>();

	// Buffer used for receiving incoming data
	byte[] mTemp = new byte[8192];

	// Current incoming buffer
	Buffer mReceiveBuffer;
	int mExpected = 0;
	int mOffset = 0;

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
			socket.Close();
			socket = null;

			lock (mPool)
			{
				while (mIn.Count != 0) mPool.Add(mIn.Dequeue());
				while (mOut.Count != 0) mPool.Add(mOut.Dequeue());
			}
		}
	}

	/// <summary>
	/// Send the specified packet.
	/// </summary>

	public void SendPacket (Buffer buffer)
	{
		if (socket.Connected)
		{
			buffer.BeginReading();

			lock (mOut)
			{
				buffer.MarkAsUsed();
				mOut.Enqueue(buffer);

				if (mOut.Count == 1)
				{
					// If it's the first packet, let's send it
					socket.BeginSend(buffer.buffer, buffer.position,
						buffer.size, SocketFlags.None, OnSend, buffer);
				}
			}
		}
	}

	/// <summary>
	/// Send data one packet at a time.
	/// </summary>

	void OnSend (IAsyncResult result)
	{
		int bytes = socket.EndSend(result);

		if (bytes > 0)
		{
			Console.WriteLine("...sent " + bytes + " bytes");
			Buffer finished = (Buffer)result.AsyncState;

			lock (mOut)
			{
				//mOut.Dequeue();
				Buffer deq = mOut.Dequeue();

				// This shouldn't be hit, but just in case...
				if (deq != finished)
					Console.WriteLine("WARNING: Unexpected...?");

				// Recycle this buffer if it's no longer in use
				if (finished.MarkAsUnused()) Connection.ReleaseBuffer(finished);

				// If there is another packet to send out, let's send it
				Buffer next = (mOut.Count == 0) ? null : mOut.Peek();
				if (next != null) socket.BeginSend(next.buffer, next.position, next.size,
					SocketFlags.None, OnSend, next);
			}
		}
		else Disconnect();
	}

	/// <summary>
	/// Start receiving incoming messages.
	/// </summary>

	public virtual void StartReceiving ()
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
		if (socket == null) return;
		int bytes = 0;

		try
		{
			bytes = socket.EndReceive(result);
		}
		catch (System.Exception ex)
		{
			Console.WriteLine(ex.Message);
			Disconnect();
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
					if (mExpected == 0) break;
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
		else Disconnect();
	}
}
}