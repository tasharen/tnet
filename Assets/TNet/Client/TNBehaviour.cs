//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2015 Tasharen Entertainment
//---------------------------------------------

using UnityEngine;
using TNet;

/// <summary>
/// If your MonoBehaviour will need to use a TNObject, deriving from this class will make it easier.
/// </summary>

public abstract class TNBehaviour : MonoBehaviour
{
	[System.NonSerialized] TNObject mTNO;

	public TNObject tno
	{
		get
		{
			if (mTNO == null) mTNO = GetComponentInParent<TNObject>();
			return mTNO;
		}
	}

	protected virtual void OnEnable ()
	{
		if (tno == null)
			mTNO = gameObject.AddComponent<TNObject>();

		if (Application.isPlaying)
			tno.rebuildMethodList = true;
	}

	/// <summary>
	/// Destroy this game object.
	/// </summary>

	public virtual void DestroySelf () { if (mTNO != null) mTNO.DestroySelf(); }
}
