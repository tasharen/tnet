//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;
using UnityEditor;
using TNet;

[CanEditMultipleObjects]
[CustomEditor(typeof(TNManager), true)]
public class TNManagerEditor : Editor
{
	public override void OnInspectorGUI ()
	{
		EditorGUILayout.LabelField("LAN IP", TNet.Tools.localAddress.ToString());
		EditorGUILayout.LabelField("WAN IP", TNet.Tools.externalAddress.ToString());
		EditorGUILayout.LabelField("Player Name", TNManager.playerName);

		if (TNManager.isConnected)
		{
			EditorGUILayout.LabelField("Ping", TNManager.ping.ToString());
			List<Channel> list = TNManager.channels;

			foreach (Channel ch in list)
			{
				GUILayout.Space(6f);
				EditorGUILayout.LabelField("Channel #" + ch.id, TNManager.GetHost(ch.id).name);
				EditorGUILayout.LabelField("Players", (TNManager.GetPlayers(ch.id).size + 1).ToString());
			}
		}

		serializedObject.Update();

		if (Application.isPlaying)
		{
			EditorGUI.BeginDisabledGroup(true);
			serializedObject.DrawProperty("objects", "Referenced Objects");
			EditorGUI.EndDisabledGroup();
		}
		else serializedObject.DrawProperty("objects", "Referenced Objects");

		serializedObject.ApplyModifiedProperties();
	}
}
