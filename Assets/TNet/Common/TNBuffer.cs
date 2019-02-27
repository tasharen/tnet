//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
//-------------------------------------------------

#define RECYCLE_BUFFERS
//#define DEBUG_BUFFERS

using System.IO;
using System.Threading;
using System.Collections.Generic;

namespace TNet
{
	/// <summary>
	/// This class merges BinaryWriter and BinaryReader into one.
	/// </summary>

	public class Buffer
	{
		static Queue<Buffer> mPool = new Queue<Buffer>();
		static int mPoolCount = 0;

		/// <summary>
		/// Number of unused entries in the pool.
		/// </summary>

		static public int poolSize { get { return mPoolCount; } }

		MemoryStream mStream;
		BinaryWriter mWriter;
		BinaryReader mReader;

#if RECYCLE_BUFFERS
		int mCounter = 0;
#endif
#if DEBUG_BUFFERS
		static int mUniqueCounter = 0;
		internal int mUniqueID = 0;
		public int id { get { return mUniqueID; } }
#endif
		int mSize = 0;
		bool mWriting = false;

		Buffer ()
		{
			mStream = new MemoryStream();
			mWriter = new BinaryWriter(mStream);
			mReader = new BinaryReader(mStream);
#if DEBUG_BUFFERS
			mUniqueID = Interlocked.Increment(ref mUniqueCounter);
#endif
		}

		~Buffer ()
		{
#if DEBUG_BUFFERS
			FastLog.Log("DISPOSED of " + mUniqueID + " (" + position + " " + size + " " + (Packet)PeekByte(4) + ")");
#endif
			if (mStream != null)
			{
				mStream.Dispose();
				mStream = null;
			}
		}

		/// <summary>
		/// Whether the buffer is currently in write mode.
		/// </summary>

		public bool isWriting { get { return mWriting; } }

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

		static public int recycleQueue { get { return mPool.Count; } }

		/// <summary>
		/// Create a new buffer, reusing an old one if possible.
		/// </summary>

		static public Buffer Create ()
		{
			Buffer b = null;

			if (mPool.Count == 0)
			{
				b = new Buffer();
#if DEBUG_BUFFERS
				FastLog.Log("Buffer.Create (new) " + b.mUniqueID);
#endif
			}
			else
			{
				lock (mPool)
				{
					if (mPoolCount > 0)
					{
						--mPoolCount;
						b = mPool.Dequeue();
#if DEBUG_BUFFERS
						FastLog.Log("Buffer.Create (existing) " + b.mUniqueID + " (" + mPool.Count + ")");
#endif
					}
					else
					{
						b = new Buffer();
#if DEBUG_BUFFERS
						FastLog.Log("Buffer.Create (new) " + b.mUniqueID);
#endif
					}
				}
			}
#if RECYCLE_BUFFERS
#if UNITY_EDITOR && DEBUG_BUFFERS
			if (b.mCounter != 0) UnityEngine.Debug.LogWarning("Acquiring a buffer that's potentially in use (counter == " + b.mCounter + "): " + b.mUniqueID);
#elif UNITY_EDITOR
			if (b.mCounter != 0) UnityEngine.Debug.LogWarning("Acquiring a buffer that's potentially in use (counter == " + b.mCounter + ")");
#endif
			b.mCounter = 1;
#endif
			return b;
		}

		/// <summary>
		/// Release the buffer into the reusable pool.
		/// </summary>

		public bool Recycle (bool threadSafe = true)
		{
#if RECYCLE_BUFFERS
 #if UNITY_EDITOR
			if (mCounter == 0)
			{
  #if DEBUG_BUFFERS
				UnityEngine.Debug.LogWarning("Releasing a buffer that's already in the pool: " + mUniqueID);
  #else
				UnityEngine.Debug.LogWarning("Releasing a buffer that's already in the pool!");
  #endif
				return false;
			}
 #endif
			if (Interlocked.Decrement(ref mCounter) > 0) return false;

			Clear();

			if (mPoolCount < 250)
			{
				if (threadSafe)
				{
					lock (mPool)
					{
						++mPoolCount;
						mPool.Enqueue(this);
#if DEBUG_BUFFERS
						FastLog.Log("Recycling " + mUniqueID + " (" + mPool.Count + ")");
#endif
					}
#endif
				}
				else
				{
					++mPoolCount;
					mPool.Enqueue(this);
#if DEBUG_BUFFERS
					FastLog.Log("Recycling " + mUniqueID + " (" + mPool.Count + ")");
#endif
				}
			}
			return true;
		}

