using UnityEngine;

/// <summary>
/// Smooth Transform script can be used to smooth out abrupt changes in the transform's position, rotation and velocity.
/// Its ideal use is attached to a renderer root of a moving object that gets repositioned as part of a network update.
/// This script should never be attached to the root object. It must always go on a child object containing the renderers.
/// </summary>

public class SmoothTransform : MonoBehaviour
{
	public float lerpTime = 0.5f;

	[System.NonSerialized] Transform mParent;
	[System.NonSerialized] Transform mTrans;
	[System.NonSerialized] Rigidbody mRb;
	[System.NonSerialized] Vector3 mFromWorldPos;
	[System.NonSerialized] Vector3 mLocalPos;
	[System.NonSerialized] Quaternion mFromWorldRot;
	[System.NonSerialized] Quaternion mLocalRot;
	[System.NonSerialized] Vector3 mFromVel;
	[System.NonSerialized] Vector3 mFromAngVel;
	[System.NonSerialized] float mStart = 0f;

	public Transform trans { get { if (mTrans == null) Cache(); return mTrans; } }

	public void Cache ()
	{
		mTrans = transform;
		mParent = mTrans.parent;
		mRb = GetComponentInParent<Rigidbody>();
		mLocalPos = mTrans.localPosition;
		mLocalRot = mTrans.localRotation;
	}

	static float EaseInOut (float val)
	{
		const float pi2 = Mathf.PI * 2f;
		return val - Mathf.Sin(val * pi2) / pi2;
	}

	void LateUpdate ()
	{
		if (lerpTime == 0f || mTrans == null) { enabled = false; return; }

		var time = Time.time;
		var delta = time - mStart;
		var factor = delta / lerpTime;

		if (factor < 1f)
		{
			factor = EaseInOut(factor);
			var estimatedPos = mFromWorldPos + mFromVel * delta;
			var estimatedRot = Quaternion.Euler(mFromAngVel * delta) * mFromWorldRot;

			var pos = Vector3.Lerp(estimatedPos, mParent.TransformPoint(mLocalPos), factor);
			var rot = Quaternion.Slerp(estimatedRot, mParent.rotation * mLocalRot, factor);

			mTrans.position = pos;
			mTrans.rotation = rot;
		}
		else Finish();
	}

	/// <summary>
	/// Prepare to animiate. Caches all the necessary values. Call this BEFORE changing the transform / rigidbody values.
	/// </summary>

	public void Init ()
	{
		if (mTrans == null) Cache();

		mStart = Time.time;
		mFromWorldPos = mTrans.position;
		mFromWorldRot = mTrans.rotation;

		if (mRb != null)
		{
			mFromVel = mRb.velocity;
			mFromAngVel = mRb.angularVelocity * Mathf.Rad2Deg;
		}
		else
		{
			mFromVel = Vector3.zero;
			mFromAngVel = Vector3.zero;
		}
	}

	/// <summary>
	/// Activate the smooth transform transition from the values saved in Init() to the current. Call this AFTER changing the transform / rigidbody values.
	/// </summary>

	public void Activate ()
	{
		enabled = true;
		LateUpdate();
	}

	/// <summary>
	/// Immediately finish the smooth transition.
	/// </summary>

	public void Finish ()
	{
		if (mTrans == null) Cache();
		mTrans.position = mParent.TransformPoint(mLocalPos);
		mTrans.rotation = mParent.rotation * mLocalRot;
		enabled = false;
	}

	/// <summary>
	/// Given the specified world position, transform it to the value adjusted by the smoothing operation.
	/// Use it if you need to have some renderer positions to be adjusted that can't be a part of the smoothing hierarchy, for example.
	/// NOTE: It assumes that the SmoothTransform has no starting rotation present. If rotation is required, it should be on the child object.
	/// </summary>

	public Vector3 TransformPoint (Vector3 worldPos)
	{
		if (enabled)
		{
			if (mTrans == null) Cache();
			worldPos = mParent.InverseTransformPoint(worldPos);
			worldPos = mTrans.TransformPoint(worldPos - mLocalPos);
		}
		return worldPos;
	}
}
