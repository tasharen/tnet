//------------------------------------------
//            Tasharen Network
// Copyright © 2012-2015 Tasharen Entertainment
//------------------------------------------

using UnityEngine;

/// <summary>
/// This script shows how to create objects dynamically over the network.
/// The same Create call will work perfectly fine even if you're not currently connected.
/// This script is attached to the floor in Example 2.
/// </summary>

public class ExampleCreate : MonoBehaviour
{
	/// <summary>
	/// Create a new object above the clicked position
	/// </summary>

	void OnClick ()
	{
		Vector3 pos = TouchHandler.worldPos;
		pos.y += 3f;
		Quaternion rot = Quaternion.Euler(Random.value * 180f, Random.value * 180f, Random.value * 180f);
		TNManager.Create("Created Cube", pos, rot);
	}
}
