//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using System;
using System.Net;
using System.IO;

namespace TNet
{
/// <summary>
/// Server list is a helper class containing a list of servers.
/// </summary>

public class ServerList
{
	public struct Entry
	{
		public string name;
		public int playerCount;
		public IPEndPoint ip;
		public long expirationTime;
	}

	/// <summary>
	/// List of active server entries. Be sure to lock it before using it,
	/// as it can be changed from a different thread.
	/// </summary>

	public List<Entry> list = new List<Entry>();

	/// <summary>
	/// Add a new entry to the list.
	/// </summary>

	public void Add (string name, int playerCount, IPEndPoint ip, long time)
	{
		for (int i = 0; i < list.size; ++i)
		{
			Entry ent = list[i];

			if (ent.ip.Equals(ip))
			{
				ent.name = name;
				ent.playerCount = playerCount;
				ent.expirationTime = time + 5000;
				list[i] = ent;
				return;
			}
		}

		Entry e = new Entry();
		e.name = name;
		e.playerCount = playerCount;
		e.ip = ip;
		e.expirationTime = time + 5000;
		lock (list) list.Add(e);
	}

	/// <summary>
	/// Remove an existing entry from the list.
	/// </summary>

	public void Remove (IPEndPoint ip)
	{
		for (int i = 0; i < list.size; ++i)
		{
			Entry ent = list[i];

			if (ent.Equals(ip))
			{
				lock (list) list.RemoveAt(i);
				return;
			}
		}
	}

	/// <summary>
	/// Remove expired entries.
	/// </summary>

	public bool Cleanup (long time)
	{
		bool changed = false;

		for (int i = 0; i < list.size; )
		{
			Entry ent = list[i];

			if (ent.expirationTime < time)
			{
				changed = true;
				lock (list) list.RemoveAt(i);
				continue;
			}
			++i;
		}
		return changed;
	}

	/// <summary>
	/// Clear the list of servers.
	/// </summary>

	public void Clear () { lock (list) list.Clear(); }

	/// <summary>
	/// Save the list of servers to the specified binary writer.
	/// </summary>

	public void WriteTo (BinaryWriter writer, GameServer localServer)
	{
		writer.Write(GameServer.gameID);

		if (localServer != null && localServer.isActive)
		{
			writer.Write(true);
			writer.Write(localServer.name);
			writer.Write((ushort)localServer.playerCount);
			writer.Write((ushort)localServer.tcpPort);
		}
		else writer.Write(false);

		writer.Write((ushort)list.size);

		lock (list)
		{
			for (int i = 0; i < list.size; ++i)
			{
				Entry ent = list[i];

				writer.Write(ent.name);
				writer.Write((ushort)ent.playerCount);
				byte[] bytes = ent.ip.Address.GetAddressBytes();
				writer.Write((byte)bytes.Length);
				writer.Write(bytes);
				writer.Write((ushort)ent.ip.Port);
			}
		}
	}

	/// <summary>
	/// Read a list of servers from the binary reader.
	/// </summary>

	public void ReadFrom (BinaryReader reader, IPEndPoint source, long time)
	{
		if (reader.ReadUInt16() == GameServer.gameID)
		{
			if (reader.ReadBoolean())
			{
				string name = reader.ReadString();
				int playerCount = reader.ReadUInt16();
				IPEndPoint ip = new IPEndPoint(source.Address, reader.ReadUInt16());
				Add(name, playerCount, ip, time);
			}

			int count = reader.ReadUInt16();

			for (int i = 0; i < count; ++i)
			{
				string name = reader.ReadString();
				int playerCount = reader.ReadUInt16();
				byte[] bytes = reader.ReadBytes(reader.ReadByte());
				IPEndPoint ip = new IPEndPoint(new IPAddress(bytes), reader.ReadUInt16());
				Add(name, playerCount, ip, time);
			}
		}
	}
}
}
