//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using UnityEngine;
using TNet;

/// <summary>
/// This simple script changes the color of the object when the object gets clicked on.
/// Note that this color change is not synchronized across the network. The synchronization
/// is actually done by a separate script -- TNSyncProperty.
/// </summary>

public class ChangeObjectColor : MonoBehaviour
{
	public Color color
	{
		get { return renderer.material.color; }
		set { renderer.material.color = value; }
	}

	void OnClick ()
	{
		if		(color == Color.red)	color = Color.green;
		else if (color == Color.green)	color = Color.blue;
		else							color = Color.red;
	}
}