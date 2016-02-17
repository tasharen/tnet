//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;
using TNet;
using System.Collections;

/// <summary>
/// This script is attached to the car used by the Multiple Channels example.
/// TNet 3 allows you to be present in multiple channels at once and join/leave them at will.
/// The most obvious example would be an open world multiplayer game where you want players to
/// only be able to see others that are close to them and not receive packets from those that
/// they aren't close to. This script handles joining nearby channels and transferring the
/// object it's attached to to the closest channel.
/// </summary>

public class ExamplePlayerAvatar : TNBehaviour
{
	public float joinDistance = 14f;
	public float leaveDistance = 16f;

	IEnumerator Start ()
	{
		if (tno.isMine)
		{
			// Wait until we've joined the channel before starting the periodic checks
			while (TNManager.isJoiningChannel) yield return null;
			InvokeRepeating("PeriodicCheck", 0.001f, 0.25f);
		}
		else Destroy(this);
	}

	void PeriodicCheck ()
	{
		Vector3 myPos = transform.position;
		ExampleRegion closestRegion = null;
		float closestDistance = float.MaxValue;

		// First find the closest region -- this is the region the player avatar should belong to
		for (int i = 0; i < ExampleRegion.list.size; ++i)
		{
			ExampleRegion region = ExampleRegion.list[i];
			float distance = Vector3.Distance(region.transform.position, myPos);

			if (distance < closestDistance)
			{
				closestDistance = distance;
				closestRegion = region;
			}
		}

		// Now ensure we've joined all the nearby regions in addition to the closest region
		for (int i = 0; i < ExampleRegion.list.size; ++i)
		{
			ExampleRegion region = ExampleRegion.list[i];
			float distance = Vector3.Distance(region.transform.position, myPos);

			if (distance < joinDistance || region == closestRegion)
			{
				// We're close -- join the region's channel
				if (!TNManager.IsInChannel(region.channelID))
					TNManager.JoinChannel(region.channelID, true);
			}
			else if (distance > leaveDistance && tno.channelID != region.channelID)
			{
				// We're far away -- leave the region's channel
				if (TNManager.IsInChannel(region.channelID))
					TNManager.LeaveChannel(region.channelID);
			}
		}

		// Transfer the car to the closest region's channel
		if (closestRegion != null && tno.channelID != closestRegion.channelID)
			tno.TransferToChannel(closestRegion.channelID);
	}
}
