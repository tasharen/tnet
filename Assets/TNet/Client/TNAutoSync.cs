//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using UnityEngine;
using TNet;
using System.Collections;
using System.Reflection;

/// <summary>
/// This script makes it really easy to sync some value across all connected clients.
/// Keep in mind that this script should only be used for rapid prototyping. It's still better
/// to create custom to-the-point sync scripts as they will yield better performance.
/// </summary>

[ExecuteInEditMode]
public class TNAutoSync : TNBehaviour
{
	public Component target;
	public string propertyName;

	public int updatesPerSecond = 20;
	public bool isPersistent = true;
	public bool onlyHostCanSync = true;
	public bool isImportant = true;

	FieldInfo mField;
	PropertyInfo mProperty;
	object mLast;
	bool mCanSync = false;

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
			if (target != null)
			{
				mField = target.GetType().GetField(propertyName, BindingFlags.Instance | BindingFlags.Public);

				if (mField != null)
				{
					mLast = mField.GetValue(target);
				}
				else
				{
					mProperty = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
					if (mProperty != null) mLast = mProperty.GetValue(target, null);
				}

				if (mProperty == null && mField == null)
				{
					Debug.LogError("Unable to find property: '" + propertyName + "' on " + target.GetType());
					enabled = false;
					return;
				}

				// Only start the coroutine if we wanted to run periodic updates
				if (updatesPerSecond > 0) StartCoroutine(PeriodicSync());
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
			if (mCanSync && TNManager.isConnected && (!onlyHostCanSync || TNManager.isHosting))
			{
				Sync();
			}

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
		if (TNManager.isConnected && target != null && enabled)
		{
			if (mField != null)
			{
				object val = mField.GetValue(target);

				if (mLast == null || !val.Equals(mLast))
				{
					mLast = val;

					if (isImportant)
					{
						tno.Send(255, isPersistent ? Target.OthersSaved : Target.Others, val);
					}
					else
					{
						tno.SendQuickly(255, isPersistent ? Target.OthersSaved : Target.Others, val);
					}
				}
			}
			else if (mProperty != null)
			{
				object val = mProperty.GetValue(target, null);

				if (mLast == null || !val.Equals(mLast))
				{
					mLast = val;

					if (isImportant)
					{
						tno.Send(255, isPersistent ? Target.OthersSaved : Target.Others, val);
					}
					else
					{
						tno.SendQuickly(255, isPersistent ? Target.OthersSaved : Target.Others, val);
					}
				}
			}
		}
	}

	[RFC(255)]
	void OnSync (object val)
	{
		if (target != null && enabled)
		{
			mLast = val;

			if (mField != null)
			{
				mField.SetValue(target, val);
			}
			else if (mProperty != null)
			{
				mProperty.SetValue(target, val, null);
			}
		}
	}
}