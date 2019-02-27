//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;
using UnityEditor;
using TNet;

[CanEditMultipleObjects]
[CustomEditor(typeof(TNObject), true)]
public class TNObjectEditor : Editor
{
	static void Print (DataNode data)
	{
		var lines = data.ToString().Split('\n');
		foreach (var line in lines) EditorGUILayout.LabelField(line.Replace("\t", "  "));
	}

	public override void OnInspectorGUI ()
	{
		TNObject obj = target as TNObject;

		if (Application.isPlaying)
		{
			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.LabelField("Channel", obj.channelID.ToString("N0"));
			EditorGUILayout.LabelField("ID", obj.uid.ToString("N0"));
			EditorGUILayout.LabelField("Creator", obj.creatorPlayerID.ToString("N0"));

			if (obj.owner != null)
			{
				EditorGUILayout.LabelField("Owner", obj.owner.name + " (" + obj.ownerID.ToString("N0") + ")");
			}
			else EditorGUILayout.LabelField("Owner", obj.ownerID.ToString("N0"));

			var host = TNManager.GetHost(obj.channelID);
			EditorGUILayout.LabelField("Host", (host != null) ? host.name : "<none>");

			var data = obj.dataNode;
			if (data != null && data.children.size > 0) Print(data);

			EditorGUI.EndDisabledGroup();
		}
		else
		{
			serializedObject.Update();
			var staticID = serializedObject.FindProperty("mStaticID");
			EditorGUILayout.PropertyField(staticID, new GUIContent("ID"));
			var sp = serializedObject.FindProperty("ignoreWarnings");
			EditorGUILayout.PropertyField(sp, new GUIContent("Ignore Warnings"));

			var type = PrefabUtility.GetPrefabType(obj.gameObject);

			if (type == PrefabType.Prefab)
			{
				serializedObject.ApplyModifiedProperties();
				return;
			}

			if (staticID.intValue == 0)
			{
				EditorGUILayout.HelpBox("Object ID of '0' means this object must be dynamically instantiated via TNManager.Instantiate.", MessageType.Info);
				if (GUILayout.Button("Assign Unique ID")) staticID.intValue = (int)TNObject.GetUniqueID(false);
			}
			else
			{
				var tnos = FindObjectsOfType<TNObject>();

				foreach (TNObject o in tnos)
				{
					if (o == obj) continue;

					if (o.uid == obj.uid)
					{
						EditorGUILayout.HelpBox("This ID is shared with other TNObjects. A unique ID is required in order for RFCs to function properly.", MessageType.Error);
						break;
					}
				}
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
}
