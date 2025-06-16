//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2025 Tasharen Entertainment Inc
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
		[Tooltip("How many times per second to send updates. The actual number of updates sent may be higher (if a collision happens) or lower (if the rigidbody is still). If this value is '0', then only collisions will cause a sync to happen without you calling Sync() manually.")]
		public float updatesPerSecond = 10f;

		[Tooltip("Whether to send through UDP or TCP. If it's important, TCP will be used. If not, UDP.  If you have a lot of frequent updates, mark it as not important. If the number of updates per second is 1 or lower, this is always considered to be 'true'.")]
		public bool isImportant = false;

		[Tooltip("(Optional) Assign this value to the SmoothTransform that resides underneath this object's hierarchy that should be informed when the rigidodby performs its correction")]
		public SmoothTransform smoothTrans;

		[Header("Sync Delta Thresholds")]
		[Tooltip("Position must change by at least this value for from the previous sync in order for another to be considered")]
		public float posSyncDelta = 0.01f;
		
		[Tooltip("Rotation must change by at least this many degrees for from the previous sync in order for another to be considered")]
		public float rotSyncDelta = 0.01f;
		
		[Tooltip("Velocity must change by at least this value for from the previous sync in order for another to be considered")]
		public float velSyncDelta = 0.01f;
		
		[Tooltip("Angular velocity must change by at least this many degrees for from the previous sync in order for another to be considered")]
		public float angSyncDelta = 0.01f;

		/// <summary>
		/// Set this to 'false' to stop sending updates.
		/// </summary>

		[System.NonSerialized] public bool isActive = true;

		[System.NonSerialized] protected Transform mTrans;
		[System.NonSerialized] protected Rigidbody mRb;
		[System.NonSerialized] protected float mNext = 0f;
		[System.NonSerialized] protected bool mWasSleeping = false;
		[System.NonSerialized] protected Quaternion mLastRot;
		[System.NonSerialized] protected Vector3 mLastPos;
		[System.NonSerialized] protected Vector3 mLastVel;
		[System.NonSerialized] protected Vector3 mLastAngVel;

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
			mLastPos = mTrans.position;
			mLastVel = mRb.velocity;
			mLastAngVel = mRb.angularVelocity;

			// This ensures that multiple rigidbody updates are spaced out and don't happen at once
			if (updatesPerSecond > 0f) mNext = Random.Range(0.5f, 1.5f) * 1f / updatesPerSecond;
		}

		/// <summary>
		/// Update the timer, offsetting the time by the update frequency.
		/// </summary>

		void UpdateInterval () { if (updatesPerSecond > 0f) mNext = 1f / updatesPerSecond; }

		static bool IsNanOrInfinity (Vector3 v)
		{
			if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)) return true;
			if (float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z)) return true;
			return false;
		}

		/// <summary>
		/// Only the host should be sending out updates. Everyone else should be simply observing the changes.
		/// </summary>

		void FixedUpdate ()
		{
			if (updatesPerSecond < 0.001f || !isActive || !tno.isMine || !tno.canSend) return;

			var isSleeping = mRb.IsSleeping();
			if (isSleeping && mWasSleeping) return;

			mNext -= Time.deltaTime;
			if (mNext > 0f) return;

			UpdateInterval();

			var pos = mTrans.position;

			if (IsNanOrInfinity(pos))
			{
				Debug.LogError("Invalid position: " + pos + ", fixing", this);
				pos = new Vector3(0f, 30f + Random.Range(0f, 20f), 0f);
				mTrans.position = pos;
			}

			var rot = mTrans.rotation;
			var vel = mRb.velocity;
			var ang = mRb.angularVelocity;
			var asd = angSyncDelta * Mathf.Deg2Rad;

			if (mWasSleeping || (pos - mLastPos).sqrMagnitude > posSyncDelta * posSyncDelta ||
				Quaternion.Angle(rot, mLastRot) > rotSyncDelta ||
				(vel - mLastVel).sqrMagnitude > velSyncDelta * velSyncDelta ||
				(ang - mLastAngVel).sqrMagnitude > asd * asd)
			{
				mLastPos = pos;
				mLastRot = rot;
				mLastVel = vel;
				mLastAngVel = ang;

				// Send the update. Note that we're using an RFC ID here instead of the function name.
				// Using an ID speeds up the function lookup time and reduces the size of the packet.
				// Since the target is "OthersSaved", even players that join later will receive this update.
				// Each consecutive Send() updates the previous, so only the latest one is kept on the server.

				if (isImportant || updatesPerSecond <= 1f) tno.Send(1, Target.OthersSaved, pos, rot, vel, ang);
				else tno.SendQuickly(1, Target.OthersSaved, pos, rot, vel, ang);
			}

			mWasSleeping = isSleeping;
		}

		/// <summary>
		/// Actual synchronization function -- arrives only on clients that aren't hosting the game.
		/// Note that an RFC ID is specified here. This shrinks the size of the packet and speeds up
		/// the function lookup time. It's a good idea to do this with all frequently called RFCs.
		/// </summary>

		[RFC(1)]
		void OnSync (Vector3 pos, Quaternion rot, Vector3 vel, Vector3 ang)
		{
			mLastPos = pos;
			mLastRot = rot;
			mLastVel = vel;
			mLastAngVel = ang;

			if (mRb.isKinematic)
			{
				mTrans.position = pos;
				mTrans.rotation = rot;

				mRb.isKinematic = false;
				mRb.velocity = vel;
				mRb.angularVelocity = ang;
				mRb.isKinematic = true;
			}
			else
			{
				if (TNManager.IsJoiningChannel(tno.channelID))
				{
					mTrans.rotation = rot;
					mTrans.position = pos;
					
					mRb.velocity = vel;
					mRb.angularVelocity = ang;
				}
				else
				{
					if (smoothTrans && updatesPerSecond > 0f)
					{
						if (updatesPerSecond > 1f)
						{
							Debug.LogWarning("Smooth transform is meant to be used with rigidbodies that don't sync frequently -- usually only once every couple seconds at most.", smoothTrans);
							updatesPerSecond = 1f;
						}

						var delta = updatesPerSecond > 0f ? 1f / updatesPerSecond : 0f;
						if (smoothTrans.lerpTime > delta) smoothTrans.lerpTime = delta;

						var mag = Mathf.Max(((mRb.position - pos).magnitude - 0.1f) * 0.5f, (Quaternion.Angle(mRb.rotation, rot) - 2f) / 20f);

						if (mag > 0f)
						{
							// Immediately finish the smooth transform animation if it's currently in progress
							if (smoothTrans.enabled) smoothTrans.Finish();

							// Use the current position/rotation as the start of the smooth transition
							smoothTrans.Init();
						}

						mRb.rotation = rot;
						mRb.position = pos;
						mRb.velocity = vel;
						mRb.angularVelocity = ang;

						// Start the smooth transition
						if (mag > 0f) smoothTrans.Activate();
					}
					else
					{
						mRb.rotation = rot;
						mRb.position = pos;
						mRb.velocity = vel;
						mRb.angularVelocity = ang;
					}
				}
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
			if (isActive && tno.canSend)
			{
				UpdateInterval();

				mLastPos = mRb.position;
				var vel = mRb.velocity;

				mWasSleeping = false;
				mLastRot = mRb.rotation;
				tno.Send(1, Target.OthersSaved, mLastPos, mLastRot, vel, mRb.angularVelocity);
			}
		}
	}
}
