//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;
using TNet;

/// <summary>
/// This script shows how to create objects dynamically over the network.
/// The same Instantiate call will work perfectly fine even if you're not currently connected.
/// This script is attached to the floor in Example 2.
/// </summary>

public class ExampleCreate : MonoBehaviour
{
	public int channelID = 0;
	public string prefabName = "Created Cube";
	public float autoDestroyDelay = 5f;

	/// <summary>
	/// Channel ID is not required for TNManager.Instantiate calls, however if you are working with
	/// multiple channels, you will want to pass which channel you want the object to be created in.
	/// </summary>

	void Start () { if (channelID < 1) channelID = TNManager.lastChannelID; }

	/// <summary>
	/// Create a new object above the clicked position
	/// </summary>

	void OnClick ()
	{
		// Let's not try to create objects unless we are in this channel
		if (TNManager.isConnected && !TNManager.IsInChannel(channelID)) return;

		// Object's position will be up in the air so that it can fall down
		Vector3 pos = TouchHandler.worldPos + Vector3.up * 3f;

		// Object's rotation is completely random
		Quaternion rot = Quaternion.Euler(Random.value * 180f, Random.value * 180f, Random.value * 180f);

		// Object's color is completely random
		Color color = new Color(Random.value, Random.value, Random.value, 1f);

		// Create the object using a custom creation function defined below.
		// Note that passing "channelID" is optional. If you don't pass anything, TNet will pick one for you.
		TNManager.Instantiate(channelID, "ColoredObject", prefabName, true, pos, rot, color, autoDestroyDelay);
	}

	/// <summary>
	/// RCCs (Remote Creation Calls) allow you to pass arbitrary amount of parameters to the object you are creating.
	/// TNManager will call this function, passing a prefab to it that you should then instantiate.
	/// </summary>

	[RCC]
	static GameObject ColoredObject (GameObject prefab, Vector3 pos, Quaternion rot, Color c, float autoDestroyDelay)
	{
		// Instantiate the prefab
		GameObject go = prefab.Instantiate();

		// Set the position and rotation based on the passed values
		Transform t = go.transform;
		t.position = pos;
		t.rotation = rot;

		// Set the renderer's color as well
		go.GetComponentInChildren<Renderer>().material.color = c;

		// Destroy the object after enough time has passed
		if (autoDestroyDelay > 0f) go.DestroySelf(autoDestroyDelay);
		return go;
	}
}
