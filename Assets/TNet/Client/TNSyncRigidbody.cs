//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

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
public class TNSyncRigidbody : TNBehaviour
{
	/// <summary>
	/// How many times per second to send updates.
	/// The actual number of updates sent may be higher (if new players connect) or lower (if the rigidbody is still).
	/// The number of updates is also limited by the number of physics updates per second.
	/// With each sync packet having 57 bytes (9 byte header + 48 byte payload), that's 0.57 kb/sec bandwidth
	/// usage with a frequency of 10.
	/// </summary>

	public int frequency = 10;

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

	void UpdateInterval () { mNext = Time.time + (frequency > 0 ? (1f / frequency) : 0f); }

	/// <summary>
	/// Only the host should be sending out updates. Everyone else should be simply observing the changes.
	/// </summary>

	void FixedUpdate ()
	{
		if (frequency > 0 && mNext < Time.time && TNManager.isHosting && TNManager.isConnected)
		{
			bool isSleeping = mRb.IsSleeping();
			if (isSleeping && mWasSleeping) return;

			UpdateInterval();

			Vector3 pos = mTrans.position;
			Vector3 rot = mTrans.rotation.eulerAngles;

			if (mWasSleeping || pos != mLastPos || rot != mLastRot)
			{
				mLastPos = pos;
				mLastRot = rot;

				// Send the update. Note that we're using an RFC ID here instead of the function name.
				// Using an ID speeds up the function lookup time and reduces the size of the packet.
				// Since the target is "OthersSaved", even players that join later will receive this update.
				// Each consecutive Send() updates the previous, so only the latest one is kept on the server.
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
	void Sync (Vector3 pos, Vector3 rot, Vector3 vel, Vector3 ang)
	{
		mTrans.position = pos;
		mTrans.rotation = Quaternion.Euler(rot);
		mRb.velocity = vel;
		mRb.angularVelocity = ang;
	}
}