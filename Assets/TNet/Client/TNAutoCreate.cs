//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;
using System.Collections;

namespace TNet
{
/// <summary>
/// Instantiate the specified prefab at the game object's position.
/// </summary>

public class TNAutoCreate : MonoBehaviour
{
	/// <summary>
	/// ID of the channel where the prefab should be created. '0' means "last used channel" (not recommended).
	/// </summary>

	public int channelID = 0;

	/// <summary>
	/// Prefab to instantiate.
	/// </summary>

	public string prefabPath;

	/// <summary>
	/// Whether the instantiated object will remain in the game when the player that created it leaves.
	/// Set this to 'false' for the player's avatar.
	/// </summary>

	public bool persistent = false;

	IEnumerator Start ()
	{
		if (channelID < 1) channelID = TNManager.lastChannelID;
		while (TNManager.isJoiningChannel || !TNManager.IsInChannel(channelID)) yield return null;
		TNManager.Instantiate(channelID, "CreateAtPosition", prefabPath, persistent, transform.position, transform.rotation);
		Destroy(gameObject);
	}

	[RCC]
	static GameObject CreateAtPosition (GameObject prefab, Vector3 pos, Quaternion rot)
	{
		// Instantiate the prefab
		GameObject go = prefab.Instantiate();

		// Set the position and rotation based on the passed values
		Transform t = go.transform;
		t.position = pos;
		t.rotation = rot;
		return go;
	}
}
}
