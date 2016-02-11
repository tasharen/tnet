//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;
using TNet;
using UnityEngine.UI;

/// <summary>
/// Simple script that can be attached to a text field in order to show all error messages.
/// </summary>

[RequireComponent(typeof(Text))]
public class ExampleError : MonoBehaviour
{
	void OnEnable () { TNManager.onError += OnError; }
	void OnDisable () { TNManager.onError -= OnError; }

	void OnError (string message)
	{
		GetComponent<Text>().text = message;
	}
}
