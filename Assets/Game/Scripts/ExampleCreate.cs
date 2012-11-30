//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using UnityEngine;

/// <summary>
/// This script shows how to create objects dynamically over the network.
/// The same Create call will work perfectly fine even if you're not currently connected.
/// This script is attached to the floor in Example 3.
/// </summary>

public class ExampleCreate : MonoBehaviour
{
	public GameObject objectToCreate;

	/// <summary>
	/// Raycast into the screen to determine where we've clicked and create a new object above that position.
	/// </summary>

	void OnMouseUpAsButton ()
	{
		Vector3 pos = (Input.touchCount > 0) ? (Vector3)Input.GetTouch(0).position : Input.mousePosition;
		Ray ray = Camera.main.ScreenPointToRay(pos);
		RaycastHit hit;

		if (Physics.Raycast(ray, out hit, 100f, -1))
		{
			pos = hit.point;
			pos.y += 3f;
			Quaternion rot = Quaternion.Euler(Random.value * 180f, Random.value * 180f, Random.value * 180f);

			// Create a new object above the clicked position
			TNManager.Create(objectToCreate, pos, rot);
		}
	}
}