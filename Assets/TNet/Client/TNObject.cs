//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
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

	[SerializeField] int id = 0;

	/// <summary>
	/// ID of the channel this TNObject belongs to.
	/// </summary>

	[System.NonSerialized][HideInInspector] public int channelID = 0;

	/// <summary>
	/// Object's unique identifier (Static object IDs range 1 to 32767. Dynamic object IDs range from 32,768 to 16,777,215).
	/// </summary>

	public uint uid
	{
		get
		{
			return (mParent != null) ? mParent.uid : (uint)id;
		}
		set
		{
			if (mParent != null) mParent.uid = value;
			else id = (int)(value & 0xFFFFFF);
		}
	}

	/// <summary>
	/// TNObject's parent, if it has any.
	/// </summary>

	public TNObject parent { get { return mParent; } }

	/// <summary>
	/// When set to 'true', it will cause the list of remote function calls to be rebuilt next time they're needed.
	/// </summary>

	[System.NonSerialized][HideInInspector] public bool rebuildMethodList = true;

	// Cached RFC functions
	[System.NonSerialized] Dictionary<int, CachedFunc> mDict0 = new Dictionary<int, CachedFunc>();
	[System.NonSerialized] Dictionary<string, CachedFunc> mDict1 = new Dictionary<string, CachedFunc>();

	// Whether the object has been registered with the lists
	[System.NonSerialized] bool mIsRegistered = false;

	// ID of the object's owner
	[System.NonSerialized] Player mOwner = null;

	// Child objects don't get their own unique IDs, so if we have a parent TNObject, that's the object that will be getting all events.
	[System.NonSerialized] TNObject mParent = null;

	/// <summary>
	/// When objects get destroyed, they immediately get marked as such so that no RFCs go out between the destroy call
	/// and the response coming back from the server.
	/// </summary>
	
	[System.NonSerialized] bool mDestroyed = false;

	public delegate void OnDestroyCallback ();

	/// <summary>
	/// If you want to know when this object is getting destroyed, subscribe to this delegate.
	/// This delegate is guaranteed to be called before OnDestroy() notifications get sent out.
	/// This is useful if you want parts of the object to remain behind (such as buggy Unity 4 cloth).
	/// </summary>

	[System.NonSerialized] public OnDestroyCallback onDestroy;

	/// <summary>
	/// Whether this object belongs to the player.
	/// </summary>

	public bool isMine
	{
		get
		{
			var owner = this.owner;
			return (owner != null) ? owner == TNManager.player : TNManager.isHosting;
		}
	}

	/// <summary>
	/// ID of the player that owns this object.
	/// </summary>

	public int ownerID { get { return (mParent != null) ? mParent.ownerID : (mOwner ?? TNManager.GetHost(channelID)).id; } }

	/// <summary>
	/// ID of the player that owns this object.
	/// </summary>

	public Player owner { get { return (mParent != null) ? mParent.owner : (mOwner ?? TNManager.GetHost(channelID)); } }

	/// <summary>
	/// Destroy this game object on all connected clients and remove it from the server.
	/// </summary>

	[ContextMenu("Destroy")]
	public void DestroySelf ()
	{
		if (!mDestroyed)
		{
			mDestroyed = true;

			if (TNManager.IsInChannel(channelID))
			{
				if (TNManager.IsChannelLocked(channelID))
				{
					Debug.LogWarning("Trying to destroy an object in a locked channel. Call will be ignored.");
				}
				else
				{
					Invoke("EnsureDestroy", 5f);
					BinaryWriter bw = TNManager.BeginSend(Packet.RequestDestroyObject);
					bw.Write(channelID);
					bw.Write(uid);
					TNManager.EndSend(channelID, true);
				}
			}
			else
			{
				if (onDestroy != null) onDestroy();
				Object.Destroy(gameObject);
			}
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
	/// If this function is still here in 5 seconds then something went wrong, so force-destroy the object.
	/// </summary>

	void EnsureDestroy ()
	{
		if (onDestroy != null) onDestroy();
		Object.Destroy(gameObject);
	}

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
		if (!Application.isPlaying && id != 0)
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
		if (this.channelID == channelID && uid > 32767)
			Object.Destroy(gameObject);
	}

	/// <summary>
	/// Retrieve the Tasharen Network Object by ID.
	/// </summary>

	static public TNObject Find (int channelID, uint tnID)
	{
		if (mDictionary == null) return null;
		TNObject tno = null;

		if (channelID == 0)
		{
			// Broadcasts are sent with the channel ID of '0'
			foreach (KeyValuePair<int, TNet.List<TNObject>> pair in mList)
			{
				TNet.List<TNObject> list = pair.Value;

				for (int i = 0; i < list.size; ++i)
				{
					TNObject ts = list[i];
					if (ts.id == tnID) return ts;
				}
			}
		}
		else
		{
			Dictionary<uint, TNObject> dict;
			if (!mDictionary.TryGetValue(channelID, out dict)) return null;
			if (!dict.TryGetValue(tnID, out tno)) return null;
		}
		return tno;
	}

#if UNITY_EDITOR
	// Last used ID
	static uint mLastID = 0;

	/// <summary>
	/// Get a new unique object identifier.
	/// </summary>

	static internal uint GetUniqueID ()
	{
		foreach (KeyValuePair<int, TNet.List<TNObject>> pair in mList)
		{
			TNet.List<TNObject> list = pair.Value;

			for (int i = 0; i < list.size; ++i)
			{
				TNObject ts = list[i];
				if (ts != null && ts.uid > mLastID && ts.uid < 32768) mLastID = ts.uid;
			}
		}
		return ++mLastID;
	}

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
		if (id < 0) id = -id;

		if (id == 0)
		{
			uid = GetUniqueID();
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
						Debug.LogError("Network ID " + id + " is already in use by " +
							GetHierarchy(tobj.gameObject) +
							".\nPlease make sure that the network IDs are unique.", this);
					}
					else
					{
						Debug.LogError("Network ID of 0 is used by " + GetHierarchy(gameObject) +
							"\nPlease make sure that a unique non-zero ID is given to all objects.", this);
					}
				}
				uid = GetUniqueID();
			}
		}
	}
