//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
//-------------------------------------------------

//#define COUNT_PACKETS

using System.IO;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;
using UnityTools = TNet.UnityTools;

namespace TNet
{
	/// <summary>
	/// Tasharen Network Object makes it possible to easily send and receive remote function calls.
	/// Unity networking calls this type of object a "Network View".
	/// </summary>

	[ExecuteInEditMode]
	public sealed class TNObject : MonoBehaviour, IStartable
	{
		// List of network objs to iterate through (key = channel ID, value = list of objects)
		static Dictionary<int, List<TNObject>> mList = new Dictionary<int, List<TNObject>>();

		// List of network objs to quickly look up
		static Dictionary<int, Dictionary<uint, TNObject>> mDictionary = new Dictionary<int, Dictionary<uint, TNObject>>();
		static Stack<TNet.List<TNObject>> mUnused;

		/// <summary>
		/// Unique Network Identifier. All TNObjects have them and is how messages arrive at the correct destination.
		/// The ID is supposed to be a 'uint', but Unity is not able to serialize 'uint' types. Sigh.
		/// </summary>

		[SerializeField, UnityEngine.Serialization.FormerlySerializedAs("id")] int mStaticID = 0;
		[System.NonSerialized] uint mDynamicID = 0;

		// ID of the player that created this object. May or may not actually be connected.
		[System.NonSerialized] public int creatorPlayerID = 0;

		/// <summary>
		/// If set to 'true', missing RFCs won't produce warning messages.
		/// </summary>

		[Tooltip("If set to 'true', missing RFCs won't produce warning messages")]
		public bool ignoreWarnings = false;

		/// <summary>
		/// ID of the channel this TNObject belongs to.
		/// </summary>

		[System.NonSerialized] [HideInInspector] public int channelID = 0;

		/// <summary>
		/// When set to 'true', it will cause the list of remote function calls to be rebuilt next time they're needed.
		/// </summary>

		[System.NonSerialized] [HideInInspector] public bool rebuildMethodList = true;

		// Cached RFC functions
		[System.NonSerialized] Dictionary<int, CachedFunc> mDict0 = new Dictionary<int, CachedFunc>();
		[System.NonSerialized] Dictionary<string, CachedFunc> mDict1 = new Dictionary<string, CachedFunc>();

		// Whether the object has been registered with the lists
		[System.NonSerialized] bool mIsRegistered = false;

		// ID of the object's owner
		[System.NonSerialized] Player mOwner = null;

		/// <summary>
		/// When objects get destroyed, they immediately get marked as such so that no RFCs go out between the destroy call
		/// and the response coming back from the server.
		/// </summary>

		[System.NonSerialized] byte mDestroyed = 0;
		[System.NonSerialized] bool mHasBeenRegistered = false;
		[System.NonSerialized] DataNode mData = null;
		[System.NonSerialized] bool mCallDataChanged = false;
		[System.NonSerialized] bool mSettingData = false;

		/// <summary>
		/// Object's unique identifier (Static object IDs range 1 to 32767. Dynamic object IDs range from 32,768 to 16,777,215).
		/// </summary>

		public uint uid
		{
			get
			{
				return (mDynamicID != 0 ? mDynamicID : (uint)mStaticID);
			}
			set
			{
				mDynamicID = value;
				mStaticID = 0;
			}
		}

		/// <summary>
		/// Channel + UID in a single value.
		/// </summary>

		public ulong fullID { get { return ((ulong)channelID << 32) | uid; } }

		/// <summary>
		/// Whether the player is currently joining this object's channel.
		/// </summary>

		public bool isJoiningChannel { get { return TNManager.IsJoiningChannel(channelID); } }

		/// <summary>
		/// Whether the object has been destroyed. It can happen when the object has been requested to be
		/// transferred to another channel, but has not yet completed the operation.
		/// </summary>

		public bool hasBeenDestroyed { get { return mDestroyed != 0; } }

		/// <summary>
		/// An object gets marked as registered after the creation call has completed and the object's ID has been assigned.
		/// </summary>

		public bool hasBeenRegistered { get { return mHasBeenRegistered; } }

		/// <summary>
		/// Whether sending messages through this object is possible or not.
		/// </summary>

		public bool canSend
		{
			get
			{
				if (!TNManager.isConnected) return true;

				if (uid != 0 && !hasBeenDestroyed)
				{
					if (mNextChannelID != 0 && TNManager.IsInChannel(mNextChannelID, true)) return true;
					return TNManager.IsInChannel(channelID);
				}
				return false;
			}
		}

		/// <summary>
		/// If you want to know when this object is getting destroyed, subscribe to this delegate.
		/// This delegate is guaranteed to be called before OnDestroy() notifications get sent out.
		/// This is useful if you want parts of the object to remain behind (such as buggy Unity 4 cloth).
		/// </summary>

		[System.NonSerialized] public OnDestroyCallback onDestroy;
		public delegate void OnDestroyCallback ();

		/// <summary>
		/// Delegate called when the object is being transferred to another channel.
		/// </summary>

		[System.NonSerialized] public OnTransfer onTransfer;
		public delegate void OnTransfer (int newChannel, uint newID);

		/// <summary>
		/// Whether this object belongs to the player.
		/// </summary>

		public bool isMine
		{
			get { return owner == TNManager.player; }
			set { if (value) owner = TNManager.player; }
		}

		/// <summary>
		/// ID of the player that owns this object.
		/// </summary>

		public int ownerID
		{
			get
			{
				if (mOwner != null) return mOwner.id;
				if (uid == 0) return TNManager.playerID;
				var host = TNManager.GetHost(channelID);
				if (host != null) return host.id;
				return 0;
			}
			set
			{
				if (ownerID != value)
				{
					if (value == 0 || TNManager.IsPlayerInChannel(value, channelID))
					{
						// /exe var bw = TNManager.BeginSend(Packet.RequestSetOwner); bw.Write(FactionOutpost.closestToPlayer.tno.channelID); bw.Write(FactionOutpost.closestToPlayer.tno.uid); bw.Write(TNManager.playerID); TNManager.EndSend();
						// /exe FactionOutpost.closestToPlayer.tno.ownerID = TNManager.playerID;
						var bw = TNManager.BeginSend(Packet.RequestSetOwner);
						bw.Write(channelID);
						bw.Write(uid);
						bw.Write(value);
						TNManager.EndSend();
#if UNITY_EDITOR && COUNT_PACKETS
						if (sentDictionary.ContainsKey("ownerID")) ++sentDictionary["ownerID"];
						else sentDictionary["ownerID"] = 1;
#endif
					}
#if UNITY_EDITOR
					else Debug.LogWarning("Trying to assign an object's owner to someone who isn't in the object's channel", this);
#endif
				}
			}
		}

		/// <summary>
		/// ID of the player that owns this object.
		/// </summary>

		public Player owner
		{
			get
			{
				if (mOwner != null) return mOwner;
				if (uid == 0) return TNManager.player;
				return TNManager.GetHost(channelID);
			}
			set
			{
				ownerID = (value != null ? value.id : 0);
			}
		}

		public void OnChangeOwnerPacket (Player p) { mOwner = p; }

		/// <summary>
		/// Object's DataNode synchronized using TNObject.Set commands. It's better to retrieve data using TNObject.Get instead of checking the node directly.
		/// Note that setting the entire data node is only possible during the object creation (RCC). After that the individual Set functions should be used.
		/// </summary>

		public DataNode dataNode
		{
			get
			{
				return mData;
			}
			set
			{
				if (!mHasBeenRegistered)
				{
					mData = value;
					mCallDataChanged = true;
				}
			}
		}

		/// <summary>
		/// Get the object-specific child data node.
		/// </summary>

		public DataNode Get (string name) { return (mData != null ? mData.GetChild(name) : null); }

		/// <summary>
		/// Get the object-specific data.
		/// </summary>

