//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2015 Tasharen Entertainment
//---------------------------------------------

using UnityEngine;
using TNet;
using System.Collections;

/// <summary>
/// Instantiate the specified prefab at the game object's position.
/// </summary>

public class TNAutoCreate : MonoBehaviour
{
	/// <summary>
	/// Prefab to instantiate.
	/// </summary>

	public GameObject prefab;

	/// <summary>
	/// Whether the instantiated object will remain in the game when the player that created it leaves.
	/// Set this to 'false' for the player's avatar.
	/// </summary>

	public bool persistent = false;

	IEnumerator Start ()
	{
		while (TNManager.isJoiningChannel) yield return null;
		TNManager.Create(prefab, transform.position, transform.rotation, persistent);
		Destroy(gameObject);
	}
}
