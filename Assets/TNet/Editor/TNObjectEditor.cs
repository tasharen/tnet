//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;
using UnityEditor;
using TNet;

[CanEditMultipleObjects]
[CustomEditor(typeof(TNObject), true)]
public class TNObjectEditor : Editor
{
	public override void OnInspectorGUI ()
	{
		TNObject obj = target as TNObject;

		if (Application.isPlaying)
		{
			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.LabelField("Channel", obj.channelID.ToString());
			EditorGUILayout.LabelField("ID", obj.uid.ToString());

			if (obj.owner != null)
			{
				EditorGUILayout.LabelField("Owner", obj.owner.name + " (" + obj.ownerID + ")");
			}
			else EditorGUILayout.LabelField("Owner", obj.ownerID.ToString());

			TNet.Player host = TNManager.GetHost(TNManager.lastChannelID);
			EditorGUILayout.LabelField("Host", (host != null) ? host.name : "<none>");
			if (obj.parent != null) EditorGUILayout.ObjectField("Parent", obj.parent, typeof(TNObject), true);
			EditorGUI.EndDisabledGroup();
		}
		else
		{
			serializedObject.Update();
			SerializedProperty sp = serializedObject.FindProperty("id");
			EditorGUILayout.PropertyField(sp, new GUIContent("ID"));
			serializedObject.ApplyModifiedProperties();

			PrefabType type = PrefabUtility.GetPrefabType(obj.gameObject);
			if (type == PrefabType.Prefab) return;

			if (obj.uid == 0)
			{
				EditorGUILayout.HelpBox("Object ID of '0' means this object must be dynamically instantiated via TNManager.Instantiate.", MessageType.Info);
			}
			else
			{
				TNObject[] tnos = FindObjectsOfType<TNObject>();

				foreach (TNObject o in tnos)
				{
					if (o == obj || o.parent != null) continue;

					if (o.uid == obj.uid)
					{
						EditorGUILayout.HelpBox("This ID is shared with other TNObjects. A unique ID is required in order for RFCs to function properly.", MessageType.Error);
						break;
					}
				}
			}
		}
	}
}
