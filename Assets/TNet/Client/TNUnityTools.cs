//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2015 Tasharen Entertainment
//---------------------------------------------

using UnityEngine;
using System.Reflection;

namespace TNet
{
/// <summary>
/// Common Tasharen Network-related functionality and helper functions to be used with Unity.
/// </summary>

static public class UnityTools
{
	/// <summary>
	/// Clear the array references.
	/// </summary>

	static public void Clear (object[] objs)
	{
		for (int i = 0, imax = objs.Length; i < imax; ++i)
			objs[i] = null;
	}

	/// <summary>
	/// Print out useful information about an exception that occurred when trying to call a function.
	/// </summary>

	static void PrintException (System.Exception ex, CachedFunc ent, int funcID, string funcName, params object[] parameters)
	{
		string received = "";

		if (parameters != null)
		{
			for (int b = 0; b < parameters.Length; ++b)
			{
				if (b != 0) received += ", ";
				received += (parameters[b] != null) ? parameters[b].GetType().ToString() : "<null>";
			}
		}

		string expected = "";

		if (ent.parameters != null)
		{
			for (int b = 0; b < ent.parameters.Length; ++b)
			{
				if (b != 0) expected += ", ";
				expected += ent.parameters[b].ParameterType.ToString();
			}
		}

		string err = "[TNet] Failed to call ";
		
		if (ent.obj != null && ent.obj is TNBehaviour)
		{
			TNBehaviour tb = ent.obj as TNBehaviour;
			err += "TNO #" + tb.tno.uid + " ";
		}

		if (string.IsNullOrEmpty(funcName))
		{
			err += "RFC #" + funcID + " on " + (ent.obj != null ? ent.obj.GetType().ToString() : "<null>");
		}
		else err += "RFC " + ent.obj.GetType() + "." + funcName;

		if (ex.InnerException != null) err += ": " + ex.InnerException.Message + "\n";
		else err += ": " + ex.Message + "\n";

		if (received != expected)
		{
			err += "  Expected args: " + expected + "\n";
			err += "  Received args: " + received + "\n\n";
		}

		if (ex.InnerException != null) err += ex.InnerException.StackTrace + "\n";
		else err += ex.StackTrace + "\n";

		Debug.LogError(err, ent.obj as Object);
	}

	/// <summary>
	/// Execute the first function matching the specified ID.
	/// </summary>

	static public bool ExecuteFirst (List<CachedFunc> rfcs, byte funcID, out object retVal, params object[] parameters)
	{
		retVal = null;

		for (int i = 0; i < rfcs.size; ++i)
		{
			CachedFunc ent = rfcs[i];

			if (ent.id == funcID)
			{
				if (ent.parameters == null)
					ent.parameters = ent.func.GetParameters();

				try
				{
					retVal = (ent.parameters.Length == 1 && ent.parameters[0].ParameterType == typeof(object[])) ?
						ent.func.Invoke(ent.obj, new object[] { parameters }) :
						ent.func.Invoke(ent.obj, parameters);
					return (retVal != null);
				}
				catch (System.Exception ex)
				{
					if (ex.GetType() == typeof(System.NullReferenceException)) return false;
					PrintException(ex, ent, funcID, "", parameters);
					return false;
				}
			}
		}
		return false;
	}

	/// <summary>
	/// Invoke the function specified by the ID.
	/// </summary>

	static public bool ExecuteAll (List<CachedFunc> rfcs, byte funcID, params object[] parameters)
	{
		for (int i = 0; i < rfcs.size; ++i)
		{
			CachedFunc ent = rfcs[i];

			if (ent.id == funcID)
			{
				if (ent.parameters == null)
					ent.parameters = ent.func.GetParameters();

				try
				{
					ent.func.Invoke(ent.obj, parameters);
					return true;
				}
				catch (System.Exception ex)
				{
					if (ex.GetType() == typeof(System.NullReferenceException)) return false;
				    PrintException(ex, ent, funcID, "", parameters);
				    return false;
				}
			}
		}
		return false;
	}

	/// <summary>
	/// Invoke the function specified by the function name.
	/// </summary>

	static public bool ExecuteAll (List<CachedFunc> rfcs, string funcName, params object[] parameters)
	{
		bool retVal = false;

		for (int i = 0; i < rfcs.size; ++i)
		{
			CachedFunc ent = rfcs[i];

			if (ent.func.Name == funcName)
			{
				retVal = true;

				if (ent.parameters == null)
					ent.parameters = ent.func.GetParameters();

				try
				{
					ent.func.Invoke(ent.obj, parameters);
					return true;
				}
				catch (System.Exception ex)
				{
					if (ex.GetType() == typeof(System.NullReferenceException)) return false;
					PrintException(ex, ent, 0, funcName, parameters);
				}
			}
		}
		return retVal;
	}

	/// <summary>
	/// Call the specified function on all the scripts. It's an expensive function, so use sparingly.
	/// </summary>

