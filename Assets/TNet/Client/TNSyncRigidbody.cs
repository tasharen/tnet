//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2013 Tasharen Entertainment
//---------------------------------------------

//#define TNDEBUG

using UnityEngine;
using TNet;

/// <summary>
/// This script makes it easy to sync rigidbodies across the network.
/// Use this script on all the objects in your scene that have a rigidbody
/// and can move as a result of physics-based interaction with other objects.
/// Note that any user-based interaction (such as applying a force of any kind)
/// should still be sync'd via an explicit separate RFC call for optimal results.
/// </summary>

[RequireComponent(typeof(Rigidbody))]
[AddComponentMenu("TNet/Sync Rigidbody")]
public class TNSyncRigidbody : TNBehaviour
{
	/// <summary>
	/// How many times per second to send updates.
	/// The actual number of updates sent may be higher (if new players connect) or lower (if the rigidbody is still).
	/// </summary>

	public int updatesPerSecond = 10;

	Transform mTrans;
	Rigidbody mRb;
	float mNext;
	bool mWasSleeping = false;

	Vector3 mLastPos;
	Vector3 mLastRot;

	void Awake ()
	{
		mTrans = transform;
		mRb = rigidbody;
		mLastPos = mTrans.position;
		mLastRot = mTrans.rotation.eulerAngles;
		UpdateInterval();
	}

	/// <summary>
	/// Update the timer, offsetting the time by the update frequency.
	/// </summary>

	void UpdateInterval () { mNext = Time.time + (updatesPerSecond > 0 ? (1f / updatesPerSecond) : 0f); }

	/// <summary>
	/// Only the host should be sending out updates. Everyone else should be simply observing the changes.
	/// </summary>

	void FixedUpdate ()
	{
		if (updatesPerSecond > 0 && mNext < Time.time && tno.isMine && TNManager.isInChannel)
		{
			bool isSleeping = mRb.IsSleeping();

			if (isSleeping && mWasSleeping)
			{
#if TNDEBUG
				renderer.material.color = Color.blue;
#endif
				return;
			}

			UpdateInterval();

			Vector3 pos = mTrans.position;
			Vector3 rot = mTrans.rotation.eulerAngles;

			if (mWasSleeping || pos != mLastPos || rot != mLastRot)
			{
				mLastPos = pos;
				mLastRot = rot;
#if TNDEBUG
				renderer.material.color = Color.red;
#endif
				// Send the update. Note that we're using an RFC ID here instead of the function name.
				// Using an ID speeds up the function lookup time and reduces the size of the packet.
				// Since the target is "OthersSaved", even players that join later will receive this update.
				// Each consecutive Send() updates the previous, so only the latest one is kept on the server.
				// Note that you can also replace "Send" with "SendQuickly" in order to decrease bandwidth usage.
				tno.Send(1, Target.OthersSaved, pos, rot, mRb.velocity, mRb.angularVelocity);
			}
			mWasSleeping = isSleeping;
		}
	}

	/// <summary>
	/// Actual synchronization function -- arrives only on clients that aren't hosting the game.
	/// Note that an RFC ID is specified here. This shrinks the size of the packet and speeds up
	/// the function lookup time. It's a good idea to do this with all frequently called RFCs.
	/// </summary>

	[RFC(1)]
	void OnSync (Vector3 pos, Vector3 rot, Vector3 vel, Vector3 ang)
	{
#if TNDEBUG
		renderer.material.color = Color.green;
#endif
		mTrans.position = pos;
		mTrans.rotation = Quaternion.Euler(rot);
		mRb.velocity = vel;
		mRb.angularVelocity = ang;
		UpdateInterval();
	}

	/// <summary>
	/// It's a good idea to send an update when a collision occurs.
	/// </summary>

	void OnCollisionEnter () { if (TNManager.isHosting) Sync(); }

	/// <summary>
	/// Send out an update to everyone on the network.
	/// </summary>

	public void Sync ()
	{
		if (TNManager.isInChannel)
		{
			UpdateInterval();
			mWasSleeping = false;
			mLastPos = mTrans.position;
			mLastRot = mTrans.rotation.eulerAngles;
#if TNDEBUG
			renderer.material.color = Color.red;
#endif
			tno.Send(1, Target.OthersSaved, mLastPos, mLastRot, mRb.velocity, mRb.angularVelocity);
		}
	}
}
