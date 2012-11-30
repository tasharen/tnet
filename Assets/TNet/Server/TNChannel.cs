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

public class Channel
{
	public class RFC
	{
		public int id;
		public string funcName;
		public Buffer buffer;
	}

	public class CreatedObject
	{
		public short objectID;
		public int uniqueID;
		public Buffer buffer;
	}

	public int id;
	public string password = "";
	public string level = "";
	public bool persistent = false;
	public bool closed = false;
	public BetterList<ServerPlayer> players = new BetterList<ServerPlayer>();
	public BetterList<RFC> rfcs = new BetterList<RFC>();
	public BetterList<CreatedObject> created = new BetterList<CreatedObject>();
	public BetterList<int> destroyed = new BetterList<int>();
	public int objectCounter = 0;
	public ServerPlayer host;

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
		objectCounter = 0;
	}

	/// <summary>
	/// Remove the specified player from the channel.
	/// </summary>

	public void RemovePlayer (ServerPlayer p)
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

	public void CreateRFC (int inID, string funcName, Buffer buffer)
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

	public void DeleteRFC (int inID, string funcName)
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

	public void DeleteObjectRFCs (int objectID)
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
		objectCounter = reader.ReadInt32();
		password = reader.ReadString();
		persistent = reader.ReadBoolean();

		int size = reader.ReadInt32();

		for (int i = 0; i < size; ++i)
		{
			RFC rfc = new RFC();
			rfc.id = reader.ReadInt32();
			Buffer b = Buffer.Create();
			b.BeginWriting(false).Write(reader.ReadBytes(reader.ReadInt32()));
			rfc.buffer = b;
			rfcs.Add(rfc);
		}

		size = reader.ReadInt32();

		for (int i = 0; i < size; ++i)
		{
			CreatedObject co = new CreatedObject();
			co.uniqueID = reader.ReadInt32();
			co.objectID = reader.ReadInt16();
			Buffer b = Buffer.Create();
			b.BeginWriting(false).Write(reader.ReadBytes(reader.ReadInt32()));
			co.buffer = b;
			created.Add(co);
		}

		size = reader.ReadInt32();
		for (int i = 0; i < size; ++i) destroyed.Add(reader.ReadInt32());
	}
}
}