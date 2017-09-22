//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2017 Tasharen Entertainment Inc
//-------------------------------------------------

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
	public sealed class TNObject : MonoBehaviour
	{
		// List of network objs to iterate through
		static Dictionary<int, TNet.List<TNObject>> mList =
			new Dictionary<int, TNet.List<TNObject>>();

		// List of network objs to quickly look up
		static Dictionary<int, Dictionary<uint, TNObject>> mDictionary =
			new Dictionary<int, Dictionary<uint, TNObject>>();

		/// <summary>
		/// Unique Network Identifier. All TNObjects have them and is how messages arrive at the correct destination.
		/// The ID is supposed to be a 'uint', but Unity is not able to serialize 'uint' types. Sigh.
		/// </summary>

		[SerializeField, UnityEngine.Serialization.FormerlySerializedAs("id")] int mStaticID = 0;
		[System.NonSerialized] uint mDynamicID = 0;

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

		// Child objects don't get their own unique IDs, so if we have a parent TNObject, that's the object that will be getting all events.
		[System.NonSerialized] TNObject mParent = null;
		[System.NonSerialized] bool mParentCheck = true;

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
				return (parent == null) ? (mDynamicID != 0 ? mDynamicID : (uint)mStaticID) : mParent.uid;
			}
			set
			{
				if (parent == null)
				{
					mDynamicID = value;
					mStaticID = 0;
				}
				else mParent.uid = value;
			}
		}

		/// <summary>
		/// TNObject's parent, if it has any.
		/// </summary>

		public TNObject parent
		{
			get
			{
				if (mParentCheck)
				{
					mParentCheck = false;

					if (uid == 0)
					{
						var pt = transform.parent;
						mParent = (pt != null) ? pt.GetComponentInParent<TNObject>() : null;
					}
				}
				return mParent;
			}
		}

		/// <summary>
		/// Whether the player is currently joining this object's channel.
		/// </summary>

		public bool isJoiningChannel { get { return TNManager.IsJoiningChannel(channelID); } }

		/// <summary>
		/// Whether the object has been destroyed. It can happen when the object has been requested to be
		/// transferred to another channel, but has not yet completed the operation.
		/// </summary>

		public bool hasBeenDestroyed { get { return (parent == null) ? mDestroyed != 0 : mParent.hasBeenDestroyed; } }

		/// <summary>
		/// An object gets marked as registered after the creation call has completed and the object's ID has been assigned.
		/// </summary>

		public bool hasBeenRegistered { get { return (parent == null) ? mHasBeenRegistered : mParent.hasBeenRegistered; } }

		/// <summary>
		/// Whether sending messages through this object is possible or not.
		/// </summary>

		public bool canSend { get { return !hasBeenDestroyed && !TNManager.IsJoiningChannel(channelID); } }

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
			get
			{
				var owner = this.owner;
				return (owner != null) ? owner == TNManager.player : TNManager.IsHosting(channelID);
			}
		}

		/// <summary>
		/// ID of the player that owns this object.
		/// </summary>

		public int ownerID
		{
			get
			{
				if (mParent != null) return mParent.ownerID;
				if (mOwner != null) return mOwner.id;
				var host = TNManager.GetHost(channelID);
				if (host != null) return host.id;
				return 0;
			}
			set
			{
				if (mParent != null)
				{
					mParent.ownerID = value;
				}
				else
				{
					var bw = TNManager.BeginSend(Packet.RequestSetOwner);
					bw.Write(channelID);
					bw.Write(uid);
					bw.Write(value);
					TNManager.EndSend();
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
				return (parent == null) ? (mOwner ?? TNManager.GetHost(channelID)) : mParent.owner;
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
				return (parent == null) ? mData : mParent.dataNode;
			}
			set
			{
				if (parent == null)
				{
					if (!mHasBeenRegistered)
					{
						mData = value;
						mCallDataChanged = true;
					}
				}
				else mParent.dataNode = value;
			}
		}

		/// <summary>
		/// Get the object-specific data.
		/// </summary>

		public T Get<T> (string name) { return (parent == null) ? ((mData != null) ? mData.GetChild<T>(name) : default(T)) : mParent.Get<T>(name); }

		/// <summary>
		/// Get the object-specific data.
		/// </summary>

		public T Get<T> (string name, T defVal) { return (parent == null) ? ((mData != null) ? mData.GetChild<T>(name, defVal) : defVal) : mParent.Get<T>(name, defVal); }

		/// <summary>
		/// Set the object-specific data.
		/// </summary>

		public void Set (string name, object val)
		{
			if (parent == null)
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
					if (isMine) Send("OnSetData", Target.OthersSaved, mData);
					else Send("OnSet", ownerID, name, val);
				}
			}
			else mParent.Set(name, val);
		}

		[RFC]
		void OnSet (string name, object val)
		{
			if (parent == null)
			{
				if (mData == null) mData = new DataNode("ObjectData");
				mData.SetHierarchy(name, val);
				OnSetData(mData);
				Send("OnSetData", Target.OthersSaved, mData);
			}
			else mParent.OnSet(name, val);
		}

		[RFC]
		void OnSetData (DataNode data)
		{
			if (parent != null)
			{
				parent.OnSetData(data);
			}
			else if (!mSettingData)
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
			if (parent == null)
			{
				if (mDestroyed != 0) return;
				mDestroyed = 1;

				if (!enabled)
				{
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
					}
				}
				else OnDestroyPacket();
			}
			else mParent.DestroySelf();
		}

		/// <summary>
		/// Notification of the object needing to be destroyed immediately.
		/// </summary>

		internal void OnDestroyPacket ()
		{
			if (!enabled)
			{
				Object.Destroy(gameObject);
			}
			else if (mDestroyed < 2)
			{
				mDestroyed = 2;
				if (onDestroy != null) onDestroy();

				if (ignoreDestroyCall)
				{
					enabled = false;
					mDestroyed = 3;
				}
				else Object.Destroy(gameObject);
			}
		}

		/// <summary>
		/// Destroy this game object on all connected clients and remove it from the server.
		/// </summary>

		public void DestroySelf (float delay, bool onlyIfOwner = true)
		{
			if (parent == null)
			{
				if (onlyIfOwner) Invoke("DestroyIfMine", delay);
				else Invoke("DestroySelf", delay);
			}
			else parent.DestroySelf(delay, onlyIfOwner);
		}

		void DestroyIfMine () { if (isMine) DestroySelf(); }

		/// <summary>
		/// Remember the object's ownership, for convenience.
		/// </summary>

		void Awake ()
		{
			mOwner = TNManager.isConnected ? TNManager.currentObjectOwner : TNManager.player;
			channelID = TNManager.lastChannelID;
		}

		void OnEnable ()
		{
#if UNITY_EDITOR
			// This usually happens after scripts get recompiled.
			// When this happens, static variables are erased, so the list of objects has to be rebuilt.
			if (!Application.isPlaying && uid != 0)
			{
				Unregister();
				Register();
			}
#endif
			TNManager.onPlayerLeave += OnPlayerLeave;
			TNManager.onLeaveChannel += OnLeaveChannel;
		}

		void OnDisable ()
		{
			TNManager.onPlayerLeave -= OnPlayerLeave;
			TNManager.onLeaveChannel -= OnLeaveChannel;
		}

		/// <summary>
		/// Automatically transfer the ownership. The same action happens on the server.
		/// </summary>

		void OnPlayerLeave (int channelID, Player p)
		{
			if (channelID == this.channelID && p != null && mOwner == p)
				mOwner = TNManager.GetHost(channelID);
		}

		/// <summary>
		/// Destroy this object when leaving the scene it belongs to, but only if this is a dynamic object.
		/// </summary>

		void OnLeaveChannel (int channelID)
		{
			if (parent == null && this.channelID == channelID && uid > 32767)
			{
#if UNITY_EDITOR && W2
				if (TNManager.isConnected)
				{
					var pv = ControllableEntity.controlled;
					if (pv != null && pv == GetComponent<ControllableEntity>())
					{
						var tile = ProceduralTerrain.GetTile(channelID);
						Debug.LogWarning("Destroying a channel " + channelID + " with the player's vehicle still in it!\n" +
							"pos: " + pv.truePosition.x + " " + pv.truePosition.z + ", " + (tile != null ? tile.ix + " " + tile.iz : "null"));
					}
				}
#endif
				Object.Destroy(gameObject);
			}
		}

		/// <summary>
		/// Retrieve the Tasharen Network Object by ID.
		/// </summary>

		static public TNObject Find (int channelID, uint tnID)
		{
			if (mDictionary == null) return null;
			TNObject tno = null;
			Dictionary<uint, TNObject> dict;
			if (!mDictionary.TryGetValue(channelID, out dict)) return null;
			if (!dict.TryGetValue(tnID, out tno)) return null;
			return tno;
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
						TNObject ts = list[i];
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
						TNObject ts = list[i];
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
			List<TNObject> temp = null;

			foreach (var pair in mList)
			{
				var list = pair.Value;

				for (int i = 0; i < list.size; ++i)
				{
					var ts = list[i];

					if (ts != null && ts.channelID == channelID)
					{
						if (temp == null) temp = new List<TNObject>();
						temp.Add(ts);
					}
				}
			}

			if (temp != null) foreach (var ts in temp) ts.OnDestroyPacket();
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
								".\nPlease make sure that the network IDs are unique.", this);
						}
						else
						{
							Debug.LogError("Network ID of 0 is used by " + GetHierarchy(gameObject) +
								"\nPlease make sure that a unique non-zero ID is given to all objects.", this);
						}
					}
					uid = GetUniqueID(false);
				}
			}
		}
