//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

#define RECYCLE_BUFFERS

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;

namespace TNet
{
/// <summary>
/// This class merges BinaryWriter and BinaryReader into one.
/// </summary>

public class Buffer
{
	static List<Buffer> mPool = new List<Buffer>();

	volatile MemoryStream mStream;
	volatile BinaryWriter mWriter;
	volatile BinaryReader mReader;

#if RECYCLE_BUFFERS
	volatile int mCounter = 0;
#endif
#if DEBUG_BUFFERS
	static volatile int mUniqueCounter = 0;
	internal volatile int mUniqueID = 0;
#endif
	volatile int mSize = 0;
	volatile bool mWriting = false;

	Buffer ()
	{
		mStream = new MemoryStream();
		mWriter = new BinaryWriter(mStream);
		mReader = new BinaryReader(mStream);
#if DEBUG_BUFFERS
		mUniqueID = ++mUniqueCounter;
#endif
	}

	~Buffer ()
	{
#if DEBUG_BUFFERS
		Log("DISPOSED of " + mUniqueID + " (" + (Packet)PeekByte(4) + ")");
#endif
		if (mStream != null)
		{
			mStream.Dispose();
			mStream = null;
		}
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

	public int position
	{
		get
		{
			return (int)mStream.Position;
		}
		set
		{
			mStream.Seek(value, SeekOrigin.Begin);
		}
	}

	/// <summary>
	/// Underlying memory stream.
	/// </summary>

	public MemoryStream stream { get { return mStream; } }

	/// <summary>
	/// Get the entire buffer (note that it may be bigger than 'size').
	/// </summary>

	public byte[] buffer
	{
		get
		{
			return mStream.GetBuffer();
		}
	}

	/// <summary>
	/// Number of buffers in the recycled list.
	/// </summary>

	static public int recycleQueue { get { return mPool.size; } }

#if DEBUG_BUFFERS
 #if !UNITY_EDITOR
	static List<string> mEntries = new List<string>();
 #endif

	static void Log (string text)
	{
 #if UNITY_EDITOR
		UnityEngine.Debug.Log(text);
 #else
		lock (mEntries)
		{
			mEntries.Add(text);

			if (mEntries.size == 100)
			{
				FileStream fs = File.Open("log.txt", FileMode.Append);
				StreamWriter sw = new StreamWriter(fs);
				foreach (string s in mEntries) sw.WriteLine(s);
				sw.Flush();
				sw.Dispose();
				mEntries.Clear();
			}
		}
 #endif
	}
#endif

	/// <summary>
	/// Create a new buffer, reusing an old one if possible.
	/// </summary>

	static public Buffer Create ()
	{
		Buffer b = null;

		if (mPool.size == 0)
		{
			b = new Buffer();
#if DEBUG_BUFFERS
			Log("New " + b.mUniqueID);
#endif
		}
		else
		{
			lock (mPool)
			{
				if (mPool.size != 0)
				{
					b = mPool.Pop();
#if DEBUG_BUFFERS
					Log("Existing " + b.mUniqueID + " (" + mPool.size + ")");
#endif
				}
				else
				{
					b = new Buffer();
#if DEBUG_BUFFERS
					Log("New " + b.mUniqueID);
#endif
				}
			}
		}
#if RECYCLE_BUFFERS
 #if UNITY_EDITOR && DEBUG_BUFFERS
		if (b.mCounter != 0) UnityEngine.Debug.LogWarning("Acquiring a buffer that's potentially in use: " + b.mUniqueID);
 #endif
		b.mCounter = 1;
#endif
		return b;
	}

	/// <summary>
	/// Release the buffer into the reusable pool.
	/// </summary>

	public bool Recycle ()
	{
#if RECYCLE_BUFFERS
		lock (this)
		{
 #if UNITY_EDITOR
			if (mCounter == 0)
			{
  #if DEBUG_BUFFERS
				UnityEngine.Debug.LogWarning("Releasing a buffer that's already in the pool: " + mUniqueID);
  #else
				UnityEngine.Debug.LogWarning("Releasing a buffer that's already in the pool");
  #endif
				return false;
			}
 #endif
			if (--mCounter > 0) return false;

			lock (mPool)
			{
				ClearNotThreadSafe();
				mPool.Add(this);
 #if DEBUG_BUFFERS
				Log("Recycling " + mUniqueID + " (" + mPool.size + ")");
 #endif
			}
			return true;
		}
#else
		return true;
#endif
	}

	/// <summary>
	/// Recycle an entire queue of buffers.
	/// </summary>

	static public void Recycle (Queue<Buffer> list)
	{
#if RECYCLE_BUFFERS
		lock (mPool)
		{
			while (list.Count != 0)
			{
				Buffer b = list.Dequeue();
				b.Recycle();
			}
		}
#else
		list.Clear();
#endif
	}

	/// <summary>
	/// Recycle an entire queue of buffers.
	/// </summary>

	static public void Recycle (Queue<Datagram> list)
	{
#if RECYCLE_BUFFERS
		lock (mPool)
		{
			while (list.Count != 0)
			{
				Datagram dg = list.Dequeue();

				if (dg.buffer != null)
				{
					dg.buffer.Recycle();
					dg.buffer = null;
				}
			}
		}
#else
		list.Clear();
#endif
	}

	/// <summary>
	/// Recycle an entire list of buffers.
	/// </summary>

	static public void Recycle (List<Buffer> list)
	{
#if RECYCLE_BUFFERS
		lock (mPool)
		{
			for (int i = 0; i < list.size; ++i)
			{
				Buffer b = list[i];
				b.Recycle();
			}
			list.Clear();
		}
#else
		list.Clear();
#endif
	}

	/// <summary>
	/// Recycle an entire list of buffers.
	/// </summary>

	static public void Recycle (List<Datagram> list)
	{
#if RECYCLE_BUFFERS
		lock (mPool)
		{
			for (int i = 0; i < list.size; ++i)
			{
				Datagram dg = list[i];

				if (dg.buffer != null)
				{
					dg.buffer.Recycle();
					dg.buffer = null;
				}
			}
			list.Clear();
		}
#else
		list.Clear();
#endif
	}

	/// <summary>
	/// Release all currently unused memory sitting in the memory pool.
	/// </summary>

	static public void ReleaseUnusedMemory () { lock (mPool) mPool.Release(); }

	/// <summary>
	/// Mark the buffer as being in use.
	/// </summary>

	public void MarkAsUsed ()
	{
#if RECYCLE_BUFFERS
		lock (this) ++mCounter;
#endif
	}

	/// <summary>
	/// Clear the buffer.
	/// </summary>

#if RECYCLE_BUFFERS
	public void Clear () { lock (this) ClearNotThreadSafe(); }
#else
	public void Clear () { ClearNotThreadSafe(); }
#endif

	/// <summary>
	/// Clear the buffer.
	/// </summary>

	void ClearNotThreadSafe ()
	{
		mSize = 0;
#if RECYCLE_BUFFERS
		mCounter = 0;
#endif
		if (mStream != null)
		{
			if (mStream.Capacity > 1024)
			{
				mStream = new MemoryStream();
				mReader = new BinaryReader(mStream);
				mWriter = new BinaryWriter(mStream);
			}
			else mStream.Seek(0, SeekOrigin.Begin);
		}
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
	/// Begin the writing process.
	/// </summary>

	public BinaryWriter BeginWriting (bool append = false)
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
		if (mStream.Position != startOffset)
		{
			if (startOffset > mStream.Length)
			{
				mStream.Seek(0, SeekOrigin.End);
				for (long i = mStream.Length; i < startOffset; ++i)
					mWriter.Write((byte)0);
			}
			else mStream.Seek(startOffset, SeekOrigin.Begin);
		}

		mSize = startOffset;
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
			mWriting = false;
			mSize = (int)mStream.Position;
			mStream.Seek(0, SeekOrigin.Begin);
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

	public int PeekByte (int pos)
	{
		int val = 0;
		long oldPos = mStream.Position;
		if (pos < 0 || pos + 1 > size) return -1;
		mStream.Seek(pos, SeekOrigin.Begin);
		val = mReader.ReadByte();
		mStream.Seek(oldPos, SeekOrigin.Begin);
		return val;
	}

	/// <summary>
	/// Peek at the first integer at the specified offset.
	/// </summary>

	public int PeekInt (int pos)
	{
		int val = 0;
		long oldPos = mStream.Position;
		if (pos < 0 || pos + 4 > size) return -1;
		mStream.Seek(pos, SeekOrigin.Begin);
		val = mReader.ReadInt32();
		mStream.Seek(oldPos, SeekOrigin.Begin);
		return val;
	}

	/// <summary>
	/// Peek-read the specified number of bytes.
	/// </summary>

	public byte[] PeekBytes (int pos, int length)
	{
		byte[] bytes = null;
		long oldPos = mStream.Position;
		if (pos < 0 || pos + length > size) return null;
		mStream.Seek(pos, SeekOrigin.Begin);
		bytes = mReader.ReadBytes(length);
		mStream.Seek(oldPos, SeekOrigin.Begin);
		return bytes;
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

	public int EndTcpPacketStartingAt (int startOffset)
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

	/// <summary>
	/// Finish writing the packet and reposition the stream's position to the specified offset.
	/// </summary>

	public int EndTcpPacketWithOffset (int offset)
	{
		if (mWriting)
		{
			mSize = position;
			mStream.Seek(0, SeekOrigin.Begin);
			mWriter.Write(mSize - 4);
			mStream.Seek(offset, SeekOrigin.Begin);
			mWriting = false;
		}
		return mSize;
	}
}
}
