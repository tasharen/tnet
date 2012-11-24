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

	~Buffer () { mStream.Dispose(); }

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
		if (mStream.Capacity > 1024) mStream.SetLength(256);
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
		target.EndWriting();
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
	/// Begin the writing process, appending from the specified offset.
	/// </summary>

	public BinaryWriter BeginWriting (int startOffset)
	{
		mStream.Seek(startOffset, SeekOrigin.Begin);
		mWriting = true;
		return mWriter;
	}

	/// <summary>
	/// Finish the writing process, returning the packet's size.
	/// </summary>

	public int EndWriting ()
	{
		if (mWriting)
		{
			mSize = position;
			mStream.Seek(0, SeekOrigin.Begin);
			mWriting = false;
		}
		return mSize;
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
	/// Begin the reading process.
	/// </summary>

	public BinaryReader BeginReading (int startOffset)
	{
		if (mWriting)
		{
			mWriting = false;
			mSize = (int)mStream.Position;
		}
		mStream.Seek(startOffset, SeekOrigin.Begin);
		return mReader;
	}

	/// <summary>
	/// Peek at the first byte at the specified offset.
	/// </summary>

	public int PeekByte (int offset)
	{
		long pos = mStream.Position;
		if (offset + 1 > pos) return -1;
		mStream.Seek(offset, SeekOrigin.Begin);
		int val = mReader.ReadByte();
		mStream.Seek(pos, SeekOrigin.Begin);
		return val;
	}

	/// <summary>
	/// Peek at the first integer at the specified offset.
	/// </summary>

	public int PeekInt (int offset)
	{
		long pos = mStream.Position;
		if (offset + 4 > pos) return -1;
		mStream.Seek(offset, SeekOrigin.Begin);
		int val = mReader.ReadInt32();
		mStream.Seek(pos, SeekOrigin.Begin);
		return val;
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
	/// Begin writing a packet: the first 4 bytes indicate the size of the data that will follow.
	/// </summary>

	public BinaryWriter BeginPacket (Packet packet, int startOffset)
	{
		BinaryWriter writer = BeginWriting(startOffset);
		writer.Write(0);
		writer.Write((byte)packet);
		return writer;
	}

	/// <summary>
	/// Begin writing a packet: the first 4 bytes indicate the size of the data that will follow.
	/// </summary>

	public BinaryWriter BeginPacket (byte packetID)
	{
		BinaryWriter writer = BeginWriting(false);
		writer.Write(0);
		writer.Write(packetID);
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

	/// <summary>
	/// Finish writing of the packet, updating (and returning) its size.
	/// </summary>

	public int EndPacket (int startOffset)
	{
		if (mWriting)
		{
			mSize = position;
			mStream.Seek(startOffset, SeekOrigin.Begin);
			mWriter.Write(mSize - 4 - startOffset);
			mStream.Seek(0, SeekOrigin.Begin);
			mWriting = false;
		}
		return mSize;
	}
}
}