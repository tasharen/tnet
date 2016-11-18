//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;

namespace TNet
{
/// <summary>
/// If your MonoBehaviour will need to use a TNObject, deriving from this class will make it easier.
/// </summary>

public abstract class TNBehaviour : MonoBehaviour
{
	[System.NonSerialized] bool mWaitingOnStart = false;
	[System.NonSerialized] TNObject mTNO;

	public TNObject tno
	{
		get
		{
			if (mTNO == null)
			{
				mTNO = GetComponent<TNObject>() ?? GetComponentInParent<TNObject>();

				if (mTNO != null && Application.isPlaying)
				{
					mTNO.rebuildMethodList = true;
					if (mTNO.hasBeenRegistered) OnInit();
					else mTNO.onRegister += OnInit;
				}
			}
			return mTNO;
		}
	}

	/// <summary>
	/// This function will be called after the TNObject's ID has been assigned. It's a slightly delayed version of Awake().
	/// </summary>

	public virtual void OnInit () { }

	/// <summary>
	/// Get the object-specific data.
	/// </summary>

	public T Get<T> (string name) { return tno.Get<T>(name); }

	/// <summary>
	/// Get the object-specific data.
	/// </summary>

	public T Get<T> (string name, T defVal) { return tno.Get<T>(name, defVal); }

	/// <summary>
	/// Set the object-specific data.
	/// </summary>

	public void Set (string name, object val) { tno.Set(name, val); }

	/// <summary>
	/// Convenience function to set the data using a single string notation such as "key = value".
	/// </summary>

	public void Set (string text)
	{
		if (!string.IsNullOrEmpty(text))
		{
			var parts = text.Split(new char[] { '=' }, 2);

			if (parts.Length == 2)
			{
				var key = parts[0].Trim();
				var val = parts[1].Trim();
				var node = new DataNode(key, val);
				if (node.ResolveValue()) Set(node.name, node.value);
			}
			else Debug.LogWarning("Invalid syntax [" + text + "]. Expected [key = value].");
		}
	}

	/// <summary>
	/// If the TNObject is not yet present, wait until the next frame in case it will be added in Start().
	/// OnEnable() is called on object instantiation and prior to any code being able to execute that would
	/// change the transform's parent, so ideally this code should simply run in Start() -- however due to
	/// backwards compatibility reasons it will instead execute via Invoke on the next frame.
	/// </summary>

	protected virtual void OnEnable ()
	{
		if (!mWaitingOnStart && tno == null && Application.isPlaying)
		{
			mWaitingOnStart = true;
			Invoke("AddTNO", 0.001f);
		}
	}

	void AddTNO ()
	{
		mWaitingOnStart = false;

		if (tno == null)
		{
			Debug.LogWarning("Your game object is missing a TNObject script needed for network communication.\n" +
				"Simply attach a TNObject script to this game object to fix this problem. If instantiating a prefab, attach it to your prefab instead.", this);

			// Add a TNObject manually to make scripts work properly. Doing so won't make network communication
			// work properly however, so beware! Make sure that a TNObject is present on the same object or any
			// parent of an object containing your TNBehaviour-derived scripts.
			mTNO = gameObject.AddComponent<TNObject>();
			if (Application.isPlaying) mTNO.rebuildMethodList = true;
		}
	}

	/// <summary>
	/// Destroy this game object.
	/// </summary>

	public virtual void DestroySelf () { if (tno != null) mTNO.DestroySelf(); }

	/// <summary>
	/// Destroy this game object on all connected clients and remove it from the server.
	/// </summary>

	public void DestroySelf (float delay, bool onlyIfOwner = true) { if (tno != null) mTNO.DestroySelf(delay, onlyIfOwner); }

	/// <summary>
	/// Convenience method mirroring TNManager.Instantiate.
	/// Instantiate a new game object in the behaviour's channel on all connected players.
	/// </summary>

	public void Instantiate (int rccID, string path, bool persistent, params object[] objs)
	{
		TNManager.Instantiate(tno.channelID, rccID, null, path, persistent, objs);
	}

	/// <summary>
	/// Convenience method mirroring TNManager.Instantiate.
	/// Instantiate a new game object in the behaviour's channel on all connected players.
	/// </summary>

	public void Instantiate (string funcName, string path, bool persistent, params object[] objs)
	{
		TNManager.Instantiate(tno.channelID, 0, funcName, path, persistent, objs);
	}
}
}
