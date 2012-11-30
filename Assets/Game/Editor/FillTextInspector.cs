//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using UnityEngine;
using UnityEditor;

/// <summary>
/// This is an inspector class for the FillText helper class.
/// </summary>

[CustomEditor(typeof(FillText))]
public class FillTextInspector : Editor
{
	public override void OnInspectorGUI ()
	{
		EditorGUIUtility.LookLikeControls(80f);
		FillText fill = target as FillText;

		string text = fill.text;
		text = EditorGUILayout.TextArea(text, GUI.skin.textArea, GUILayout.Height(100f));

		if (text != fill.text)
		{
			Undo.RegisterUndo(fill, "Fill Text Change");
			EditorUtility.SetDirty(fill);
			fill.text = text;
		}
	}
}