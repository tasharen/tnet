using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

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

	int mCounter = 0;
	int mSize = 0;
	bool mWriting = false;

	public Buffer ()
	{
		mStream = new MemoryStream();
		mWriter = new BinaryWriter(mStream);
		mReader = new BinaryReader(mStream);
	}

	~Buffer ()
	{
		mStream.Dispose();
	}

	/// <summary>
	/// The size of the data present in the buffer.
	/// </summary>

	public int size
	{
		get
		{
			return mWriting ? (int)mStream.Position : mSize - (int)mStream.Position;
		}
	}

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
	/// Mark the buffer as being in use.
	/// </summary>

	public void MarkAsUsed () { Interlocked.Increment(ref mCounter); }

	/// <summary>
	/// Mark the buffer as no longer being in use. Return 'true' if no one is using the buffer.
	/// </summary>

	public bool MarkAsUnused ()
	{
		if (Interlocked.Decrement(ref mCounter) == 0)
		{
			mSize = 0;
			mStream.Seek(0, SeekOrigin.Begin);
			mWriting = true;
			return true;
		}
		return false;
	}

	/// <summary>
	/// Clear the buffer.
	/// </summary>

	public void Clear ()
	{
		mCounter = 0;
		mSize = 0;
		mStream.Seek(0, SeekOrigin.Begin);
		mWriting = true;
	}

	/// <summary>
	/// Copy the contents of this buffer into the target one, trimming away unused space.
	/// </summary>

	public void CopyTo (Buffer target)
	{
		BinaryWriter w = target.BeginWriting(false);
		int bytes = size;
		if (bytes > 0) w.Write(buffer, position, bytes);
	}

	/// <summary>
	/// Dispose of the allocated memory.
	/// </summary>

	public void Dispose () { mStream.Dispose(); }

	/// <summary>
	/// Begin the writing process.
	/// </summary>

	public BinaryWriter BeginWriting (bool append)
	{
		if (!append || !mWriting)
		{
			mStream.Seek(0, SeekOrigin.Begin);
			mSize = 0;
		}
		mWriting = true;
		return mWriter;
	}

	/// <summary>
	/// Receive the specified number of bytes and immediately switch to reading.
	/// TODO: Eliminate this function, or at least made it use a temporary incoming buffer.
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
		
		mStream.Seek(0, SeekOrigin.Begin);
		mWriting = false;
		return mReader;
	}

	/// <summary>
	/// Begin the reading process.
	/// </summary>

	public BinaryReader BeginReading ()
	{
		if (mWriting)
		{
			mWriting = false;
			mSize = (int)mStream.Position;
			mStream.Seek(0, SeekOrigin.Begin);
		}
		return mReader;
	}

	/// <summary>
	/// Read the packet's size (first 4 bytes).
	/// </summary>

	public int PeekInt (int offset)
	{
		long pos = mStream.Position;
		if (offset + 4 > pos) return 0;
		mStream.Seek(offset, SeekOrigin.Begin);
		int size = mReader.ReadInt32();
		mStream.Seek(pos, SeekOrigin.Begin);
		return size;
	}

	/// <summary>
	/// Begin writing a packet: the first 4 bytes indicate the size of the data that will follow.
	/// </summary>

	public BinaryWriter BeginPacket ()
	{
		BinaryWriter writer = BeginWriting(false);
		writer.Write(0);
		return writer;
	}

	/// <summary>
	/// Begin writing a packet: the first 4 bytes indicate the size of the data that will follow.
	/// </summary>

	public BinaryWriter BeginPacket (Packet packet)
	{
		BinaryWriter writer = BeginWriting(false);
		writer.Write(0);
		writer.Write((byte)packet);
		return writer;
	}

	/// <summary>
	/// Finish writing of the packet, updating (and returning) its size.
	/// </summary>

	public int EndPacket ()
	{
		if (mWriting)
		{
			mSize = position;
			mStream.Seek(0, SeekOrigin.Begin);
			mWriter.Write(mSize - 4);
			mStream.Seek(0, SeekOrigin.Begin);
			mWriting = false;
		}
		return mSize;
	}
}
}