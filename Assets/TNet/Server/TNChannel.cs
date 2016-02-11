//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using System;
using System.IO;

namespace TNet
{
/// <summary>
/// A channel contains one or more players.
/// All information broadcast by players is visible by others in the same channel.
/// </summary>

public class Channel : DataNodeContainer
{
	/// <summary>
	/// Remote function call entry stored within the channel.
	/// </summary>

	public class RFC
	{
		// Object ID (24 bytes), RFC ID (8 bytes)
		public uint uid;
		public string functionName;
		public Buffer data;

		public uint objectID { get { return (uid >> 8); } set { uid = ((value << 8) | (uid & 0xFF)); } }
		public uint functionID { get { return (uid & 0xFF); } }

		/// <summary>
		/// Write a complete ForwardToOthers packet to the specified buffer.
		/// </summary>

		public int WritePacket (int channelID, Buffer buffer, int offset)
		{
			BinaryWriter writer = buffer.BeginPacket(Packet.ForwardToOthers, offset);
			writer.Write(0);
			writer.Write(channelID);
			writer.Write(uid);
			if (functionID == 0) writer.Write(functionName);
			writer.Write(data.buffer, 0, data.size);
			return buffer.EndTcpPacketStartingAt(offset);
		}
	}

	/// <summary>
	/// Created objects are saved by the channels.
	/// </summary>

	public class CreatedObject
	{
		public int playerID;
		public uint objectID;
		public byte type;
		public Buffer buffer;
	}

	/// <summary>
	/// Channel information class created as a result of retrieving a list of channels.
	/// </summary>

	public class Info
	{
		public int id;				// Channel's ID
		public ushort players;		// Number of players present
		public ushort limit;		// Player limit
		public bool hasPassword;	// Whether the channel is password-protected or not
		public bool isPersistent;	// Whether the channel is persistent or not
		public string level;		// Name of the loaded level
		public DataNode data;		// Data associated with the channel
	}

	public int id;
	public string password = "";
	public string level = "";
	public bool persistent = false;
	public bool closed = false;
	public bool isLocked = false;
	public ushort playerLimit = 65535;
	public List<Player> players = new List<Player>();
	public List<RFC> rfcs = new List<RFC>();
	public List<CreatedObject> created = new List<CreatedObject>();
	public List<uint> destroyed = new List<uint>();
	public uint objectCounter = 0xFFFFFF;
	public Player host;

	// Key = Object ID. Value is 'true'. This dictionary is used for a quick lookup checking to see
	// if the object actually exists. It's used to store RFCs. RFCs for objects that don't exist are not stored.
	[System.NonSerialized] System.Collections.Generic.Dictionary<uint, bool> mCreatedObjectDictionary =
		new System.Collections.Generic.Dictionary<uint, bool>();

	/// <summary>
	/// Whether the channel has data that can be saved.
	/// </summary>

	public bool hasData { get { return rfcs.size > 0 || created.size > 0 || destroyed.size > 0; } }

	/// <summary>
	/// Whether the channel can be joined.
	/// </summary>

	public bool isOpen { get { return !closed && players.size < playerLimit; } }

	/// <summary>
	/// Helper function that returns a new unique ID that's not currently used by any object.
	/// </summary>

	public uint GetUniqueID ()
	{
		for (; ; )
		{
			uint uniqueID = --objectCounter;

			// 1-32767 is reserved for existing scene objects.
			// 32768 - 16777215 is for dynamically created objects.
			if (uniqueID < 32768)
			{
				objectCounter = 0xFFFFFF;
				uniqueID = 0xFFFFFF;
			}

			// Ensure that this object ID is not already in use
			if (!mCreatedObjectDictionary.ContainsKey(uniqueID))
				return uniqueID;
		}
	}

	/// <summary>
	/// Add a new created object to the list. This object's ID must always be above 32767.
	/// </summary>

	public void AddCreatedObject (CreatedObject obj)
	{
		created.Add(obj);
		mCreatedObjectDictionary[obj.objectID] = true;
	}

	/// <summary>
	/// Return a player with the specified ID.
	/// </summary>

	public Player GetPlayer (int pid)
	{
		for (int i = 0; i < players.size; ++i)
		{
			Player p = players[i];
			if (p.id == pid) return p;
		}
		return null;
	}

	/// <summary>
	/// Remove the player with the specified ID.
	/// </summary>

