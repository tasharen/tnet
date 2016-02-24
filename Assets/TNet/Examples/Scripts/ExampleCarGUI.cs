//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;
using TNet;

/// <summary>
/// Visible GUI element for the car allowing the player to adjust the frequency of network updates.
/// </summary>

[RequireComponent(typeof(ExampleCar))]
public class ExampleCarGUI : MonoBehaviour
{
	ExampleCar mCar;

	void Awake ()
	{
		mCar = GetComponent<ExampleCar>();
	}

	void OnGUI ()
	{
		if (mCar.tno.isMine)
		{
			GUI.color = Color.black;

			Rect rect = new Rect(10f, 80f, 200f, 20f);
			GUI.Label(rect, "Input sync per second: " + mCar.inputUpdates);
			rect.y += 15f;
			mCar.inputUpdates = GUI.HorizontalSlider(rect, mCar.inputUpdates, 1f, 20f);

			rect.y += 20f;
			GUI.Label(rect, "RB sync per second: " + mCar.rigidbodyUpdates);
			rect.y += 15f;
			mCar.rigidbodyUpdates = GUI.HorizontalSlider(rect, mCar.rigidbodyUpdates, 0.25f, 5f);
		}
	}
}
