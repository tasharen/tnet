//------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//------------------------------------------

using UnityEngine;

/// <summary>
/// This helper script was created to make it possible to add multi-line text to TextMeshes.
/// </summary>

[ExecuteInEditMode]
public class FillText : MonoBehaviour
{
	public string text = "Hello World";

	TextMesh mMesh;
	GUIText mText;

	void Awake ()
	{
		mMesh = GetComponent<TextMesh>();
		mText = GetComponent<GUIText>();
		
		if (Application.isPlaying)
		{
			Update();
			Destroy(this);
		}
	}

	void Update ()
	{
		if (mMesh != null && mMesh.text != text) mMesh.text = text;
		if (mText != null && mText.text != text) mText.text = text;
	}
}