	public bool RemovePlayer (int pid)
	{
		for (int i = 0; i < players.size; ++i)
		{
			Player p = players[i];
			if (p.id == pid)
			{
				players.RemoveAt(i);
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Reset the channel to its initial state.
	/// </summary>

	public void Reset ()
	{
		for (int i = 0; i < rfcs.size; ++i) rfcs[i].data.Recycle();
		for (int i = 0; i < created.size; ++i) created[i].buffer.Recycle();

		rfcs.Clear();
		created.Clear();
		destroyed.Clear();
		mCreatedObjectDictionary.Clear();
		objectCounter = 0xFFFFFF;
	}

	/// <summary>
	/// Remove the specified player from the channel.
	/// </summary>

	public void RemovePlayer (TcpPlayer p, List<uint> destroyedObjects)
	{
		destroyedObjects.Clear();

		if (players.Remove(p))
		{
			// When the host leaves, clear the host (it gets changed in SendLeaveChannel)
			if (p == host) host = null;

			// Remove all of the non-persistent objects that were created by this player
			for (int i = created.size; i > 0; )
			{
				Channel.CreatedObject obj = created[--i];

				if (obj.playerID == p.id)
				{
					if (obj.type == 2)
					{
						if (obj.buffer != null) obj.buffer.Recycle();
						uint objID = obj.objectID;
						created.RemoveAt(i);
						destroyedObjects.Add(objID);
						if (objID >= 32768) mCreatedObjectDictionary.Remove(objID);
						DestroyObjectRFCs(objID);
					}
					else if (players.size != 0)
					{
						// The same operation happens on the client as well
						obj.playerID = players[0].id;
					}
				}
			}

			// Close the channel if it wasn't persistent
			if ((!persistent || playerLimit < 1) && players.size == 0)
			{
				closed = true;

				for (int i = 0; i < rfcs.size; ++i)
				{
					RFC r = rfcs[i];
					if (r.data != null) r.data.Recycle();
				}
				rfcs.Clear();
			}
		}
	}

	/// <summary>
	/// Remove an object with the specified unique identifier.
	/// </summary>

	public bool DestroyObject (uint objID)
	{
		if (objID < 32768)
		{
			// Static objects have ID below 32768
			if (!destroyed.Contains(objID))
			{
				destroyed.Add(objID);
				DestroyObjectRFCs(objID);
				return true;
			}
		}
		else if (mCreatedObjectDictionary.Remove(objID))
		{
			// Dynamic objects are always a part of the 'created' array and the lookup table
			for (int i = 0; i < created.size; ++i)
			{
				Channel.CreatedObject obj = created[i];

				if (obj.objectID == objID)
				{
					if (obj.buffer != null) obj.buffer.Recycle();
					created.RemoveAt(i);
					DestroyObjectRFCs(objID);
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>
	/// Delete the specified remote function call.
	/// </summary>

	public void DestroyObjectRFCs (uint objectID)
	{
		for (int i = rfcs.size; i > 0; )
		{
			RFC r = rfcs[--i];

			if (r.objectID == objectID)
			{
				rfcs.RemoveAt(i);
				r.data.Recycle();
			}
		}
	}

	/// <summary>
	/// Transfer the specified object to another channel, changing its Object ID in the process.
	/// </summary>

	public CreatedObject TransferObject (uint objectID, Channel other)
	{
		if (objectID < 32768)
		{
			Tools.LogError("Transferring objects only works with objects that were instantiated at run-time.");
		}
		else if (mCreatedObjectDictionary.Remove(objectID))
		{
			for (int i = 0; i < created.size; ++i)
			{
				CreatedObject obj = created[i];

				if (obj.objectID == objectID)
				{
					// Move the created object over to the other channel
					obj.objectID = other.GetUniqueID();

					// If the other channel doesn't contain the object's owner, assign a new owner
					bool changeOwner = true;

					for (int b = 0; b < other.players.size; ++b)
					{
						if (other.players[b].id == obj.playerID)
						{
							changeOwner = false;
							break;
						}
					}

					if (changeOwner) obj.playerID = (other.host != null) ? other.host.id : 0;

					created.RemoveAt(i);
					other.created.Add(obj);
					other.mCreatedObjectDictionary[obj.objectID] = true;

					// Move RFCs over to the other channel
					for (int b = rfcs.size; b > 0; )
					{
						RFC r = rfcs[--b];

						if (r.objectID == objectID)
						{
							r.objectID = obj.objectID;
							rfcs.RemoveAt(b);
							other.rfcs.Add(r);
						}
					}
					return obj;
				}
			}
		}
		return null;
	}

	/// <summary>
	/// Add a new saved remote function call.
	/// </summary>

	public void AddRFC (uint uid, string funcName, Buffer buffer)
	{
		if (closed || buffer == null) return;
		uint objID = (uid >> 8);

		// Ignore objects that don't exist
		if (objID < 32768) { if (destroyed.Contains(objID)) return; }
		else if (!mCreatedObjectDictionary.ContainsKey(objID)) return;

		Buffer b = Buffer.Create();
		b.BeginWriting(false).Write(buffer.buffer, buffer.position, buffer.size);
		b.EndWriting();

		for (int i = 0; i < rfcs.size; ++i)
		{
			RFC r = rfcs[i];

			if (r.uid == uid && r.functionName == funcName)
			{
				if (r.data != null) r.data.Recycle();
				r.data = b;
				return;
			}
		}

		RFC rfc = new RFC();
		rfc.uid = uid;
		rfc.data = b;
		rfc.functionName = funcName;
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

			if (r.uid == inID && r.functionName == funcName)
			{
				rfcs.RemoveAt(i);
				r.data.Recycle();
			}
		}
	}

	// Cached to reduce memory allocations
	[System.NonSerialized] List<uint> mCleanedOBJs = new List<uint>();
	[System.NonSerialized] List<CreatedObject> mCreatedOBJs = new List<CreatedObject>();
	[System.NonSerialized] List<RFC> mCreatedRFCs = new List<RFC>();

	/// <summary>
	/// Save the channel's data into the specified file.
	/// </summary>

	public void SaveTo (BinaryWriter writer)
	{
		writer.Write(Player.version);
		writer.Write(level);
		writer.Write(dataNode);
		writer.Write(objectCounter);
		writer.Write(password);
		writer.Write(persistent);
		writer.Write(playerLimit);

		// Record which objects are temporary and which ones are not
		for (int i = 0; i < created.size; ++i)
		{
			CreatedObject co = created[i];

			if (co.type == 1)
			{
				mCreatedOBJs.Add(co);
				mCleanedOBJs.Add(co.objectID);
			}
		}

		// Record all RFCs that don't belong to temporary objects
		for (int i = 0; i < rfcs.size; ++i)
		{
			RFC rfc = rfcs[i];
			uint objID = rfc.objectID;

			if (objID < 32768)
			{
				mCreatedRFCs.Add(rfc);
			}
			else
			{
				for (int b = 0; b < mCleanedOBJs.size; ++b)
				{
					if (mCleanedOBJs.buffer[b] == objID)
					{
						mCreatedRFCs.Add(rfc);
						break;
					}
				}
			}
		}

		writer.Write(mCreatedRFCs.size);

		for (int i = 0; i < mCreatedRFCs.size; ++i)
		{
			RFC rfc = mCreatedRFCs[i];
			writer.Write(rfc.uid);
			if (rfc.functionID == 0) writer.Write(rfc.functionName);
			writer.Write(rfc.data.size);
			if (rfc.data.size > 0) writer.Write(rfc.data.buffer, rfc.data.position, rfc.data.size);
		}

		writer.Write(mCreatedOBJs.size);

		for (int i = 0; i < mCreatedOBJs.size; ++i)
		{
			CreatedObject co = mCreatedOBJs[i];
			writer.Write(co.playerID);
			writer.Write(co.objectID);
			writer.Write(co.buffer.size);
			if (co.buffer.size > 0) writer.Write(co.buffer.buffer, co.buffer.position, co.buffer.size);
		}

		writer.Write(destroyed.size);
		for (int i = 0; i < destroyed.size; ++i) writer.Write(destroyed[i]);

		mCleanedOBJs.Clear();
		mCreatedOBJs.Clear();
		mCreatedRFCs.Clear();

		writer.Write(isLocked);
	}

	/// <summary>
	/// Load the channel's data from the specified file.
	/// </summary>

	public bool LoadFrom (BinaryReader reader)
	{
		int version = reader.ReadInt32();
		if (version < 20160207)
		{
#if UNITY_EDITOR
			UnityEngine.Debug.LogWarning("Incompatible data: " + version);
#endif
			return false;
		}

		// Clear all RFCs, just in case
		for (int i = 0; i < rfcs.size; ++i)
		{
			RFC r = rfcs[i];
			if (r.data != null) r.data.Recycle();
		}

		rfcs.Clear();
		created.Clear();
		destroyed.Clear();
		mCreatedObjectDictionary.Clear();

		level = reader.ReadString();
		dataNode = reader.ReadDataNode();
		objectCounter = reader.ReadUInt32();
		password = reader.ReadString();
		persistent = reader.ReadBoolean();
		playerLimit = reader.ReadUInt16();

		int size = reader.ReadInt32();

		for (int i = 0; i < size; ++i)
		{
			RFC rfc = new RFC();
			rfc.uid = reader.ReadUInt32();
			if (rfc.functionID == 0) rfc.functionName = reader.ReadString();
			Buffer b = Buffer.Create();
			b.BeginWriting(false).Write(reader.ReadBytes(reader.ReadInt32()));
			b.EndWriting();
			rfc.data = b;
			rfcs.Add(rfc);
		}

		size = reader.ReadInt32();

		for (int i = 0; i < size; ++i)
		{
			CreatedObject co = new CreatedObject();
			co.playerID = reader.ReadInt32();
			co.objectID = reader.ReadUInt32();
			co.type = 1;
			Buffer b = Buffer.Create();
			b.BeginWriting(false).Write(reader.ReadBytes(reader.ReadInt32()));
			b.EndWriting();
			co.buffer = b;
			AddCreatedObject(co);
		}

		size = reader.ReadInt32();

		for (int i = 0; i < size; ++i)
		{
			uint uid = reader.ReadUInt32();
			if (uid < 32768) destroyed.Add(uid);
		}

		isLocked = reader.ReadBoolean();
		return true;
	}
}
}
