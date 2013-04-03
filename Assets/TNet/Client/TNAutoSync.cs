//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2013 Tasharen Entertainment
//---------------------------------------------

using UnityEngine;
using TNet;
using System.Collections;
using System.Reflection;

/// <summary>
/// This script makes it really easy to sync some value across all connected clients.
/// Keep in mind that this script should ideally only be used for rapid prototyping.
/// It's still better to create custom to-the-point sync scripts as they will yield
/// better performance.
/// </summary>

[ExecuteInEditMode]
public class TNAutoSync : TNBehaviour
{
	[System.Serializable]
	public class SavedEntry
	{
		public Component target;
		public string propertyName;
	}

	public System.Collections.Generic.List<SavedEntry> entries = new System.Collections.Generic.List<SavedEntry>();

	public int updatesPerSecond = 20;
	public bool isSavedOnServer = true;
	public bool onlyOwnerCanSync = true;
	public bool isImportant = true;

	bool mCanSync = false;

	class ExtendedEntry : SavedEntry
	{
		public FieldInfo field;
		public PropertyInfo property;
		public object lastValue;
	}

	List<ExtendedEntry> mList = new List<ExtendedEntry>();
	object[] mCached = null;

	/// <summary>
	/// Can only sync once we've joined a channel.
	/// </summary>

	void OnNetworkJoinChannel (bool success, string err) { mCanSync = success; }

	/// <summary>
	/// Locate the property that we should be synchronizing.
	/// </summary>

	void Awake ()
	{
#if UNITY_EDITOR
		if (!Application.isPlaying)
		{
			TNAutoSync[] tns = GetComponents<TNAutoSync>();

			if (tns.Length > 1 && tns[0] != this)
			{
				Debug.LogError("Can't have more than one " + GetType() + " per game object", gameObject);
				DestroyImmediate(this);
			}
		}
		else
#endif
		{
			// Find all properties, converting the saved list into the usable list of reflected properties
			for (int i = 0, imax = entries.Count; i < imax; ++i)
			{
				SavedEntry ent = entries[i];
				
				if (ent.target != null && !string.IsNullOrEmpty(ent.propertyName))
				{
					FieldInfo field = ent.target.GetType().GetField(ent.propertyName, BindingFlags.Instance | BindingFlags.Public);

					if (field != null)
					{
						ExtendedEntry ext = new ExtendedEntry();
						ext.target = ent.target;
						ext.field = field;
						ext.lastValue = field.GetValue(ent.target);
						mList.Add(ext);
						continue;
					}
					else
					{
						PropertyInfo pro = ent.target.GetType().GetProperty(ent.propertyName, BindingFlags.Instance | BindingFlags.Public);

						if (pro != null)
						{
							ExtendedEntry ext = new ExtendedEntry();
							ext.target = ent.target;
							ext.property = pro;
							ext.lastValue = pro.GetValue(ent.target, null);
							mList.Add(ext);
							continue;
						}
						else
						{
							Debug.LogError("Unable to find property: '" + ent.propertyName + "' on " + ent.target.GetType());
						}
					}
				}
			}

			if (mList.size > 0)
			{
				// Only start the coroutine if we wanted to run periodic updates
				if (updatesPerSecond > 0 && TNManager.isInChannel)
				{
					// If we're already in a channel, we can now sync this object
					if (TNManager.isInChannel) mCanSync = true;
					StartCoroutine(PeriodicSync());
				}
				else enabled = false;
			}
			else enabled = false;
		}
	}

	/// <summary>
	/// Sync periodically.
	/// </summary>

	IEnumerator PeriodicSync ()
	{
		for (; ; )
		{
			if (!TNManager.isInChannel) break;
			if (mCanSync && (!onlyOwnerCanSync || tno.isMine)) Sync();

			if (updatesPerSecond > 0)
			{
				yield return new WaitForSeconds(1f / updatesPerSecond);
			}
			else yield return new WaitForSeconds(0.01f);
		}
	}

	/// <summary>
	/// Sync everything now.
	/// </summary>

	public void Sync ()
	{
		if (TNManager.isInChannel && mList.size != 0 && enabled)
		{
			bool initial = false;
			bool changed = false;

			if (mCached == null)
			{
				initial = true;
				mCached = new object[mList.size];
			}

			for (int i = 0; i < mList.size; ++i)
			{
				ExtendedEntry ext = mList[i];

				object val = (ext.field != null) ?
					val = ext.field.GetValue(ext.target) :
					val = ext.property.GetValue(ext.target, null);

				if (!val.Equals(ext.lastValue))
					changed = true;

				if (initial || changed)
				{
					ext.lastValue = val;
					mCached[i] = val;
				}
			}

			if (changed)
			{
				if (isImportant)
				{
					tno.Send(255, isSavedOnServer ? Target.OthersSaved : Target.Others, mCached);
				}
				else
				{
					tno.SendQuickly(255, isSavedOnServer ? Target.OthersSaved : Target.Others, mCached);
				}
			}
		}
	}

	/// <summary>
	/// The actual synchronization function function.
	/// </summary>

	[RFC(255)]
	void OnSync (object[] val)
	{
		if (enabled)
		{
			for (int i = 0; i < mList.size; ++i)
			{
				ExtendedEntry ext = mList[i];
				ext.lastValue = val[i];
				if (ext.field != null) ext.field.SetValue(ext.target, ext.lastValue);
				else ext.property.SetValue(ext.target, ext.lastValue, null);
			}
		}
	}
}
