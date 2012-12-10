//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using UnityEngine;
using TNet;

/// <summary>
/// This script shows how it's possible to associate objects with players.
/// You can see it used on draggable cubes in Example 3.
/// </summary>

[RequireComponent(typeof(Rigidbody))]
public class DraggedObject : TNBehaviour
{
	public int ownerID = -1;
	public int playerID = -1;

	Transform mTrans;
	Rigidbody mRb;
	Player mPlayer;
	Vector3 mTarget;

	void Awake ()
	{
		mRb = rigidbody;
		mTrans = transform;
		mTarget = mTrans.position;
	}

	/// <summary>
	/// Apply force to the object, making it move toward the target position.
	/// </summary>

	void Update ()
	{
		if (mPlayer != null)
		{
			ownerID = mPlayer.id;
			Vector3 delta = mTarget - mTrans.position;
			mRb.AddForce(delta * 10f, ForceMode.Acceleration);
		}
		else ownerID = -1;

		playerID = TNManager.player.id;
	}

	/// <summary>
	/// When pressed on an object, claim it for the player (unless it was already claimed).
	/// </summary>

	void OnPress ()
	{
		if (mPlayer == null)
		{
			// Call the claim function directly in order to make it feel more responsive
			ClaimObject(TNManager.playerID, mTrans.position);

			// Inform everyone else
			tno.Send(2, Target.OthersSaved, TNManager.playerID, mTrans.position);
		}
	}

	/// <summary>
	/// When the object gets released, inform everyone that the player no longer has control.
	/// </summary>

	void OnRelease ()
	{
		if (mPlayer == TNManager.player)
		{
			ClaimObject(0, mTrans.position);
			tno.Send(2, Target.OthersSaved, 0, mTrans.position);
		}
	}

	/// <summary>
	/// When the player is dragging the object around, update the target position for everyone.
	/// </summary>

	void OnDrag (Vector2 delta)
	{
		if (mPlayer == TNManager.player)
		{
			mTarget = TouchHandler.worldPos + Vector3.up;
			tno.Send(3, Target.OthersSaved, mTarget);
		}
	}

	/// <summary>
	/// Remember the last player who claimed control of this object.
	/// </summary>

	[RFC(2)] void ClaimObject (int playerID, Vector3 pos)
	{
		mPlayer = TNManager.GetPlayer(playerID);
		mTrans.position = pos;
		mTarget = pos;

		// Move the object to the Ignore Raycast layer while it's being dragged
		gameObject.layer = LayerMask.NameToLayer( (mPlayer != null) ? "Ignore Raycast" : "Default");
	}

	/// <summary>
	/// Save the target position.
	/// </summary>

	[RFC(3)] void MoveObject (Vector3 pos) { mTarget = pos; }
}