//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using TNet;
using UnityEngine;
using UnityEditor;
using System.Reflection;

/// <summary>
/// Inspector class used to view and edit TNAutoSync.
/// </summary>

[CustomEditor(typeof(TNAutoSync))]
public class TNAutoSyncInspector : Editor
{
	override public void OnInspectorGUI ()
	{
		TNAutoSync sync = target as TNAutoSync;

		DrawTarget(sync);
		DrawProperty(sync);

		int updates = EditorGUILayout.IntField("Updates Per Second", sync.updatesPerSecond);
		bool persistent = EditorGUILayout.Toggle("Persistent", sync.isPersistent);
		bool important = EditorGUILayout.Toggle("Important", sync.isImportant);
		bool host = EditorGUILayout.Toggle("Only Host Can Sync", sync.onlyHostCanSync);

		if (sync.updatesPerSecond != updates ||
			sync.isPersistent != persistent ||
			sync.isImportant != important ||
			sync.onlyHostCanSync != host)
		{
			sync.updatesPerSecond = updates;
			sync.isPersistent = persistent;
			sync.isImportant = important;
			sync.onlyHostCanSync = host;
			EditorUtility.SetDirty(sync);
		}
	}

	void DrawTarget (TNAutoSync sync)
	{
		Component[] comps = sync.GetComponents<Component>();

		List<Component> list = new List<Component>();

		for (int i = 0, imax = comps.Length; i < imax; ++i)
		{
			if (comps[i] != sync)
			{
				list.Add(comps[i]);
			}
		}

		int index = 0;
		string[] names = new string[list.size + 1];
		names[0] = "<None>";

		for (int i = 0; i < list.size; ++i)
		{
			if (list[i] == sync.target) index = i + 1;
			names[i + 1] = list[i].GetType().ToString();
		}

		int newIndex = EditorGUILayout.Popup("Target", index, names);

		if (newIndex != index)
		{
			sync.target = (newIndex == 0) ? null : list[newIndex - 1];
			sync.propertyName = "";
			EditorUtility.SetDirty(sync);
		}
	}

	void DrawProperty (TNAutoSync sync)
	{
		if (sync.target == null) return;

		FieldInfo[] fields = sync.target.GetType().GetFields(
			BindingFlags.Instance | BindingFlags.Public);
		
		PropertyInfo[] properties = sync.target.GetType().GetProperties(
			BindingFlags.Instance | BindingFlags.Public);

		int index = 0;
		List<string> names = new List<string>();
		names.Add("<None>");

		for (int i = 0; i < fields.Length; ++i)
		{
			if (TNet.Tools.CanBeSerialized(fields[i].FieldType))
			{
				if (fields[i].Name == sync.propertyName) index = names.size;
				names.Add(fields[i].Name);
			}
		}
		
		names[fields.Length + 2] = "";
		
		for (int i = 0; i < properties.Length; ++i)
		{
			PropertyInfo pi = properties[i];

			if (TNet.Tools.CanBeSerialized(pi.PropertyType) && pi.CanWrite && pi.CanRead)
			{
				if (pi.Name == sync.propertyName) index = names.size;
				names.Add(pi.Name);
			}
		}

		int newIndex = EditorGUILayout.Popup("Target", index, names.ToArray());

		if (newIndex != index)
		{
			sync.propertyName = (newIndex == 0) ? "" : names[newIndex];
			EditorUtility.SetDirty(sync);
		}
	}
}