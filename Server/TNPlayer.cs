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
	static byte[] mTemp = new byte[8192];

	public int id = 0;
	public string name;
	public string address;
	public TcpClient tcp;
	public Channel channel;
	public bool verified = false;
	public long timestamp = 0;

	public BinaryReader reader;
	public BinaryWriter writer;

	NetworkStream mStream;
	int mSize = 0;
	int mLast = 0;
	int mPacketSize = 0;

	/// <summary>
	/// Whether the player has some data to receive.
	/// </summary>

	public bool hasData { get { return tcp.Available >= 0; } }

	/// <summary>
	/// Whether the next packet is ready for processing.
	/// </summary>

	public bool hasPacket { get { return tcp.Available >= mSize; } }

	/// <summary>
	/// Size of the packet.
	/// </summary>

	public int packetSize { get { return mSize; } }

	/// <summary>
	/// Players can only be created with a TCP client.
	/// </summary>

	public Player (TcpClient client)
	{
		tcp = client;
		mStream = tcp.GetStream();
		reader = new BinaryReader(mStream);
		writer = new BinaryWriter(mStream);
	}

	/// <summary>
	/// Disconnect the player, freeing all resources.
	/// </summary>

	public void Disconnect ()
	{
		if (mStream != null)
		{
			writer.Flush();
			
			Socket sock = tcp.Client;
			tcp.Close();

			// TODO: Graceful shutdown
			//mWriter.Close();
			//mReader.Close();

			//sock.Shutdown(SocketShutdown.Both);
			//sock.Disconnect(false);
			
			mStream = null;
			reader = null;
			writer = null;
		}
	}

	/// <summary>
	/// Receive the player's version number.
	/// </summary>

	public int ReceiveVersion ()
	{
		// We must have at least 4 bytes to work with
		if (tcp.Available < 4) return 0;
		return ReadInt();
	}

	/// <summary>
	/// Helper function: Read a 32-bit integer value from the network stream.
	/// </summary>

	int ReadInt ()
	{
		NetworkStream stream = tcp.GetStream();

		int a = stream.ReadByte();
		int b = stream.ReadByte();
		int c = stream.ReadByte();
		int d = stream.ReadByte();

		int intVal = a | (b << 8) | (c << 16) | (d << 24);
		return intVal;
	}

	/// <summary>
	/// Receive a packet from the network.
	/// </summary>

	public bool ReceivePacket (long time)
	{
		// TODO:
		// Eliminate this function, replacing it with an async BeginRead() operation
		// that will save incoming data into a buffer, and flip a flag when a packet is ready.
		// Dual buffers, maybe? Write into one while another is being processed?

		int available = tcp.Available;

		// We must have at least 4 bytes to work with
		if (mSize == 0)
		{
			if (available < 4) return false;
			
			// Determine the size of the packet
			mSize = ReadInt();

			// Skip the first 4 bytes used for size
			available -= 4;
		}

		// Nothing left to receive? Do nothing.
		if (available == 0) return false;

		// If we don't have an entire packet, don't do anything
		if (available < mSize)
		{
			if (mLast != available)
			{
				Console.WriteLine("Received " + available + "/" + mSize + " bytes");
				timestamp = time;
				mLast = available;
			}
			return false;
		}
		Console.WriteLine("Received " + mSize + " bytes");
		timestamp = time;
		return true;
	}

	/// <summary>
	/// We're done processing the packet.
	/// </summary>

	public void ReleasePacket ()
	{
		mSize = 0;
		mLast = 0;
	}

	/// <summary>
	/// Returns the stand-alone buffer of the packet from the current position onwards.
	/// Should only be used after ReceivePacket().
	/// </summary>

	public byte[] ExtractPacket (bool copy)
	{
		if (copy)
		{
			byte[] buff = new byte[mSize];
			reader.Read(buff, 0, mSize);
			return buff;
		}
		else
		{
			if (mSize > mTemp.Length) mTemp = new byte[mSize];
			reader.Read(mTemp, 0, mSize);
			return mTemp;
		}
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
		if (tcp.Connected)
		{
			NetworkStream stream = tcp.GetStream();
			writer.Write(size);
			stream.Write(buffer, offset, size);
		}
	}
}
}