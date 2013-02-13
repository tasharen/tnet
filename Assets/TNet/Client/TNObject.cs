//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using System.IO;
using System.Reflection;
using UnityEngine;
using TNet;

/// <summary>
/// Tasharen Network Object makes it possible to easily send and receive remote function calls.
/// Unity networking calls this type of object a "Network View".
/// </summary>

[ExecuteInEditMode]
[AddComponentMenu("TNet/Network Object")]
public sealed class TNObject : MonoBehaviour
{
	/// <summary>
	/// Remote function calls that can't be executed immediately get stored,
	/// and will be executed when an appropriate Object ID gets added.
	/// </summary>

	class DelayedCall
	{
		public uint objID;
		public byte funcID;
		public string funcName;
		public object[] parameters;
	}

	/// <summary>
	/// Remote function calls consist of a method called on some object (such as a MonoBehavior).
	/// This method may or may not have an explicitly specified RFC ID. If an ID is specified, the function
	/// will require less data to be sent across the network as the ID will be sent instead of the function's name.
	/// </summary>

	struct CachedRFC
	{
		public byte rfcID;
		public object obj;
		public MethodInfo func;
	}

	// List of network objs to iterate through
	static List<TNObject> mList = new List<TNObject>();

	// List of network objs to quickly look up
	static System.Collections.Generic.Dictionary<uint, TNObject> mDictionary =
		new System.Collections.Generic.Dictionary<uint, TNObject>();

	// List of delayed calls -- calls that could not execute at the time of the call
	static List<DelayedCall> mDelayed = new List<DelayedCall>();

	/// <summary>
	/// Unique Network Identifier. All TNObjects have them and is how messages arrive at the correct destination.
	/// The ID is supposed to be a 'uint', but Unity is not able to serialize 'uint' types. Sigh.
	/// </summary>

	[SerializeField] int id = 0;

	/// <summary>
	/// Object's unique identifier (Static object IDs range 1 to 32767. Dynamic object IDs range from 32768 to 2^24-1).
	/// </summary>

	public uint uid
	{
		get { return (uint)id; }
		set { id = (int)(value & 0xFFFFFF); }
	}

	/// <summary>
	/// When set to 'true', it will cause the list of remote function calls to be rebuilt next time they're needed.
	/// </summary>

	[HideInInspector] public bool rebuildMethodList = true;

	// Cached RFC functions
	List<CachedRFC> mRFCs = new List<CachedRFC>();

	// Whether the object has been registered with the lists
	bool mIsRegistered = false;

	// ID of the object's owner
	int mOwner = -1;

	/// <summary>
	/// Whether this object belongs to the player.
	/// </summary>

	public bool isMine { get { return (mOwner == -1) ? TNManager.isThisMyObject : mOwner == TNManager.playerID; } }

	/// <summary>
	/// Remember the object's ownership, for convenience.
	/// </summary>

	void Awake () { mOwner = TNManager.objectOwnerID; }

	/// <summary>
	/// Automatically transfer the ownership.
	/// </summary>

	void OnNetworkPlayerLeave (Player p) { if (p.id == mOwner) p.id = TNManager.hostID; }

	/// <summary>
	/// Retrieve the Tasharen Network Object by ID.
	/// </summary>

