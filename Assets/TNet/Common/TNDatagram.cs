//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;

namespace TNet
{
/// <summary>
/// Simple datagram container -- contains a data buffer and the address of where it came from (or where it's going).
/// </summary>

public class Datagram
{
	static List<Datagram> mPool = new List<Datagram>();

	IPEndPoint mEndPoint = new IPEndPoint(IPAddress.Any, 0);
	IPEndPoint mOverride;
	Buffer mBuffer;
	bool mInPool = false;

	/// <summary>
	/// Datagrams should only be created via Datagram.Create().
	/// </summary>

	Datagram () { }

	/// <summary>
	/// IPEndPoint is a class, meaning the same reference is used everywhere. This can be bad when datagrams get recycled.
	/// Better approach is to keep an "override" value that will be used if set.
	/// </summary>

	public IPEndPoint endPoint { get { return (mOverride != null) ? mOverride : mEndPoint; } set { mOverride = value; } }
	
	/// <summary>
	/// Data buffer.
	/// </summary>

	public Buffer buffer
	{
		get
		{
			return mBuffer;
		}
		set
		{
			if (mBuffer != null) mBuffer.Recycle();
			mBuffer = value;
		}
	}

	/// <summary>
	/// Create a new datagram using the specified buffer.
	/// </summary>

	static public Datagram Create (Buffer b)
	{
		Datagram dg = null;

		if (mPool.size != 0)
		{
			lock (mPool)
			{
				if (mPool.size != 0)
				{
					dg = mPool.Pop();
					dg.mInPool = false;
				}
				else dg = new Datagram();
			}
		}
		else dg = new Datagram();

		if (b != null)
		{
			if (dg.buffer != null) dg.buffer.Recycle();
			dg.buffer = b;
		}
		else if (dg.buffer == null)
		{
			dg.buffer = Buffer.Create();
		}
		return dg;
	}

	/// <summary>
	/// Create a new datagram.
	/// </summary>

	static public Datagram Create () { return Create(null); }

	/// <summary>
	/// Recycle the datagram, putting it back in the unused pool.
	/// </summary>

	public void Recycle ()
	{
		if (!mInPool)
		{
			endPoint = null;
			
			lock (mPool)
			{
				mPool.Add(this);
				mInPool = true;
			}
		}
	}

	/// <summary>
	/// Recycle an entire list of datagrams.
	/// </summary>

	static public void Recycle (List<Datagram> list)
	{
		lock (mPool)
		{
			for (int i = 0; i < list.size; ++i)
			{
				Datagram dg = list[i];

				if (!dg.mInPool)
				{
					dg.mInPool = true;

					if (dg.buffer != null)
					{
						dg.buffer.Recycle();
						dg.buffer = null;
					}
					dg.endPoint = null;
					mPool.Add(dg);
				}
			}
		}
		list.Clear();
	}

	/// <summary>
	/// Recycle an entire list of datagrams.
	/// </summary>

	static public void Recycle (Queue<Datagram> list)
	{
		lock (mPool)
		{
			while (list.Count != 0)
			{
				Datagram dg = list.Dequeue();

				if (!dg.mInPool)
				{
					dg.mInPool = true;

					if (dg.buffer != null)
					{
						dg.buffer.Recycle();
						dg.buffer = null;
					}
					dg.endPoint = null;
					mPool.Add(dg);
				}
			}
		}
		list.Clear();
	}
}
}