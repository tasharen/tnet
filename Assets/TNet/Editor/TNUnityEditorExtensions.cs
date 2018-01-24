//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;
using UnityEditor;

namespace TNet
{
	static public class UnityEditorExtensions
	{
		/// <summary>
		/// Show a file export dialog.
		/// </summary>

		static public string ShowExportDialog (string name, string fileName, string extension = "bytes", string prefsName = "TNet Path")
		{
			string currentPath = EditorPrefs.GetString(prefsName, "Assets/");
			string path = EditorUtility.SaveFilePanel(name, currentPath, fileName + "." + extension, extension);

			if (!string.IsNullOrEmpty(path))
				EditorPrefs.SetString(prefsName, System.IO.Path.GetDirectoryName(path));

			return path;
		}

		/// <summary>
		/// Show a file import dialog.
		/// </summary>

		static public string ShowImportDialog (string name, string extension = "bytes", string prefsName = "TNet Path")
		{
			string currentPath = EditorPrefs.GetString(prefsName, "Assets/");
			string path = EditorUtility.OpenFilePanel(name, currentPath, extension);

			if (!string.IsNullOrEmpty(path))
				EditorPrefs.SetString(prefsName, System.IO.Path.GetDirectoryName(path));

			return path;
		}

		/// <summary>
		/// Helper function that draws a serialized property.
		/// </summary>

		static public SerializedProperty DrawProperty (this SerializedObject serializedObject, string property, params GUILayoutOption[] options)
		{
			return DrawProperty(null, serializedObject, property, options);
		}

		/// <summary>
		/// Helper function that draws a serialized property.
		/// </summary>

		static public SerializedProperty DrawProperty (this SerializedObject serializedObject, string property, string label, params GUILayoutOption[] options)
		{
			return DrawProperty(label, serializedObject, property, options);
		}

		/// <summary>
		/// Helper function that draws a serialized property.
		/// </summary>

		static public SerializedProperty DrawProperty (string label, SerializedObject serializedObject, string property, params GUILayoutOption[] options)
		{
			SerializedProperty sp = serializedObject.FindProperty(property);

			if (sp != null)
			{
				if (sp.isArray && sp.type != "string") DrawArray(serializedObject, property, label ?? property);
				else if (label != null) EditorGUILayout.PropertyField(sp, new GUIContent(label), options);
				else EditorGUILayout.PropertyField(sp, options);
			}
			else Debug.LogWarning("Unable to find property " + property);
			return sp;
		}

		/// <summary>
		/// Helper function that draws an array property.
		/// </summary>

		static public void DrawArray (this SerializedObject obj, string property, string title)
		{
			SerializedProperty sp = obj.FindProperty(property + ".Array.size");

			if (sp != null && DrawHeader(title))
			{
				BeginContents();
				int size = sp.intValue;
				int newSize = EditorGUILayout.IntField("Size", size);
				if (newSize != size) obj.FindProperty(property + ".Array.size").intValue = newSize;

				EditorGUI.indentLevel = 1;

				for (int i = 0; i < newSize; i++)
				{
					SerializedProperty p = obj.FindProperty(string.Format("{0}.Array.data[{1}]", property, i));
					if (p != null) EditorGUILayout.PropertyField(p);
				}
				EditorGUI.indentLevel = 0;
				EndContents();
			}
		}

		/// <summary>
		/// Begin drawing the content area.
		/// </summary>

		static public void BeginContents ()
		{
			GUILayout.BeginHorizontal();
			EditorGUILayout.BeginHorizontal("AS TextArea", GUILayout.MinHeight(10f));
			GUILayout.BeginVertical();
			GUILayout.Space(2f);
		}

		/// <summary>
		/// End drawing the content area.
		/// </summary>

		static public void EndContents ()
		{
			GUILayout.Space(3f);
			GUILayout.EndVertical();
			EditorGUILayout.EndHorizontal();
			GUILayout.Space(3f);
			GUILayout.EndHorizontal();
			GUILayout.Space(3f);
		}

		/// <summary>
		/// Draw a simple collapsible header header.
		/// </summary>

		static public bool DrawHeader (string text) { return DrawHeader(text, text, false); }

		/// <summary>
		/// Draw a simple collapsible header header.
		/// </summary>

		static public bool DrawHeader (string text, string key, bool forceOn)
		{
			bool state = EditorPrefs.GetBool(key, true);

			GUILayout.Space(3f);
			if (!forceOn && !state) GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f);
			GUILayout.BeginHorizontal();
			GUI.changed = false;

			text = "<b><size=11>" + text + "</size></b>";
			if (state) text = "\u25BC " + text;
			else text = "\u25BA " + text;
			if (!GUILayout.Toggle(true, text, "dragtab", GUILayout.MinWidth(20f))) state = !state;

			if (GUI.changed) EditorPrefs.SetBool(key, state);

			GUILayout.Space(2f);
			GUILayout.EndHorizontal();
			GUI.backgroundColor = Color.white;
			if (!forceOn && !state) GUILayout.Space(3f);
			return state;
		}

		static public void SetDirty (Object obj, string undoName = "last change")
		{
#if UNITY_EDITOR
			if (obj)
			{
#if UNITY_4_7 || UNITY_5_4 || UNITY_5_5 || UNITY_5_6
				UnityEditor.EditorUtility.SetDirty(obj);
#else
				Undo.RecordObject(obj, undoName);
#endif
			}
#endif
		}
	}
}
