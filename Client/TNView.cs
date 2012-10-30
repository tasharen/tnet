using System;
using UnityEngine;
using TNet;
using System.Reflection;
using System.Collections.Generic;

[ExecuteInEditMode]
public class TNView : MonoBehaviour
{
	/// <summary>
	/// Remote function calls that can't be executed immediately get stored,
	/// and will be executed when an appropriate View ID gets added.
	/// </summary>

	class DelayedCall
	{
		public int viewID;
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

	// List of network views to iterate through
	static BetterList<TNView> mList = new BetterList<TNView>();

	// List of network views to quickly look up
	static Dictionary<int, TNView> mDictionary = new Dictionary<int, TNView>();

	// List of delayed calls -- calls that could not execute at the time of the call
	static BetterList<DelayedCall> mDelayed = new BetterList<DelayedCall>();

	// Last used ID
	static int mLastID = 0;

	/// <summary>
	/// Unique Network Identifier. All TNViews have them and is how messages arrive at the correct destination.
	/// </summary>

	public int id = 0;

	/// <summary>
	/// When set to 'true', it will cause the list of remote function calls to be rebuilt next time they're needed.
	/// </summary>

	[HideInInspector] public bool rebuildMethodList = true;

	// Cached RFC functions
	BetterList<CachedRFC> mRFCs = new BetterList<CachedRFC>();

	/// <summary>
	/// Retrieve the Tasharen Network Behaviour by ID.
	/// </summary>

	static public TNView Find (int tnID)
	{
		if (mDictionary == null) return null;
		TNView tnb = null;
		mDictionary.TryGetValue(tnID, out tnb);
		return tnb;
	}

	/// <summary>
	/// Get a new unique view identifier.
	/// </summary>

	int GetUniqueID ()
	{
		TNView[] tns = (TNView[])FindObjectsOfType(typeof(TNView));

		for (int i = 0, imax = tns.Length; i < imax; ++i)
		{
			TNView ts = tns[i];
			if (ts != null) mLastID = Mathf.Max(ts.id, mLastID);
		}
		return ++mLastID;
	}

	/// <summary>
	/// Choose a new ID if one has not been specified.
	/// </summary>

	void OnEnable ()
	{
#if UNITY_EDITOR
		if (id == 0)
		{
			id = GetUniqueID();
		}
		else if (Find(id) != null)
		{
			Debug.LogWarning("Network ID " + id + " already exists. Assigning a new one.", this);
			id = GetUniqueID();
		}
#endif
		// TODO:
		// - Think about this:
		// - Scene with a game object that has a TNView on it.
		// - Player 1 joins, destroys the object.
		// - Player 2 joins... how does he know that the object was destroyed?

		//if (mDestroyed.Contains(id))
		//{
		//	Destroy(gameObject);
		//}
		//else
		{
			mDictionary[id] = this;
			mList.Add(this);
		}
	}

	/// <summary>
	/// Remove this view from the list.
	/// </summary>

	void OnDisable ()
	{
		if (mDictionary != null) mDictionary.Remove(id);
		if (mList != null) mList.Remove(this);
	}

	/// <summary>
	/// Invoke the function specified by the ID.
	/// </summary>

	bool Execute (int funcID, params object[] parameters)
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

	bool Execute (string funcName, params object[] parameters)
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

	static public void FindAndExecute (int viewID, int funcID, params object[] parameters)
	{
		TNView view = TNView.Find(viewID);

		if (view != null)
		{
			view.Execute(funcID, parameters);
		}
		else
		{
			DelayedCall dc = new DelayedCall();
			dc.viewID = viewID;
			dc.funcID = funcID;
			dc.parameters = parameters;
			mDelayed.Add(dc);
		}
	}

	/// <summary>
	/// Invoke the specified function. It's unlikely that you will need to call this function yourself.
	/// </summary>

	static public void FindAndExecute (int viewID, string funcName, params object[] parameters)
	{
		TNView view = TNView.Find(viewID);

		if (view != null)
		{
			view.Execute(funcName, parameters);
		}
		else
		{
			DelayedCall dc = new DelayedCall();
			dc.viewID = viewID;
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
}