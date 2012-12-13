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
/// Protocol that requires a persistent connection: TCP or rUDP.
/// </summary>

public abstract class ConnectedProtocol : Player
{
	public enum Stage
	{
		NotConnected,
		Connecting,
		Verifying,
		Connected,
	}

	/// <summary>
	/// Current connection stage.
	/// </summary>

	public Stage stage = Stage.NotConnected;

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
	protected byte[] mTemp = new byte[8192];

	// Current incoming buffer
	Buffer mReceiveBuffer;
	int mExpected = 0;
	int mOffset = 0;
	
	// Static as it's temporary
	static Buffer mBuffer;

	/// <summary>
	/// Whether the connection is currently active.
	/// </summary>

	public bool isConnected { get { return stage == Stage.Connected; } }

	/// <summary>
	/// Try to establish a connection with the specified address.
	/// </summary>

	public abstract void Connect (string addr, int port);

	/// <summary>
	/// Start the protocol verification process.
	/// </summary>

	protected void SendVerification (bool success)
	{
		if (success)
		{
			stage = Stage.Verifying;

			// Request a player ID
			BinaryWriter writer = BeginSend(Packet.RequestID);
			writer.Write(Player.version);
			writer.Write(string.IsNullOrEmpty(name) ? "Guest" : name);
			EndSend();
		}
		else Close(false);
	}

	/// <summary>
	/// Verify the server's version -- it must match the client.
	/// </summary>

	public bool VerifyVersion (int clientVersion, int clientID)
	{
		if (clientVersion == version)
		{
			id = clientID;
			stage = Stage.Connected;
			return true;
		}
		else
		{
			id = 0;
			Close(false);
			return false;
		}
	}

	/// <summary>
	/// Disconnect the player, freeing all resources.
	/// </summary>

	public abstract void Disconnect ();

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

	/// Send the outgoing buffer.
	/// </summary>

	public void EndSend ()
	{
		mBuffer.EndPacket();
		SendPacket(mBuffer, false);
		mBuffer = null;
	}

	/// <summary>
	/// Send the outgoing buffer.
	/// </summary>

	public void EndSend (bool immediate)
	{
		mBuffer.EndPacket();
		SendPacket(mBuffer, immediate);
		mBuffer = null;
	}

	/// <summary>
	/// Close the connection.
	/// </summary>

	public virtual void Close (bool notify)
	{
		if (mReceiveBuffer != null)
		{
			mReceiveBuffer.Recycle();
			mReceiveBuffer = null;
		}
		stage = Stage.NotConnected;
	}

	/// <summary>
	/// Release the buffers.
	/// </summary>

	public virtual void Release ()
	{
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

	public void SendPacket (Buffer buffer) { SendPacket(buffer, false); }

	/// <summary>
	/// Send the specified packet. Marks the buffer as used.
	/// </summary>

	protected abstract void SendPacket (Buffer buffer, bool immediate);

	/// <summary>
	/// Start receiving incoming messages on the current socket.
	/// </summary>

	public void StartReceiving () { StartReceiving(null); }

	/// <summary>
	/// Start receiving incoming messages on the specified socket
	/// </summary>

	public abstract void StartReceiving (Socket socket);

	/// <summary>
	/// Extract the first incoming packet.
	/// </summary>

	public Buffer ReceivePacket ()
	{
		if (mIn.Count == 0) return null;
		lock (mIn) return mIn.Dequeue();
	}

	/// <summary>
	/// See if the received packet can be processed and split it up into different ones.
	/// </summary>

	protected bool OnReceive (int bytes)
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
					return false;
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
		return true;
	}
}
}