		public T Get<T> (string name) { return (mData != null) ? mData.GetChild<T>(name) : default(T); }

		/// <summary>
		/// Get the object-specific data.
		/// </summary>

		public T Get<T> (string name, T defVal) { return (mData != null) ? mData.GetChild<T>(name, defVal) : defVal; }

		/// <summary>
		/// Set the object-specific data.
		/// </summary>

		public void Set (string name, object val)
		{
			if (mData == null) mData = new DataNode("ObjectData");
			mData.SetHierarchy(name, val);

			if (!mSettingData)
			{
				mSettingData = true;
				mCallDataChanged = false;
				if (onDataChanged != null) onDataChanged(mData);
				mSettingData = false;
			}

			if (enabled && uid != 0)
			{
				if (isMine) Send(2, Target.OthersSaved, mData);
				else Send("OnSet", ownerID, name, val);
			}
		}

		/// <summary>
		/// Can be used after modifying the data directly. Will sync all of the object's data.
		/// </summary>

		public void SyncData ()
		{
			if (mData == null) return;

			if (!mSettingData)
			{
				mSettingData = true;
				mCallDataChanged = false;
				if (onDataChanged != null) onDataChanged(mData);
				mSettingData = false;
			}

			if (enabled && uid != 0)
			{
				if (isMine) Send(2, Target.OthersSaved, mData);
				else Send("OnSet", ownerID, null, mData);
			}
		}

		[RFC]
		void OnSet (string name, object val)
		{
			if (string.IsNullOrEmpty(name))
			{
				mData = val as DataNode;
				if (mData == null) mData = new DataNode("ObjectData");
			}
			else
			{
				if (mData == null) mData = new DataNode("ObjectData");
				mData.SetHierarchy(name, val);
			}

			OnSetData(mData);
			Send(2, Target.OthersSaved, mData);
		}

		[RFC(2)]
		void OnSetData (DataNode data)
		{
			if (!mSettingData)
			{
				mSettingData = true;
				mCallDataChanged = false;
				mData = data;
				if (onDataChanged != null) onDataChanged(data);
				mSettingData = false;
			}
		}

		/// <summary>
		/// Callback triggered the the object's get/set data changes.
		/// </summary>

		public OnDataChanged onDataChanged;
		public delegate void OnDataChanged (DataNode data);

		/// <summary>
		/// Set this to 'true' if you plan on destroying the object yourself and don't want it to be destroyed immediately after onDestroy has been called.
		/// This is useful if you want to destroy the object yourself after performing some action such as fracturing it into pieces.
		/// </summary>

		[System.NonSerialized] public bool ignoreDestroyCall = false;

		/// <summary>
		/// Destroy this game object on all connected clients and remove it from the server.
		/// </summary>

		[ContextMenu("Destroy")]
		public void DestroySelf ()
		{
			if (isJoiningChannel) StartCoroutine("DestroyAfterJoin");
			else DestroyNow();
		}

		System.Collections.IEnumerator DestroyAfterJoin ()
		{
			while (isJoiningChannel) yield return null;
			DestroyNow();
		}

		void DestroyNow ()
		{
			if (mDestroyed != 0) return;
			mDestroyed = 1;

			if (!enabled)
			{
				Unregister();
				Object.Destroy(gameObject);
			}
			else if (uid == 0)
			{
				OnDestroyPacket();
			}
			else if (TNManager.isConnected && TNManager.IsInChannel(channelID))
			{
				if (TNManager.IsChannelLocked(channelID))
				{
					Debug.LogWarning("Trying to destroy an object in a locked channel. Call will be ignored.");
				}
				else
				{
					BinaryWriter bw = TNManager.BeginSend(Packet.RequestDestroyObject);
					bw.Write(channelID);
					bw.Write(uid);
					TNManager.EndSend(channelID, true);
#if UNITY_EDITOR && COUNT_PACKETS
					if (sentDictionary.ContainsKey("DestroyNow")) ++sentDictionary["DestroyNow"];
					else sentDictionary["DestroyNow"] = 1;
#endif
				}
			}
			else OnDestroyPacket();
		}

		/// <summary>
		/// Notification of the object needing to be destroyed immediately.
		/// </summary>

		internal void OnDestroyPacket ()
		{
			if (!enabled)
			{
				Unregister();
				Object.Destroy(gameObject);
			}
			else if (mDestroyed < 2)
			{
				mDestroyed = 2;
				if (onDestroy != null) onDestroy();
				Unregister();

				if (ignoreDestroyCall)
				{
					enabled = false;
					mDestroyed = 3;
				}
				else Object.Destroy(gameObject);
			}
			else
			{
				Unregister();
				Object.Destroy(gameObject);
			}
		}

		/// <summary>
		/// Destroy this game object on all connected clients and remove it from the server.
		/// </summary>

		public void DestroySelf (float delay, bool onlyIfOwner = true)
		{
			if (onlyIfOwner) Invoke("DestroyIfMine", delay);
			else Invoke("DestroySelf", delay);
		}

		void DestroyIfMine () { if (isMine) DestroySelf(); }

		/// <summary>
		/// Remember the object's ownership, for convenience.
		/// </summary>

		void Awake ()
		{
			mOwner = TNManager.isConnected ? TNManager.currentObjectOwner : TNManager.player;
			channelID = TNManager.lastChannelID;
			creatorPlayerID = TNManager.packetSourceID;
			TNUpdater.AddStart(this);
		}

		/// <summary>
		/// Automatically transfer the ownership. The same action happens on the server.
		/// </summary>

		static internal void OnPlayerLeave (int channelID, Player p)
		{
			if (p == null) return;

			List<TNObject> list;
			if (!mList.TryGetValue(channelID, out list)) return;

			for (int i = 0; i < list.size; ++i)
			{
				var item = list.buffer[i];
				if (item.mOwner == p) item.mOwner = TNManager.GetHost(channelID);
			}
		}

		/// <summary>
		/// Retrieve the Tasharen Network Object by ID.
		/// </summary>

		static public TNObject Find (int channelID, uint tnID)
		{
#if !MODDING
			if (mDictionary == null) return null;
			TNObject tno = null;
			Dictionary<uint, TNObject> dict;
			if (!mDictionary.TryGetValue(channelID, out dict)) return FindForwardRecord(channelID, tnID);
			if (!dict.TryGetValue(tnID, out tno)) return FindForwardRecord(channelID, tnID);
			return tno;
#else
			return null;
#endif
		}

#if !MODDING
		struct ForwardRecord
		{
			public int oldChannelID;
			public uint oldObjectID;
			public int newChannelID;
			public uint newObjectID;
			public long expiration;

			public bool Matches (int channel, uint obj) { return oldChannelID == channel && oldObjectID == obj; }
		}

		[System.NonSerialized] static List<ForwardRecord> mForwardRecords;

		static TNObject FindForwardRecord (int channelID, uint tnID)
		{
			if (mForwardRecords == null) return null;

			var time = TNManager.serverTime;

			for (int i = 0; i < mForwardRecords.size; ++i)
			{
				if (mForwardRecords.buffer[i].expiration < time)
				{
					mForwardRecords.RemoveAt(i--);
				}
				else if (mForwardRecords.buffer[i].Matches(channelID, tnID))
				{
					return Find(mForwardRecords.buffer[i].newChannelID, mForwardRecords.buffer[i].newObjectID);
				}
			}
			return null;
		}
#endif

		/// <summary>
		/// Retrieve the Tasharen Network Object by its full ID.
		/// </summary>

		static public TNObject Find (ulong fullID)
		{
			var ch = (int)(fullID >> 32);
			var id = (uint)(fullID & 0xFFFFFFFF);
			return Find(ch, id);
		}

		// Last used ID
		[System.NonSerialized] static uint mLastID = 0;
		[System.NonSerialized] static uint mLastDynID = 16777216;

		/// <summary>
		/// Get a new unique object identifier.
		/// </summary>

