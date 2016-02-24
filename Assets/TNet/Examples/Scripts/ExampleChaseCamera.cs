//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;
using TNet;

/// <summary>
/// Very simple chase camera used on the Car example.
/// This script is attached to the "Chase Camera" object underneath the Car prefab.
/// It takes care of smoothly tweening the position and rotation of the chase camera.
/// Note that in order for this script to work properly the car's rigidbody must be set to "Interpolate".
/// </summary>

public class ExampleChaseCamera : TNBehaviour
{
	static public Transform target;

	Vector3 mPos;
	Quaternion mRot;
	Transform mTrans;

	void Start ()
	{
		if (tno == null || tno.isMine)
		{
			mTrans = transform;
			mPos = mTrans.position;
			mRot = mTrans.rotation;
		}
		else Destroy(this);
	}

	void Update ()
	{
		if (target)
		{
			Transform t = transform;
			Vector3 forward = t.forward;
			forward.y = 0f;
			forward.Normalize();

			Vector3 pos = t.position;
			Quaternion rot = Quaternion.LookRotation(forward);

			float delta = Time.deltaTime;
			mPos = Vector3.Lerp(mPos, pos, delta * 8f);
			mRot = Quaternion.Slerp(mRot, rot, delta * 4f);

			target.position = mPos;
			target.rotation = mRot;
		}
	}
}
