using UnityEngine;
using UnityEditor;
using System;

public static class TNEditorTools
{
	[MenuItem("Component/TNet/Normalize IDs")]
	static public void NormalizeIDs ()
	{
		Undo.RegisterSceneUndo("Normalize TNObject IDs");
		TNObject[] objs = UnityEngine.Object.FindObjectsOfType(typeof(TNObject)) as TNObject[];
		Array.Sort(objs, delegate(TNObject o1, TNObject o2) { return o1.uid.CompareTo(o2.uid); });
		
		for (int i = 0; i < objs.Length; ++i)
		{
			objs[i].uid = (uint)(1 + i);
			EditorUtility.SetDirty(objs[i]);
		}
	}
}