		static public uint GetUniqueID (bool isDynamic)
		{
			if (isDynamic)
			{
				foreach (var pair in mList)
				{
					var list = pair.Value;

					for (int i = 0; i < list.size; ++i)
					{
						var ts = list.buffer[i];
						if (ts != null && ts.uid < mLastDynID && ts.uid > 32767) mLastDynID = ts.uid;
					}
				}
				return --mLastDynID;
			}
			else
			{
				foreach (var pair in mList)
				{
					var list = pair.Value;

					for (int i = 0; i < list.size; ++i)
					{
						var ts = list.buffer[i];
						if (ts != null && ts.uid > mLastID && ts.uid < 32768) mLastID = ts.uid;
					}
				}
				return ++mLastID;
			}
		}

		/// <summary>
		/// Called by TNManager when loading a new level. All objects belonging to the previous level need to be destroyed.
		/// </summary>

		static internal void CleanupChannelObjects (int channelID)
		{
			List<TNObject> list;
			if (!mList.TryGetValue(channelID, out list)) return;

			for (int i = 0; i < list.size; ++i)
			{
				var item = list.buffer[i];
				if (item) Destroy(item.gameObject);
			}

			if (mUnused == null) mUnused = new Stack<List<TNObject>>();
			list.Clear();
			mUnused.Push(list);

			mList.Remove(channelID);
			mDictionary.Remove(channelID);
		}

#if UNITY_EDITOR
		/// <summary>
		/// Helper function that returns the game object's hierarchy in a human-readable format.
		/// </summary>

		static public string GetHierarchy (GameObject obj)
		{
			string path = obj.name;

			while (obj.transform.parent != null)
			{
				obj = obj.transform.parent.gameObject;
				path = obj.name + "/" + path;
			}
			return "\"" + path + "\"";
		}

		/// <summary>
		/// Make sure that this object's ID is actually unique.
		/// </summary>

		void UniqueCheck ()
		{
			if (uid == 0)
			{
				if (!Application.isPlaying) uid = GetUniqueID(false);
				else Debug.LogError("All TNObjects must be instantiated via TNManager.Instantiate, or network communication is not going to be possible.", this);
			}
			else
			{
				TNObject tobj = Find(channelID, uid);

				if (tobj != null && tobj != this)
				{
					if (Application.isPlaying)
					{
						if (tobj != null)
						{
							Debug.LogError("Network ID " + channelID + "." + uid + " is already in use by " +
								GetHierarchy(tobj.gameObject) +
								".\nPlease make sure that the network IDs are unique.", tobj.gameObject);
							Destroy(gameObject);
						}
						else
						{
							Debug.LogError("Network ID of 0 is used by " + GetHierarchy(gameObject) +
								"\nPlease make sure that a unique non-zero ID is given to all objects.", this);
							uid = GetUniqueID(false);
						}
					}
				}
			}
		}

		[ContextMenu("Export data as Text")]
		void ExportDataText () { dataNode.Write("data.txt", DataNode.SaveType.Text); }

		[ContextMenu("Export data as Binary")]
		void ExportDataBin () { dataNode.Write("data.bin", DataNode.SaveType.Binary); }
#endif

		/// <summary>
		/// Register the object with the lists.
		/// </summary>

		public void OnStart ()
		{
			if (uid == 0 && !TNManager.isConnected && Application.isPlaying)
			{
				uid = GetUniqueID(true);
			}

			if (uid != 0)
			{
				Register();

				if (mCallDataChanged && onDataChanged != null)
				{
					mCallDataChanged = false;
					onDataChanged(mData);
				}
			}
		}

		/// <summary>
		/// Remove this object from the list. Should be already called by leaving the channel.
		/// </summary>

		//void OnDestroy () { Unregister(); }

		/// <summary>
		/// Register the network object with the lists.
		/// </summary>

		public void Register ()
		{
			if (!mIsRegistered)
			{
				if (uid != 0)
				{
#if UNITY_EDITOR
					UniqueCheck();
#endif
					Dictionary<uint, TNObject> dict;

					if (!mDictionary.TryGetValue(channelID, out dict) || dict == null)
					{
						dict = new Dictionary<uint, TNObject>();
						mDictionary[channelID] = dict;
					}

					dict[uid] = this;

					TNet.List<TNObject> list;

					if (!mList.TryGetValue(channelID, out list) || list == null)
					{
						if (mUnused != null && mUnused.Count > 0) list = mUnused.Pop();
						else list = new TNet.List<TNObject>();
						mList[channelID] = list;
					}

					list.Add(this);
					mIsRegistered = true;
				}
			}

			mHasBeenRegistered = true;
		}

		/// <summary>
		/// Unregister the network object.
		/// </summary>

		internal void Unregister ()
		{
			if (mIsRegistered)
			{
				if (mDictionary != null)
				{
					Dictionary<uint, TNObject> dict;

					if (mDictionary.TryGetValue(channelID, out dict) && dict != null)
					{
						dict.Remove(uid);
						if (dict.Count == 0) mDictionary.Remove(channelID);
					}
				}

				if (mList != null)
				{
					TNet.List<TNObject> list;

					if (mList.TryGetValue(channelID, out list) && list != null)
					{
						list.Remove(this);

						if (list.size == 0)
						{
							if (mUnused == null) mUnused = new Stack<List<TNObject>>();
							mUnused.Push(list);
							mList.Remove(channelID);
						}
					}
				}

				mIsRegistered = false;
			}
		}

		/// <summary>
		/// Find an RFC function by its ID.
		/// </summary>

		public CachedFunc FindFunction (byte funcID)
		{
			if (rebuildMethodList) RebuildMethodList();
			CachedFunc ent = null;
			if (mDict0 != null) mDict0.TryGetValue(funcID, out ent);
			return ent;
		}

		/// <summary>
		/// Find an RFC function by its name.
		/// </summary>

		public CachedFunc FindFunction (string funcName)
		{
			if (rebuildMethodList) RebuildMethodList();
			CachedFunc ent = null;
			if (mDict1 != null) mDict1.TryGetValue(funcName, out ent);
			return ent;
		}

		/// <summary>
		/// Invoke the function specified by the ID.
		/// </summary>

