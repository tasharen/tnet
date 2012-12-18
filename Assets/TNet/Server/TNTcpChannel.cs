//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using System;
using System.IO;

namespace TNet
{
/// <summary>
/// A channel contains one or more players.
/// All information broadcast by players is visible by others in the same channel.
/// </summary>

public class TcpChannel
{
	public class RFC
	{
		// Object ID (24 bytes), RFC ID (8 bytes)
		public uint id;
		public string funcName;
		public Buffer buffer;
	}

	public class CreatedObject
	{
		public ushort objectID;
		public uint uniqueID;
		public Buffer buffer;
	}

	public int id;
	public string password = "";
	public string level = "";
	public bool persistent = false;
	public bool closed = false;
	public List<TcpPlayer> players = new List<TcpPlayer>();
	public List<RFC> rfcs = new List<RFC>();
	public List<CreatedObject> created = new List<CreatedObject>();
	public List<uint> destroyed = new List<uint>();
	public uint objectCounter = 0xFFFFFF;
	public TcpPlayer host;

	/// <summary>
	/// Whether the channel has data that can be saved.
	/// </summary>

	public bool hasData { get { return rfcs.size > 0 || created.size > 0 || destroyed.size > 0; } }

	/// <summary>
	/// Reset the channel to its initial state.
	/// </summary>

	public void Reset ()
	{
		for (int i = 0; i < rfcs.size; ++i) rfcs[i].buffer.Recycle();
		for (int i = 0; i < created.size; ++i) created[i].buffer.Recycle();

		rfcs.Clear();
		created.Clear();
		destroyed.Clear();
		objectCounter = 0xFFFFFF;
	}

	/// <summary>
	/// Remove the specified player from the channel.
	/// </summary>

	public void RemovePlayer (TcpPlayer p)
	{
		if (p == host) host = null;

		if (players.Remove(p) && !persistent && players.size == 0)
		{
			closed = true;

			for (int i = 0; i < rfcs.size; ++i)
			{
				RFC r = rfcs[i];
				if (r.buffer != null) r.buffer.Recycle();
			}
			rfcs.Clear();
		}
	}

	/// <summary>
	/// Create a new buffered remote function call.
	/// </summary>

	public void CreateRFC (uint inID, string funcName, Buffer buffer)
	{
		if (closed || buffer == null) return;
		buffer.MarkAsUsed();

		for (int i = 0; i < rfcs.size; ++i)
		{
			RFC r = rfcs[i];
			
			if (r.id == inID && r.funcName == funcName)
			{
				if (r.buffer != null) r.buffer.Recycle();
				r.buffer = buffer;
				return;
			}
		}

		RFC rfc = new RFC();
		rfc.id = inID;
		rfc.buffer = buffer;
		rfc.funcName = funcName;
		rfcs.Add(rfc);
	}

	/// <summary>
	/// Delete the specified remote function call.
	/// </summary>

	public void DeleteRFC (uint inID, string funcName)
	{
		for (int i = 0; i < rfcs.size; ++i)
		{
			RFC r = rfcs[i];

			if (r.id == inID && r.funcName == funcName)
			{
				rfcs.RemoveAt(i);
				r.buffer.Recycle();
			}
		}
	}

	/// <summary>
	/// Delete the specified remote function call.
	/// </summary>

	public void DeleteObjectRFCs (uint objectID)
	{
		for (int i = 0; i < rfcs.size; )
		{
			RFC r = rfcs[i];

			if ((r.id >> 8) == objectID)
			{
				rfcs.RemoveAt(i);
				r.buffer.Recycle();
				continue;
			}
			++i;
		}
	}

	/// <summary>
	/// Save the channel's data into the specified file.
	/// </summary>

	public void SaveTo (BinaryWriter writer)
	{
		writer.Write(level);
		writer.Write(objectCounter);
		writer.Write(password);
		writer.Write(persistent);
		writer.Write(rfcs.size);

		for (int i = 0; i < rfcs.size; ++i)
		{
			RFC rfc = rfcs[i];
			writer.Write(rfc.id);
			writer.Write(rfc.buffer.size);
			
			if (rfc.buffer.size > 0)
			{
				rfc.buffer.BeginReading();
				writer.Write(rfc.buffer.buffer, rfc.buffer.position, rfc.buffer.size);
			}
		}

		writer.Write(created.size);

		for (int i = 0; i < created.size; ++i)
		{
			CreatedObject co = created[i];
			writer.Write(co.uniqueID);
			writer.Write(co.objectID);
			writer.Write(co.buffer.size);
			
			if (co.buffer.size > 0)
			{
				co.buffer.BeginReading();
				writer.Write(co.buffer.buffer, co.buffer.position, co.buffer.size);
			}
		}

		writer.Write(destroyed.size);
		for (int i = 0; i < destroyed.size; ++i) writer.Write(destroyed[i]);
	}

	/// <summary>
	/// Load the channel's data from the specified file.
	/// </summary>

	public void LoadFrom (BinaryReader reader)
	{
		// Clear all RFCs, just in case
		for (int i = 0; i < rfcs.size; ++i)
		{
			RFC r = rfcs[i];
			if (r.buffer != null) r.buffer.Recycle();
		}
		rfcs.Clear();
		created.Clear();
		destroyed.Clear();

		level = reader.ReadString();
		objectCounter = reader.ReadUInt32();
		password = reader.ReadString();
		persistent = reader.ReadBoolean();

		int size = reader.ReadInt32();

		for (int i = 0; i < size; ++i)
		{
			RFC rfc = new RFC();
			rfc.id = reader.ReadUInt32();
			Buffer b = Buffer.Create();
			b.BeginWriting(false).Write(reader.ReadBytes(reader.ReadInt32()));
			rfc.buffer = b;
			rfcs.Add(rfc);
		}

		size = reader.ReadInt32();

		for (int i = 0; i < size; ++i)
		{
			CreatedObject co = new CreatedObject();
			co.uniqueID = reader.ReadUInt32();
			co.objectID = reader.ReadUInt16();
			Buffer b = Buffer.Create();
			b.BeginWriting(false).Write(reader.ReadBytes(reader.ReadInt32()));
			co.buffer = b;
			created.Add(co);
		}

		size = reader.ReadInt32();
		for (int i = 0; i < size; ++i) destroyed.Add(reader.ReadUInt32());
	}
}
}