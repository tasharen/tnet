using System;
using System.IO;
using System.Net.Sockets;

namespace TNet
{
/// <summary>
/// This class merges BinaryWriter and BinaryReader into one.
/// </summary>

public class Buffer
{
	MemoryStream mStream;
	BinaryWriter mWriter;
	BinaryReader mReader;
	int mSize = 0;
	bool mWriting = false;

	public Buffer ()
	{
		mStream = new MemoryStream();
		mWriter = new BinaryWriter(mStream);
		mReader = new BinaryReader(mStream);
	}

	~Buffer () { mStream.Dispose(); }

	/// <summary>
	/// The size of the data present in the buffer.
	/// </summary>

	public int size { get { return mWriting ? (int)mStream.Position : mSize - (int)mStream.Position; } }

	/// <summary>
	/// Position within the stream.
	/// </summary>

	public int position { get { return (int)mStream.Position; } set { mStream.Seek(value, SeekOrigin.Begin); } }

	/// <summary>
	/// Underlying memory stream.
	/// </summary>

	public MemoryStream stream { get { return mStream; } }

	/// <summary>
	/// Get the entire buffer (note that it may be bigger than 'size').
	/// </summary>

	public byte[] buffer { get { return mStream.GetBuffer(); } }

	/// <summary>
	/// Clear the buffer.
	/// </summary>

	public void Clear () { mStream.Seek(0, SeekOrigin.Begin); }

	/// <summary>
	/// Dispose of the allocated memory.
	/// </summary>

	public void Dispose () { mStream.Dispose(); }

	/// <summary>
	/// Begin the writing process.
	/// </summary>

	public BinaryWriter BeginWriting ()
	{
		mWriting = true;
		mStream.Seek(0, SeekOrigin.Begin);
		return mWriter;
	}

	/// <summary>
	/// Begin the writing process.
	/// </summary>

	public BinaryWriter BeginWriting (int capacity)
	{
		mWriting = true;
		if (mStream.Capacity < capacity) mStream.SetLength(capacity);
		mStream.Seek(0, SeekOrigin.Begin);
		return mWriter;
	}

	/// <summary>
	/// Receive the specified number of bytes and immediately switch to reading.
	/// </summary>

	public BinaryReader Receive (Socket socket, int bytes)
	{
		mWriting = true;
		mStream.SetLength(bytes);
		mStream.Seek(0, SeekOrigin.Begin);

		for (mSize = 0; mSize < bytes; )
		{
			mSize += socket.Receive(buffer, mSize, bytes - mSize, SocketFlags.None);
		}
		
		Console.WriteLine("Received " + mSize + " bytes");
		mStream.Seek(0, SeekOrigin.Begin);
		mWriting = false;
		return mReader;
	}

	/// <summary>
	/// Begin the reading process.
	/// </summary>

	public BinaryReader BeginReading ()
	{
		mWriting = false;
		mSize = (int)mStream.Position;
		mStream.Seek(0, SeekOrigin.Begin);
		return mReader;
	}

	/// <summary>
	/// Begin writing a packet: the first 4 bytes indicate the size of the data that will follow.
	/// </summary>

	public BinaryWriter BeginPacket ()
	{
		BinaryWriter writer = BeginWriting();
		writer.Write(0);
		return writer;
	}

	/// <summary>
	/// Finish writing of the packet, updating (and returning) its size.
	/// </summary>

	public int EndPacket ()
	{
		int size = position;
		mStream.Seek(0, SeekOrigin.Begin);
		mWriter.Write(size - 4);
		mWriting = false;
		return size;
	}
}
}