//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;
using UnityEditor;
using TNet;

[CanEditMultipleObjects]
[CustomEditor(typeof(TNServerInstance), true)]
public class TNServerInstanceEditor : Editor
{
	public override void OnInspectorGUI ()
	{
		if (TNServerInstance.isActive)
		{
			EditorGUILayout.LabelField("Name", TNServerInstance.serverName);

			EditorGUILayout.LabelField("Listening Port", TNServerInstance.isListening ?
				TNServerInstance.listeningPort.ToString() : "none");

			EditorGUILayout.LabelField("Player count", TNServerInstance.playerCount.ToString());
		}
	}
}
