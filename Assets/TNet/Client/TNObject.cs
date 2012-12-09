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
public class TNObject : MonoBehaviour
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

	public int id = 0;

	/// <summary>
	/// When set to 'true', it will cause the list of remote function calls to be rebuilt next time they're needed.
	/// </summary>

	[HideInInspector] public bool rebuildMethodList = true;

	// Cached RFC functions
	List<CachedRFC> mRFCs = new List<CachedRFC>();

	// Whether the object has been registered with the lists
	bool mIsRegistered = false;

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
	static int mLastID = 0;

	/// <summary>
	/// Get a new unique object identifier.
	/// </summary>

	static int GetUniqueID ()
	{
		TNObject[] tns = (TNObject[])FindObjectsOfType(typeof(TNObject));

		for (int i = 0, imax = tns.Length; i < imax; ++i)
		{
			TNObject ts = tns[i];
			if (ts != null && ts.id > mLastID) mLastID = ts.id;
		}
		return ++mLastID;
	}

	/// <summary>
	/// Make sure that this object's ID is actually unique.
	/// </summary>

	void UniqueCheck ()
	{
		TNObject tobj = Find((uint)id);

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
			id = GetUniqueID();
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
			mDictionary[(uint)id] = this;
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
			if (mDictionary != null) mDictionary.Remove((uint)id);
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
				ent.func.Invoke(ent.obj, parameters);
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
				ent.func.Invoke(ent.obj, parameters);
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
		else
		{
			DelayedCall dc = new DelayedCall();
			dc.objID = objID;
			dc.funcID = funcID;
			dc.parameters = parameters;
			mDelayed.Add(dc);
		}
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
		else
		{
			DelayedCall dc = new DelayedCall();
			dc.objID = objID;
			dc.funcName = funcName;
			dc.parameters = parameters;
			mDelayed.Add(dc);
		}
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

	public void Send (byte rfcID, Target target, params object[] objs)
	{
		SendRFC((uint)id, rfcID, null, target, objs);
	}

	/// <summary>
	/// Send a remote function call.
	/// </summary>

	public void Send (string rfcName, Target target, params object[] objs)
	{
		SendRFC((uint)id, 0, rfcName, target, objs);
	}

	/// <summary>
	/// Send a remote function call.
	/// </summary>

	public void Send (byte rfcID, Player target, params object[] objs)
	{
		SendRFC((uint)id, rfcID, null, target, objs);
	}

	/// <summary>
	/// Send a remote function call.
	/// </summary>

	public void Send (string rfcName, Player target, params object[] objs)
	{
		SendRFC((uint)id, 0, rfcName, target, objs);
	}

	/// <summary>
	/// Send a broadcast to the entire LAN. Does not require an active connection.
	/// </summary>

	public void BroadcastToLAN (int port, byte rfcID, params object[] objs) { BroadcastToLAN(port, (uint)id, rfcID, null, objs); }

	/// <summary>
	/// Send a broadcast to the entire LAN. Does not require an active connection.
	/// </summary>

	public void BroadcastToLAN (int port, string rfcName, params object[] objs) { BroadcastToLAN(port, (uint)id, 0, rfcName, objs); }

	/// <summary>
	/// Remove a previously saved remote function call.
	/// </summary>

	public void Remove (string rfcName) { RemoveSavedRFC((uint)id, 0, rfcName); }

	/// <summary>
	/// Remove a previously saved remote function call.
	/// </summary>

	public void Remove (byte rfcID) { RemoveSavedRFC((uint)id, rfcID, null); }

	/// <summary>
	/// Send a new RFC call to the specified target.
	/// </summary>

	static void SendRFC (uint objID, byte rfcID, string rfcName, Target target, params object[] objs)
	{
#if UNITY_EDITOR
		if (!Application.isPlaying) return;
#endif
		if (TNManager.isConnected)
		{
			byte packetID = (byte)((int)Packet.ForwardToAll + (int)target);
			BinaryWriter writer = TNManager.BeginSend(packetID);
			writer.Write((objID << 8) | rfcID);
			if (rfcID == 0) writer.Write(rfcName);
			Tools.Write(writer, objs);
			TNManager.EndSend();
		}
		else if (target == Target.All || target == Target.AllSaved)
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

	static void SendRFC (uint objID, byte rfcID, string rfcName, Player target, params object[] objs)
	{
		if (TNManager.isConnected)
		{
			BinaryWriter writer = TNManager.BeginSend(Packet.ForwardToPlayer);
			writer.Write(target.id);
			writer.Write((objID << 8) | rfcID);
			if (rfcID == 0) writer.Write(rfcName);
			Tools.Write(writer, objs);
			TNManager.EndSend();
		}
	}

	/// <summary>
	/// Broadcast a remote function call to all players on the network.
	/// </summary>

	static void BroadcastToLAN (int port, uint objID, byte rfcID, string rfcName, params object[] objs)
	{
		Buffer buffer = Buffer.Create();
		BinaryWriter writer = TNManager.BeginSend(Packet.ForwardToAll);
		writer.Write((objID << 8) | rfcID);
		if (rfcID == 0) writer.Write(rfcName);
		Tools.Write(writer, objs);
		TNManager.EndSend(port);
		buffer.Recycle();
	}

	/// <summary>
	/// Remove a previously saved remote function call.
	/// </summary>

	static void RemoveSavedRFC (uint objID, byte rfcID, string funcName)
	{
		if (TNManager.isConnected)
		{
			BinaryWriter writer = TNManager.BeginSend(Packet.RequestRemoveRFC);
			writer.Write((objID << 24) | rfcID);
			if (rfcID == 0) writer.Write(funcName);
			TNManager.EndSend();
		}
	}
}