#endif

		/// <summary>
		/// Register the object with the lists.
		/// </summary>

		void Start ()
		{
			if (uid == 0 && !TNManager.isConnected && Application.isPlaying) uid = GetUniqueID(true);

			if (uid == 0)
			{
				mParentCheck = true;

				if (parent != null)
				{
#if UNITY_EDITOR
					Debug.LogWarning("It's not a good idea to nest network objects. TNBehaviour-derived scripts will find " +
						"the root network object by default. Unexpected behaviours may occur with nested networked objects, " +
						"such as certain RFCs not being called. If you need to join multiple networked objects together " +
						"(such as a player avatar traveling inside a car), consider using a FixedJoint instead.", this);
#endif
				}
				else if (Application.isPlaying)
				{
					Debug.LogError("Objects that are not instantiated via TNManager.Create must have a non-zero ID.", this);
					return;
				}
			}
			else
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
		/// Remove this object from the list.
		/// </summary>

		void OnDestroy () { Unregister(); }

		/// <summary>
		/// Register the network object with the lists.
		/// </summary>

		public void Register ()
		{
			if (!mIsRegistered && parent == null)
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
						list = new TNet.List<TNObject>();
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
						if (list.size == 0) mList.Remove(channelID);
					}
				}

				mIsRegistered = false;
			}
		}

		/// <summary>
		/// Invoke the function specified by the ID.
		/// </summary>

		public bool Execute (byte funcID, params object[] parameters)
		{
			if (parent == null)
			{
				if (rebuildMethodList)
					RebuildMethodList();

				CachedFunc ent;

				if (mDict0.TryGetValue(funcID, out ent))
				{
					if (ent.parameters == null)
						ent.parameters = ent.mi.GetParameters();

					try
					{
						ent.mi.Invoke(ent.obj, parameters);
						return true;
					}
					catch (System.Exception ex)
					{
						if (ex.GetType() == typeof(System.NullReferenceException)) return false;
						UnityTools.PrintException(ex, ent, funcID, "", parameters);
						return false;
					}
				}
				return false;
			}
			else return mParent.Execute(funcID, parameters);
		}

		/// <summary>
		/// Invoke the function specified by the function name.
		/// </summary>

		public bool Execute (string funcName, params object[] parameters)
		{
			if (parent == null)
			{
				if (rebuildMethodList)
					RebuildMethodList();

				CachedFunc ent;

				if (mDict1.TryGetValue(funcName, out ent))
				{
					if (ent.parameters == null)
						ent.parameters = ent.mi.GetParameters();

					try
					{
						ent.mi.Invoke(ent.obj, parameters);
						return true;
					}
					catch (System.Exception ex)
					{
						if (ex.GetType() == typeof(System.NullReferenceException)) return false;
						UnityTools.PrintException(ex, ent, 0, funcName, parameters);
						return false;
					}
				}
				return false;
			}
			else return mParent.Execute(funcName, parameters);
		}

		/// <summary>
		/// Invoke the specified function. It's unlikely that you will need to call this function yourself.
		/// </summary>

		static public void FindAndExecute (int channelID, uint objID, byte funcID, params object[] parameters)
		{
			TNObject obj = TNObject.Find(channelID, objID);

			if (obj != null)
			{
				if (obj.Execute(funcID, parameters)) return;
#if UNITY_EDITOR
				if (!obj.ignoreWarnings && !TNManager.IsJoiningChannel(channelID))
					Debug.LogWarning("[TNet] Unable to execute function with ID of '" + funcID + "'. Make sure there is a script that can receive this call.\n" +
						"GameObject: " + GetHierarchy(obj.gameObject), obj.gameObject);
#endif
			}
#if UNITY_EDITOR
			else Debug.LogWarning("[TNet] Trying to execute RFC #" + funcID + " on TNObject #" + objID + " before it has been created.");
#endif
		}

		/// <summary>
		/// Invoke the specified function. It's unlikely that you will need to call this function yourself.
		/// </summary>

		static public void FindAndExecute (int channelID, uint objID, string funcName, params object[] parameters)
		{
			TNObject obj = TNObject.Find(channelID, objID);

			if (obj != null)
			{
				if (obj.Execute(funcName, parameters)) return;
#if UNITY_EDITOR
				if (!obj.ignoreWarnings && !TNManager.IsJoiningChannel(channelID))
					Debug.LogWarning("[TNet] Unable to execute function '" + funcName + "'. Did you forget an [RFC] prefix, perhaps?\n" +
						"GameObject: " + GetHierarchy(obj.gameObject), obj.gameObject);
#endif
			}
#if UNITY_EDITOR
			else Debug.LogWarning("[TNet] Trying to execute a function '" + funcName + "' on TNObject #" + objID + " before it has been created.");
#endif
		}

		[System.NonSerialized] static System.Collections.Generic.List<MonoBehaviour> mTempMono = new System.Collections.Generic.List<MonoBehaviour>();
		[System.NonSerialized]
		static Dictionary<System.Type, System.Collections.Generic.List<CachedMethodInfo>> mMethodCache =
			new Dictionary<System.Type, System.Collections.Generic.List<CachedMethodInfo>>();

		public class CachedMethodInfo
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
			mDict0.Clear();
			mDict1.Clear();
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
						var ent = cache[b];
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
					}
					else if (ci.rfc.property != null)
					{
						mDict1[ci.name + "/" + ci.rfc.GetUniqueID(mb)] = ent;
					}
					else mDict1[ci.name] = ent;
				}
			}
		}

		/// <summary>
		/// Send a remote function call.
		/// </summary>

		public void Send (byte rfcID, Target target, params object[] objs) { SendRFC(rfcID, null, target, true, objs); }

		/// <summary>
		/// Send a remote function call.
		/// Note that you should not use this version of the function if you care about performance (as it's much slower than others),
		/// or if players can have duplicate names, as only one of them will actually receive this message.
		/// </summary>

		public void Send (byte rfcID, string targetName, params object[] objs) { SendRFC(rfcID, null, targetName, true, objs); }

		/// <summary>
		/// Send a remote function call.
		/// </summary>

		public void Send (string rfcName, Target target, params object[] objs) { SendRFC(0, rfcName, target, true, objs); }

		/// <summary>
		/// Send a remote function call.
		/// Note that you should not use this version of the function if you care about performance (as it's much slower than others),
		/// or if players can have duplicate names, as only one of them will actually receive this message.
		/// </summary>

		public void Send (string rfcName, string targetName, params object[] objs) { SendRFC(0, rfcName, targetName, true, objs); }

		/// <summary>
		/// Send a remote function call.
		/// </summary>

		public void Send (byte rfcID, Player target, params object[] objs)
		{
			if (target != null) SendRFC(rfcID, null, target.id, true, objs);
			else SendRFC(rfcID, null, Target.All, true, objs);
		}

		/// <summary>
		/// Send a remote function call.
		/// </summary>

		public void Send (string rfcName, Player target, params object[] objs)
		{
			if (target != null) SendRFC(0, rfcName, target.id, true, objs);
			else SendRFC(0, rfcName, Target.All, true, objs);
		}

		/// <summary>
		/// Send a remote function call.
		/// </summary>

		public void Send (byte rfcID, int playerID, params object[] objs) { SendRFC(rfcID, null, playerID, true, objs); }

		/// <summary>
		/// Send a remote function call.
		/// </summary>

		public void Send (string rfcName, int playerID, params object[] objs) { SendRFC(0, rfcName, playerID, true, objs); }

		/// <summary>
		/// Send a remote function call via UDP (if possible).
		/// </summary>

		public void SendQuickly (byte rfcID, Target target, params object[] objs) { SendRFC(rfcID, null, target, false, objs); }

		/// <summary>
		/// Send a remote function call via UDP (if possible).
		/// </summary>

		public void SendQuickly (string rfcName, Target target, params object[] objs) { SendRFC(0, rfcName, target, false, objs); }

		/// <summary>
		/// Send a remote function call via UDP (if possible).
		/// </summary>

		public void SendQuickly (byte rfcID, Player target, params object[] objs)
		{
			if (target != null) SendRFC(rfcID, null, target.id, false, objs);
			else SendRFC(rfcID, null, Target.All, false, objs);
		}

		/// <summary>
		/// Send a remote function call via UDP (if possible).
		/// </summary>

		public void SendQuickly (string rfcName, Player target, params object[] objs) { SendRFC(0, rfcName, target.id, false, objs); }

		/// <summary>
		/// Send a broadcast to the entire LAN. Does not require an active connection.
		/// </summary>

		public void BroadcastToLAN (int port, byte rfcID, params object[] objs) { BroadcastToLAN(port, rfcID, null, objs); }

		/// <summary>
		/// Send a broadcast to the entire LAN. Does not require an active connection.
		/// </summary>

		public void BroadcastToLAN (int port, string rfcName, params object[] objs) { BroadcastToLAN(port, 0, rfcName, objs); }

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

		static uint GetUID (uint objID, byte rfcID)
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

		void SendRFC (byte rfcID, string rfcName, Target target, bool reliable, params object[] objs)
		{
#if UNITY_EDITOR
			if (!Application.isPlaying) return;

			//Debug.Log("Sending " + rfcName);
#endif
			if (parent == null)
			{
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
				bool executeLocally = false;
				bool connected = TNManager.isConnected;

				if (target == Target.Broadcast)
				{
					if (connected)
					{
						if (uid != 0)
						{
							BinaryWriter writer = TNManager.BeginSend(Packet.Broadcast);
							writer.Write(TNManager.playerID);
							writer.Write(channelID);
							writer.Write(GetUID(uid, rfcID));
							if (rfcID == 0) writer.Write(rfcName);
							writer.WriteArray(objs);
							TNManager.EndSend(channelID, reliable);
						}
#if UNITY_EDITOR
						else Debug.LogWarning("Network object ID of 0 can't be used for communication. Use TNManager.Instantiate to create your objects.", this);
#endif
					}
					else executeLocally = true;
				}
				else if (target == Target.Admin)
				{
					if (connected)
					{
						if (uid != 0)
						{
							BinaryWriter writer = TNManager.BeginSend(Packet.BroadcastAdmin);
							writer.Write(TNManager.playerID);
							writer.Write(channelID);
							writer.Write(GetUID(uid, rfcID));
							if (rfcID == 0) writer.Write(rfcName);
							writer.WriteArray(objs);
							TNManager.EndSend(channelID, reliable);
						}
#if UNITY_EDITOR
						else Debug.LogWarning("Network object ID of 0 can't be used for communication. Use TNManager.Instantiate to create your objects.", this);
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

					if (connected && TNManager.IsInChannel(channelID))
					{
						if (uid != 0)
						{
							byte packetID = (byte)((int)Packet.ForwardToAll + (int)target);
							BinaryWriter writer = TNManager.BeginSend(packetID);
							writer.Write(TNManager.playerID);
							writer.Write(channelID);
							writer.Write(GetUID(uid, rfcID));
							if (rfcID == 0) writer.Write(rfcName);
							writer.WriteArray(objs);
							TNManager.EndSend(channelID, reliable);
						}
#if UNITY_EDITOR
						else Debug.LogWarning("Network object ID of 0 can't be used for communication. Use TNManager.Instantiate to create your objects.", this);
#endif
					}
				}

				if (executeLocally)
				{
					if (rfcID != 0) Execute(rfcID, objs);
					else Execute(rfcName, objs);
				}
			}
			else mParent.SendRFC(rfcID, rfcName, target, reliable, objs);
		}

		/// <summary>
		/// Send a new RFC call to the specified target.
		/// </summary>

		void SendRFC (byte rfcID, string rfcName, string targetName, bool reliable, params object[] objs)
		{
#if UNITY_EDITOR
			if (!Application.isPlaying) return;
#endif
			if (parent == null)
			{
				if (mDestroyed != 0 || uid == 0 || string.IsNullOrEmpty(targetName)) return;

				if (targetName == TNManager.playerName)
				{
					if (rfcID != 0) Execute(rfcID, objs);
					else Execute(rfcName, objs);
				}
				else
				{
					BinaryWriter writer = TNManager.BeginSend(Packet.ForwardByName);
					writer.Write(TNManager.playerID);
					writer.Write(targetName);
					writer.Write(channelID);
					writer.Write(GetUID(uid, rfcID));
					if (rfcID == 0) writer.Write(rfcName);
					writer.WriteArray(objs);
					TNManager.EndSend(channelID, reliable);
				}
			}
			else mParent.SendRFC(rfcID, rfcName, targetName, reliable, objs);
		}

		/// <summary>
		/// Send a new remote function call to the specified player.
		/// </summary>

		void SendRFC (byte rfcID, string rfcName, int target, bool reliable, params object[] objs)
		{
			if (parent == null)
			{
				if (hasBeenDestroyed || uid == 0) return;

				if (TNManager.isConnected)
				{
					BinaryWriter writer = TNManager.BeginSend(Packet.ForwardToPlayer);
					writer.Write(TNManager.playerID);
					writer.Write(target);
					writer.Write(channelID);
					writer.Write(GetUID(uid, rfcID));
					if (rfcID == 0) writer.Write(rfcName);
					writer.WriteArray(objs);
					TNManager.EndSend(channelID, reliable);
				}
				else if (target == TNManager.playerID)
				{
					if (rfcID != 0) Execute(rfcID, objs);
					else Execute(rfcName, objs);
				}
			}
			else mParent.SendRFC(rfcID, rfcName, target, reliable, objs);
		}

		/// <summary>
		/// Broadcast a remote function call to all players on the network.
		/// </summary>

		void BroadcastToLAN (int port, byte rfcID, string rfcName, params object[] objs)
		{
			if (parent == null)
			{
				if (hasBeenDestroyed || uid == 0) return;
				BinaryWriter writer = TNManager.BeginSend(Packet.ForwardToAll);
				writer.Write(TNManager.playerID);
				writer.Write(channelID);
				writer.Write(GetUID(uid, rfcID));
				if (rfcID == 0) writer.Write(rfcName);
				writer.WriteArray(objs);
				TNManager.EndSendToLAN(port);
			}
			else mParent.BroadcastToLAN(port, rfcID, rfcName, objs);
		}

		/// <summary>
		/// Remove a previously saved remote function call.
		/// </summary>

		static void RemoveSavedRFC (int channelID, uint objID, byte rfcID, string funcName)
		{
			if (TNManager.IsInChannel(channelID))
			{
				BinaryWriter writer = TNManager.BeginSend(Packet.RequestRemoveRFC);
				writer.Write(channelID);
				writer.Write(GetUID(objID, rfcID));
				if (rfcID == 0) writer.Write(funcName);
				TNManager.EndSend(channelID, true);
			}
		}

		/// <summary>
		/// Transfer this object to another channel. Only the object's owner can perform this action.
		/// Note that if the object has a nested TNObject hierarchy, newly entered clients won't see this hierarchy.
		/// It's up to you, the developer, to set the hierarchy if you need it. You can do it by setting a custom RFC
		/// with the hierarchy path (or simply the TNObject parent ID) before calling TransferToChannel.
		/// </summary>

		public void TransferToChannel (int newChannelID)
		{
			if (parent == null)
			{
				if (mDestroyed != 0) return;

				if (uid > 32767 && channelID != newChannelID)
				{
//#if W2 && UNITY_EDITOR
//				var pv = ControllableEntity.controlled;

//				if (pv != null && pv == GetComponent<ControllableEntity>())
//				{
//					var before = ProceduralTerrain.GetTile(channelID);
//					var after = ProceduralTerrain.GetTile(newChannelID);
//					Debug.Log("Transfer: " + name + " from " +
//						(before != null ? before.ix + " " + before.iz : channelID.ToString()) + " to " +
//						(after != null ? after.ix + " " + after.iz : newChannelID.ToString()) + "\n", this);
//				}
//#endif
					mDestroyed = 2;

					if (TNManager.isConnected)
					{
						BinaryWriter writer = TNManager.BeginSend(Packet.RequestTransferObject);
						writer.Write(channelID);
						writer.Write(newChannelID);
						writer.Write(uid);
						TNManager.EndSend(channelID, true);
					}
					else FinalizeTransfer(newChannelID, TNObject.GetUniqueID(true));
				}
			}
			else parent.TransferToChannel(newChannelID);
		}

		/// <summary>
		/// This function is called when the object's IDs change. This happens after the object was transferred to another channel.
		/// </summary>

		internal void FinalizeTransfer (int newChannel, uint newObjectID)
		{
			if (onTransfer != null) onTransfer(newChannel, newObjectID);

			if (parent == null)
			{
				Unregister();
				channelID = newChannel;
				uid = newObjectID;
				Register();
				mDestroyed = 0;
			}
			else parent.FinalizeTransfer(newChannel, newObjectID);
		}
	}
}