	static public TNObject Find (uint tnID)
	{
		if (mDictionary == null) return null;
		TNObject tno = null;
		mDictionary.TryGetValue(tnID, out tno);
		return tno;
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

#if UNITY_EDITOR
	// Last used ID
	static uint mLastID = 0;

	/// <summary>
	/// Get a new unique object identifier.
	/// </summary>

	static uint GetUniqueID ()
	{
		TNObject[] tns = (TNObject[])FindObjectsOfType(typeof(TNObject));

		for (int i = 0, imax = tns.Length; i < imax; ++i)
		{
			TNObject ts = tns[i];
			if (ts != null && ts.uid > mLastID && ts.uid < 32768) mLastID = ts.uid;
		}
		return ++mLastID;
	}

	/// <summary>
	/// Make sure that this object's ID is actually unique.
	/// </summary>

	void UniqueCheck ()
	{
		if (id < 0) id = -id;
		TNObject tobj = Find(uid);

		if (id == 0 || tobj != null)
		{
			if (Application.isPlaying && TNManager.isConnected)
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

	/// <summary>
	/// This usually happens after scripts get recompiled.
	/// When this happens, static variables are erased, so the list of objects has to be rebuilt.
	/// </summary>

	void OnEnable ()
	{
		if (!Application.isPlaying)
		{
			Unregister();
			Register();
		}
	}
#endif

	/// <summary>
	/// Register the object with the lists.
	/// </summary>

	void Start () { Register(); }

	/// <summary>
	/// Remove this object from the list.
	/// </summary>

	void OnDestroy () { Unregister(); }

	/// <summary>
	/// Register the network object with the lists.
	/// </summary>

	public void Register ()
	{
		if (!mIsRegistered)
		{
#if UNITY_EDITOR
			UniqueCheck();
#endif
			mDictionary[uid] = this;
			mList.Add(this);
			mIsRegistered = true;
		}
	}

	/// <summary>
	/// Unregister the network object.
	/// </summary>

	void Unregister ()
	{
		if (mIsRegistered)
		{
			if (mDictionary != null) mDictionary.Remove(uid);
			if (mList != null) mList.Remove(this);
			mIsRegistered = false;
		}
	}

	/// <summary>
	/// Invoke the function specified by the ID.
	/// </summary>

	public bool Execute (byte funcID, params object[] parameters)
	{
		if (rebuildMethodList) RebuildMethodList();

		bool retVal = false;

		for (int i = 0; i < mRFCs.size; ++i)
		{
			CachedRFC ent = mRFCs[i];

			if (ent.rfcID == funcID)
			{
				retVal = true;
#if UNITY_EDITOR
				try
				{
					ParameterInfo[] infos = ent.func.GetParameters();

					if (infos.Length == 1 && infos[0].ParameterType == typeof(object[]))
					{
						ent.func.Invoke(ent.obj, new object[] { parameters });
					}
					else
					{
						ent.func.Invoke(ent.obj, parameters);
					}
				}
				catch (System.Exception ex)
				{
					string types = "";
					
					for (int b = 0; b < parameters.Length; ++b)
					{
						if (b != 0) types += ", ";
						types += parameters[b].GetType().ToString();
					}
					Debug.LogError(ex.Message + "\n" + ent.obj.GetType() + "." + ent.func.Name + " (" + types + ")");
				}
#else
				ParameterInfo[] infos = ent.func.GetParameters();

				if (infos.Length == 1 && infos[0].ParameterType == typeof(object[]))
				{
					ent.func.Invoke(ent.obj, new object[] { parameters });
				}
				else
				{
					ent.func.Invoke(ent.obj, parameters);
				}
#endif
			}
		}
		return retVal;
	}

	/// <summary>
	/// Invoke the function specified by the function name.
	/// </summary>

	public bool Execute (string funcName, params object[] parameters)
	{
		if (rebuildMethodList) RebuildMethodList();

		bool retVal = false;

		for (int i = 0; i < mRFCs.size; ++i)
		{
			CachedRFC ent = mRFCs[i];

			if (ent.func.Name == funcName)
			{
				retVal = true;
#if UNITY_EDITOR
				try
				{
					ent.func.Invoke(ent.obj, parameters);
				}
				catch (System.Exception ex)
				{
					string types = "";

					for (int b = 0; b < parameters.Length; ++b)
					{
						if (b != 0) types += ", ";
						types += parameters[b].GetType().ToString();
					}
					Debug.LogError(ex.Message + "\n" + ent.obj.GetType() + "." + ent.func.Name + " (" + types + ")");
				}
#else
				ent.func.Invoke(ent.obj, parameters);
#endif
			}
		}
		return retVal;
	}

	/// <summary>
	/// Invoke the specified function. It's unlikely that you will need to call this function yourself.
	/// </summary>

	static public void FindAndExecute (uint objID, byte funcID, params object[] parameters)
	{
		TNObject obj = TNObject.Find(objID);

		if (obj != null)
		{
			obj.Execute(funcID, parameters);
		}
		else if (TNManager.isConnected)
		{
			DelayedCall dc = new DelayedCall();
			dc.objID = objID;
			dc.funcID = funcID;
			dc.parameters = parameters;
			mDelayed.Add(dc);
		}
#if UNITY_EDITOR
		else Debug.LogError("Trying to execute a function " + funcID + " on TNObject #" + objID +
			" before it has been created.");
#endif
	}

	/// <summary>
	/// Invoke the specified function. It's unlikely that you will need to call this function yourself.
	/// </summary>

	static public void FindAndExecute (uint objID, string funcName, params object[] parameters)
	{
		TNObject obj = TNObject.Find(objID);

		if (obj != null)
		{
			obj.Execute(funcName, parameters);
		}
		else if (TNManager.isConnected)
		{
			DelayedCall dc = new DelayedCall();
			dc.objID = objID;
			dc.funcName = funcName;
			dc.parameters = parameters;
			mDelayed.Add(dc);
		}
#if UNITY_EDITOR
		else Debug.LogError("Trying to execute a function '" + funcName + "' on TNObject #" + objID +
			" before it has been created.");
#endif
	}

	/// <summary>
	/// Rebuild the list of known RFC calls.
	/// </summary>

	void RebuildMethodList ()
	{
		rebuildMethodList = false;
		mRFCs.Clear();
		MonoBehaviour[] mbs = GetComponents<MonoBehaviour>();

		for (int i = 0, imax = mbs.Length; i < imax; ++i)
		{
			MonoBehaviour mb = mbs[i];
			System.Type type = mb.GetType();

			MethodInfo[] methods = type.GetMethods(
				BindingFlags.Public |
				BindingFlags.NonPublic |
				BindingFlags.Instance);

			for (int b = 0; b < methods.Length; ++b)
			{
				if (methods[b].IsDefined(typeof(RFC), true))
				{
					CachedRFC ent = new CachedRFC();
					ent.obj = mb;
					ent.func = methods[b];

					RFC tnc = (RFC)ent.func.GetCustomAttributes(typeof(RFC), true)[0];
					ent.rfcID = tnc.id;
					mRFCs.Add(ent);
				}
			}
		}
	}

	/// <summary>
	/// Send a remote function call.
	/// </summary>

	public void Send (byte rfcID, Target target, params object[] objs) { SendRFC(uid, rfcID, null, target, true, objs); }

	/// <summary>
	/// Send a remote function call.
	/// </summary>

	public void Send (string rfcName, Target target, params object[] objs) { SendRFC(uid, 0, rfcName, target, true, objs); }

	/// <summary>
	/// Send a remote function call.
	/// </summary>

	public void Send (byte rfcID, Player target, params object[] objs) { SendRFC(uid, rfcID, null, target, true, objs); }

	/// <summary>
	/// Send a remote function call.
	/// </summary>

	public void Send (string rfcName, Player target, params object[] objs) { SendRFC(uid, 0, rfcName, target, true, objs); }

	/// <summary>
	/// Send a remote function call via UDP (if possible).
	/// </summary>

	public void SendQuickly (byte rfcID, Target target, params object[] objs) { SendRFC(uid, rfcID, null, target, false, objs); }

	/// <summary>
	/// Send a remote function call via UDP (if possible).
	/// </summary>

	public void SendQuickly (string rfcName, Target target, params object[] objs) { SendRFC(uid, 0, rfcName, target, false, objs); }

	/// <summary>
	/// Send a remote function call via UDP (if possible).
	/// </summary>

	public void SendQuickly (byte rfcID, Player target, params object[] objs) { SendRFC(uid, rfcID, null, target, false, objs); }

	/// <summary>
	/// Send a remote function call via UDP (if possible).
	/// </summary>

	public void SendQuickly (string rfcName, Player target, params object[] objs) { SendRFC(uid, 0, rfcName, target, false, objs); }

	/// <summary>
	/// Send a broadcast to the entire LAN. Does not require an active connection.
	/// </summary>

	public void BroadcastToLAN (int port, byte rfcID, params object[] objs) { BroadcastToLAN(port, uid, rfcID, null, objs); }

	/// <summary>
	/// Send a broadcast to the entire LAN. Does not require an active connection.
	/// </summary>

	public void BroadcastToLAN (int port, string rfcName, params object[] objs) { BroadcastToLAN(port, uid, 0, rfcName, objs); }

	/// <summary>
	/// Remove a previously saved remote function call.
	/// </summary>

	public void Remove (string rfcName) { RemoveSavedRFC(uid, 0, rfcName); }

	/// <summary>
	/// Remove a previously saved remote function call.
	/// </summary>

	public void Remove (byte rfcID) { RemoveSavedRFC(uid, rfcID, null); }

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

	static void SendRFC (uint objID, byte rfcID, string rfcName, Target target, bool reliable, params object[] objs)
	{
#if UNITY_EDITOR
		if (!Application.isPlaying) return;
#endif
		bool executeLocally = (target == Target.Host && TNManager.isHosting);

		if (!reliable)
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

		if (!executeLocally && TNManager.isConnected)
		{
			byte packetID = (byte)((int)Packet.ForwardToAll + (int)target);
			BinaryWriter writer = TNManager.BeginSend(packetID);
			writer.Write(GetUID(objID, rfcID));
			if (rfcID == 0) writer.Write(rfcName);
			UnityTools.Write(writer, objs);
			TNManager.EndSend(reliable);
		}
		else if (target == Target.All || target == Target.AllSaved)
		{
			executeLocally = true;
		}
		
		if (executeLocally)
		{
			if (rfcID != 0)
			{
				TNObject.FindAndExecute(objID, rfcID, objs);
			}
			else
			{
				TNObject.FindAndExecute(objID, rfcName, objs);
			}
		}
	}

	/// <summary>
	/// Send a new remote function call to the specified player.
	/// </summary>

	static void SendRFC (uint objID, byte rfcID, string rfcName, Player target, bool reliable, params object[] objs)
	{
		if (TNManager.isConnected)
		{
			BinaryWriter writer = TNManager.BeginSend(Packet.ForwardToPlayer);
			writer.Write(target.id);
			writer.Write(GetUID(objID, rfcID));
			if (rfcID == 0) writer.Write(rfcName);
			UnityTools.Write(writer, objs);
			TNManager.EndSend(reliable);
		}
	}

	/// <summary>
	/// Broadcast a remote function call to all players on the network.
	/// </summary>

	static void BroadcastToLAN (int port, uint objID, byte rfcID, string rfcName, params object[] objs)
	{
		BinaryWriter writer = TNManager.BeginSend(Packet.ForwardToAll);
		writer.Write(GetUID(objID, rfcID));
		if (rfcID == 0) writer.Write(rfcName);
		UnityTools.Write(writer, objs);
		TNManager.EndSend(port);
	}

	/// <summary>
	/// Remove a previously saved remote function call.
	/// </summary>

	static void RemoveSavedRFC (uint objID, byte rfcID, string funcName)
	{
		if (TNManager.isConnected)
		{
			BinaryWriter writer = TNManager.BeginSend(Packet.RequestRemoveRFC);
			writer.Write(GetUID(objID, rfcID));
			if (rfcID == 0) writer.Write(funcName);
			TNManager.EndSend();
		}
	}
}
