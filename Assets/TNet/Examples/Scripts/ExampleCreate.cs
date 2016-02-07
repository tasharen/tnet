//------------------------------------------
//            Tasharen Network
// Copyright © 2012-2016 Tasharen Entertainment
//------------------------------------------

using UnityEngine;
using TNet;

/// <summary>
/// This script shows how to create objects dynamically over the network.
/// The same Instantiate call will work perfectly fine even if you're not currently connected.
/// This script is attached to the floor in Example 2.
/// </summary>

public class ExampleCreate : MonoBehaviour
{
	/// <summary>
	/// Create a new object above the clicked position
	/// </summary>

	void OnClick ()
	{
		// Object's position will be up in the air so that it can fall down
		Vector3 pos = TouchHandler.worldPos + Vector3.up * 3f;

		// Object's rotation is completely random
		Quaternion rot = Quaternion.Euler(Random.value * 180f, Random.value * 180f, Random.value * 180f);

		// Object's color is completely random
		Color color = new Color(Random.value, Random.value, Random.value, 1f);

		// Create the object using a custom creation function defined below
		TNManager.Instantiate("ColoredObject", "Created Cube", true, pos, rot, color);
	}

	/// <summary>
	/// RCCs (Remote Creation Calls) allow you to pass arbitrary amount of parameters to the object you are creating.
	/// TNManager will call this function, passing a prefab to it that you should then instantiate.
	/// </summary>

	[RCC]
	static GameObject ColoredObject (GameObject prefab, Vector3 pos, Quaternion rot, Color c)
	{
		// Instantiate the prefab
		GameObject go = Object.Instantiate(prefab) as GameObject;

		// Set the position and rotation based on the passed values
		Transform t = go.transform;
		t.position = pos;
		t.rotation = rot;

		// Set the renderer's color as well
		go.GetComponentInChildren<Renderer>().material.color = c;
		return go;
	}
}