	static public void Broadcast (string methodName, params object[] parameters)
	{
		MonoBehaviour[] mbs = UnityEngine.Object.FindObjectsOfType(typeof(MonoBehaviour)) as MonoBehaviour[];

		for (int i = 0, imax = mbs.Length; i < imax; ++i)
		{
			MonoBehaviour mb = mbs[i];
			MethodInfo method = mb.GetType().GetMethod(methodName,
				BindingFlags.Instance |
				BindingFlags.NonPublic |
				BindingFlags.Public);

			if (method != null)
			{
				try
				{
					method.Invoke(mb, parameters);
				}
				catch (System.Exception ex)
				{
					Debug.LogError(ex.InnerException.Message + " (" + mb.GetType() + "." + methodName + ")\n" +
						ex.InnerException.StackTrace + "\n", mb);
				}
			}
		}
	}

	/// <summary>
	/// Mathf.Lerp(from, to, Time.deltaTime * strength) is not framerate-independent. This function is.
	/// </summary>

	static public float SpringLerp (float from, float to, float strength, float deltaTime)
	{
		if (deltaTime > 1f) deltaTime = 1f;
		int ms = Mathf.RoundToInt(deltaTime * 1000f);
		deltaTime = 0.001f * strength;
		for (int i = 0; i < ms; ++i) from = Mathf.Lerp(from, to, deltaTime);
		return from;
	}

	/// <summary>
	/// Pad the specified rectangle, returning an enlarged rectangle.
	/// </summary>

	static public Rect PadRect (Rect rect, float padding)
	{
		Rect r = rect;
		r.xMin -= padding;
		r.xMax += padding;
		r.yMin -= padding;
		r.yMax += padding;
		return r;
	}

	/// <summary>
	/// Whether the specified game object is a child of the specified parent.
	/// </summary>

	static public bool IsParentChild (GameObject parent, GameObject child)
	{
		if (parent == null || child == null) return false;
		return IsParentChild(parent.transform, child.transform);
	}

	/// <summary>
	/// Whether the specified transform is a child of the specified parent.
	/// </summary>

	static public bool IsParentChild (Transform parent, Transform child)
	{
		if (parent == null || child == null) return false;

		while (child != null)
		{
			if (parent == child) return true;
			child = child.parent;
		}
		return false;
	}

	/// <summary>
	/// Convenience function that instantiates a game object and sets its velocity.
	/// </summary>

	static public GameObject Instantiate (GameObject go, Vector3 pos, Quaternion rot, Vector3 velocity, Vector3 angularVelocity)
	{
		if (go != null)
		{
			go = GameObject.Instantiate(go, pos, rot) as GameObject;
			Rigidbody rb = go.GetComponent<Rigidbody>();

			if (rb != null)
			{
				if (rb.isKinematic)
				{
					rb.isKinematic = false;
					rb.velocity = velocity;
					rb.angularVelocity = angularVelocity;
					rb.isKinematic = true;
				}
				else
				{
					rb.velocity = velocity;
					rb.angularVelocity = angularVelocity;
				}
			}
		}
		return go;
	}

	/// <summary>
	/// Get the game object's child that matches the specified name.
	/// </summary>

	static public GameObject GetChild (this GameObject go, string name)
	{
		Transform trans = go.transform;

		for (int i = 0, imax = trans.childCount; i < imax; ++i)
		{
			Transform t = trans.GetChild(i);
			if (t.name == name) return t.gameObject;
		}
		return null;
	}

	public delegate System.Type GetTypeFunc (string name);
	static public GetTypeFunc ResolveType = System.Type.GetType;

	public delegate UnityEngine.Object LoadFunc (string path);
	static public LoadFunc LoadResource = Resources.Load;

	/// <summary>
	/// Locate the specified object in the Resources folder.
	/// This function will only work properly in the Unity Editor. It's not possible to locate resources outside of it.
	/// </summary>

	static public string LocateResource (Object obj)
	{
#if UNITY_EDITOR
		Object prefab = UnityEditor.PrefabUtility.GetPrefabParent(obj) ?? obj;

		if (prefab != null)
		{
			string childPrefabPath = UnityEditor.AssetDatabase.GetAssetPath(prefab);

			if (!string.IsNullOrEmpty(childPrefabPath) && childPrefabPath.Contains("/Resources/"))
			{
				int index = childPrefabPath.IndexOf("/Resources/");

				if (index != -1)
				{
					childPrefabPath = childPrefabPath.Substring(index + "/Resources/".Length);
					childPrefabPath = Tools.GetFilePathWithoutExtension(childPrefabPath).Replace("\\", "/");

					if (LoadResource(childPrefabPath) != null)
						return childPrefabPath;
				}
			}
		}
#endif
		return null;
	}

	/// <summary>
	/// Set the layer of this game object and all of its children.
	/// </summary>

	static public void SetLayerRecursively (this GameObject go, int layer)
	{
		go.layer = layer;
		Transform t = go.transform;
		for (int i = 0, imax = t.childCount; i < imax; ++i)
			t.GetChild(i).SetLayerRecursively(layer);
	}

	/// <summary>
	/// Set the layer of this transform and all of its children.
	/// </summary>

	static public void SetLayerRecursively (this Transform trans, int layer)
	{
		trans.gameObject.layer = layer;
		for (int i = 0, imax = trans.childCount; i < imax; ++i)
			trans.GetChild(i).SetLayerRecursively(layer);
	}
}
}