		public bool Execute (byte funcID, params object[] parameters)
		{
			if (rebuildMethodList) RebuildMethodList();

			CachedFunc ent;

			if (mDict0 != null && mDict0.TryGetValue(funcID, out ent))
			{
				ent.Execute(parameters);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Invoke the function specified by the function name.
		/// </summary>

		public bool Execute (string funcName, params object[] parameters)
		{
			if (rebuildMethodList) RebuildMethodList();

			CachedFunc ent;

			if (mDict1 != null && mDict1.TryGetValue(funcName, out ent))
			{
				ent.Execute(parameters);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Invoke the specified function. It's unlikely that you will need to call this function yourself.
		/// </summary>

		static public void FindAndExecute (int channelID, uint objID, byte funcID, params object[] parameters)
		{
			var obj = Find(channelID, objID);

			if (obj != null)
			{
				if (obj.Execute(funcID, parameters)) return;
#if UNITY_EDITOR
				if (!obj.ignoreWarnings && !TNManager.IsJoiningChannel(channelID))
					Debug.LogWarning("[TNet] Unable to execute function with ID of '" + funcID + "'. Make sure there is a script that can receive this call.\n" +
						"GameObject: " + GetHierarchy(obj.gameObject), obj.gameObject);
#endif
			}
//#if UNITY_EDITOR
//			else
//			{
//				Debug.LogWarning("[TNet] Trying to execute RFC #" + funcID + " on TNObject #" + objID + " before it has been created in channel " + channelID);
//			}
//#endif
		}

		/// <summary>
		/// Invoke the specified function. It's unlikely that you will need to call this function yourself.
		/// </summary>

		static public void FindAndExecute (int channelID, uint objID, string funcName, params object[] parameters)
		{
			var obj = Find(channelID, objID);

			if (obj != null)
			{
				if (obj.Execute(funcName, parameters)) return;
#if UNITY_EDITOR
				if (!obj.ignoreWarnings && !TNManager.IsJoiningChannel(channelID))
					Debug.LogWarning("[TNet] Unable to execute function '" + funcName + "'. Did you forget an [RFC] prefix, perhaps?\n" +
						"GameObject: " + GetHierarchy(obj.gameObject), obj.gameObject);
#endif
			}
//#if UNITY_EDITOR
//			else
//			{
//				Debug.LogWarning("[TNet] Trying to execute a function '" + funcName + "' on TNObject #" + objID + " before it has been created in channel " + channelID);
//			}
//#endif
		}

		[System.NonSerialized] static System.Collections.Generic.List<MonoBehaviour> mTempMono = new System.Collections.Generic.List<MonoBehaviour>();
		[System.NonSerialized]
		static Dictionary<System.Type, System.Collections.Generic.List<CachedMethodInfo>> mMethodCache =
			new Dictionary<System.Type, System.Collections.Generic.List<CachedMethodInfo>>();

		public struct CachedMethodInfo
		{
			public string name;
			public CachedFunc cf;
			public RFC rfc;
		}

		/// <summary>
		/// Rebuild the list of known RFC calls.
		/// </summary>

		void RebuildMethodList ()
		{
			rebuildMethodList = false;
			if (mDict0 != null) mDict0.Clear();
			else mDict0 = new Dictionary<int, CachedFunc>();
			if (mDict1 != null) mDict1.Clear();
			else mDict1 = new Dictionary<string, CachedFunc>();
			GetComponentsInChildren(true, mTempMono);

			for (int i = 0, imax = mTempMono.Count; i < imax; ++i)
			{
				var mb = mTempMono[i];
				var type = mb.GetType();
				System.Collections.Generic.List<CachedMethodInfo> ret;

				if (!mMethodCache.TryGetValue(type, out ret))
				{
					ret = new System.Collections.Generic.List<CachedMethodInfo>();
					var cache = type.GetCache().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

					for (int b = 0, bmax = cache.Count; b < bmax; ++b)
					{
						var ent = cache.buffer[b];
						if (!ent.method.IsDefined(typeof(RFC), true)) continue;

						var ci = new CachedMethodInfo();
						ci.name = ent.name;
						ci.rfc = (RFC)ent.method.GetCustomAttributes(typeof(RFC), true)[0];

						ci.cf = new CachedFunc();
						ci.cf.mi = ent.method;

						ret.Add(ci);
					}

					mMethodCache.Add(type, ret);
				}

				for (int b = 0, bmax = ret.Count; b < bmax; ++b)
				{
					var ci = ret[b];

					var ent = new CachedFunc();
					ent.obj = mb;
					ent.mi = ci.cf.mi;

					if (ci.rfc.id > 0)
					{
						if (ci.rfc.id < 256) mDict0[ci.rfc.id] = ent;
						else Debug.LogError("RFC IDs need to be between 1 and 255 (1 byte). If you need more, just don't specify an ID and use the function's name instead.");
						mDict1[ci.name] = ent;
					}
					else if (ci.rfc.property != null)
					{
						mDict1[ci.name + "/" + ci.rfc.GetUniqueID(mb)] = ent;
					}
					else mDict1[ci.name] = ent;
				}
			}
		}

#region Send functions
		[System.NonSerialized] static object[] mObj1;
		[System.NonSerialized] static object[] mObj2;
		[System.NonSerialized] static object[] mObj3;
		[System.NonSerialized] static object[] mObj4;
		[System.NonSerialized] static object[] mObj5;
		[System.NonSerialized] static object[] mObj6;
		[System.NonSerialized] static object[] mObj7;

		/// <summary>
		/// Send a remote function call.
		/// </summary>

		public void Send (byte rfcID, Target target, params object[] objs) { SendRFC(rfcID, null, target, true, objs); }

		public void Send (byte rfcID, Target target) { SendRFC(rfcID, null, target, true, null); }

		public void Send (byte rfcID, Target target, object obj0)
		{
			if (mObj1 == null) mObj1 = new object[1];
			mObj1[0] = obj0;
			SendRFC(rfcID, null, target, true, mObj1);
		}

		public void Send (byte rfcID, Target target, object obj0, object obj1)
		{
			if (mObj2 == null) mObj2 = new object[2];
			mObj2[0] = obj0;
			mObj2[1] = obj1;
			SendRFC(rfcID, null, target, true, mObj2);
		}

		public void Send (byte rfcID, Target target, object obj0, object obj1, object obj2)
		{
			if (mObj3 == null) mObj3 = new object[3];
			mObj3[0] = obj0;
			mObj3[1] = obj1;
			mObj3[2] = obj2;
			SendRFC(rfcID, null, target, true, mObj3);
		}

		public void Send (byte rfcID, Target target, object obj0, object obj1, object obj2, object obj3)
		{
			if (mObj4 == null) mObj4 = new object[4];
			mObj4[0] = obj0;
			mObj4[1] = obj1;
			mObj4[2] = obj2;
			mObj4[3] = obj3;
			SendRFC(rfcID, null, target, true, mObj4);
		}

		public void Send (byte rfcID, Target target, object obj0, object obj1, object obj2, object obj3, object obj4)
		{
			if (mObj5 == null) mObj5 = new object[5];
			mObj5[0] = obj0;
			mObj5[1] = obj1;
			mObj5[2] = obj2;
			mObj5[3] = obj3;
			mObj5[4] = obj4;
			SendRFC(rfcID, null, target, true, mObj5);
		}

		/// <summary>
		/// Send a remote function call.
		/// Note that you should not use this version of the function if you care about performance (as it's much slower than others),
		/// or if players can have duplicate names, as only one of them will actually receive this message.
		/// </summary>

		public void Send (byte rfcID, string targetName, params object[] objs) { SendRFC(rfcID, null, targetName, true, objs); }

		public void Send (byte rfcID, string targetName) { SendRFC(rfcID, null, targetName, true, null); }

		public void Send (byte rfcID, string targetName, object obj0)
		{
			if (mObj1 == null) mObj1 = new object[1];
			mObj1[0] = obj0;
			SendRFC(rfcID, null, targetName, true, mObj1);
		}

		public void Send (byte rfcID, string targetName, object obj0, object obj1)
		{
			if (mObj2 == null) mObj2 = new object[2];
			mObj2[0] = obj0;
			mObj2[1] = obj1;
			SendRFC(rfcID, null, targetName, true, mObj2);
		}

		public void Send (byte rfcID, string targetName, object obj0, object obj1, object obj2)
		{
			if (mObj3 == null) mObj3 = new object[3];
			mObj3[0] = obj0;
			mObj3[1] = obj1;
			mObj3[2] = obj2;
			SendRFC(rfcID, null, targetName, true, mObj3);
		}

		public void Send (byte rfcID, string targetName, object obj0, object obj1, object obj2, object obj3)
		{
			if (mObj4 == null) mObj4 = new object[4];
			mObj4[0] = obj0;
			mObj4[1] = obj1;
			mObj4[2] = obj2;
			mObj4[3] = obj3;
			SendRFC(rfcID, null, targetName, true, mObj4);
		}

		/// <summary>
		/// Send a remote function call.
		/// </summary>

		public void Send (string rfcName, Target target, params object[] objs) { SendRFC(0, rfcName, target, true, objs); }

		public void Send (string rfcName, Target target) { SendRFC(0, rfcName, target, true, null); }

		public void Send (string rfcName, Target target, object obj0)
		{
			if (mObj1 == null) mObj1 = new object[1];
			mObj1[0] = obj0;
			SendRFC(0, rfcName, target, true, mObj1);
		}

		public void Send (string rfcName, Target target, object obj0, object obj1)
		{
			if (mObj2 == null) mObj2 = new object[2];
			mObj2[0] = obj0;
			mObj2[1] = obj1;
			SendRFC(0, rfcName, target, true, mObj2);
		}

		public void Send (string rfcName, Target target, object obj0, object obj1, object obj2)
		{
			if (mObj3 == null) mObj3 = new object[3];
			mObj3[0] = obj0;
			mObj3[1] = obj1;
			mObj3[2] = obj2;
			SendRFC(0, rfcName, target, true, mObj3);
		}

		public void Send (string rfcName, Target target, object obj0, object obj1, object obj2, object obj3)
		{
			if (mObj4 == null) mObj4 = new object[4];
			mObj4[0] = obj0;
			mObj4[1] = obj1;
			mObj4[2] = obj2;
			mObj4[3] = obj3;
			SendRFC(0, rfcName, target, true, mObj4);
		}

		public void Send (string rfcName, Target target, object obj0, object obj1, object obj2, object obj3, object obj4)
		{
			if (mObj5 == null) mObj5 = new object[5];
			mObj5[0] = obj0;
			mObj5[1] = obj1;
			mObj5[2] = obj2;
			mObj5[3] = obj3;
			mObj5[4] = obj4;
			SendRFC(0, rfcName, target, true, mObj5);
		}

		public void Send (string rfcName, Target target, object obj0, object obj1, object obj2, object obj3, object obj4, object obj5)
		{
			if (mObj6 == null) mObj6 = new object[6];
			mObj6[0] = obj0;
			mObj6[1] = obj1;
			mObj6[2] = obj2;
			mObj6[3] = obj3;
			mObj6[4] = obj4;
			mObj6[5] = obj5;
			SendRFC(0, rfcName, target, true, mObj6);
		}

		public void Send (string rfcName, Target target, object obj0, object obj1, object obj2, object obj3, object obj4, object obj5, object obj6)
		{
			if (mObj7 == null) mObj7 = new object[7];
			mObj7[0] = obj0;
			mObj7[1] = obj1;
			mObj7[2] = obj2;
			mObj7[3] = obj3;
			mObj7[4] = obj4;
			mObj7[5] = obj5;
			mObj7[6] = obj6;
			SendRFC(0, rfcName, target, true, mObj7);
		}

		/// <summary>
		/// Send a remote function call.
		/// Note that you should not use this version of the function if you care about performance (as it's much slower than others),
		/// or if players can have duplicate names, as only one of them will actually receive this message.
		/// </summary>

		public void Send (string rfcName, string targetName, params object[] objs) { SendRFC(0, rfcName, targetName, true, objs); }

		public void Send (string rfcName, string targetName) { SendRFC(0, rfcName, targetName, true, null); }

		public void Send (string rfcName, string targetName, object obj0)
		{
			if (mObj1 == null) mObj1 = new object[1];
			mObj1[0] = obj0;
			SendRFC(0, rfcName, targetName, true, mObj1);
		}

		public void Send (string rfcName, string targetName, object obj0, object obj1)
		{
			if (mObj2 == null) mObj2 = new object[2];
			mObj2[0] = obj0;
			mObj2[1] = obj1;
			SendRFC(0, rfcName, targetName, true, mObj2);
		}

		public void Send (string rfcName, string targetName, object obj0, object obj1, object obj2)
		{
			if (mObj3 == null) mObj3 = new object[3];
			mObj3[0] = obj0;
			mObj3[1] = obj1;
			mObj3[2] = obj2;
			SendRFC(0, rfcName, targetName, true, mObj3);
		}

		public void Send (string rfcName, string targetName, object obj0, object obj1, object obj2, object obj3)
		{
			if (mObj4 == null) mObj4 = new object[4];
			mObj4[0] = obj0;
			mObj4[1] = obj1;
			mObj4[2] = obj2;
			mObj4[3] = obj3;
			SendRFC(0, rfcName, targetName, true, mObj4);
		}

		/// <summary>
		/// Send a remote function call.
		/// </summary>

		public void Send (byte rfcID, Player target, params object[] objs) { if (target != null) SendRFC(rfcID, null, target.id, true, objs); }
		public void Send (byte rfcID, Player target) { if (target != null) Send(rfcID, target.id); }
		public void Send (byte rfcID, Player target, object obj0) { if (target != null) Send(rfcID, target.id, obj0); }
		public void Send (byte rfcID, Player target, object obj0, object obj1) { if (target != null) Send(rfcID, target.id, obj0, obj1); }
		public void Send (byte rfcID, Player target, object obj0, object obj1, object obj2) { if (target != null) Send(rfcID, target.id, obj0, obj1, obj2); }
		public void Send (byte rfcID, Player target, object obj0, object obj1, object obj2, object obj3) { if (target != null) Send(rfcID, target.id, obj0, obj1, obj2, obj3); }

		/// <summary>
		/// Send a remote function call.
		/// </summary>

		public void Send (string rfcName, Player target, params object[] objs) { if (target != null) SendRFC(0, rfcName, target.id, true, objs); }
		public void Send (string rfcName, Player target) { if (target != null) Send(rfcName, target.id); }
		public void Send (string rfcName, Player target, object obj0) { if (target != null) Send(rfcName, target.id, obj0); }
		public void Send (string rfcName, Player target, object obj0, object obj1) { if (target != null) Send(rfcName, target.id, obj0, obj1); }
		public void Send (string rfcName, Player target, object obj0, object obj1, object obj2) { if (target != null) Send(rfcName, target.id, obj0, obj1, obj2); }
		public void Send (string rfcName, Player target, object obj0, object obj1, object obj2, object obj3) { if (target != null) Send(rfcName, target.id, obj0, obj1, obj2, obj3); }
		public void Send (string rfcName, Player target, object obj0, object obj1, object obj2, object obj3, object obj4) { if (target != null) Send(rfcName, target.id, obj0, obj1, obj2, obj3, obj4); }
		public void Send (string rfcName, Player target, object obj0, object obj1, object obj2, object obj3, object obj4, object obj5) { if (target != null) Send(rfcName, target.id, obj0, obj1, obj2, obj3, obj4, obj5); }

		/// <summary>
		/// Send a remote function call.
		/// </summary>

		public void Send (byte rfcID, int playerID, params object[] objs) { SendRFC(rfcID, null, playerID, true, objs); }

		public void Send (byte rfcID, int playerID) { SendRFC(rfcID, null, playerID, true, null); }

		public void Send (byte rfcID, int playerID, object obj0)
		{
			if (mObj1 == null) mObj1 = new object[1];
			mObj1[0] = obj0;
			SendRFC(rfcID, null, playerID, true, mObj1);
		}

		public void Send (byte rfcID, int playerID, object obj0, object obj1)
		{
			if (mObj2 == null) mObj2 = new object[2];
			mObj2[0] = obj0;
			mObj2[1] = obj1;
			SendRFC(rfcID, null, playerID, true, mObj2);
		}

		public void Send (byte rfcID, int playerID, object obj0, object obj1, object obj2)
		{
			if (mObj3 == null) mObj3 = new object[3];
			mObj3[0] = obj0;
			mObj3[1] = obj1;
			mObj3[2] = obj2;
			SendRFC(rfcID, null, playerID, true, mObj3);
		}

		public void Send (byte rfcID, int playerID, object obj0, object obj1, object obj2, object obj3)
		{
			if (mObj4 == null) mObj4 = new object[4];
			mObj4[0] = obj0;
			mObj4[1] = obj1;
			mObj4[2] = obj2;
			mObj4[3] = obj3;
			SendRFC(rfcID, null, playerID, true, mObj4);
		}

		/// <summary>
		/// Send a remote function call.
		/// </summary>

		public void Send (string rfcName, int playerID, params object[] objs) { SendRFC(0, rfcName, playerID, true, objs); }

		public void Send (string rfcName, int playerID) { SendRFC(0, rfcName, playerID, true, null); }

		public void Send (string rfcName, int playerID, object obj0)
		{
			if (mObj1 == null) mObj1 = new object[1];
			mObj1[0] = obj0;
			SendRFC(0, rfcName, playerID, true, mObj1);
		}

		public void Send (string rfcName, int playerID, object obj0, object obj1)
		{
			if (mObj2 == null) mObj2 = new object[2];
			mObj2[0] = obj0;
			mObj2[1] = obj1;
			SendRFC(0, rfcName, playerID, true, mObj2);
		}

		public void Send (string rfcName, int playerID, object obj0, object obj1, object obj2)
		{
			if (mObj3 == null) mObj3 = new object[3];
			mObj3[0] = obj0;
			mObj3[1] = obj1;
			mObj3[2] = obj2;
			SendRFC(0, rfcName, playerID, true, mObj3);
		}

		public void Send (string rfcName, int playerID, object obj0, object obj1, object obj2, object obj3)
		{
			if (mObj4 == null) mObj4 = new object[4];
			mObj4[0] = obj0;
			mObj4[1] = obj1;
			mObj4[2] = obj2;
			mObj4[3] = obj3;
			SendRFC(0, rfcName, playerID, true, mObj4);
		}

		public void Send (string rfcName, int playerID, object obj0, object obj1, object obj2, object obj3, object obj4)
		{
			if (mObj5 == null) mObj5 = new object[5];
			mObj5[0] = obj0;
			mObj5[1] = obj1;
			mObj5[2] = obj2;
			mObj5[3] = obj3;
			mObj5[4] = obj4;
			SendRFC(0, rfcName, playerID, true, mObj5);
		}

		public void Send (string rfcName, int playerID, object obj0, object obj1, object obj2, object obj3, object obj4, object obj5)
		{
			if (mObj6 == null) mObj6 = new object[6];
			mObj6[0] = obj0;
			mObj6[1] = obj1;
			mObj6[2] = obj2;
			mObj6[3] = obj3;
			mObj6[4] = obj4;
			mObj6[5] = obj5;
			SendRFC(0, rfcName, playerID, true, mObj6);
		}

		/// <summary>
		/// Send a remote function call via UDP (if possible).
		/// </summary>

		public void SendQuickly (byte rfcID, Target target, params object[] objs) { SendRFC(rfcID, null, target, false, objs); }

		public void SendQuickly (byte rfcID, Target target) { SendRFC(rfcID, null, target, false, null); }

		public void SendQuickly (byte rfcID, Target target, object obj0)
		{
			if (mObj1 == null) mObj1 = new object[1];
			mObj1[0] = obj0;
			SendRFC(rfcID, null, target, false, mObj1);
		}

		public void SendQuickly (byte rfcID, Target target, object obj0, object obj1)
		{
			if (mObj2 == null) mObj2 = new object[2];
			mObj2[0] = obj0;
			mObj2[1] = obj1;
			SendRFC(rfcID, null, target, false, mObj2);
		}

		public void SendQuickly (byte rfcID, Target target, object obj0, object obj1, object obj2)
		{
			if (mObj3 == null) mObj3 = new object[3];
			mObj3[0] = obj0;
			mObj3[1] = obj1;
			mObj3[2] = obj2;
			SendRFC(rfcID, null, target, false, mObj3);
		}

		public void SendQuickly (byte rfcID, Target target, object obj0, object obj1, object obj2, object obj3)
		{
			if (mObj4 == null) mObj4 = new object[4];
			mObj4[0] = obj0;
			mObj4[1] = obj1;
			mObj4[2] = obj2;
			mObj4[3] = obj3;
			SendRFC(rfcID, null, target, false, mObj4);
		}

		/// <summary>
		/// Send a remote function call via UDP (if possible).
		/// </summary>

		public void SendQuickly (byte rfcID, int playerID, params object[] objs) { SendRFC(rfcID, null, playerID, false, objs); }

		public void SendQuickly (byte rfcID, int playerID) { SendRFC(rfcID, null, playerID, false, null); }

		public void SendQuickly (byte rfcID, int playerID, object obj0)
		{
			if (mObj1 == null) mObj1 = new object[1];
			mObj1[0] = obj0;
			SendRFC(rfcID, null, playerID, false, mObj1);
		}

		public void SendQuickly (byte rfcID, int playerID, object obj0, object obj1)
		{
			if (mObj2 == null) mObj2 = new object[2];
			mObj2[0] = obj0;
			mObj2[1] = obj1;
			SendRFC(rfcID, null, playerID, false, mObj2);
		}

		public void SendQuickly (byte rfcID, int playerID, object obj0, object obj1, object obj2)
		{
			if (mObj3 == null) mObj3 = new object[3];
			mObj3[0] = obj0;
			mObj3[1] = obj1;
			mObj3[2] = obj2;
			SendRFC(rfcID, null, playerID, false, mObj3);
		}

		public void SendQuickly (byte rfcID, int playerID, object obj0, object obj1, object obj2, object obj3)
		{
			if (mObj4 == null) mObj4 = new object[4];
			mObj4[0] = obj0;
			mObj4[1] = obj1;
			mObj4[2] = obj2;
			mObj4[3] = obj3;
			SendRFC(rfcID, null, playerID, false, mObj4);
		}

		/// <summary>
		/// Send a remote function call via UDP (if possible).
		/// </summary>

		public void SendQuickly (string rfcName, Target target, params object[] objs) { SendRFC(0, rfcName, target, false, objs); }

		public void SendQuickly (string rfcName, Target target) { SendRFC(0, rfcName, target, false, null); }

		public void SendQuickly (string rfcName, Target target, object obj0)
		{
			if (mObj1 == null) mObj1 = new object[1];
			mObj1[0] = obj0;
			SendRFC(0, rfcName, target, false, mObj1);
		}

		public void SendQuickly (string rfcName, Target target, object obj0, object obj1)
		{
			if (mObj2 == null) mObj2 = new object[2];
			mObj2[0] = obj0;
			mObj2[1] = obj1;
			SendRFC(0, rfcName, target, false, mObj2);
		}

		public void SendQuickly (string rfcName, Target target, object obj0, object obj1, object obj2)
		{
			if (mObj3 == null) mObj3 = new object[3];
			mObj3[0] = obj0;
			mObj3[1] = obj1;
			mObj3[2] = obj2;
			SendRFC(0, rfcName, target, false, mObj3);
		}

		public void SendQuickly (string rfcName, Target target, object obj0, object obj1, object obj2, object obj3)
		{
			if (mObj4 == null) mObj4 = new object[4];
			mObj4[0] = obj0;
			mObj4[1] = obj1;
			mObj4[2] = obj2;
			mObj4[3] = obj3;
			SendRFC(0, rfcName, target, false, mObj4);
		}

		/// <summary>
		/// Send a remote function call via UDP (if possible).
		/// </summary>

		public void SendQuickly (byte rfcID, Player target, params object[] objs) { if (target != null) SendRFC(rfcID, null, target.id, false, objs); }
		public void SendQuickly (byte rfcID, Player target) { if (target != null) SendQuickly(rfcID, target.id); }
		public void SendQuickly (byte rfcID, Player target, object obj0) { if (target != null) SendQuickly(rfcID, target.id, obj0); }
		public void SendQuickly (byte rfcID, Player target, object obj0, object obj1) { if (target != null) SendQuickly(rfcID, target.id, obj0, obj1); }
		public void SendQuickly (byte rfcID, Player target, object obj0, object obj1, object obj2) { if (target != null) SendQuickly(rfcID, target.id, obj0, obj1, obj2); }
		public void SendQuickly (byte rfcID, Player target, object obj0, object obj1, object obj2, object obj3) { if (target != null) SendQuickly(rfcID, target.id, obj0, obj1, obj2, obj3); }

		/// <summary>
		/// Send a remote function call via UDP (if possible).
		/// </summary>

		public void SendQuickly (string rfcName, Player target, params object[] objs) { SendRFC(0, rfcName, target.id, false, objs); }
		public void SendQuickly (string rfcName, Player target) { if (target != null) SendQuickly(rfcName, target.id); }
		public void SendQuickly (string rfcName, Player target, object obj0) { if (target != null) SendQuickly(rfcName, target.id, obj0); }
		public void SendQuickly (string rfcName, Player target, object obj0, object obj1) { if (target != null) SendQuickly(rfcName, target.id, obj0, obj1); }
		public void SendQuickly (string rfcName, Player target, object obj0, object obj1, object obj2) { if (target != null) SendQuickly(rfcName, target.id, obj0, obj1, obj2); }
		public void SendQuickly (string rfcName, Player target, object obj0, object obj1, object obj2, object obj3) { if (target != null) SendQuickly(rfcName, target.id, obj0, obj1, obj2, obj3); }

		/// <summary>
		/// Send a remote function call via UDP (if possible).
		/// </summary>

		public void SendQuickly (string rfcName, int playerID, params object[] objs) { SendRFC(0, rfcName, playerID, false, objs); }

		public void SendQuickly (string rfcName, int playerID) { SendRFC(0, rfcName, playerID, false, null); }

		public void SendQuickly (string rfcName, int playerID, object obj0)
		{
			if (mObj1 == null) mObj1 = new object[1];
			mObj1[0] = obj0;
			SendRFC(0, rfcName, playerID, false, mObj1);
		}

		public void SendQuickly (string rfcName, int playerID, object obj0, object obj1)
		{
			if (mObj2 == null) mObj2 = new object[2];
			mObj2[0] = obj0;
			mObj2[1] = obj1;
			SendRFC(0, rfcName, playerID, false, mObj2);
		}

		public void SendQuickly (string rfcName, int playerID, object obj0, object obj1, object obj2)
		{
			if (mObj3 == null) mObj3 = new object[3];
			mObj3[0] = obj0;
			mObj3[1] = obj1;
			mObj3[2] = obj2;
			SendRFC(0, rfcName, playerID, false, mObj3);
		}

		public void SendQuickly (string rfcName, int playerID, object obj0, object obj1, object obj2, object obj3)
		{
			if (mObj4 == null) mObj4 = new object[4];
			mObj4[0] = obj0;
			mObj4[1] = obj1;
			mObj4[2] = obj2;
			mObj4[3] = obj3;
			SendRFC(0, rfcName, playerID, false, mObj4);
		}

		/// <summary>
		/// Send a broadcast to the entire LAN. Does not require an active connection.
		/// </summary>

		public void BroadcastToLAN (int port, byte rfcID, params object[] objs) { BroadcastToLAN(port, rfcID, null, objs); }

		/// <summary>
		/// Send a broadcast to the entire LAN. Does not require an active connection.
		/// </summary>

		public void BroadcastToLAN (int port, string rfcName, params object[] objs) { BroadcastToLAN(port, 0, rfcName, objs); }

#endregion

		/// <summary>
		/// Remove a previously saved remote function call.
		/// </summary>

		public void RemoveSavedRFC (string rfcName) { RemoveSavedRFC(channelID, uid, 0, rfcName); }

		/// <summary>
		/// Remove a previously saved remote function call.
		/// </summary>

		public void RemoveSavedRFC (byte rfcID) { RemoveSavedRFC(channelID, uid, rfcID, null); }

		/// <summary>
		/// Remove a previously saved remote function call.
		/// </summary>

		[System.Obsolete("Use RemoveSavedRFC instead")]
		public void Remove (string rfcName) { RemoveSavedRFC(channelID, uid, 0, rfcName); }

		/// <summary>
		/// Remove a previously saved remote function call.
		/// </summary>

		[System.Obsolete("Use RemoveSavedRFC instead")]
		public void Remove (byte rfcID) { RemoveSavedRFC(channelID, uid, rfcID, null); }

		/// <summary>
		/// Convert object and RFC IDs into a single UINT.
		/// </summary>

		static public uint GetUID (uint objID, byte rfcID)
		{
			return (objID << 8) | rfcID;
		}

		/// <summary>
		/// Decode object ID and RFC IDs encoded in a single UINT.
		/// </summary>

		static public void DecodeUID (uint uid, out uint objID, out byte rfcID)
		{
			rfcID = (byte)(uid & 0xFF);
			objID = (uid >> 8);
		}

		/// <summary>
		/// Send a new RFC call to the specified target.
		/// </summary>

		void SendRFC (byte rfcID, string rfcName, Target target, bool reliable, object[] objs)
		{
#if UNITY_EDITOR
			if (!Application.isPlaying) return;
#endif
			if (!enabled) return;

			if (hasBeenDestroyed)
			{
#if UNITY_EDITOR
				Debug.LogWarning("Trying to send RFC " + (rfcID != 0 ? rfcID.ToString() : rfcName) + " through a destroyed object. Ignoring.", this);
				return;
#endif
			}

#if UNITY_EDITOR
			if (rebuildMethodList) RebuildMethodList();

			CachedFunc ent;

			if (rfcID != 0)
			{
				if (!mDict0.TryGetValue(rfcID, out ent))
				{
					Debug.LogWarning("RFC " + rfcID + " is not present on " + name, this);
					return;
				}
			}
			else if (!mDict1.TryGetValue(rfcName, out ent))
			{
				Debug.LogWarning("RFC " + rfcName + " is not present on " + name, this);
				return;
			}
#endif
			// Some very odd special case... sending a string[] as the only parameter
			// results in objs[] being a string[] instead, when it should be object[string[]].
			if (objs != null && objs.GetType() != typeof(object[]))
				objs = new object[] { objs };

			var uid = this.uid;
			var executeLocally = false;
			var connected = TNManager.isConnected;

			if (target == Target.Broadcast)
			{
				if (connected)
				{
#if !MODDING
					if (uid != 0)
					{
						var writer = TNManager.BeginSend(Packet.Broadcast);
						writer.Write(TNManager.playerID);
						writer.Write(channelID);
						writer.Write(GetUID(uid, rfcID));
						if (rfcID == 0) writer.Write(rfcName);
						writer.WriteArray(objs);
						TNManager.EndSend(channelID, reliable);
#if UNITY_EDITOR && COUNT_PACKETS
						var sid = (rfcID == 0) ? rfcName : "RFC " + rfcID;
						if (sentDictionary.ContainsKey(sid)) ++sentDictionary[sid];
						else sentDictionary[sid] = 1;
#endif
					}
#if UNITY_EDITOR
					else Debug.LogWarning("Network object ID of 0 can't be used for communication. Use TNManager.Instantiate to create your objects.", this);
#endif
#endif
				}
				else executeLocally = true;
			}
			else if (target == Target.Admin)
			{
				if (connected)
				{
#if !MODDING
					if (uid != 0)
					{
						var writer = TNManager.BeginSend(Packet.BroadcastAdmin);
						writer.Write(TNManager.playerID);
						writer.Write(channelID);
						writer.Write(GetUID(uid, rfcID));
						if (rfcID == 0) writer.Write(rfcName);
						writer.WriteArray(objs);
						TNManager.EndSend(channelID, reliable);
#if UNITY_EDITOR && COUNT_PACKETS
						var sid = (rfcID == 0) ? rfcName : "RFC " + rfcID;
						if (sentDictionary.ContainsKey(sid)) ++sentDictionary[sid];
						else sentDictionary[sid] = 1;
#endif
					}
#if UNITY_EDITOR
					else Debug.LogWarning("Network object ID of 0 can't be used for communication. Use TNManager.Instantiate to create your objects.", this);
#endif
#endif
				}
				else executeLocally = true;
			}
			else if (target == Target.Host && TNManager.IsHosting(channelID))
			{
				// We're the host, and the packet should be going to the host -- just echo it locally
				executeLocally = true;
			}
			else
			{
				if (!connected || !reliable)
				{
					if (target == Target.All)
					{
						target = Target.Others;
						executeLocally = true;
					}
					else if (target == Target.AllSaved)
					{
						target = Target.OthersSaved;
						executeLocally = true;
					}
				}
#if !MODDING
				if (connected && TNManager.IsInChannel(channelID))
				{
					if (uid != 0)
					{
						var packetID = (byte)((int)Packet.ForwardToAll + (int)target);
						var writer = TNManager.BeginSend(packetID);
						writer.Write(TNManager.playerID);
						writer.Write(channelID);
						writer.Write(GetUID(uid, rfcID));
						if (rfcID == 0) writer.Write(rfcName);
						writer.WriteArray(objs);
						TNManager.EndSend(channelID, reliable);
#if UNITY_EDITOR && COUNT_PACKETS
						var sid = (rfcID == 0) ? rfcName : "RFC " + rfcID;
						if (sentDictionary.ContainsKey(sid)) ++sentDictionary[sid];
						else sentDictionary[sid] = 1;
#endif
					}
#if UNITY_EDITOR
					else Debug.LogWarning("Network object ID of 0 can't be used for communication. Use TNManager.Instantiate to create your objects.", this);
#endif
				}
#endif
			}

			if (executeLocally)
			{
				TNManager.packetSourceID = TNManager.playerID;
				if (rfcID != 0) Execute(rfcID, objs);
				else Execute(rfcName, objs);
			}
		}

#if UNITY_EDITOR && COUNT_PACKETS
		// Used for debugging to determine what packets are sent too frequently
		static internal Dictionary<string, int> sentDictionary = new Dictionary<string, int>();
		static internal Dictionary<string, int> lastSentDictionary = new Dictionary<string, int>();
#endif
		/// <summary>
		/// Send a new RFC call to the specified target.
		/// </summary>

		void SendRFC (byte rfcID, string rfcName, string targetName, bool reliable, object[] objs)
		{
#if UNITY_EDITOR
			if (!Application.isPlaying) return;
#endif
			if (mDestroyed != 0 || uid == 0 || string.IsNullOrEmpty(targetName)) return;

			if (targetName == TNManager.playerName)
			{
				TNManager.packetSourceID = TNManager.playerID;
				if (rfcID != 0) Execute(rfcID, objs);
				else Execute(rfcName, objs);
			}
			else
			{
#if !MODDING
				var writer = TNManager.BeginSend(Packet.ForwardByName);
				writer.Write(TNManager.playerID);
				writer.Write(targetName);
				writer.Write(channelID);
				writer.Write(GetUID(uid, rfcID));
				if (rfcID == 0) writer.Write(rfcName);
#if UNITY_EDITOR && COUNT_PACKETS
				var sid = (rfcID == 0) ? rfcName : "RFC " + rfcID;
				if (sentDictionary.ContainsKey(sid)) ++sentDictionary[sid];
				else sentDictionary[sid] = 1;
#endif
				writer.WriteArray(objs);
				TNManager.EndSend(channelID, reliable);
#endif
			}
		}

		/// <summary>
		/// Send a new remote function call to the specified player.
		/// </summary>

		void SendRFC (byte rfcID, string rfcName, int target, bool reliable, object[] objs)
		{
			if (hasBeenDestroyed || uid == 0) return;

			if (TNManager.isConnected)
			{
#if !MODDING
				var writer = TNManager.BeginSend(Packet.ForwardToPlayer);
				writer.Write(TNManager.playerID);
				writer.Write(target);
				writer.Write(channelID);
				writer.Write(GetUID(uid, rfcID));
				if (rfcID == 0) writer.Write(rfcName);
#if UNITY_EDITOR && COUNT_PACKETS
				var sid = (rfcID == 0) ? rfcName : "RFC " + rfcID;
				if (sentDictionary.ContainsKey(sid)) ++sentDictionary[sid];
				else sentDictionary[sid] = 1;
#endif
				writer.WriteArray(objs);
				TNManager.EndSend(channelID, reliable);
#endif
			}
			else if (target == TNManager.playerID)
			{
				if (rfcID != 0) Execute(rfcID, objs);
				else Execute(rfcName, objs);
			}
		}

		/// <summary>
		/// Broadcast a remote function call to all players on the network.
		/// </summary>

		void BroadcastToLAN (int port, byte rfcID, string rfcName, object[] objs)
		{
#if !MODDING
			if (hasBeenDestroyed || uid == 0) return;
			BinaryWriter writer = TNManager.BeginSend(Packet.ForwardToAll);
			writer.Write(TNManager.playerID);
			writer.Write(channelID);
			writer.Write(GetUID(uid, rfcID));
			if (rfcID == 0) writer.Write(rfcName);
			writer.WriteArray(objs);
			TNManager.EndSendToLAN(port);
#if UNITY_EDITOR && COUNT_PACKETS
			var sid = (rfcID == 0) ? rfcName : "RFC " + rfcID;
			if (sentDictionary.ContainsKey(sid)) ++sentDictionary[sid];
			else sentDictionary[sid] = 1;
#endif
#endif
		}

		/// <summary>
		/// Remove a previously saved remote function call.
		/// </summary>

		static void RemoveSavedRFC (int channelID, uint objID, byte rfcID, string funcName)
		{
#if !MODDING
			if (TNManager.IsInChannel(channelID))
			{
				var writer = TNManager.BeginSend(Packet.RequestRemoveRFC);
				writer.Write(channelID);
				writer.Write(GetUID(objID, rfcID));
				if (rfcID == 0) writer.Write(funcName);
				TNManager.EndSend(channelID, true);
#if UNITY_EDITOR && COUNT_PACKETS
				var sid = "Remove RFC " + rfcID + " " + funcName;
				if (sentDictionary.ContainsKey(sid)) ++sentDictionary[sid];
				else sentDictionary[sid] = 1;
#endif
			}
#endif
		}

		[System.NonSerialized] int mNextChannelID = 0;

		/// <summary>
		/// Transfer this object to another channel. Only the object's owner should perform this action.
		/// Note that if the object has a nested TNObject hierarchy, newly entered clients won't see this hierarchy.
		/// It's up to you, the developer, to set the hierarchy if you need it. You can do it by setting a custom RFC
		/// with the hierarchy path (or simply the TNObject parent ID) before calling TransferToChannel.
		/// </summary>

		public bool TransferToChannel (int newChannelID)
		{
#if !MODDING
			if (mDestroyed != 0 || mNextChannelID != 0) return false;

			if (uid > 32767 && channelID != newChannelID)
			{
				mNextChannelID = newChannelID;

				if (TNManager.isConnected)
				{
					var writer = TNManager.BeginSend(Packet.RequestTransferObject);
					writer.Write(channelID);
					writer.Write(newChannelID);
					writer.Write(uid);
					TNManager.EndSend(channelID, true);
#if UNITY_EDITOR && COUNT_PACKETS
					var sid = "TransferToChannel";
					if (sentDictionary.ContainsKey(sid)) ++sentDictionary[sid];
					else sentDictionary[sid] = 1;
#endif
				}
				else FinalizeTransfer(newChannelID, TNObject.GetUniqueID(true));
				return true;
			}
#endif
			return false;
		}

		/// <summary>
		/// This function is called when the object's IDs change. This happens after the object was transferred to another channel.
		/// </summary>

		internal void FinalizeTransfer (int newChannel, uint newObjectID)
		{
#if !MODDING
			var fw = new ForwardRecord();
			fw.oldChannelID = channelID;
			fw.newChannelID = newChannel;
			fw.oldObjectID = uid;
			fw.newObjectID = newObjectID;
			fw.expiration = TNManager.serverTime + 2000;
			if (mForwardRecords == null) mForwardRecords = new List<ForwardRecord>();
			mForwardRecords.Add(fw);

			if (onTransfer != null) onTransfer(newChannel, newObjectID);

			Unregister();
			channelID = newChannel;
			uid = newObjectID;
			Register();
			mNextChannelID = 0;
#endif
		}
	}
}
