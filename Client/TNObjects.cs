using UnityEngine;

/// <summary>
/// List of objects that can be instantiated by the network.
/// Must be present on all clients in the scene that uses it.
/// </summary>

[RequireComponent(typeof(TNView))]
[AddComponentMenu("TNet/Network Objects")]
public class TNObjects : MonoBehaviour
{
	public GameObject[] list;

	static public TNObjects instance;

	void Awake () { instance = this; }
	void OnDestroy () { instance = null; }

	
}