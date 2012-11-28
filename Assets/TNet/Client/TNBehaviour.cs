//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using UnityEngine;
using TNet;

/// <summary>
/// If your MonoBehaviour will need to use a TNObject, deriving from this class will make it easier.
/// </summary>

[RequireComponent(typeof(TNObject))]
public class TNBehaviour : MonoBehaviour
{
	TNObject mTNO;

	public TNObject tno
	{
		get
		{
			if (mTNO == null)
			{
				mTNO = GetComponent<TNObject>();
			}
			return mTNO;
		}
	}
}