#endif

	/// <summary>
	/// Finds the specified component on the game object or one of its parents.
	/// </summary>

	static TNObject FindParent (Transform t)
	{
		while (t != null)
		{
			TNObject tno = t.gameObject.GetComponent<TNObject>();
			if (tno != null) return tno;
			t = t.parent;
		}
		return null;
	}

	/// <summary>
	/// Register the object with the lists.
	/// </summary>

	void Start ()
	{
		if (id == 0)
		{
			mParent = FindParent(transform.parent);
			if (!TNManager.isConnected) return;

			if (mParent == null && Application.isPlaying)
			{
				Debug.LogError("Objects that are not instantiated via TNManager.Create must have a non-zero ID.", this);
				return;
			}
		}
		else Register();
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
		if (!mIsRegistered && uid != 0 && mParent == null)
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

	/// <summary>
	/// Unregister the network object.
	/// </summary>

	internal void Unregister ()
	{
		if (mIsRegistered)
		{
			if (mDictionary != null)
			{
				Dictionary<uint, TNObject> dict = mDictionary[channelID];

				if (dict != null)
				{
					dict.Remove(uid);
					if (dict.Count == 0) mDictionary.Remove(channelID);
				}
			}

			if (mList != null)
			{
				TNet.List<TNObject> list = mList[channelID];

				if (list != null)
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
		if (mParent != null) return mParent.Execute(funcID, parameters);

		if (rebuildMethodList)
			RebuildMethodList();

		CachedFunc ent;
		
		if (mDict0.TryGetValue(funcID, out ent))
		{
			if (ent.parameters == null)
				ent.parameters = ent.func.GetParameters();

			try
			{
				ent.func.Invoke(ent.obj, parameters);
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

	/// <summary>
	/// Invoke the function specified by the function name.
	/// </summary>

	public bool Execute (string funcName, params object[] parameters)
	{
		if (mParent != null) return mParent.Execute(funcName, parameters);

		if (rebuildMethodList)
			RebuildMethodList();

		CachedFunc ent;

		if (mDict1.TryGetValue(funcName, out ent))
		{
			if (ent.parameters == null)
				ent.parameters = ent.func.GetParameters();

			try
			{
				ent.func.Invoke(ent.obj, parameters);
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
			Debug.LogError("[TNet] Unable to execute function with ID of '" + funcID + "'. Make sure there is a script that can receive this call.\n" +
				"GameObject: " + GetHierarchy(obj.gameObject), obj.gameObject);
#endif
		}
#if UNITY_EDITOR
		else if (TNManager.isJoiningChannel)
		{
			Debug.Log("[TNet] Trying to execute RFC #" + funcID + " on TNObject #" + objID + " before it has been created.");
		}
		else
		{
			Debug.LogWarning("[TNet] Trying to execute RFC #" + funcID + " on TNObject #" + objID + " before it has been created.");
		}
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
			Debug.LogError("[TNet] Unable to execute function '" + funcName + "'. Did you forget an [RFC] prefix, perhaps?\n" +
				"GameObject: " + GetHierarchy(obj.gameObject), obj.gameObject);
#endif
		}
#if UNITY_EDITOR
		else if (TNManager.isJoiningChannel)
		{
			Debug.Log("[TNet] Trying to execute a function '" + funcName + "' on TNObject #" + objID +
				" before it has been created.");
		}
		else
		{
			Debug.LogWarning("[TNet] Trying to execute a function '" + funcName + "' on TNObject #" + objID +
				" before it has been created.");
		}
#endif
	}

	/// <summary>
	/// Rebuild the list of known RFC calls.
	/// </summary>

	void RebuildMethodList ()
	{
		rebuildMethodList = false;
		mDict0.Clear();
		mDict1.Clear();
		MonoBehaviour[] mbs = GetComponentsInChildren<MonoBehaviour>(true);

		for (int i = 0, imax = mbs.Length; i < imax; ++i)
		{
			MonoBehaviour mb = mbs[i];
			System.Type type = mb.GetType();

			MethodInfo[] methods = type.GetMethods(
				BindingFlags.Public |
				BindingFlags.NonPublic |
				BindingFlags.Instance);

			for (int b = 0, bmax = methods.Length; b < bmax; ++b)
			{
				MethodInfo method = methods[b];

				if (method.IsDefined(typeof(RFC), true))
				{
					CachedFunc ent = new CachedFunc();
					ent.obj = mb;
					ent.func = method;

					RFC tnc = (RFC)ent.func.GetCustomAttributes(typeof(RFC), true)[0];

					if (tnc.id > 0)
					{
						if (tnc.id < 256) mDict0[tnc.id] = ent;
						else Debug.LogError("RFC IDs need to be between 1 and 255 (1 byte). If you need more, just don't specify an ID and use the function's name instead.");
					}
					else mDict1[method.Name] = ent;
				}
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

	public void Remove (string rfcName) { RemoveSavedRFC(channelID, uid, 0, rfcName); }

	/// <summary>
	/// Remove a previously saved remote function call.
	/// </summary>

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
#endif
		if (mDestroyed) return;

		// Some very odd special case... sending a string[] as the only parameter
		// results in objs[] being a string[] instead, when it should be object[string[]].
		if (objs != null && objs.GetType() != typeof(object[]))
			objs = new object[] { objs };

		bool executeLocally = false;
		bool connected = TNManager.isConnected;

		if (target == Target.Broadcast)
		{
			if (connected)
			{
				BinaryWriter writer = TNManager.BeginSend(Packet.Broadcast);
				writer.Write(TNManager.playerID);
				writer.Write(channelID);
				writer.Write(GetUID(uid, rfcID));
				if (rfcID == 0) writer.Write(rfcName);
				writer.WriteArray(objs);
				TNManager.EndSend(channelID, reliable);
			}
			else executeLocally = true;
		}
		else if (target == Target.Admin)
		{
			if (connected)
			{
				BinaryWriter writer = TNManager.BeginSend(Packet.BroadcastAdmin);
				writer.Write(TNManager.playerID);
				writer.Write(channelID);
				writer.Write(GetUID(uid, rfcID));
				if (rfcID == 0) writer.Write(rfcName);
				writer.WriteArray(objs);
				TNManager.EndSend(channelID, reliable);
			}
			else executeLocally = true;
		}
		else if (target == Target.Host && TNManager.isHosting)
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
				byte packetID = (byte)((int)Packet.ForwardToAll + (int)target);
				BinaryWriter writer = TNManager.BeginSend(packetID);
				writer.Write(TNManager.playerID);
				writer.Write(channelID);
				writer.Write(GetUID(uid, rfcID));
				if (rfcID == 0) writer.Write(rfcName);
				writer.WriteArray(objs);
				TNManager.EndSend(channelID, reliable);
			}
		}

		if (executeLocally)
		{
			if (rfcID != 0) Execute(rfcID, objs);
			else Execute(rfcName, objs);
		}
	}

	/// <summary>
	/// Send a new RFC call to the specified target.
	/// </summary>

	void SendRFC (byte rfcID, string rfcName, string targetName, bool reliable, params object[] objs)
	{
#if UNITY_EDITOR
		if (!Application.isPlaying) return;
#endif
		if (mDestroyed || string.IsNullOrEmpty(targetName)) return;

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

	/// <summary>
	/// Send a new remote function call to the specified player.
	/// </summary>

	void SendRFC (byte rfcID, string rfcName, int target, bool reliable, params object[] objs)
	{
		if (mDestroyed) return;

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

	/// <summary>
	/// Broadcast a remote function call to all players on the network.
	/// </summary>

	void BroadcastToLAN (int port, byte rfcID, string rfcName, params object[] objs)
	{
		if (mDestroyed) return;
		BinaryWriter writer = TNManager.BeginSend(Packet.ForwardToAll);
		writer.Write(TNManager.playerID);
		writer.Write(channelID);
		writer.Write(GetUID(uid, rfcID));
		if (rfcID == 0) writer.Write(rfcName);
		writer.WriteArray(objs);
		TNManager.EndSendToLAN(port);
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
	/// </summary>

	public void TransferToChannel (int newChannelID)
	{
		if (!mDestroyed && isMine && channelID != newChannelID && TNManager.IsInChannel(channelID))
		{
			mDestroyed = true;
			BinaryWriter writer = TNManager.BeginSend(Packet.RequestTransferObject);
			writer.Write(channelID);
			writer.Write(newChannelID);
			writer.Write(uid);
			TNManager.EndSend(channelID, true);
		}
	}

	/// <summary>
	/// This function is called when the object's IDs change. This happens after the object was transferred to another channel.
	/// </summary>

	internal void FinalizeTransfer (int newChannel, uint newObjectID)
	{
		Unregister();
		channelID = newChannel;
		uid = newObjectID;
		Register();
		mDestroyed = false;
	}
}
}
