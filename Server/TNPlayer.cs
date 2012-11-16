using System;
using System.Net.Sockets;
using System.IO;

namespace TNet
{
/// <summary>
/// Class containing information about connected players.
/// </summary>

public class Player
{
	public int id = 0;
	public string name;
	public string address;
	//public int ping;
	public Socket socket;
	public Channel channel;
	public bool verified = false;
	public long timestamp;

	NetworkStream mStream;
	BinaryWriter mWriter;

	int mSize = 0;
	Buffer mBuffer = new Buffer();

	/// <summary>
	/// Access to the internal buffer.
	/// </summary>

	public Buffer buffer { get { return mBuffer; } }

	/// <summary>
	/// Whether the player has some data to receive.
	/// </summary>

	public bool hasData { get { return socket.Available >= 4; } }

	/// <summary>
	/// Receive the player's version number.
	/// </summary>

	public int ReceiveVersion ()
	{
		// We must have at least 4 bytes to work with
		if (socket.Available < 4) return 0;

		// Determine the size of the packet
		BinaryReader reader = mBuffer.Receive(socket, 4);
		return reader.ReadInt32();
	}

	/// <summary>
	/// Receive a packet from the associated socket.
	/// </summary>

	public BinaryReader ReceivePacket ()
	{
		// We must have at least 4 bytes to work with
		if (socket.Available < 4) return null;

		// Determine the size of the packet
		if (mSize == 0) mSize = mBuffer.Receive(socket, 4).ReadInt32();

		// If we don't have the entire packet waiting, don't do anything.
		if (socket.Available < mSize)
		{
			Console.WriteLine("Expecting " + mSize + " bytes, have " + socket.Available);
			return null;
		}

		// Receive the entire packet
		return mBuffer.Receive(socket, mSize);
	}

	/// <summary>
	/// Forward the remaining data of the last received packet to the specified player.
	/// Must follow ReceivePacket() in order to work.
	/// </summary>

	public void ForwardLastPacket (Player recipient)
	{
		if (recipient != null) recipient.Send(mBuffer.buffer, mBuffer.position, mBuffer.size);
	}

	/// <summary>
	/// Returns the stand-alone buffer of the packet from the current position onwards.
	/// Should only be used after ReceivePacket().
	/// </summary>

	public byte[] CopyBuffer ()
	{
		if (mBuffer.size > 0)
		{
			byte[] data = new byte[mBuffer.size];
			int offset = (int)mBuffer.position;
			mBuffer.stream.Read(data, offset, mBuffer.size);
			mBuffer.stream.Seek(offset, SeekOrigin.Begin);
			return data;
		}
		return null;
	}

	/// <summary>
	/// Send a packet to this player.
	/// The packet will always be prefixed with 4 bytes indicating the size of the packet.
	/// </summary>

	public void Send (byte[] buffer) { Send(buffer, 0, buffer.Length); }

	/// <summary>
	/// Send a packet to this player.
	/// The packet will always be prefixed with 4 bytes indicating the size of the packet.
	/// </summary>

	public void Send (byte[] buffer, int offset, int size)
	{
		if (socket.Connected)
		{
			if (mWriter == null)
			{
				mStream = new NetworkStream(socket);
				mWriter = new BinaryWriter(mStream);
			}
			mWriter.Write(size);
			mStream.Write(buffer, offset, size);
		}
	}
}
}