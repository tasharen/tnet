using System;
using UnityEngine;
using TNet;
using System.Reflection;
using System.Collections.Generic;
using System.IO;

[ExecuteInEditMode]
public class TNObject : MonoBehaviour
{
	/// <summary>
	/// Remote function calls that can't be executed immediately get stored,
	/// and will be executed when an appropriate Object ID gets added.
	/// </summary>

	class DelayedCall
	{
		public int objID;
		public int funcID;
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
		public int id;
		public object obj;
		public MethodInfo func;
	}

	// List of network objs to iterate through
	static BetterList<TNObject> mList = new BetterList<TNObject>();

	// List of network objs to quickly look up
	static Dictionary<int, TNObject> mDictionary = new Dictionary<int, TNObject>();

	// List of delayed calls -- calls that could not execute at the time of the call
	static BetterList<DelayedCall> mDelayed = new BetterList<DelayedCall>();

	/// <summary>
	/// Unique Network Identifier. All TNObjects have them and is how messages arrive at the correct destination.
	/// </summary>

	public int id = 0;

	/// <summary>
	/// When set to 'true', it will cause the list of remote function calls to be rebuilt next time they're needed.
	/// </summary>

	[HideInInspector] public bool rebuildMethodList = true;

	// Cached RFC functions
	BetterList<CachedRFC> mRFCs = new BetterList<CachedRFC>();

	// Whether the object has been registered with the lists
	bool mIsRegistered = false;

	/// <summary>
	/// Retrieve the Tasharen Network Object by ID.
	/// </summary>

	static public TNObject Find (int tnID)
	{
		if (mDictionary == null) return null;
		TNObject tno = null;
		mDictionary.TryGetValue(tnID, out tno);
		return tno;
	}

	/// <summary>
	/// Register the object with the lists.
	/// </summary>

	void Awake () { Register(); }

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
			if (ts != null) mLastID = Mathf.Max(ts.id, mLastID);
		}
		return ++mLastID;
	}

	/// <summary>
	/// Re-register the object.
	/// </summary>

	void OnEnable ()
	{
		if (!Application.isPlaying)
		{
			Unregister();
			
			if (id == 0)
			{
				id = GetUniqueID();
			}
			else if (Find(id) != null)
			{
				Debug.LogWarning("Network ID " + id + " already exists. Assigning a new one.", this);
				id = GetUniqueID();
			}
			Register();
		}
	}
#endif

	/// <summary>
	/// Remove this object from the list.
	/// </summary>

	void OnDestroy () { Unregister(); }

	/// <summary>
	/// Register the network object with the lists.
	/// </summary>

	void Register ()
	{
		if (!mIsRegistered)
		{
			mDictionary[id] = this;
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
			if (mDictionary != null) mDictionary.Remove(id);
			if (mList != null) mList.Remove(this);
			mIsRegistered = false;
		}
	}

	/// <summary>
	/// Invoke the function specified by the ID.
	/// </summary>

	public bool Execute (int funcID, params object[] parameters)
	{
		if (rebuildMethodList) RebuildMethodList();

		bool retVal = false;

		for (int i = 0; i < mRFCs.size; ++i)
		{
			CachedRFC ent = mRFCs[i];

			if (ent.id == funcID)
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

	static public void FindAndExecute (int objID, int funcID, params object[] parameters)
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

	static public void FindAndExecute (int objID, string funcName, params object[] parameters)
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
			Type type = mb.GetType();

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
					ent.id = tnc.id;
					mRFCs.Add(ent);
				}
			}
		}
	}

	/// <summary>
	/// Send a remote function call.
	/// </summary>

	public void RFC (byte rfcID, Target target, params object[] objs) { SendRFC(id, rfcID, null, target, objs); }

	/// <summary>
	/// Send a remote function call.
	/// </summary>

	public void RFC (string rfcName, Target target, params object[] objs) { SendRFC(id, 0, rfcName, target, objs); }

	/// <summary>
	/// Send a remote function call.
	/// </summary>

	public void RFC (byte rfcID, ClientPlayer target, params object[] objs) { SendRFC(id, rfcID, null, target, objs); }

	/// <summary>
	/// Send a remote function call.
	/// </summary>

	public void RFC (string rfcName, ClientPlayer target, params object[] objs) { SendRFC(id, 0, rfcName, target, objs); }

	/// <summary>
	/// Send a new RFC call to the specified target.
	/// </summary>

	static void SendRFC (int objID, byte rfcID, string rfcName, Target target, params object[] objs)
	{
		if (TNManager.isConnected)
		{
			byte packetID = (byte)((int)Packet.ForwardToAll + (int)target);
			BinaryWriter writer = TNManager.instance.client.BeginSend(packetID);
			writer.Write((objID << 8) | rfcID);
			if (rfcID == 0) writer.Write(rfcName);
			Tools.Write(writer, objs);
			TNManager.instance.client.EndSend();
		}
		else if (target == Target.All || target == Target.AllBuffered)
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
	/// Send a new RFC call to the specified player.
	/// </summary>

	static void SendRFC (int objID, byte rfcID, string rfcName, ClientPlayer target, params object[] objs)
	{
		if (TNManager.isConnected)
		{
			BinaryWriter writer = TNManager.instance.client.BeginSend(Packet.ForwardToPlayer);
			writer.Write(target.id);
			writer.Write((objID << 8) | rfcID);
			if (rfcID == 0) writer.Write(rfcName);
			Tools.Write(writer, objs);
			TNManager.instance.client.EndSend();
		}
	}
}