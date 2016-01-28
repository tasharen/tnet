//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2016 Tasharen Entertainment
//---------------------------------------------

using UnityEngine;
using System.Collections;
using TNet;

/// <summary>
/// Instantiate the specified prefab at the game object's position.
/// </summary>

public class TNAutoCreate : MonoBehaviour
{
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
		while (TNManager.isJoiningChannel) yield return null;
		TNManager.Instantiate("CreateAtPosition", prefabPath, persistent, transform.position, transform.rotation);
		Destroy(gameObject);
	}

	[RCC]
	static GameObject CreateAtPosition (GameObject prefab, Vector3 pos, Quaternion rot)
	{
		// Instantiate the prefab
		GameObject go = Object.Instantiate(prefab) as GameObject;

		// Set the position and rotation based on the passed values
		Transform t = go.transform;
		t.position = pos;
		t.rotation = rot;
		return go;
	}
}
