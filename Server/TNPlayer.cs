using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Collections.Generic;
using System.Threading;

namespace TNet
{
/// <summary>
/// Class containing information about connected players.
/// </summary>

public class Player
{
	static int mPlayerCounter = 0;

	/// <summary>
	/// Server protocol version. Must match the client.
	/// </summary>

	public const int version = 1;

	public int id = 0;
	public string name;
	public string address;
	public Socket socket;
	public Channel channel;
	public bool verified = false;
	public long timestamp = 0;
	byte[] mTemp = new byte[8192];

	// Pool of messages for simple reuse
	BetterList<Buffer> mPool;

	// Current incoming buffer
	Buffer mCurrent;
	int mExpected = 0;
	int mOffset = 0;

	// Incoming and outgoing queues
	Queue<Buffer> mIn = new Queue<Buffer>();
	Queue<Buffer> mOut = new Queue<Buffer>();

	/// <summary>
	/// Players can only be created with a TCP client.
	/// </summary>

	public Player (Socket sock, BetterList<Buffer> pool)
	{
		socket = sock;
		mPool = pool;

		id = Interlocked.Increment(ref mPlayerCounter);
		address = ((IPEndPoint)socket.RemoteEndPoint).ToString();
		timestamp = DateTime.Now.Ticks / 10000;

		// Queue up the read operation
		socket.BeginReceive(mTemp, 0, mTemp.Length, SocketFlags.None, OnReceive, null);
	}

	/// <summary>
	/// Create a new buffer, reusing an old one if possible.
	/// </summary>

	Buffer CreateBuffer ()
	{
		if (mPool.size == 0) return new Buffer();

		lock (mPool)
		{
			if (mPool.size != 0) return mPool.Pop();
			else return new Buffer();
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
				finished.MarkAsUnused(mPool);

				// If there is another packet to send out, let's send it
				Buffer next = (mOut.Count == 0) ? null : mOut.Peek();
				if (next != null) socket.BeginSend(next.buffer, next.position, next.size,
					SocketFlags.None, OnSend, next);
			}
		}
		else Disconnect();
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
			if (mCurrent == null)
			{
				// Create a new packet buffer
				mCurrent = CreateBuffer();
				mCurrent.BeginWriting(false).Write(mTemp, 0, bytes);
			}
			else
			{
				// Append this data to the end of the last used buffer
				mCurrent.BeginWriting(true).Write(mTemp, 0, bytes);
			}

			for (int available = mCurrent.size - mOffset; available >= 4; )
			{
				if (!verified)
				{
					// Version hasn't been verified yet? The first 4 bytes must be the version number.
					if (mCurrent.PeekSize(mOffset) != version)
					{
						Console.WriteLine(address + " failed verification");
						Disconnect();
						return;
					}
					verified = true;
					mOffset += 4;
					available -= 4;

					Console.WriteLine(address + " has been verified");

					// Send a response
					Buffer temp = CreateBuffer();
					BinaryWriter writer = temp.BeginPacket();
					writer.Write((byte)Packet.ResponseVersion);
					writer.Write(version);
					writer.Write(id);
					temp.EndPacket();
					SendPacket(temp);
				}

				// Figure out the expected size of the packet
				if (mExpected == 0)
				{
					mExpected = mCurrent.PeekSize(mOffset);
					if (mExpected == 0) break;
				}

				// The first 4 bytes of any packet always contain the number of bytes in that packet
				available -= 4;

				// If the entire packet is present
				if (available == mExpected)
				{
					// Reset the position to the beginning of the packet
					mCurrent.BeginReading();
					mCurrent.position = mOffset + 4;

					// This packet is now ready to be processed
					lock (mIn) mIn.Enqueue(mCurrent);
					mCurrent = null;
					mExpected = 0;
					mOffset = 0;
					break;
				}
				else if (available > mExpected)
				{
					// Skip the size
					mOffset += 4;

					// There is more than one packet. Extract this packet.
					Buffer temp = CreateBuffer();
					temp.BeginWriting(false).Write(mCurrent.buffer, mOffset, mExpected);
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
}
}