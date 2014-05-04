//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2014 Tasharen Entertainment
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

#if UNITY_EDITOR

	/// <summary>
	/// Print out useful information about an exception that occurred when trying to call a function.
	/// </summary>

	static void PrintException (System.Exception ex, CachedFunc ent, params object[] parameters)
	{
		if (ex.InnerException != null)
		{
			Debug.LogError(ex.InnerException.Message + "\n" + ex.InnerException.StackTrace + "\n");
		}
		else
		{
			Debug.LogError(ex.Message + "\n" + ex.StackTrace + "\n");
		}
	}
#endif

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
#if UNITY_EDITOR
				try
				{
#endif
					ParameterInfo[] infos = ent.func.GetParameters();

					if (infos.Length == 1 && infos[0].ParameterType == typeof(object[]))
					{
						retVal = ent.func.Invoke(ent.obj, new object[] { parameters });
						return true;
					}
					else
					{
						retVal = ent.func.Invoke(ent.obj, parameters);
						return true;
					}
#if UNITY_EDITOR
				}
				catch (System.Exception ex)
				{
					PrintException(ex, ent, parameters);
				}
#endif
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
#if UNITY_EDITOR
				try
				{
#endif
					ParameterInfo[] infos = ent.func.GetParameters();

					if (infos.Length == 1 && infos[0].ParameterType == typeof(object[]))
					{
						ent.func.Invoke(ent.obj, new object[] { parameters });
						return true;
					}
					else
					{
						ent.func.Invoke(ent.obj, parameters);
						return true;
					}
#if UNITY_EDITOR
				}
				catch (System.Exception ex)
				{
					PrintException(ex, ent, parameters);
					return false;
				}
#endif
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
#if UNITY_EDITOR
				try
				{
					ent.func.Invoke(ent.obj, parameters);
				}
				catch (System.Exception ex)
				{
					PrintException(ex, ent, parameters);
				}
#else
				ent.func.Invoke(ent.obj, parameters);
#endif
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
#if UNITY_EDITOR
				try
				{
					method.Invoke(mb, parameters);
				}
				catch (System.Exception ex)
				{
					Debug.LogError(ex.Message + " (" + mb.GetType() + "." + methodName + ")", mb);
				}
#else
				method.Invoke(mb, parameters);
#endif
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
			Rigidbody rb = go.rigidbody;

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
}
}
