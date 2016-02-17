//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;

/// <summary>
/// Very simple chase camera used in the Car example.
/// </summary>

public class ExampleChaseCamera : MonoBehaviour
{
	static public Transform target;

	Transform mTrans;

	void Awake () { mTrans = transform; }

	void FixedUpdate ()
	{
		if (target)
		{
			Vector3 forward = target.forward;
			forward.y = 0f;
			forward.Normalize();

			float delta = Time.deltaTime * 4f;
			mTrans.position = Vector3.Lerp(mTrans.position, target.position, delta * 4f);
			mTrans.rotation = Quaternion.Slerp(mTrans.rotation, Quaternion.LookRotation(forward), delta * 8f);
		}
	}
}