		/// <summary>
		/// Recycle an entire queue of buffers.
		/// </summary>

		static public void Recycle (Queue<Buffer> list)
		{
#if RECYCLE_BUFFERS
			lock (mPool) while (list.Count != 0) list.Dequeue().Recycle(false);
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
			lock (mPool) while (list.Count != 0) list.Dequeue().Recycle(false);
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
				for (int i = 0; i < list.size; ++i) list.buffer[i].Recycle(false);
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
				for (int i = 0; i < list.size; ++i) list.buffer[i].Recycle(false);
				list.Clear();
			}
#else
			list.Clear();
#endif
		}

		/// <summary>
		/// Release all currently unused memory sitting in the memory pool.
		/// </summary>

		static public void ReleaseUnusedMemory ()
		{
#if STANDALONE
			var timer = System.Diagnostics.Stopwatch.StartNew();
#endif
			lock (mPool)
			{
				mPoolCount = 0;
				mPool.Clear();
			}

			System.GC.Collect();
#if STANDALONE
			System.Console.WriteLine("GC took " + timer.ElapsedMilliseconds + " ms");
#endif
		}

		/// <summary>
		/// Mark the buffer as being in use.
		/// </summary>

		public void MarkAsUsed ()
		{
#if RECYCLE_BUFFERS
			Interlocked.Increment(ref mCounter);
#endif
		}

		/// <summary>
		/// Clear the buffer.
		/// </summary>

		public void Clear ()
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
			var w = target.BeginWriting(false);
			int bytes = size;
			if (bytes > 0) w.Write(buffer, position, bytes);
			target.EndWriting();
		}

		/// <summary>
		/// Begin the writing process.
		/// </summary>

		public BinaryWriter BeginWriting (bool append = false)
		{
			if (!mWriting)
			{
				if (append)
				{
					mStream.Seek(mSize, SeekOrigin.Begin);
				}
				else
				{
					mStream.Seek(0, SeekOrigin.Begin);
					mSize = 0;
				}

				mWriting = true;
			}
			else if (!append)
			{
				mStream.Seek(0, SeekOrigin.Begin);
				mSize = 0;
			}
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

		public int PeekByte (int pos) { return pos > -1 && pos < size ? buffer[pos] : 0; }

		/// <summary>
		/// Peek at the first integer at the specified offset.
		/// </summary>

		public int PeekInt (int pos)
		{
			if (pos > -1 && 4 < size)
			{
				var buffer = this.buffer;
				return (int)buffer[pos] | ((int)buffer[pos + 1] << 8) | ((int)buffer[pos + 2] << 16) | ((int)buffer[pos + 3] << 24);
			}
			return 0;
		}

		/// <summary>
		/// Peek at the first integer at the specified offset.
		/// </summary>

		public uint PeekUInt (int pos)
		{
			if (pos > -1 && 4 < size)
			{
				var buffer = this.buffer;
				return (uint)buffer[pos] | ((uint)buffer[pos + 1] << 8) | ((uint)buffer[pos + 2] << 16) | ((uint)buffer[pos + 3] << 24);
			}
			return 0;
		}

		/// <summary>
		/// Peek-read the specified number of bytes.
		/// </summary>

		public byte[] PeekBytes (int pos, int length)
		{
			byte[] bytes = null;
			long oldPos = mStream.Position;

			if (pos > -1 && length < size)
			{
				mStream.Seek(pos, SeekOrigin.Begin);
				bytes = mReader.ReadBytes(length);
				mStream.Seek(oldPos, SeekOrigin.Begin);
			}
			return bytes;
		}

		/// <summary>
		/// Begin writing a packet: the first 4 bytes indicate the size of the data that will follow.
		/// </summary>

		public BinaryWriter BeginPacket (byte packetID)
		{
			var writer = BeginWriting(false);
			writer.Write(0);
			writer.Write(packetID);
			return writer;
		}

		/// <summary>
		/// Begin writing a packet: the first 4 bytes indicate the size of the data that will follow.
		/// </summary>

		public BinaryWriter BeginPacket (Packet packet)
		{
			var writer = BeginWriting(false);
			writer.Write(0);
			writer.Write((byte)packet);
			return writer;
		}

		/// <summary>
		/// Begin writing a packet: the first 4 bytes indicate the size of the data that will follow.
		/// </summary>

		public BinaryWriter BeginPacket (Packet packet, int startOffset)
		{
			var writer = BeginWriting(startOffset);
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
