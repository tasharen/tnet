//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

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

	static public void PrintException (System.Exception ex, CachedFunc ent, int funcID, string funcName, params object[] parameters)
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
			err += "function #" + funcID + " on " + (ent.obj != null ? ent.obj.GetType().ToString() : "<null>");
		}
		else if (ent.obj != null)
		{
			err += "function " + ent.obj.GetType() + "." + funcName;
		}
		else err += "function " + funcName;

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
					Debug.LogError((ex.InnerException ?? ex).Message + " (" + mb.GetType() + "." + methodName + ")\n" +
						(ex.InnerException ?? ex).StackTrace + "\n", mb);
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

	/// <summary>
	/// Clone this game object.
	/// </summary>

	static public GameObject Instantiate (this GameObject prefab)
	{
		var obj = Object.Instantiate(prefab) as GameObject;
		obj.name = prefab.name;
		return obj;
	}

	/// <summary>
	/// Destroy the game object.
	/// </summary>

	static public void DestroySelf (this GameObject go)
	{
		var tno = go.GetComponent<TNObject>();
		if (tno != null) tno.DestroySelf();
		else Object.Destroy(go);
	}

	/// <summary>
	/// Destroy the game object after the specified amount of seconds have passed.
	/// </summary>

	static public void DestroySelf (this GameObject go, float delay, bool onlyIfMine = true)
	{
		var tno = go.GetComponent<TNObject>();
		if (tno != null) tno.DestroySelf(delay, onlyIfMine);
		else Object.Destroy(go, delay);
	}

	public delegate System.Type GetTypeFunc (string name);
	public delegate UnityEngine.Object LoadFunc (string path, System.Type type);
	public delegate UnityEngine.Object LoadExFunc (string path, System.Type type, string name);
	public delegate byte[] LoadBinaryFunc (string path);

	static System.Collections.Generic.Dictionary<string, System.Type> mTypeCache =
		new System.Collections.Generic.Dictionary<string, System.Type>();

	/// <summary>
	/// Try to resolve a type. Considers all loaded binaries.
	/// </summary>

	static new public GetTypeFunc GetType = GetTypeEx;

	/// <summary>
	/// Basic GetType() function, expanded to support type retrieval across all loaded assemblies.
	/// </summary>

	static public System.Type GetTypeEx (string name)
	{
		System.Type t = null;
		if (mTypeCache.TryGetValue(name, out t) && t != null) return t;

		t = System.Type.GetType(name);

		if (t == null)
		{
			var s = "UnityEngine." + name + ", UnityEngine, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
			t = System.Type.GetType(s);

			if (t == null)
			{
				Assembly[] asm = TypeExtensions.GetAssemblies();

				for (int i = 0, imax = asm.Length; i < imax; ++i)
				{
					Assembly assembly = asm[i];
					t = assembly.GetType(name);
					if (t != null) break;
				}
			}
		}

		if (Application.isPlaying) mTypeCache[name] = t;
		return t;
	}

	/// <summary>
	/// Function used to load binaries. Loads built-in TextAssets first, then tries appending the "bytes" extension.
	/// </summary>

	static public LoadBinaryFunc LoadBinary = delegate(string path)
	{
		TextAsset asset = Resources.Load<TextAsset>(path);
		if (asset != null) return asset.bytes;
		return Tools.ReadFile(path + ".bytes");
	};

	/// <summary>
	/// Just like Resources.Load, but capable of loading prefabs saved in DataNode format.
	/// </summary>

	static public LoadFunc LoadResource = delegate(string path, System.Type type)
	{
		if (string.IsNullOrEmpty(path)) return null;
		if (type == typeof(GameObject)) return LoadPrefab(path);
		return Resources.Load(path, type);
	};

	/// <summary>
	/// Extended Load function that's also capable of loading meshes residing inside model files.
	/// </summary>

	static public LoadExFunc LoadResourceEx = delegate(string path, System.Type type, string name)
	{
		if (string.IsNullOrEmpty(path)) return null;

		if (type == typeof(Mesh))
		{
			Mesh[] meshes = Resources.LoadAll<Mesh>(path);
			foreach (Mesh m in meshes)
				if (m.name == name)
					return m;
		}
		return LoadResource(path, type);
	};

	/// <summary>
	/// Just like Resources.Load, but capable of loading prefabs saved in DataNode format.
	/// </summary>

	static public T Load<T> (string path) where T : Object
	{
		if (string.IsNullOrEmpty(path)) return null;
		return LoadResource(path, typeof(T)) as T;
	}

	/// <summary>
	/// Extended Load function that's also capable of loading meshes residing inside model files.
	/// </summary>

	static public T Load<T> (string path, string name) where T : Object
	{
		if (string.IsNullOrEmpty(path)) return null;
		return LoadResourceEx(path, typeof(T), name) as T;
	}

	/// <summary>
	/// Locate the specified object in the Resources folder.
	/// This function will only work in the Unity Editor.
	/// </summary>

	static public string LocateResource (Object obj, bool allowPrefabInstances = false)
	{
#if UNITY_EDITOR
		Object prefab = UnityEditor.PrefabUtility.GetPrefabParent(obj) ?? obj;

		if (prefab != null)
		{
			if (!allowPrefabInstances && prefab != obj) return null;

			string childPrefabPath = UnityEditor.AssetDatabase.GetAssetPath(prefab);

			if (!string.IsNullOrEmpty(childPrefabPath) && childPrefabPath.Contains("/Resources/"))
			{
				int index = childPrefabPath.IndexOf("/Resources/");

				if (index != -1)
				{
					childPrefabPath = childPrefabPath.Substring(index + "/Resources/".Length);
					childPrefabPath = Tools.GetFilePathWithoutExtension(childPrefabPath).Replace("\\", "/");

					Object loaded = Resources.Load(childPrefabPath);
					if (loaded != null && loaded.GetType() == obj.GetType()) return childPrefabPath;
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

	/// <summary>
	/// Returns the hierarchy of the object in a human-readable format.
	/// </summary>

	static public string GetHierarchy (this Transform obj)
	{
		string path = obj.name;

		while (obj.parent != null)
		{
			obj = obj.parent;
			path = obj.name + "/" + path;
		}
		return path;
	}

	/// <summary>
	/// Returns the hierarchy of the object in a human-readable format.
	/// </summary>

	static public string GetHierarchy (this Transform target, Transform source)
	{
		List<Transform> sourceList = new List<Transform>();
		List<Transform> targetList = new List<Transform>();

		sourceList.Add(source);
		targetList.Add(target);

		while (source.parent != null)
		{
			sourceList.Insert(0, source.parent);
			source = source.parent;
		}

		while (target.parent != null)
		{
			targetList.Insert(0, target.parent);
			target = target.parent;
		}

		while (sourceList.size > 0 && targetList.size > 0)
		{
			if (sourceList[0] == targetList[0])
			{
				sourceList.RemoveAt(0);
				targetList.RemoveAt(0);
			}
			else break;
		}

		string path = "";

		for (int i = sourceList.size; i > 0; )
		{
			path += "../";
			sourceList.RemoveAt(--i);
		}

		for (int i = 0; i < targetList.size; ++i)
		{
			if (i + 1 == targetList.size) path += targetList[i].name;
			else path += targetList[i].name + "/";
		}
		return path;
	}

	/// <summary>
	/// Type.GetType that works with multiple assemblies.
	/// </summary>

	static public System.Type FindType (string name)
	{
		System.Type t = GetTypeEx(name);
		if (t != null) return t;

		List<System.Type> types = TypeExtensions.GetTypes();
		List<string> names = TypeExtensions.GetTypeNames();

		for (int i = 0; i < names.size; ++i)
			if (names[i] == name || types[i].Name == name)
				return types[i];

		return null;
	}

	/// <summary>
	/// Convert an object reference to a serializable string format.
	/// </summary>

	static public string ReferenceToString (this GameObject go, UnityEngine.Object obj)
	{
		if (obj == null) return null;

		if (obj is Shader)
		{
			return "asset|" + obj.GetType().ToString().Replace("UnityEngine.", "") + "|" + (obj as Shader).name;
		}
		else
		{
#if UNITY_EDITOR
			string assetPath = UnityEditor.AssetDatabase.GetAssetPath(obj);

			if (!string.IsNullOrEmpty(assetPath))
			{
				int index = assetPath.IndexOf("Resources/");
				if (index != -1) assetPath = assetPath.Substring(index + "Resources/".Length);
				assetPath = Tools.GetFilePathWithoutExtension(assetPath).Replace('\\', '/');
				return "asset|" + obj.GetType().ToString().Replace("UnityEngine.", "") + "|" + assetPath;
			}
#endif
			UnityEngine.GameObject gobj = obj as UnityEngine.GameObject;
			UnityEngine.Component comp = obj as UnityEngine.Component;

			if (comp != null || gobj != null)
			{
				string path = (gobj ?? comp.gameObject).transform.GetHierarchy(go.transform);
				if (!string.IsNullOrEmpty(path))
					return "ref|" + obj.GetType().ToString().Replace("UnityEngine.", "") + "|" + path;
			}
		}
		return null;
	}

	/// <summary>
	/// Convert a serialized string reference to an actual object reference.
	/// </summary>

	static public UnityEngine.Object StringToReference (this GameObject go, string path)
	{
		if (string.IsNullOrEmpty(path)) return null;
		string[] split = path.Split(new char[] { '|' }, 3);

		if (split.Length == 3)
		{
			System.Type myType = UnityTools.FindType(split[1]);

			if (myType != null)
			{
				if (myType == typeof(Shader))
				{
					return Shader.Find(split[2]);
				}
				else if (split[0] == "asset")
				{
					return Resources.Load(split[2], myType);
				}
				else if (split[0] == "ref")
				{
					Transform t = go.transform;

					string[] splitPath = split[2].Split('/');

					for (int i = 0; i < splitPath.Length; ++i)
					{
						string s = splitPath[i];

						if (s == "..")
						{
							t = t.parent;
							if (t == null) break;
						}
						else if (!string.IsNullOrEmpty(s))
						{
							t = t.FindChild(s);
							if (t == null) break;
						}
					}

					if (t != null)
					{
						if (myType == typeof(GameObject)) return t.gameObject;
						return t.GetComponent(myType);
					}
					else Debug.LogWarning("Hierarchy path not found: " + split[2], go);
				}
			}
		}
		return null;
	}

	// Cache all loaded assets
	static System.Collections.Generic.Dictionary<string, GameObject> mPrefabs =
		new System.Collections.Generic.Dictionary<string, GameObject>();
	static Transform mPrefabRoot = null;

	static GameObject mDummy = null;

	/// <summary>
	/// Dummy object is used as an empty dummy prefab for when TNManager.Create is called without a prefab specified.
	/// </summary>

	static public GameObject GetDummyObject ()
	{
		if (mDummy == null)
		{
			mDummy = new GameObject("Dummy Network Object");
			mDummy.SetActive(false);
			Object.DontDestroyOnLoad(mDummy);
			mDummy.AddComponent<TNObject>();
		}
		return mDummy;
	}

	/// <summary>
	/// Load a game object prefab at the specified path. This is equivalent to Resources.Load, but it will
	/// also consider DataNode-exported binary assets as well, automatically loading them as if they were
	/// regular prefabs.
	/// </summary>

	static public GameObject LoadPrefab (string path)
	{
		if (string.IsNullOrEmpty(path)) return null;
		if (!Application.isPlaying) return Resources.Load(path, typeof(GameObject)) as GameObject;

		GameObject prefab = null;

		if (mPrefabRoot == null)
		{
			GameObject go = new GameObject("Prefabs");
			Object.DontDestroyOnLoad(go);
			mPrefabRoot = go.transform;
			mPrefabs.Clear();
		}

		// Try to get it from cache
		if (mPrefabs.TryGetValue(path, out prefab)) return prefab;

		if (prefab == null)
		{
			// Load it from resources as a Game Object
			prefab = Resources.Load(path, typeof(GameObject)) as GameObject;

			if (prefab == null)
			{
				// Load it from resources as a binary asset
				byte[] bytes = UnityTools.LoadBinary(path);

				if (bytes != null)
				{
					// Parse the DataNode hierarchy
					DataNode data = DataNode.Read(bytes);

					if (data != null)
					{
						// Instantiate and immediately disable the object
						prefab = data.Instantiate();

						if (prefab != null)
						{
							mPrefabs.Add(path, prefab);
							Object.DontDestroyOnLoad(prefab);
							prefab.transform.parent = mPrefabRoot;
							prefab.SetActive(false);
							return prefab;
						}
					}
				}
			}
		}

		if (prefab == null)
		{
#if UNITY_EDITOR
			Debug.LogError("[TNet] Attempting to create a game object that can't be found in the Resources folder: [" + path + "]");
#endif
			prefab = GetDummyObject();
		}

		mPrefabs.Add(path, prefab);
		return prefab;
	}

	/// <summary>
	/// Add a blank child to the specified game object.
	/// </summary>

	static public GameObject AddChild (this GameObject go)
	{
		GameObject inst = new GameObject();
		inst.name = inst.GetInstanceID().ToString();
		Transform t = inst.transform;
		t.parent = go.transform;
		t.localPosition = Vector3.zero;
		t.localRotation = Quaternion.identity;
		t.localScale = Vector3.one;
		return inst;
	}

	/// <summary>
	/// Load an asset from resources and instantiate it as a child of the game object.
	/// </summary>

	static public GameObject AddChild (this GameObject go, string resourceName)
	{
		GameObject prefab = LoadPrefab(resourceName);

		if (prefab != null)
		{
			GameObject inst = prefab.Instantiate();
			Transform t = inst.transform;
			t.parent = go.transform;
			t.localPosition = Vector3.zero;
			t.localRotation = Quaternion.identity;
			t.localScale = Vector3.one;
			inst.SetActive(true);
			return inst;
		}

		Debug.LogError("Unable to load prefab '" + resourceName + "'", go);
		return null;
	}

	/// <summary>
	/// Load an asset from resources and instantiate it as a child of the game object.
	/// </summary>

	static public T AddChild<T> (this GameObject go, string resourceName) where T : Component
	{
		GameObject prefab = LoadPrefab(resourceName);

		if (prefab != null)
		{
			GameObject inst = prefab.Instantiate();
			Transform t = inst.transform;
			t.parent = go.transform;
			t.localPosition = Vector3.zero;
			t.localRotation = Quaternion.identity;
			t.localScale = Vector3.one;
			inst.SetActive(true);
			return inst.GetComponent<T>();
		}

		Debug.LogError("Unable to load prefab '" + resourceName + "'", go);
		return null;
	}

	/// <summary>
	/// Load an asset from resources and instantiate it as a child of the game object.
	/// </summary>

	static public T[] AddChildren<T> (this GameObject go, string resourceName) where T : Component
	{
		GameObject prefab = LoadPrefab(resourceName);

		if (prefab != null)
		{
			GameObject inst = prefab.Instantiate();
			Transform t = inst.transform;
			t.parent = go.transform;
			t.localPosition = Vector3.zero;
			t.localRotation = Quaternion.identity;
			t.localScale = Vector3.one;
			inst.SetActive(true);
			return inst.GetComponentsInChildren<T>();
		}

		Debug.LogError("Unable to load prefab '" + resourceName + "'", go);
		return null;
	}

	/// <summary>
	/// Destroy the specified object -- works both in the Editor and when playing.
	/// </summary>

	static public void Destroy (UnityEngine.Object obj)
	{
		Debug.Log(obj.name, obj);
		if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
		else UnityEngine.Object.DestroyImmediate(obj);
	}

	/// <summary>
	/// Read a PNG file from the specified location. Expects a full path with extension.
	/// </summary>

	static public Texture2D ReadPNG (string path, Texture2D existing = null)
	{
		Texture2D tex = null;
		byte[] bytes = Tools.ReadFile(path);

		if (bytes != null)
		{
			tex = existing ?? new Texture2D(2, 2);

			if (tex.LoadImage(bytes))
			{
				tex.wrapMode = path.Contains("Repeat") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
				tex.filterMode = FilterMode.Trilinear;
				tex.anisoLevel = 4;
				tex.Apply();
				return tex;
			}

			if (existing == null)
			{
				if (Application.isPlaying) Object.Destroy(tex);
				else Object.DestroyImmediate(tex);
			}
		}
		return null;
	}

	/// <summary>
	/// Get an MD5 checksum hash of the specified byte array.
	/// </summary>

	static public string GetMD5Hash (byte[] bytes)
	{
		if (bytes == null || bytes.Length == 0) return "0";
		System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
		byte[] hash = md5.ComputeHash(bytes);
		return System.BitConverter.ToString(hash).Replace("-", "").ToLower();
	}

	static System.Collections.Generic.Dictionary<byte[], AssetBundle> mCachedBundles =
		new System.Collections.Generic.Dictionary<byte[], AssetBundle>();

	/// <summary>
	/// Load an asset bundle, given its bytes. The value gets cached so that it's reused.
	/// </summary>

	static public AssetBundle LoadAssetBundle (byte[] assetBytes)
	{
		AssetBundle ab;

		if (!mCachedBundles.TryGetValue(assetBytes, out ab))
		{
#if UNITY_4_6 || UNITY_4_7 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2
			ab = AssetBundle.CreateFromMemoryImmediate(assetBytes);
#else
			ab = AssetBundle.LoadFromMemory(assetBytes);
#endif
			mCachedBundles[assetBytes] = ab;
		}
		return ab;
	}
}
}
