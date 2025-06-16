//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2025 Tasharen Entertainment Inc
//-------------------------------------------------

using System.IO;
#if UNITY_EDITOR
using UnityEngine;
#endif

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

		public struct RFC
		{
			// Object ID (24 bytes), RFC ID (8 bytes)
			public uint uid;
			public string functionName;
			public Buffer data;

			public uint objectID { get { return (uid >> 8); } set { uid = ((value << 8) | (uid & 0xFF)); } }

			public uint functionID { get { return (uid & 0xFF); } }

			public void Recycle () { if (data != null) { data.Recycle(); data = null; } }

			public bool Matches (uint uid, in string funcName) { return uid == this.uid && funcName == functionName; }

			/// <summary>
			/// Write a complete ForwardToOthers packet to the specified buffer.
			/// </summary>

			public int WritePacket (int channelID, Buffer buffer, int offset)
			{
#if !MODDING
				if (functionID == 0 && string.IsNullOrEmpty(functionName)) return buffer.position;

				var writer = buffer.BeginPacket(Packet.ForwardToOthers, offset);
				writer.Write(0);
				writer.Write(channelID);
				writer.Write(uid);
				if (functionID == 0) writer.Write(functionName);

				if (data != null && data.size > 0)
				{
					// This already contains WriteInt() as a part of it, so no need to WriteInt(size).
					// When the client reads this, it will ReadInt() the size before processing the rest of the data.
					writer.Write(data.buffer, data.position, data.size);
				}
				else writer.WriteInt(0);

				return buffer.EndTcpPacketStartingAt(offset);
#else
				return 0;
#endif
			}
		}

		/// <summary>
		/// Created objects are saved by the channels.
		/// </summary>

		public struct CreatedObject
		{
			public int playerID;
			public uint objectID;
			public byte type;		// 1 = Persistent. 2 = Destroyed when owner leaves.
			public Buffer data;

			public void Recycle () { if (data != null) { data.Recycle(); data = null; } }
		}

		/// <summary>
		/// Channel information class created as a result of retrieving a list of channels.
		/// </summary>

		public struct Info
		{
			public int id;              // Channel's ID
			public ushort players;      // Number of players present
			public ushort limit;        // Player limit
			public bool hasPassword;    // Whether the channel is password-protected or not
			public bool isPersistent;   // Whether the channel is persistent or not
			public string level;        // Name of the loaded level
			public DataNode data;       // Data associated with the channel
		}

		public int id;
		public string password = "";
		public string level = "";
		public bool isPersistent = false;
		public bool isClosed = false;
		public bool isLocked = false;
		public bool isLeaving = false;
		public ushort playerLimit = 65535;
		public List<Player> players = new List<Player>();
		public List<RFC> rfcs = new List<RFC>();
		public List<CreatedObject> created = new List<CreatedObject>();
		public List<uint> destroyed = new List<uint>();
		public uint objectCounter = 0xFFFFFF;
		public Player host;

		[System.Obsolete("Rename to 'isPersistent'")]
		public bool persistent { get { return isPersistent; } set { isPersistent = value; } }

		[System.Obsolete("Rename to 'isClosed'")]
		public bool closed { get { return isClosed; } set { isClosed = value; } }

		// Key = Object ID. Value is 'true'. This dictionary is used for a quick lookup checking to see
		// if the object actually exists. It's used to store RFCs. RFCs for objects that don't exist are not stored.
		[System.NonSerialized]
		System.Collections.Generic.Dictionary<uint, bool> mCreatedObjectDictionary = new System.Collections.Generic.Dictionary<uint, bool>();

		// Channel data is not parsed until it's actually needed, saving memory
		[System.NonSerialized] byte[] mSource;
#if !MODDING
		int mSourceSize;
