//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;
using TNet;

/// <summary>
/// Extended car that adds TNet-based multiplayer support.
/// </summary>

[RequireComponent(typeof(TNObject))]
public class ExampleCar : ExampleCarNoNetworking
{
	/// <summary>
	/// Maximum number of updates per second when synchronizing input axes.
	/// The actual number of updates may be less if nothing is changing.
	/// </summary>

	[Range(1f, 20f)]
	public float inputUpdates = 10f;

	/// <summary>
	/// Maximum number of updates per second when synchronizing the rigidbody.
	/// </summary>

	[Range(0.25f, 5f)]
	public float rigidbodyUpdates = 1f;

	/// <summary>
	/// We want to cache the network object (TNObject) we'll use for network communication.
	/// If the script was derived from TNBehaviour, this wouldn't have been necessary.
	/// </summary>

	[System.NonSerialized]
	public TNObject tno;

	protected Vector2 mLastInput;
	protected float mLastInputSend = 0f;
	protected float mNextRB = 0f;

	protected override void Awake ()
	{
		base.Awake();
		tno = GetComponent<TNObject>();
	}

	/// <summary>
	/// Only the car's owner should be updating the movement axes, and the result should be sync'd with other players.
	/// </summary>

	protected override void Update ()
	{
		// Only the player that actually owns this car should be controlling it
		if (!tno.isMine) return;

		// Update the input axes
		base.Update();

		float time = Time.time;
		float delta = time - mLastInputSend;
		float delay = 1f / inputUpdates;

		// Don't send updates more than 20 times per second
		if (delta > 0.05f)
		{
			// The closer we are to the desired send time, the smaller is the deviation required to send an update.
			float threshold = Mathf.Clamp01(delta - delay) * 0.5f;

			// If the deviation is significant enough, send the update to other players
			if (Tools.IsNotEqual(mLastInput.x, mInput.x, threshold) ||
				Tools.IsNotEqual(mLastInput.y, mInput.y, threshold))
			{
				mLastInputSend = time;
				mLastInput = mInput;
				tno.Send("SetAxis", Target.OthersSaved, mInput);
			}
		}

		// Since the input is sent frequently, rigidbody only needs to be corrected every couple of seconds.
		// Faster-paced games will require more frequent updates.
		if (mNextRB < time)
		{
			mNextRB = time + 1f / rigidbodyUpdates;
			tno.Send("SetRB", Target.OthersSaved, mRb.position, mRb.rotation, mRb.velocity, mRb.angularVelocity);
		}
	}

	/// <summary>
	/// RFC for the input will be called several times per second.
	/// </summary>

	[RFC]
	protected void SetAxis (Vector2 v) { mInput = v; }

	/// <summary>
	/// RFC for the rigidbody will be called once per second by default.
	/// </summary>

	[RFC]
	protected void SetRB (Vector3 pos, Quaternion rot, Vector3 vel, Vector3 angVel)
	{
		mRb.position = pos;
		mRb.rotation = rot;
		mRb.velocity = vel;
		mRb.angularVelocity = angVel;
	}
}
