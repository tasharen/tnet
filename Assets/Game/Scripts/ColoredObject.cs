//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using UnityEngine;
using TNet;

/// <summary>
/// This simple script shows how to change the color of an object on all connected clients.
/// You can see it used in Example 1.
/// </summary>

[ExecuteInEditMode]
[RequireComponent(typeof(TNObject))]
public class ColoredObject : MonoBehaviour
{
	/// <summary>
	/// This function is called by the server when one of the players sends an RFC call.
	/// </summary>

	[RFC] void OnColor (Color c)
	{
		renderer.material.color = c;
	}

	/// <summary>
	/// Display 3 buttons. Clicking on each triggers a remote function on all connected players.
	/// </summary>

	void OnGUI ()
	{
		TNObject tno = GetComponent<TNObject>();
		Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
		Rect rect = new Rect(screenPos.x - 40f, Screen.height - (screenPos.y + 20f), 80f, 20f);

		if (GUI.Button(rect, "Red"))	tno.Send("OnColor", Target.AllBuffered, Color.red);
		rect.y += 20f;
		if (GUI.Button(rect, "Green"))	tno.Send("OnColor", Target.AllBuffered, Color.green);
		rect.y += 20f;
		if (GUI.Button(rect, "Blue"))	tno.Send("OnColor", Target.AllBuffered, Color.blue);
	}
}