#endif
		/// <summary>
		/// Whether the channel has data that can be saved.
		/// </summary>

		public bool hasData { get { return rfcs.size > 0 || created.size > 0 || destroyed.size > 0 || dataNode != null || mSource != null; } }

		/// <summary>
		/// Whether the channel can be joined.
		/// </summary>

		public bool isOpen { get { return !isClosed && players.size < playerLimit; } }

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
				var p = players.buffer[i];
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
				var p = players.buffer[i];

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
			for (int i = 0; i < rfcs.size; ++i) rfcs.buffer[i].Recycle();
			for (int i = 0; i < created.size; ++i) created.buffer[i].Recycle();

			rfcs.Release();
			created.Release();
			destroyed.Release();
			mCreatedObjectDictionary.Clear();
			objectCounter = 0xFFFFFF;
			mSource = null;
		}

		/// <summary>
		/// Remove the specified player from the channel.
		/// </summary>

		public bool RemovePlayer (TcpPlayer p, List<uint> destroyedObjects)
		{
#if !MODDING
			destroyedObjects.Clear();

			if (players.Remove(p))
			{
				p.channels.Remove(this);

				// When the host leaves, clear the host (it gets changed in SendLeaveChannel)
				if (p == host) host = null;

				// Remove all of the non-persistent objects that were created by this player
				for (int i = 0; i < created.size;)
				{
					var r = created.buffer[i];

					if (r.playerID == p.id)
					{
						if (r.type == 2)
						{
							created.RemoveAt(i);

							var objID = r.objectID;
							r.Recycle();

							destroyedObjects.Add(objID);
							if (objID >= 32768) mCreatedObjectDictionary.Remove(objID);
							DestroyObjectRFCs(objID);
							continue;
						}

						// Give object ownership to another player
						if (players.size != 0)
						{
							r.playerID = players.buffer[0].id;
							created.buffer[i] = r;
						}
					}
					++i;
				}

				// Close the channel if it wasn't persistent
				if (players.size == 0 && (!isPersistent || isClosed || playerLimit < 1))
				{
					isClosed = true;
					Reset();
				}
				return true;
			}
#endif
			return false;
		}

		/// <summary>
		/// Export the specified object, writing its RCC and RFCs into the binary writer. Only dynamically created objects can be exported.
		/// </summary>

		public bool ExportObject (uint objID, BinaryWriter writer)
		{
#if !MODDING
			if (objID < 32768) return false;

			if (mCreatedObjectDictionary.ContainsKey(objID))
			{
				for (int i = 0; i < created.size; ++i)
				{
					var co = created.buffer[i];
					if (co.objectID != objID) continue;

					writer.Write(co.type);
					writer.Write(co.data.size);

					if (co.data.size > 0) writer.Write(co.data.buffer, co.data.position, co.data.size);

					var count = 0;

					for (int r = 0; r < rfcs.size; ++r)
					{
						var rfc = rfcs.buffer[r];
						if (rfc.objectID == objID) ++count;
					}

					writer.Write(count);

					if (count != 0)
					{
						for (int b = 0; b < rfcs.size; ++b)
						{
							var r = rfcs.buffer[b];
							if (r.objectID != objID) continue;
							if (r.functionID == 0 && string.IsNullOrEmpty(r.functionName)) continue;

							writer.Write(r.uid);
							if (r.functionID == 0) writer.Write(r.functionName);

							if (r.data != null && r.data.size > 0)
							{
								writer.Write(r.data.size);
								writer.Write(r.data.buffer, r.data.position, r.data.size);
							}
							else writer.Write(0);
						}
					}
					return true;
				}
			}
			else if (mForward.size != 0)
			{
				for (int i = 0; i < mForward.size; ++i)
				{
					if (mForward.buffer[i].objectID == objID && mForward.buffer[i].newChannel != null)
						return mForward.buffer[i].newChannel.ExportObject(mForward.buffer[i].newID, writer);
				}
			}
#endif
			return false;
		}

		/// <summary>
		/// Import a previously exported object. Returns its object ID, or '0' if failed.
		/// </summary>

		public uint ImportObject (int playerID, BinaryReader reader)
		{
#if !MODDING
			// Create a new object and read its RCC data
			var co = new CreatedObject();
			co.objectID = GetUniqueID();
			co.type = reader.ReadByte();
			co.data = Buffer.Create();
			var bytes = reader.ReadBytes(reader.ReadInt32());
			co.data.BeginWriting(false).Write(bytes);
			co.data.EndWriting();
			AddCreatedObject(co);

			// We need to inform all the players in the channel of this object's creation
			var packet = Buffer.Create();
			var writer = packet.BeginPacket(Packet.ResponseCreateObject);
			writer.Write(playerID);
			writer.Write(id);
			writer.Write(co.objectID);
			writer.Write(bytes);
			packet.EndPacket();
			SendToAll(packet);
			packet.Recycle();

			// Now read all the RFCs
			var count = reader.ReadInt32();

			if (count != 0)
			{
				for (int i = 0; i < count; ++i)
				{
					var rfc = new RFC();
					rfc.uid = reader.ReadUInt32();
					rfc.objectID = co.objectID;
					if (rfc.functionID == 0) rfc.functionName = reader.ReadString();

					var dataSize = reader.ReadInt32();

					if (dataSize > 0)
					{
						bytes = reader.ReadBytes(dataSize);

						var b = Buffer.Create();
						b.BeginWriting(false).Write(bytes);
						b.EndWriting();
						rfc.data = b;
					}

					if (rfc.functionID == 0 && string.IsNullOrEmpty(rfc.functionName)) continue;

					rfcs.Add(rfc);

					packet = Buffer.Create();
					rfc.WritePacket(id, packet, 0);
					SendToAll(packet);
					packet.Recycle();
				}
			}
			return co.objectID;
#else
			return 0;
#endif
		}

		/// <summary>
		/// Send the specified packet to all players in the channel.
		/// </summary>

		public void SendToAll (Buffer packet)
		{
			for (int i = 0; i < players.size; ++i)
			{
				var p = players.buffer[i] as TcpPlayer;
				p.SendTcpPacket(packet);
			}
		}

		/// <summary>
		/// Change the object's associated player. Only works with dynamically instantiated objects.
		/// </summary>

		public bool ChangeObjectOwner (uint objID, int playerID)
		{
#if !MODDING
			if (objID < 32768) return false;

			if (mCreatedObjectDictionary.ContainsKey(objID))
			{
				for (int i = 0; i < created.size; ++i)
				{
					var r = created.buffer[i];

					if (r.objectID == objID)
					{
						r.playerID = playerID;
						created.buffer[i] = r;
						return true;
					}
				}
			}
#endif
			return false;
		}

		/// <summary>
		/// Remove an object with the specified unique identifier.
		/// </summary>

		public bool DestroyObject (uint objID)
		{
#if !MODDING
			if (objID < 32768)
			{
				// Static objects have ID below 32768
				if (!destroyed.Contains(objID))
				{
					DestroyObjectRFCs(objID);
					destroyed.Add(objID);
					return true;
				}
			}
			else if (mCreatedObjectDictionary.Remove(objID))
			{
				// Dynamic objects are always a part of the 'created' array and the lookup table
				for (int i = 0; i < created.size; ++i)
				{
					var r = created.buffer[i];

					if (r.objectID == objID)
					{
						created.RemoveAt(i);
						DestroyObjectRFCs(objID);
						r.Recycle();
						return true;
					}
				}
			}
#endif
			return false;
		}

		/// <summary>
		/// Delete the specified remote function call.
		/// </summary>

		public void DestroyObjectRFCs (uint objectID)
		{
#if !MODDING
			for (int i = rfcs.size; i > 0;)
			{
				var r = rfcs.buffer[--i];

				if (r.objectID == objectID)
				{
					rfcs.RemoveAt(i);
					r.Recycle();
				}
			}
#endif
		}

