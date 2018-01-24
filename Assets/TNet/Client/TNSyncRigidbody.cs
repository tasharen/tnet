//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;

namespace TNet
{
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
		/// </summary>

		public float updatesPerSecond = 10f;

		/// <summary>
		/// Whether to send through UDP or TCP. If it's important, TCP will be used. If not, UDP.
		/// If you have a lot of frequent updates, mark it as not important.
		/// </summary>

		public bool isImportant = false;

		/// <summary>
		/// Set this to 'false' to stop sending updates.
		/// </summary>

		[System.NonSerialized] public bool isActive = true;

		Transform mTrans;
		Rigidbody mRb;
		float mNext = 0f;
		bool mWasSleeping = false;
		Quaternion mLastRot;
#if W2
		Vector3D mLastPos;
#else
		Vector3 mLastPos;
#endif
		protected override void Awake ()
		{
			base.Awake();
			mTrans = transform;
			mRb = GetComponent<Rigidbody>();
		}

		public override void OnStart ()
		{
			base.OnStart();
			mLastRot = mTrans.rotation;
			UpdateInterval();
#if W2
			mLastPos = FloatingOrigin.positionOffset + mTrans.position;
#else
			mLastPos = mTrans.position;
#endif
		}

		/// <summary>
		/// Update the timer, offsetting the time by the update frequency.
		/// </summary>

		void UpdateInterval () { mNext = Random.Range(0.85f, 1.15f) * (updatesPerSecond > 0f ? (1f / updatesPerSecond) : 0f); }

		/// <summary>
		/// Only the host should be sending out updates. Everyone else should be simply observing the changes.
		/// </summary>

		void FixedUpdate ()
		{
			if (updatesPerSecond < 0.001f) return;

			if (isActive && tno.isMine && !tno.hasBeenDestroyed && TNManager.IsInChannel(tno.channelID))
			{
				bool isSleeping = mRb.IsSleeping();
				if (isSleeping && mWasSleeping) return;

				mNext -= Time.deltaTime;
				if (mNext > 0f) return;
				UpdateInterval();
#if W2
				var pos = FloatingOrigin.positionOffset + mRb.position;
				var vel = FloatingOrigin.velocityOffset + mRb.velocity;
#else
				var pos = mRb.position;
				var vel = mRb.velocity;
#endif
				Quaternion rot = mTrans.rotation;

				if (mWasSleeping || pos != mLastPos || Quaternion.Dot(rot, mLastRot) < 0.99f)
				{
					mLastPos = pos;
					mLastRot = rot;

					// Send the update. Note that we're using an RFC ID here instead of the function name.
					// Using an ID speeds up the function lookup time and reduces the size of the packet.
					// Since the target is "OthersSaved", even players that join later will receive this update.
					// Each consecutive Send() updates the previous, so only the latest one is kept on the server.

					if (isImportant) tno.Send(1, Target.OthersSaved, pos, rot, vel, mRb.angularVelocity);
					else tno.SendQuickly(1, Target.OthersSaved, pos, rot, vel, mRb.angularVelocity);
				}
				mWasSleeping = isSleeping;
			}
		}

		/// <summary>
		/// Actual synchronization function -- arrives only on clients that aren't hosting the game.
		/// Note that an RFC ID is specified here. This shrinks the size of the packet and speeds up
		/// the function lookup time. It's a good idea to do this with all frequently called RFCs.
		/// </summary>
#if W2
		[RFC(1)]
		void OnSync (Vector3D pos, Quaternion rot, Vector3D vel, Vector3 ang)
#else
		[RFC(1)]
		void OnSync (Vector3 pos, Quaternion rot, Vector3 vel, Vector3 ang)
#endif
		{
			mLastPos = pos;
			mLastRot = rot;

			if (mRb.isKinematic)
			{
#if W2
				mTrans.position = pos - FloatingOrigin.positionOffset;
				mRb.velocity = vel - FloatingOrigin.velocityOffset;
#else
				mTrans.position = pos;
				mRb.velocity = vel;
#endif
				mTrans.rotation = rot;

				// Does this actually do anything? Needs to be investigated...
				mRb.isKinematic = false;
				mRb.angularVelocity = ang;
				mRb.isKinematic = true;
			}
			else
			{
				if (TNManager.IsJoiningChannel(tno.channelID))
				{
					mTrans.rotation = rot;
#if W2
					mTrans.position = pos - FloatingOrigin.positionOffset;
					mRb.velocity = vel - FloatingOrigin.velocityOffset;
#else
					mTrans.position = pos;
					mRb.velocity = vel;
#endif
				}
				else
				{
					mRb.rotation = rot;
#if W2
					mRb.position = pos - FloatingOrigin.positionOffset;
					mRb.velocity = vel - FloatingOrigin.velocityOffset;
#else
					mRb.position = pos;
					mRb.velocity = vel;
#endif
				}

				mRb.angularVelocity = ang;
			}
			UpdateInterval();
		}

		/// <summary>
		/// It's a good idea to send an update when a collision occurs.
		/// </summary>

		void OnCollisionEnter () { if (tno.isMine) Sync(); }

		/// <summary>
		/// Send out an update to everyone on the network.
		/// </summary>

		public void Sync ()
		{
			if (isActive && !tno.hasBeenDestroyed && TNManager.IsInChannel(tno.channelID))
			{
				UpdateInterval();
#if W2
				mLastPos = FloatingOrigin.positionOffset + mRb.position;
				var vel = FloatingOrigin.velocityOffset + mRb.velocity;
#else
				mLastPos = mRb.position;
				var vel = mRb.velocity;
#endif
				mWasSleeping = false;
				mLastRot = mRb.rotation;
				tno.Send(1, Target.OthersSaved, mLastPos, mLastRot, vel, mRb.angularVelocity);
			}
		}
	}
}
