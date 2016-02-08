//------------------------------------------
//            Tasharen Network
// Copyright © 2012-2016 Tasharen Entertainment
//------------------------------------------

using UnityEngine;
using TNet;
using System.Collections;

/// <summary>
/// Instantiated objects can be easily transferred from one channel to another.
/// The most obvious usage for this would be moving player avatars or NPCs from one region to another.
/// If a player is present in the region the object gets transferred to, they won't see any changes.
/// If the player is not present, the object will simply act as if it was destroyed. For players
/// that are present in the region it's transferred to that are not present in the region the object
/// was transferred from, the object will simply be created and all of its RFCs will be executed
/// as if it was loaded from a save file.
/// </summary>

public class ExampleTransfer : MonoBehaviour
{
	public int channelID = 0;
	public float joinDistance = 14f;
	public float leaveDistance = 16f;
	public float transferDistance = 7f;

	IEnumerator Start ()
	{
		// Wait until we've joined the channel
		while (TNManager.isJoiningChannel) yield return null;

		// Start the periodic checks
		InvokeRepeating("PeriodicCheck", 0.001f, 0.25f);
		UpdateRenderer();
	}

	void PeriodicCheck ()
	{
		var car = ExampleCar.mine;
		if (car == null) return;

		// If the car belongs to this channel, we don't want to leave it
		if (car.tno.channelID == channelID) return;

		// Check the distance
		float distance = Vector3.Distance(transform.position, car.transform.position);

		if (distance < joinDistance)
		{
			// We're close -- join the channel
			if (!TNManager.IsInChannel(channelID))
				TNManager.JoinChannel(channelID, true);

			// Transfer the player's car into this channel
			if (distance < transferDistance && car.tno.channelID != channelID)
				car.tno.TransferToChannel(channelID);
		}
		else if (distance > leaveDistance)
		{
			// We're far away -- leave the channel
			if (TNManager.IsInChannel(channelID))
				TNManager.LeaveChannel(channelID);
		}
	}

	void OnNetworkJoinChannel (int channelID, bool success, string msg)
	{
		if (channelID == this.channelID) UpdateRenderer();
	}

	void OnNetworkLeaveChannel (int channelID)
	{
		if (channelID == this.channelID) UpdateRenderer();
	}

	void UpdateRenderer ()
	{
		Renderer ren = GetComponent<Renderer>();
		
		if (ren != null)
		{
			Color c = TNManager.IsInChannel(channelID) ? Color.green : Color.red;
			c.a = 0.25f;
			ren.material.color = c;
		}
	}
}