#if !MODDING
		struct ForwardRecord
		{
			public uint objectID;
			public Channel newChannel;
			public uint newID;
			public long expiration;
		}

		List<ForwardRecord> mForward = null;

		void AddForwardRecord (uint objectID, Channel newChannel, uint newID, long expiration)
		{
			var fw = new ForwardRecord();
			fw.objectID = objectID;
			fw.newChannel = newChannel;
			fw.newID = newID;
			fw.expiration = expiration;
			if (mForward == null) mForward = new List<ForwardRecord>();
			mForward.Add(fw);
		}
#endif

		/// <summary>
		/// Transfer the specified object to another channel, changing its Object ID in the process.
		/// </summary>

		public CreatedObject? TransferObject (uint objectID, Channel newChannel, long time)
		{
#if !MODDING
			if (objectID < 32768)
			{
				Tools.LogError("Transferring objects only works with objects that were instantiated at run-time.");
			}
			else if (mCreatedObjectDictionary.Remove(objectID))
			{
				for (int i = 0; i < created.size; ++i)
				{
					var obj = created.buffer[i];

					if (obj.objectID == objectID)
					{
						// Move the created object over to the other channel
						obj.objectID = newChannel.GetUniqueID();

						// Add a new forward record for 10 seconds so that any packets that arrive for this object will automatically get redirected
						AddForwardRecord(objectID, newChannel, obj.objectID, time + 10000);

						// If the other channel doesn't contain the object's owner, assign a new owner
						var changeOwner = true;

						for (int b = 0; b < newChannel.players.size; ++b)
						{
							if (newChannel.players.buffer[b].id == obj.playerID)
							{
								changeOwner = false;
								break;
							}
						}

						if (changeOwner) obj.playerID = (newChannel.host != null) ? newChannel.host.id : 0;

						created.RemoveAt(i);
						newChannel.created.Add(obj);
						newChannel.mCreatedObjectDictionary[obj.objectID] = true;

						// Move RFCs over to the other channel
						for (int b = 0; b < rfcs.size;)
						{
							var r = rfcs.buffer[b];

							if (r.objectID == objectID)
							{
								r.objectID = obj.objectID;
								newChannel.rfcs.Add(r);
								rfcs.RemoveAt(b);
							}
							else ++b;
						}
						return obj;
					}
				}
			}
#endif
			return null;
		}

		/// <summary>
		/// Add a new saved remote function call.
		/// </summary>

		public void AddRFC (uint uid, in string funcName, Buffer b, long time)
		{
#if !MODDING
			if (isClosed) return;

			if (uid == 0 && string.IsNullOrEmpty(funcName))
			{
#if UNITY_EDITOR
				UnityEngine.Debug.LogError("Trying to add an RFC without an ID or function name");
#else
				Tools.LogError("Trying to add an RFC without an ID or function name");
#endif
				return;
			}

			uint objID = (uid >> 8);

			if (objID < 32768) // Static object ID
			{
				// Ignore objects that were marked as deleted
				if (destroyed.Contains(objID)) return;
			}
			else if (!mCreatedObjectDictionary.ContainsKey(objID))
			{
				if (mForward != null)
				{
					for (int i = 0; i < mForward.size; ++i)
					{
						var r = mForward.buffer[i];

						if (r.objectID == objID)
						{
							// Redirect this packet
							r.newChannel.AddRFC((r.newID << 8) | (uid & 0xFF), funcName, b, time);
							return;
						}
						else if (r.expiration < time)
						{
							// Expired entry -- remove it
							mForward.RemoveAt(i--);
							if (mForward.size == 0) { mForward = null; break; }
						}
					}
				}
				return; // This object doesn't exist
			}

			var sz = (b != null) ? b.size : 0;

			Buffer newData = null;

			if (sz > 0)
			{
				newData = Buffer.Create();
				newData.BeginWriting(false).Write(b.buffer, b.position, sz);
				newData.EndWriting();
			}

			for (int i = 0; i < rfcs.size; ++i)
			{
				var r = rfcs.buffer[i];

				if (r.Matches(uid, funcName))
				{
					// RFC's position will be changed
					rfcs.RemoveAt(i);

					r.Recycle();
					r.data = newData;

					// Move this RFC to the end of the list so that it gets called in correct order on load
					rfcs.Add(r);
					return;
				}
			}

			var rfc = new RFC();
			rfc.uid = uid;
			rfc.functionName = funcName;
			rfc.data = newData;
			rfcs.Add(rfc);
#endif
		}

		/// <summary>
		/// Delete the specified remote function call.
		/// </summary>

		public void DeleteRFC (uint uid, in string funcName, long time)
		{
#if !MODDING
			for (int i = 0; i < rfcs.size; ++i)
			{
				var r = rfcs.buffer[i];

				if (r.Matches(uid, funcName))
				{
					rfcs.RemoveAt(i);
					r.Recycle();
					return;
				}
			}

			if (mForward != null)
			{
				uint objID = (uid >> 8);

				for (int i = 0; i < mForward.size; ++i)
				{
					var r = mForward.buffer[i];

					if (r.objectID == objID)
					{
						// Redirect this packet
						r.newChannel.DeleteRFC((r.newID << 8) | (uid & 0xFF), funcName, time);
						return;
					}
					else if (r.expiration < time)
					{
						// Expired entry -- remove it
						mForward.RemoveAt(i--);
						if (mForward.size == 0) { mForward = null; break; }
					}
				}
			}
#endif
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
#if !MODDING
			if (mSource != null)
			{
				writer.Write(mSource, 0, mSourceSize);
				return;
			}

			writer.Write(Player.version);
			writer.Write(level);
			writer.Write(dataNode);
			writer.Write(objectCounter);
			writer.Write(password);
			writer.Write(isPersistent);
			writer.Write(playerLimit);

			// Record which objects are temporary and which ones are not
			for (int i = 0; i < created.size; ++i)
			{
				var co = created.buffer[i];

				if (co.type == 1) // Only persistent objects should be getting saved
				{
					mCreatedOBJs.Add(co);
					mCleanedOBJs.Add(co.objectID);
				}
			}

			// Record all RFCs that don't belong to temporary objects
			for (int i = 0; i < rfcs.size; ++i)
			{
				var objID = rfcs.buffer[i].objectID;

				if (objID < 32768)
				{
					mCreatedRFCs.Add(rfcs.buffer[i]);
				}
				else
				{
					for (int b = 0; b < mCleanedOBJs.size; ++b)
					{
						if (mCleanedOBJs.buffer[b] == objID)
						{
							mCreatedRFCs.Add(rfcs.buffer[i]);
							break;
						}
					}
				}
			}

			writer.Write(mCreatedRFCs.size);

			for (int i = 0; i < mCreatedRFCs.size; ++i)
			{
				var rfc = mCreatedRFCs.buffer[i];
				writer.Write(rfc.uid);
				if (rfc.functionID == 0) writer.Write(string.IsNullOrEmpty(rfc.functionName) ? "DoNothing" : rfc.functionName);

				var sz = rfc.data.size;
				writer.Write(sz); // Technically the size is already inside the buffer, and can be retrieved via ReadInt()...
				if (sz > 0) writer.Write(rfc.data.buffer, rfc.data.position, sz);
			}

			writer.Write(mCreatedOBJs.size);

			for (int i = 0; i < mCreatedOBJs.size; ++i)
			{
				var co = mCreatedOBJs.buffer[i];
				writer.Write(co.playerID);
				writer.Write(co.objectID);

				var sz = co.data.size;
				writer.Write(sz); // Technically the size is already inside the buffer, and can be retrieved via ReadInt()...
				if (sz > 0) writer.Write(co.data.buffer, co.data.position, sz);
			}

			writer.Write(destroyed.size);
			for (int i = 0; i < destroyed.size; ++i) writer.Write(destroyed.buffer[i]);

			mCleanedOBJs.Clear();
			mCreatedOBJs.Clear();
			mCreatedRFCs.Clear();

			writer.Write(isLocked);
#endif
		}

		/// <summary>
		/// Load the channel's data from the specified file.
		/// </summary>

		public bool LoadFrom (BinaryReader reader, bool keepInMemory = false)
		{
#if !MODDING
			var start = reader.BaseStream.Position;
			int version = reader.ReadInt32();

			if (version < 20160207)
			{
#if UNITY_EDITOR
				UnityEngine.Debug.LogWarning("Incompatible data: " + version);
#endif
				return false;
			}

			// Clear all RFCs, just in case
			for (int i = 0; i < rfcs.size; ++i) rfcs.buffer[i].Recycle();

			rfcs.Clear();
			created.Clear();
			destroyed.Clear();
			mCreatedObjectDictionary.Clear();

			level = reader.ReadString();
			dataNode = reader.ReadDataNode();
			objectCounter = reader.ReadUInt32();
			password = reader.ReadString();
			isPersistent = reader.ReadBoolean();
			playerLimit = reader.ReadUInt16();

			int size = reader.ReadInt32();

			for (int i = 0; i < size; ++i)
			{
				var rfc = new RFC();
				rfc.uid = reader.ReadUInt32();
				if (rfc.functionID == 0) rfc.functionName = reader.ReadString();

				var b = Buffer.Create();
				var sz = reader.ReadInt32();
				var bt = (sz > 0) ? reader.ReadBytes(sz) : null;
				var w = b.BeginWriting(false);
				if (bt != null) w.Write(bt);
				else w.WriteInt(0);

				b.EndWriting();

				if (rfc.functionID == 0 && string.IsNullOrEmpty(rfc.functionName))
				{
					b.Recycle();
					continue;
				}

				rfc.data = b;
				rfcs.Add(rfc);
			}

			size = reader.ReadInt32();

			for (int i = 0; i < size; ++i)
			{
				var co = new CreatedObject();
				co.playerID = reader.ReadInt32();
				co.objectID = reader.ReadUInt32();
				co.playerID = 0; // The player ID is no longer valid as player IDs reset on reload
				co.type = 1; // Only persistent objects get saved to the file

				var b = Buffer.Create();
				b.BeginWriting(false).Write(reader.ReadBytes(reader.ReadInt32()));
				b.EndWriting();
				co.data = b;
				AddCreatedObject(co);
			}

			size = reader.ReadInt32();

			for (int i = 0; i < size; ++i)
			{
				uint uid = reader.ReadUInt32();
				if (uid < 32768) destroyed.Add(uid);
			}

			isLocked = reader.ReadBoolean();
			mSource = null;

			if (!keepInMemory && players.size == 0)
			{
				Reset();
				var end = reader.BaseStream.Position;
				reader.BaseStream.Position = start;
				mSourceSize = (int)(end - start);
				mSource = reader.ReadBytes(mSourceSize);
			}
#endif
			return true;
		}

		/// <summary>
		/// When channels have no players in them, they can be put to sleep in order to reduce the server's memory footprint.
		/// </summary>

		public void Sleep ()
		{
#if !MODDING
			if (mSource == null && players.size == 0)
			{
				var ms = new MemoryStream();
				var writer = new BinaryWriter(ms);
				SaveTo(writer);
				Reset();
				mSourceSize = (int)ms.Position;

				if (mSourceSize > 0)
				{
					mSource = ms.GetBuffer();
					System.Array.Resize(ref mSource, mSourceSize);
				}
			}
#endif
		}

		/// <summary>
		/// Ensure that the channel's data has been loaded.
		/// </summary>

		public void Wake ()
		{
#if !MODDING
			if (mSource != null)
			{
				var stream = new MemoryStream(mSource);
				var reader = new BinaryReader(stream);
				LoadFrom(reader, true);
				reader.Close();
				mSource = null;
			}
#endif
		}
	}
}
