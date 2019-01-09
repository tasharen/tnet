//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
//-------------------------------------------------

#if !STANDALONE && (UNITY_EDITOR || (!UNITY_FLASH && !NETFX_CORE && !UNITY_WP8 && !UNITY_WP_8_1))
#define REFLECTION_SUPPORT

using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;

namespace TNet
{
	/// <summary>
	/// This class contains DataNode serialization methods for Unity components that make it possible
	/// to serialize behaviours and game objects.
	/// </summary>

	static public class ComponentSerialization
	{
		// For lazy referencing, this allows dummy meshes to be created before their data has been loaded
		[System.NonSerialized] static Dictionary<int, Object> mTempObjects;

		// Temporary references kept while deserializing prefabs. Used to allow ID references to objects and components.
		[System.NonSerialized] static Dictionary<int, Object> mLocalReferences = new Dictionary<int, Object>();

		// Makes it possible to assign object IDs via SetUniqueID() and retrieve then via GetUniqueID().
		[System.NonSerialized] static Dictionary<Object, int> mObjectToID = new Dictionary<Object, int>();

		// Permanent objects (prefabs and resources)
		[System.NonSerialized] static public Dictionary<int, GameObject> referencedPrefabs = new Dictionary<int, GameObject>();
		[System.NonSerialized] static public Dictionary<int, Texture> referencedTextures = new Dictionary<int, Texture>();
		[System.NonSerialized] static public Dictionary<int, Material> referencedMaterials = new Dictionary<int, Material>();
		[System.NonSerialized] static public Dictionary<int, Mesh> referencedMeshes = new Dictionary<int, Mesh>();
		[System.NonSerialized] static public Dictionary<int, AudioClip> referencedClips = new Dictionary<int, AudioClip>();

		static T GetTempObject<T> (int instanceID) where T : Object
		{
			if (mTempObjects != null)
			{
				Object retVal;
				mTempObjects.TryGetValue(instanceID, out retVal);
				return retVal as T;
			}
			return null;
		}

		static void AddTempObject (Object obj, int instanceID)
		{
			if (mTempObjects == null) mTempObjects = new Dictionary<int, Object>();
			mTempObjects[instanceID] = obj;
			obj.SetUniqueID(instanceID);
		}

		static T RemoveTempObject<T> (int instanceID) where T : Object
		{
			if (mTempObjects != null)
			{
				Object retVal;
				if (mTempObjects.TryGetValue(instanceID, out retVal)) mTempObjects.Remove(instanceID);
				return retVal as T;
			}
			return null;
		}

		/// <summary>
		/// Like GetInstanceID(), but allows assignment of this ID using SetUniqueID(). Falls back to GetInstanceID() if an ID hasn't been assigned.
		/// </summary>

		static public int GetUniqueID (this Object obj)
		{
			var id = 0;
			if (!mObjectToID.TryGetValue(obj, out id)) id = obj.GetInstanceID();
			return id;
		}

		/// <summary>
		/// Assign a unique ID retrievable via GetUniqueID().
		/// </summary>

		static public void SetUniqueID (this Object obj, int value) { mObjectToID[obj] = value; }

		/// <summary>
		/// Add a referenced object, making it known to serialization by its ID.
		/// </summary>

		static public void AddReference (Object o)
		{
			if (o == null) return;

			var id = o.GetInstanceID();
			if (o is Transform) id = (o as Transform).gameObject.GetInstanceID();
			if (!mLocalReferences.ContainsKey(id)) mLocalReferences[id] = o;

			if (o is GameObject) AddReference(o as GameObject, id);
			else if (o is Texture) AddReference(o as Texture, id);
			else if (o is Material) AddReference(o as Material, id);
			else if (o is Mesh) AddReference(o as Mesh, id);
			else if (o as AudioClip) AddReference(o as AudioClip, id);
		}

		/// <summary>
		/// Add a referenced object, making it known to serialization by its ID.
		/// </summary>

		static void AddReference (Object o, int id)
		{
			if (o == null) return;
			if (!mLocalReferences.ContainsKey(id)) mLocalReferences[id] = o;

			if (o is GameObject) AddReference(o as GameObject, id);
			else if (o is Texture) AddReference(o as Texture, id);
			else if (o is Material) AddReference(o as Material, id);
			else if (o is Mesh) AddReference(o as Mesh, id);
			else if (o as AudioClip) AddReference(o as AudioClip, id);
		}

		static void AddReference (GameObject o, int id) { if (!referencedPrefabs.ContainsKey(id)) referencedPrefabs[id] = o; }
		static void AddReference (Texture o, int id) { if (!referencedTextures.ContainsKey(id)) referencedTextures[id] = o; }
		static void AddReference (Material o, int id) { if (!referencedMaterials.ContainsKey(id)) referencedMaterials[id] = o; }
		static void AddReference (Mesh o, int id) { if (!referencedMeshes.ContainsKey(id)) referencedMeshes[id] = o; }
		static void AddReference (AudioClip o, int id) { if (!referencedClips.ContainsKey(id)) referencedClips[id] = o; }

		/// <summary>
		/// Retrieve an object by its instance ID. This will only work during serialization operations.
		/// </summary>

		static public Object GetObject (int instanceID)
		{
			Object o;
			mLocalReferences.TryGetValue(instanceID, out o);
			return o;
		}

		/// <summary>
		/// Retrieve an object by its instance ID. This will only work during serialization operations.
		/// </summary>

		static public T GetObject<T> (int instanceID) where T : Component
		{
			Object o;
			mLocalReferences.TryGetValue(instanceID, out o);
			var retVal = o as T;

			if (retVal == null)
			{
				if (o is GameObject) return (o as GameObject).GetComponent(typeof(T)) as T;
				if (o is Component) return (o as Component).GetComponent(typeof(T)) as T;
				return null;
			}
			return retVal;
		}

		/// <summary>
		/// Retrieve an object by its instance ID. This will only work during serialization operations.
		/// </summary>

		static public Object GetObject (int instanceID, System.Type type)
		{
			Object o;
			mLocalReferences.TryGetValue(instanceID, out o);
			if (typeof(Object).IsAssignableFrom(type)) return o;

			if (typeof(Component).IsAssignableFrom(type))
			{
				if (o is GameObject) return (o as GameObject).GetComponent(type);
				if (o is Component) return (o as Component).GetComponent(type);
			}
			return null;
		}

		static public GameObject GetPrefab (int instanceID) { GameObject go; referencedPrefabs.TryGetValue(instanceID, out go); return go; }

		static public Texture GetTexture (int instanceID, bool createIfMissing = true)
		{
			Texture tex;

			if (!referencedTextures.TryGetValue(instanceID, out tex) && createIfMissing)
			{
				tex = GetTempObject<Texture>(instanceID);
				if (tex == null) AddTempObject(tex = new Texture2D(2, 2), instanceID);
			}
			return tex;
		}

		static public Material GetMaterial (int instanceID, bool createIfMissing = true)
		{
			Material val;

			if (!referencedMaterials.TryGetValue(instanceID, out val) && createIfMissing)
			{
				val = GetTempObject<Material>(instanceID);
				if (val == null) AddTempObject(val = new Material(Shader.Find("Diffuse")), instanceID);
			}
			return val;
		}

		static public Mesh GetMesh (int instanceID, bool createIfMissing = true)
		{
			Mesh val;

			if (!referencedMeshes.TryGetValue(instanceID, out val) && createIfMissing)
			{
				val = GetTempObject<Mesh>(instanceID);
				if (val == null) AddTempObject(val = new Mesh(), instanceID);
			}
			return val;
		}

		static public AudioClip GetAudioClip (int instanceID) { AudioClip val; referencedClips.TryGetValue(instanceID, out val); return val; }

		/// <summary>
		/// Clear all referenced resource lists.
		/// </summary>

		static public void ClearReferences ()
		{
			mLocalReferences.Clear();
			mObjectToID.Clear();

			referencedPrefabs.Clear();
			referencedMeshes.Clear();
			referencedMaterials.Clear();
			referencedClips.Clear();
			referencedTextures.Clear();

			if (mTempObjects != null) mTempObjects.Clear();
		}

		/// <summary>
		/// Whether there are shared resources present (does not include prefabs).
		/// </summary>

		static public bool HasReferencedResources () { return referencedMeshes.Count != 0 || referencedTextures.Count != 0 || referencedMaterials.Count != 0 || referencedClips.Count != 0; }

		#region Component Serialization

		// Whether mesh and texture data will be serialized or not. Set automatically. Don't change it.
		static bool mFullSerialization = true;

		/// <summary>
		/// Generic component serialization function. You can add custom serialization
		/// to any component by adding an extension with this signature:
		/// static public void Serialize (this YourComponentType, DataNode);
		/// </summary>

		static public void Serialize (this Component c, DataNode node, System.Type type = null)
		{
			// The 'enabled' flag should only be written down if the behavior is actually disabled
			var b = c as Behaviour;
			if (b != null && !b.enabled) node.AddChild("enabled", b.enabled);

			// Try custom serialization first
			if (c.Invoke("Serialize", node)) return;

			var go = c.gameObject;
			if (type == null) type = c.GetType();
			var mb = c as MonoBehaviour;

			if (mb != null)
			{
				// For MonoBehaviours we want to serialize serializable fields
				var fields = type.GetSerializableFields();

				for (int f = 0; f < fields.size; ++f)
				{
					var field = fields.buffer[f];
					var val = field.GetValue(c);
					if (val == null) continue;

					val = EncodeReference(go, val);
					if (val == null) continue;

					node.AddChild(field.Name, val);
				}
			}
			else
			{
				// Unity components don't have fields, so we should serialize properties instead.
				var props = type.GetSerializableProperties();

				for (int f = 0; f < props.size; ++f)
				{
					var prop = props.buffer[f];

					if (prop.Name == "enabled")
					{
						if (b == null)
						{
							var bprop = prop.GetValue(c, null);
							if (bprop is bool && !(bool)bprop) node.AddChild("enabled", false);
						}
						continue;
					}

					// NOTE: Add any other fields that should not be serialized here
					if (prop.Name == "name" ||
						prop.Name == "tag" ||
						prop.Name == "hideFlags" ||
						prop.Name == "material" ||
						prop.Name == "materials") continue;

					object val = prop.GetValue(c, null);
					if (val == null) continue;

					val = EncodeReference(go, val);
					if (val == null) continue;

					node.AddChild(prop.Name, val);
				}
			}
		}

		/// <summary>
		/// Generic deserialization function. You can create a custom deserialization
		/// for your components by adding an extension method with this signature:
		/// static public void Deserialize (this YourComponentType, DataNode);
		/// </summary>

		static public void Deserialize (this Component c, DataNode node)
		{
			// Try calling the custom function first
			if (c.Invoke("Deserialize", node))
			{
				var b = c as Behaviour;
				if (b != null) b.enabled = node.GetChild<bool>("enabled", b.enabled);
				return;
			}

			var go = c.gameObject;

			// Unity Bug: Trying to set cloth coefficients on a Cloth that sits on a disabled game object fails miserably
			if (c is Cloth && !go.activeInHierarchy)
			{
				// We need to collect all the scripts and disable them in order to ensure that their Awake() won't run
				var temp = go.GetComponentsInChildren<MonoBehaviour>(true);

				for (int i = 0, imax = temp.Length; i < imax; ++i)
				{
					var mb = temp[i];
					if (mb.enabled) mb.enabled = false;
					else temp[i] = null;
				}

				// Remove the game object from its hierarchy
				var t = go.transform;
				var prev = t.parent;
				t.parent = null;

				// Enable the game object
				var state = go.activeSelf;
				if (!state) go.SetActive(true);

				// Now deserialize the Cloth component
				for (int i = 0; i < node.children.size; ++i)
				{
					var child = node.children.buffer[i];
					if (child.value == null) continue;

					var fp = c.GetFieldOrProperty(child.name);
					if (fp != null) fp.SetValue(c, child.value, go);
					else Debug.LogWarning("Unable to find " + c.GetType() + "." + child.value);
				}

				// Restore the game object's previous state and parent
				if (!state) go.SetActive(false);
				t.parent = prev;

				// Restore the states of scripts
				for (int i = 0, imax = temp.Length; i < imax; ++i)
				{
					var mb = temp[i];
					if (mb != null) mb.enabled = true;
				}
			}
			else
			{
				// Fallback -- just set the appropriate fields/properties
				for (int i = 0; i < node.children.size; ++i)
				{
					var child = node.children.buffer[i];
					if (child.value == null) continue;

					// Unity prior to 5.6.5p1 crashed here if this was performed on a disabled game object
					//if (c is Cloth && child.name == "enabled") continue;

					var fp = c.GetFieldOrProperty(child.name);
					if (fp != null) fp.SetValue(c, child.value, go);
					else Debug.LogWarning("Unable to find " + c.GetType() + "." + child.value);
				}
			}
		}

		/// <summary>
		/// Rigidbody class has a lot of properties that don't need to be serialized.
		/// </summary>

		static public void Serialize (this Rigidbody rb, DataNode node)
		{
			node.AddChild("mass", rb.mass);
			node.AddChild("drag", rb.drag);
			node.AddChild("angularDrag", rb.angularDrag);
			node.AddChild("interpolation", rb.interpolation);
			node.AddChild("collisionDetectionMode", rb.collisionDetectionMode);
			node.AddChild("isKinematic", rb.isKinematic);
			node.AddChild("useGravity", rb.useGravity);
		}

		/// <summary>
		/// Camera serialization skips a bunch of values such as "layerCullDistances", "stereoSeparation", and more.
		/// </summary>

		static public void Serialize (this Camera cam, DataNode node)
		{
			node.AddChild("clearFlags", cam.clearFlags);
			node.AddChild("backgroundColor", cam.backgroundColor);
			node.AddChild("cullingMask", cam.cullingMask);
			node.AddChild("orthographic", cam.orthographic);
			node.AddChild("orthographicSize", cam.orthographicSize);
			node.AddChild("fieldOfView", cam.fieldOfView);
			node.AddChild("nearClipPlane", cam.nearClipPlane);
			node.AddChild("farClipPlane", cam.farClipPlane);
			node.AddChild("rect", cam.rect);
			node.AddChild("depth", cam.depth);
			node.AddChild("renderingPath", cam.renderingPath);
			node.AddChild("useOcclusionCulling", cam.useOcclusionCulling);
#if UNITY_4_7 || UNITY_5_3 || UNITY_5_4 || UNITY_5_5
		node.AddChild("hdr", cam.hdr);
#else
			node.AddChild("hdr", cam.allowHDR);
#endif
		}

		/// <summary>
		/// Serialize the specified renderer into its DataNode format.
		/// </summary>

		static void Serialize (this MeshRenderer ren, DataNode root) { SerializeRenderer(ren, root); }

		/// <summary>
		/// Deserialize a previously serialized renderer.
		/// </summary>

		static void Deserialize (this MeshRenderer ren, DataNode data) { Deserialize((Renderer)ren, data); }

		/// <summary>
		/// Serialize the specified renderer into its DataNode format.
		/// </summary>

		static void Serialize (this SkinnedMeshRenderer ren, DataNode root)
		{
			var bones = ren.bones;
			var boneList = new int[bones.Length];
			for (int i = 0; i < bones.Length; ++i) boneList[i] = bones[i].gameObject.GetUniqueID();

			var rb = ren.rootBone;
			if (rb != null) root.AddChild("root", rb.gameObject.GetUniqueID());
			root.AddChild("bones", boneList);

			var sm = ren.sharedMesh;

			if (sm != null)
			{
				var sub = root.AddChild("Mesh", sm.GetUniqueID());
				if (mFullSerialization) sm.Serialize(sub);
			}

			root.AddChild("quality", ren.quality);
			root.AddChild("offscreen", ren.updateWhenOffscreen);
			root.AddChild("center", ren.localBounds.center);
			root.AddChild("size", ren.localBounds.size);

			SerializeRenderer(ren, root);
		}

		/// <summary>
		/// Deserialize a previously serialized renderer.
		/// </summary>

		static void Deserialize (this SkinnedMeshRenderer ren, DataNode data)
		{
			var go = ren.gameObject;
			var boneIDs = data.GetChild<int[]>("bones");

			if (boneIDs != null)
			{
				ren.rootBone = GetObject<Transform>(data.GetChild<int>("root"));
				var bones = new Transform[boneIDs.Length];

				for (int i = 0; i < boneIDs.Length; ++i)
				{
					bones[i] = GetObject<Transform>(boneIDs[i]);
					if (bones[i] == null) Debug.LogWarning("Bone not found: " + boneIDs[i], go);
				}
				ren.bones = bones;
			}
			else
			{
				ren.rootBone = go.StringToReference(data.GetChild<string>("root")) as Transform;
				var boneList = data.GetChild<string[]>("bones");

				if (boneList != null)
				{
					var bones = new Transform[boneList.Length];

					for (int i = 0; i < bones.Length; ++i)
					{
						bones[i] = go.StringToReference(boneList[i]) as Transform;
						if (bones[i] == null) Debug.LogWarning("Bone not found: " + boneList[i], go);
					}
					ren.bones = bones;
				}
			}

			var meshNode = data.GetChild("Mesh");
			if (meshNode != null) ren.sharedMesh = meshNode.DeserializeMesh();

			ren.quality = data.GetChild<SkinQuality>("quality", ren.quality);
			ren.updateWhenOffscreen = data.GetChild<bool>("offscreen", ren.updateWhenOffscreen);

			var center = data.GetChild<Vector3>("center", ren.localBounds.center);
			var size = data.GetChild<Vector3>("size", ren.localBounds.size);
			ren.localBounds = new Bounds(center, size);

			Deserialize((Renderer)ren, data);
		}

		/// <summary>
		/// Serialize the specified renderer into its DataNode format.
		/// </summary>

		static void SerializeRenderer (Renderer ren, DataNode root)
		{
#if UNITY_4_3 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7
			root.AddChild("castShadows", ren.castShadows);
			if (ren.lightProbeAnchor != null) root.AddChild("probeAnchor", ren.lightProbeAnchor.GetUniqueID());
#else
			root.AddChild("receiveShadows", ren.receiveShadows);
			var sm = ren.shadowCastingMode;
			if (sm == UnityEngine.Rendering.ShadowCastingMode.Off) root.AddChild("castShadows", false);
			else if (sm == UnityEngine.Rendering.ShadowCastingMode.On) root.AddChild("castShadows", true);
			else root.AddChild("shadowCasting", ren.shadowCastingMode);
			root.AddChild("rpu", (byte)ren.reflectionProbeUsage);
			if (ren.probeAnchor != null) root.AddChild("probeAnchor", ren.probeAnchor.GetUniqueID());
#endif
#if UNITY_4_7 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2 || UNITY_5_3
			root.AddChild("useLightProbes", ren.useLightProbes);
#else
			root.AddChild("lpu", (byte)ren.lightProbeUsage);
#endif
			var mats = ren.sharedMaterials;
			if (mats == null || mats.Length == 0) return;

			var matNode = root.AddChild("Materials", mats.Length);

			for (int i = 0; i < mats.Length; ++i)
			{
				var mat = mats[i];

				if (mat != null)
				{
					var node = matNode.AddChild("Material", mat.GetUniqueID());
					mat.Serialize(node);
				}
			}
		}

		/// <summary>
		/// Serialize the specified material into its DataNode format.
		/// </summary>

		static public void Serialize (this Material mat, DataNode node) { mat.Serialize(node, true); }

		/// <summary>
		/// Serialize the specified material into its DataNode format.
		/// </summary>

		static public void Serialize (this Material mat, DataNode node, bool serializeTextures)
		{
			if (!mFullSerialization) return;

			node.AddChild("name", mat.name);
			string path = UnityTools.LocateResource(mat);

			if (!string.IsNullOrEmpty(path))
			{
				node.AddChild("path", path);
				return;
			}

			Shader s = mat.shader;

			if (s != null)
			{
				node.AddChild("shader", s.name);
#if UNITY_EDITOR
				int props = UnityEditor.ShaderUtil.GetPropertyCount(s);

				for (int b = 0; b < props; ++b)
				{
					string propName = UnityEditor.ShaderUtil.GetPropertyName(s, b);
					UnityEditor.ShaderUtil.ShaderPropertyType type = UnityEditor.ShaderUtil.GetPropertyType(s, b);

					if (type == UnityEditor.ShaderUtil.ShaderPropertyType.Color)
					{
						node.AddChild(propName, mat.GetColor(propName));
					}
					else if (type == UnityEditor.ShaderUtil.ShaderPropertyType.Vector)
					{
						node.AddChild(propName, mat.GetVector(propName));
					}
					else if (type == UnityEditor.ShaderUtil.ShaderPropertyType.TexEnv)
					{
						var tex = mat.GetTexture(propName);

						if (tex != null)
						{
							var sub = new DataNode(propName, tex.GetUniqueID());
							if (serializeTextures) tex.Serialize(sub);
							sub.AddChild("offset", mat.GetTextureOffset(propName));
							sub.AddChild("scale", mat.GetTextureScale(propName));
							node.children.Add(sub);
						}
					}
					else node.AddChild(propName, mat.GetFloat(propName));
				}
#endif
			}
		}

		/// <summary>
		/// Deserialize a previously serialized renderer.
		/// </summary>

		static public void Deserialize (this Renderer ren, DataNode data)
		{
#if UNITY_4_3 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7
			ren.castShadows = data.GetChild<bool>("castShadows", ren.castShadows);
#else
			DataNode cs = data.GetChild("castShadows");

			if (cs != null)
			{
				ren.shadowCastingMode = cs.Get<bool>() ?
					UnityEngine.Rendering.ShadowCastingMode.On :
					UnityEngine.Rendering.ShadowCastingMode.Off;
			}
			else ren.shadowCastingMode = data.GetChild("shadowCastingMode", ren.shadowCastingMode);

			var rp = data.GetChild("rpu");
			if (rp == null) rp = data.GetChild("reflectionProbes"); // No longer used, but kept for backwards compatibility
			if (rp != null) ren.reflectionProbeUsage = rp.Get(ren.reflectionProbeUsage);
#endif
			ren.receiveShadows = data.GetChild<bool>("receiveShadows", ren.receiveShadows);
#if UNITY_4_7 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2 || UNITY_5_3
			ren.useLightProbes = data.GetChild<bool>("useLightProbes", ren.useLightProbes);
#else
			var lpu = data.GetChild("lpu");
			if (lpu == null) lpu = data.GetChild("lightProbeUsage"); // No longer used, but kept for backwards compatibility

			if (lpu != null)
			{
				ren.lightProbeUsage = lpu.Get(ren.lightProbeUsage);
			}
			else
			{
				// Pre-Unity 5.4 format
				ren.lightProbeUsage = data.GetChild<bool>("useLightProbes", ren.lightProbeUsage != UnityEngine.Rendering.LightProbeUsage.Off) ?
					UnityEngine.Rendering.LightProbeUsage.BlendProbes : UnityEngine.Rendering.LightProbeUsage.Off;
			}
#endif

			DataNode matRoot = data.GetChild("Materials");

			if (matRoot != null && matRoot.children.size > 0)
			{
				Material[] mats = new Material[matRoot.children.size];

				for (int i = 0; i < matRoot.children.size; ++i)
				{
					DataNode matNode = matRoot.children.buffer[i];
					mats[i] = matNode.DeserializeMaterial();
				}
				ren.sharedMaterials = mats;
			}
		}

		/// <summary>
		/// Deserialize a previously serialized material.
		/// </summary>

		static public Material DeserializeMaterial (this DataNode matNode)
		{
			Material mat = null;
			int id = matNode.Get<int>();
			if (referencedMaterials.TryGetValue(id, out mat) && mat != null) return mat;

			// Try to load this material
			string name = matNode.GetChild<string>("name", "Unnamed");
			string path = matNode.GetChild<string>("path");

			if (id == 0)
			{
				id = (path + name).GetHashCode();
				if (referencedMaterials.TryGetValue(id, out mat) && mat != null) return mat;
			}

			if (!string.IsNullOrEmpty(path))
			{
				mat = UnityTools.Load<Material>(path);

				if (mat != null)
				{
					AddReference(mat, id);
					return mat;
				}
			}

			// Material can only be created if there is a shader to work with
			var shaderName = matNode.GetChild<string>("shader");
			Shader shader;

			if (!string.IsNullOrEmpty(shaderName))
			{
				shader = Shader.Find(shaderName);

				if (shader == null)
				{
					Debug.LogWarning("Shader '" + shaderName + "' was not found", mat);
					shader = Shader.Find("Diffuse");
				}
			}
			else
			{
				Debug.LogWarning("Material has no shader, assuming the default one" + "\n" + matNode.ToString() + "\n", mat);
				shader = Shader.Find("Diffuse");
			}

			// Create a new material
			mat = RemoveTempObject<Material>(id);
			if (mat != null) mat.shader = shader;
			else mat = new Material(shader);

			mat.name = name;
			referencedMaterials[id] = mat;

			// Restore material properties
			for (int b = 0; b < matNode.children.size; ++b)
			{
				DataNode prop = matNode.children.buffer[b];
				if (prop.name == "shader") continue;

				if (prop.children.size != 0)
				{
					Texture tex = prop.DeserializeTexture();

					if (tex != null)
					{
						mat.SetTexture(prop.name, tex);
						mat.SetTextureOffset(prop.name, prop.GetChild<Vector2>("offset"));
						mat.SetTextureScale(prop.name, prop.GetChild<Vector2>("scale", Vector2.one));
					}
				}
				else if (prop.value is Vector4)
				{
					mat.SetVector(prop.name, prop.Get<Vector4>());
				}
				else if (prop.value is Color)
				{
					mat.SetColor(prop.name, prop.Get<Color>());
				}
				else if (prop.value is float || prop.value is int)
				{
					mat.SetFloat(prop.name, prop.Get<float>());
				}
			}
			return mat;
		}

		/// <summary>
		/// Serialize the Mesh Filter component.
		/// </summary>

		static public void Serialize (this MeshFilter filter, DataNode data)
		{
			var sm = filter.sharedMesh;

			if (sm != null)
			{
				var child = data.AddChild("Mesh", sm.GetUniqueID());
				sm.Serialize(child);
			}
		}

		/// <summary>
		/// Restore a previously serialized Mesh Filter component.
		/// </summary>

		static public void Deserialize (this MeshFilter filter, DataNode data)
		{
			var mesh = data.GetChild("Mesh");
			if (mesh != null) filter.sharedMesh = mesh.DeserializeMesh();
		}

		#endregion

		static void Add (DataNode node, string name, System.Array obj)
		{
			if (obj != null && obj.Length > 0) node.AddChild(name, obj);
		}

		/// <summary>
		/// Serialize the entire mesh into the specified DataNode.
		/// </summary>

		static public void Serialize (this Mesh mesh, DataNode node)
		{
			if (!mFullSerialization) return;

			node.AddChild("name", mesh.name);
			string path = UnityTools.LocateResource(mesh);

			if (!string.IsNullOrEmpty(path))
			{
				node.AddChild("path", path);
				return;
			}

			Add(node, "vertices", mesh.vertices);
			Add(node, "normals", mesh.normals);
			Add(node, "uv1", mesh.uv);
			Add(node, "uv2", mesh.uv2);
			Add(node, "tangents", mesh.tangents);
			Add(node, "colors", mesh.colors32);
			Add(node, "weights", mesh.boneWeights);
			Add(node, "poses", mesh.bindposes);
			Add(node, "triangles", mesh.triangles);
		}

		/// <summary>
		/// Serialize the AudioClip into the specified DataNode.
		/// </summary>

		static public void Serialize (this AudioClip clip, DataNode node)
		{
			if (!mFullSerialization) return;

			node.AddChild("name", clip.name);
			if (clip.length > 30f) node.AddChild("stream", true);

			string path = UnityTools.LocateResource(clip);

			if (!string.IsNullOrEmpty(path))
			{
				node.AddChild("path", path);
				return;
			}

			var bytes = clip.GetBytes();
			if (bytes != null) node.AddChild("bytes", bytes);
		}

		/// <summary>
		/// Load a previously serialized AudioClip from the DataNode.
		/// </summary>

		static public AudioClip DeserializeClip (this DataNode node)
		{
			AudioClip clip = null;

			int id = node.Get<int>();
			if (id != 0 && referencedClips.TryGetValue(id, out clip)) return clip;

			var name = node.GetChild<string>("name");
			var path = node.GetChild<string>("path");

			if (id == 0)
			{
				id = (path + name).GetHashCode();
				if (referencedClips.TryGetValue(id, out clip)) return clip;
			}

			if (!string.IsNullOrEmpty(path))
			{
				clip = UnityTools.Load<AudioClip>(path, name);
				if (clip == null) Debug.LogWarning("Unable to find AudioClip '" + name + "' in " + path);
			}
			else
			{
				var bytes = node.GetChild<byte[]>("bytes");
				if (bytes != null) clip = UnityTools.CreateAudioClip(bytes, name, node.GetChild<bool>("stream"));
			}

			AddReference(clip, id);
			return clip;
		}

		/// <summary>
		/// Set the mesh from the specified DataNode.
		/// </summary>

		static public Mesh DeserializeMesh (this DataNode node)
		{
			Mesh mesh = null;
			int id = node.Get<int>();
			if (id != 0 && referencedMeshes.TryGetValue(id, out mesh)) return mesh;

			var name = node.GetChild<string>("name");
			var path = node.GetChild<string>("path");

			if (id == 0)
			{
				id = (path + name).GetHashCode();
				if (referencedMeshes.TryGetValue(id, out mesh)) return mesh;
			}

			if (!string.IsNullOrEmpty(path))
			{
				mesh = UnityTools.Load<Mesh>(path, name);
				if (mesh == null) Debug.LogWarning("Unable to find mesh '" + name + "' in " + path);
			}
			else
			{
				mesh = RemoveTempObject<Mesh>(id);
				if (mesh == null) mesh = new Mesh();
				mesh.name = name;

				var verts = node.GetChild<Vector3[]>("vertices");
				if (verts != null) mesh.vertices = verts;

				var normals = node.GetChild<Vector3[]>("normals");
				if (normals != null) mesh.normals = normals;

				var uv1 = node.GetChild<Vector2[]>("uv1");
				if (uv1 != null) mesh.uv = uv1;

				var uv2 = node.GetChild<Vector2[]>("uv2");
				if (uv2 != null) mesh.uv2 = uv2;

				var tangents = node.GetChild<Vector4[]>("tangents");
				if (tangents != null) mesh.tangents = tangents;

				var colors = node.GetChild<Color32[]>("colors");
				if (colors != null) mesh.colors32 = colors;

				var weights = node.GetChild<BoneWeight[]>("weights");
				if (weights != null) mesh.boneWeights = weights;

				var poses = node.GetChild<Matrix4x4[]>("poses");
				if (poses != null) mesh.bindposes = poses;

				var triangles = node.GetChild<int[]>("triangles");
				if (triangles != null) mesh.triangles = triangles;

				mesh.RecalculateBounds();
			}

			AddReference(mesh, id);
			return mesh;
		}

		/// <summary>
		/// Serialize the entire texture into the specified DataNode.
		/// </summary>

		static public void Serialize (this Texture tex, DataNode node)
		{
			if (!mFullSerialization) return;

			node.AddChild("name", tex.name);
			var path = UnityTools.LocateResource(tex);

			if (!string.IsNullOrEmpty(path))
			{
				node.AddChild("path", path);
				return;
			}

			if (tex is Texture2D)
			{
				var t2 = tex as Texture2D;

				node.AddChild("width", (ushort)t2.width);
				node.AddChild("height", (ushort)t2.height);
				node.AddChild("filter", (byte)t2.filterMode);
				node.AddChild("wrap", (byte)t2.wrapMode);
				node.AddChild("af", (byte)t2.anisoLevel);
				node.AddChild("format", (byte)t2.format);

				var linear = t2.IsLinear();
				if (linear) node.AddChild("linear", true);
				if (t2.mipmapCount < 2) node.AddChild("mipmap", false);

				var bytes = t2.GetRawTextureData();
				/*var comp = LZMA.Compress(bytes);
				if (comp != null) node.AddChild("lzma", comp);
				else*/ node.AddChild("bytes", bytes);
				return;
			}

			Debug.LogWarning("Unable to save a reference to texture '" + tex.name + "' because it's not in the Resources folder.", tex);
		}

		/// <summary>
		/// Deserialize the texture that was previously serialized into the DataNode format.
		/// </summary>

		static public Texture DeserializeTexture (this DataNode node)
		{
			// First try the cache
			Texture tex = null;
			int id = node.Get<int>();
			if (id != 0 && referencedTextures.TryGetValue(id, out tex) && tex != null) return tex;

			// If the texture's ID is unknown, make a dummy one and try going through cache again
			var name = node.GetChild("name", "Unnamed");
			var path = node.GetChild<string>("path");

			if (id == 0)
			{
				id = (path + name).GetHashCode();
				if (referencedTextures.TryGetValue(id, out tex) && tex != null) return tex;
			}

			// Next try to load the texture
			if (!string.IsNullOrEmpty(path))
			{
				tex = UnityTools.Load<Texture>(path);

				if (tex != null)
				{
					AddReference(tex, id);
					return tex;
				}
			}

			var lzma = node.GetChild<byte[]>("lzma");
			var bytes = (lzma != null) ? LZMA.Decompress(lzma) : node.GetChild<byte[]>("bytes");
			var t2 = RemoveTempObject<Texture2D>(id);

			if (bytes != null)
			{
				var fmtNode = node.GetChild("format");

				if (fmtNode != null)
				{
					var format = (TextureFormat)fmtNode.Get((byte)TextureFormat.ARGB32);
					var width = node.GetChild<int>("width");
					var height = node.GetChild<int>("height");
					var linear = node.GetChild<bool>("linear", false);
					var mipmap = node.GetChild<bool>("mipmap", true);

					if (width * height > 0)
					{
						if (t2 == null) t2 = new Texture2D(width, height, format, mipmap, linear);
						else t2.Resize(width, height, format, mipmap);

						t2.name = name;
						t2.LoadRawTextureData(bytes);
						t2.filterMode = (FilterMode)node.GetChild("filter", (byte)t2.filterMode);
						t2.wrapMode = (TextureWrapMode)node.GetChild("wrap", (byte)t2.wrapMode);
						t2.anisoLevel = node.GetChild("af", t2.anisoLevel);
						t2.Apply();
						tex = t2;
					}
				}
				else
				{
					// No format specified (old style serialization)
					if (t2 == null) t2 = new Texture2D(2, 2);
					t2.name = name;
					t2.LoadImage(bytes);
					t2.filterMode = (FilterMode)node.GetChild("filter", (byte)t2.filterMode);
					t2.wrapMode = (TextureWrapMode)node.GetChild("wrap", (byte)t2.wrapMode);
					t2.anisoLevel = node.GetChild("af", t2.anisoLevel);
					t2.Apply();
					tex = t2;
				}
			}

			if (tex == null)
			{
				if (t2 == null) t2 = new Texture2D(2, 2);
#if UNITY_EDITOR
				Debug.LogWarning("Creating a dummy texture: " + name, t2);
#endif
				t2.name = name;
				t2.SetPixels(new Color[] { Color.clear, Color.clear, Color.clear, Color.clear });
				t2.Apply();
				tex = t2;
			}

			// Add it to cache
			referencedTextures[id] = tex;
			return tex;
		}

		/// <summary>
		/// Particle system renderer serialization support.
		/// </summary>

		static public void Serialize (this ParticleSystemRenderer ren, DataNode node)
		{
			node.AddChild("rm", (byte)ren.renderMode);
			node.AddChild("al", (byte)ren.alignment);
			node.AddChild("sm", (byte)ren.sortMode);
			if (ren.lengthScale != 0f) node.AddChild("ls", ren.lengthScale);
			if (ren.velocityScale != 0f) node.AddChild("vs", ren.velocityScale);
			node.AddChild("cvs", ren.cameraVelocityScale);
			node.AddChild("nd", ren.normalDirection);
			node.AddChild("pv", ren.pivot);
			if (ren.sortingFudge != 0f) node.AddChild("sf", ren.sortingFudge);
			node.AddChild("size", new Vector2(ren.minParticleSize, ren.maxParticleSize));
			if (ren.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) node.AddChild("scm", (byte)ren.shadowCastingMode);
			if (ren.receiveShadows) node.AddChild("rcv", ren.receiveShadows);
			node.AddChild("mats", EncodeReference(ren.gameObject, ren.sharedMaterials));
			if (ren.lightProbeUsage != UnityEngine.Rendering.LightProbeUsage.Off) node.AddChild("lpu", (byte)ren.lightProbeUsage);
			if (ren.reflectionProbeUsage != UnityEngine.Rendering.ReflectionProbeUsage.Off) node.AddChild("rpu", (byte)ren.reflectionProbeUsage);
			if (ren.sortingLayerName != "Default") node.AddChild("sl", ren.sortingLayerName);
			if (ren.sortingOrder != 0) node.AddChild("so", ren.sortingOrder);
			if (ren.sortingLayerID != 0) node.AddChild("sd", ren.sortingLayerID);
		}

		/// <summary>
		/// Particle system renderer serialization support.
		/// </summary>

		static public void Deserialize (this ParticleSystemRenderer ren, DataNode node)
		{
			ren.renderMode = node.GetChild<ParticleSystemRenderMode>("rm");
			ren.alignment = node.GetChild<ParticleSystemRenderSpace>("al");
			ren.renderMode = node.GetChild<ParticleSystemRenderMode>("rm");
			ren.lengthScale = node.GetChild<float>("ls");
			ren.velocityScale = node.GetChild<float>("vs");
			ren.cameraVelocityScale = node.GetChild<float>("cvs");
			ren.normalDirection = node.GetChild<float>("nd");
			ren.pivot = node.GetChild<Vector3>("pv");
			ren.sortingFudge = node.GetChild<float>("sf");

			var size = node.GetChild("size", new Vector2(0f, 1f));
			ren.minParticleSize = size.x;
			ren.maxParticleSize = size.y;

			ren.shadowCastingMode = node.GetChild<UnityEngine.Rendering.ShadowCastingMode>("scm");
			ren.receiveShadows = node.GetChild<bool>("rcv");
			ren.sharedMaterials = node.GetChild<Material[]>("mats");
			ren.lightProbeUsage = node.GetChild<UnityEngine.Rendering.LightProbeUsage>("lpu");
			ren.reflectionProbeUsage = node.GetChild<UnityEngine.Rendering.ReflectionProbeUsage>("rpu");
			ren.sortingLayerName = node.GetChild("sl", "Default");
			ren.sortingOrder = node.GetChild<int>("so");
			ren.sortingLayerID = node.GetChild<int>("sd");
		}

		/// <summary>
		/// Particle system serialization support.
		/// </summary>

		static public void Serialize (this ParticleSystem sys, DataNode node)
		{
			var props = sys.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

			for (int i = 0, imax = props.Length; i < imax; ++i)
			{
				var propInfo = props[i];
				if (!propInfo.CanRead) continue;
				if (propInfo.IsDefined(typeof(System.ObsoleteAttribute), true)) continue;

				var type = propInfo.PropertyType;
				var name = propInfo.Name;
				var obj = sys.GetFieldOrPropertyValue(name);
				if (obj == null) continue;

				if (!propInfo.CanWrite)
				{
					var prefix = Serialization.GetPrefix(type);
					if (!type.IsStruct() || (prefix < 254 && prefix != 14)) continue;
					var et = type.GetProperty("enabled");
					if (et != null && !(bool)et.GetValue(obj, null)) continue;

					var child = new DataNode(name);
					child.AddAllFields(obj);

					// Particle System modules have all these extra properties that point to the same data...
					for (int b = 0; b < child.children.size; ++b)
					{
						var c = child.children.buffer[b];

						if (c.name.EndsWith("Multiplier") && child.GetChild(c.name.Substring(0, c.name.Length - "Multiplier".Length)) != null)
						{
							child.children.RemoveAt(b--);
						}
						else if (c.value is Component || c.value is GameObject || c.value is Shader)
						{
							/*c.value = UnityTools.ReferenceToString(sys.gameObject, c.value as Object);
							if (c.value == null)*/ c.value = (c.value as Object).GetUniqueID();
						}
					}

					node.children.Add(child);
				}
				else node.AddChild(name, obj);
			}
		}

		/// <summary>
		/// Particle system serialization support.
		/// </summary>

		static public void Deserialize (this ParticleSystem sys, DataNode root)
		{
			var go = sys.gameObject;

			// Forcing these to be off unless actually turned on
			var em = sys.emission;
			var sp = sys.shape;
			em.enabled = false;
			sp.enabled = false;

			foreach (var node in root.children)
			{
				var propInfo = sys.GetFieldOrProperty(node.name);
				if (propInfo == null) continue;

				var propType = propInfo.type;

				if (!propInfo.canWrite && propType.IsStruct())
				{
					var val = propInfo.GetValue(sys);
					if (val != null) foreach (var child in node.children) val.SetFieldOrPropertyValue(child.name, child.value, go);
				}
				else propInfo.SetValue(sys, Serialization.ConvertObject(node.value, propType, go));
			}
		}

		static public void Serialize (this ParticleSystem.MinMaxCurve curve, DataNode node)
		{
			var mode = curve.mode;

			if (mode == ParticleSystemCurveMode.Curve)
			{
				var anim = curve.curve;
				if (anim != null) node.AddChild("curve", anim);
				node.AddChild("mult", curve.curveMultiplier);
			}
			else if (mode == ParticleSystemCurveMode.TwoCurves)
			{
				var anim = curve.curveMin;
				if (anim != null) node.AddChild("min", anim);

				anim = curve.curveMax;
				if (anim != null) node.AddChild("max", anim);
				node.AddChild("mult", curve.curveMultiplier);
			}
			else if (mode == ParticleSystemCurveMode.TwoConstants)
			{
				node.AddChild("range", new Vector2(curve.constantMin, curve.constantMax));
			}
			else node.AddChild("value", curve.constant);
		}

		static public void Deserialize (this ParticleSystem.MinMaxCurve curve, DataNode node)
		{
			var child = node.GetChild("value");

			if (child != null)
			{
				curve.mode = ParticleSystemCurveMode.Constant;
				curve.constant = child.Get<float>();
				TypeExtensions.invokedObject = curve;
				return;
			}

			child = node.GetChild("range");

			if (child != null)
			{
				curve.mode = ParticleSystemCurveMode.TwoConstants;
				var v = child.Get<Vector2>();
				curve.constantMin = v.x;
				curve.constantMax = v.y;
				TypeExtensions.invokedObject = curve;
				return;
			}

			child = node.GetChild("curve");

			if (child != null)
			{
				curve.mode = ParticleSystemCurveMode.Curve;
				curve.curve = child.Get<AnimationCurve>();
				curve.curveMultiplier = node.GetChild("mult", curve.curveMultiplier);
				TypeExtensions.invokedObject = curve;
				return;
			}

			var min = node.GetChild("min");
			var max = node.GetChild("max");

			if (min != null && max != null)
			{
				curve.mode = ParticleSystemCurveMode.TwoCurves;
				curve.curveMin = min.Get<AnimationCurve>();
				curve.curveMax = max.Get<AnimationCurve>();
				curve.curveMultiplier = node.GetChild("mult", curve.curveMultiplier);
				TypeExtensions.invokedObject = curve;
			}
		}

#if UNITY_EDITOR
		[System.NonSerialized] static bool mCanceled = false;
#endif

		/// <summary>
		/// Collect all prefabs that have been referenced by this object or one of its references.
		/// </summary>

		static public Dictionary<int, GameObject> CollectReferencedPrefabs (this GameObject go, bool addSelf = false)
		{
			var id = go.GetUniqueID();
			if (referencedPrefabs.ContainsKey(id)) return referencedPrefabs;

			if (addSelf) referencedPrefabs[id] = go;

			var mbs = go.GetComponentsInChildren<MonoBehaviour>(true);

			foreach (var mb in mbs)
			{
				if (!mb)
				{
					Debug.LogWarning("Invalid MonoBehaviour on " + UnityTools.GetHierarchy(go.transform), go);
					continue;
				}

				var type = mb.GetType();
				var fields = type.GetSerializableFields();

				for (int f = 0; f < fields.size; ++f)
				{
					var field = fields.buffer[f];
					var ft = field.FieldType;

					if (ft == typeof(GameObject))
					{
						var val = field.GetValue(mb) as GameObject;

						if (val != null)
						{
							id = val.GetUniqueID();

							// We want to include all local prefabs that have been referenced via scripts and happen to lie outside the Resources folder
							if (!referencedPrefabs.ContainsKey(id)
#if UNITY_EDITOR
								&& UnityEditor.PrefabUtility.GetPrefabType(mb) == UnityEditor.PrefabType.Prefab
#endif
								)
							{
								var prefab = UnityTools.LocateResource(go, false);
								if (prefab != null) val.CollectReferencedPrefabs(true);
							}
						}
					}
				}
			}
			return referencedPrefabs;
		}

		/// <summary>
		/// Determine the game object's shared resources and cache them in the local list for later usage.
		/// </summary>

		static public void CollectReferencedResources (this GameObject go, bool includeMaterials = true)
		{
			var filters = go.GetComponentsInChildren<MeshFilter>(true);
			var rens = go.GetComponentsInChildren<MeshRenderer>(true);
			var sks = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
			var mbs = go.GetComponentsInChildren<MonoBehaviour>(true);
			var aud = go.GetComponentsInChildren<AudioSource>(true);
			var cols = go.GetComponentsInChildren<MeshCollider>(true);
			var psrs = go.GetComponentsInChildren<ParticleSystemRenderer>(true);

			foreach (var f in filters) AddReference(f.sharedMesh);

			foreach (var sk in sks)
			{
				AddReference(sk.sharedMesh);

				if (includeMaterials)
				{
					var mats = sk.sharedMaterials;
					foreach (Material mt in mats) AddReference(mt);
				}
			}

			if (includeMaterials)
			{
				foreach (var r in rens)
				{
					var mats = r.sharedMaterials;
					foreach (Material m in mats) AddReference(m);
				}

				foreach (var s in psrs)
				{
					var mats = s.sharedMaterials;
					foreach (Material m in mats) AddReference(m);
				}
			}

			foreach (var a in aud) AddReference(a.clip);
			foreach (var m in cols) AddReference(m.sharedMesh);
			foreach (var mb in mbs) { if (mb) CollectReferencedResources(mb, mb.GetType(), includeMaterials); }
		}

		/// <summary>
		/// Determine the object's shared resources and cache them in the local list for later usage.
		/// </summary>

		static void CollectReferencedResources (object obj, System.Type type, bool includeMaterials)
		{
			var fields = type.GetSerializableFields();

			for (int f = 0; f < fields.size; ++f)
			{
				var field = fields.buffer[f];
				var ft = field.FieldType;

				if (ft == typeof(Material))
				{
					if (includeMaterials) AddReference(field.GetValue(obj) as Material);
				}
				else if (ft == typeof(AudioClip))
				{
					AddReference(field.GetValue(obj) as AudioClip);
				}
				else if (ft == typeof(Texture2D))
				{
					AddReference(field.GetValue(obj) as Texture2D);
				}
				else if (ft == typeof(Mesh))
				{
					AddReference(field.GetValue(obj) as Mesh);
				}
#if W2
				else if (ft == typeof(GameSound))
				{
					var val = field.GetValue(obj) as GameSound;
					if (val != null) AddReference(val.clip);
				}
#endif
				else if (ft.IsArray)
				{
					var elemType = ft.GetElementType();
					var arr = field.GetValue(obj) as IList;
					if (arr != null) foreach (var m in arr) CollectReferencedResources(m, elemType, includeMaterials);
				}
				else if (ft.IsGenericType)
				{
					var elemType = ft.GetGenericArgument();
					var arr = field.GetValue(obj) as IList;
					if (arr != null) foreach (var m in arr) CollectReferencedResources(m, elemType, includeMaterials);
				}
				else if (ft.IsClass)
				{
					var val = field.GetValue(obj);
					if (val != null) CollectReferencedResources(val, ft, includeMaterials);
				}
			}
		}

		/// <summary>
		/// Save all previously collected shared resources in the specified DataNode.
		/// </summary>

		static public void SerializeReferencedResources (DataNode root, bool progressBar = false)
		{
			mFullSerialization = true;

			var node = root.GetChild("Resources", true);
			var size = referencedTextures.Count;
			var i = 0;

#if UNITY_EDITOR
			foreach (var pair in referencedMaterials)
			{
				var mat = pair.Value;

				var s = mat.shader;
				if (s == null) continue;

				var matPath = UnityTools.LocateResource(mat);
				if (!string.IsNullOrEmpty(matPath)) continue;

				int props = UnityEditor.ShaderUtil.GetPropertyCount(s);

				for (int b = 0; b < props; ++b)
				{
					var propName = UnityEditor.ShaderUtil.GetPropertyName(s, b);
					var type = UnityEditor.ShaderUtil.GetPropertyType(s, b);
					if (type != UnityEditor.ShaderUtil.ShaderPropertyType.TexEnv) continue;
					var tex = mat.GetTexture(propName);
					if (tex != null) referencedTextures[tex.GetUniqueID()] = tex;
				}
			}
#endif

			foreach (var pair in referencedTextures)
			{
				++i;
				var tex = pair.Value;
#if UNITY_EDITOR
				if (progressBar && UnityEditor.EditorUtility.DisplayCancelableProgressBar("Working", "Exporting texture " + tex.name, i / (float)size))
				{
					mCanceled = true;
					return;
				}
#endif
				var child = node.AddChild("Texture", tex.GetUniqueID());
				tex.Serialize(child);
#if UNITY_EDITOR
				if (progressBar)
				{
					var temp = child.GetChild<byte[]>("bytes");
					if (temp != null) Debug.Log(tex.name + " (Texture)\n" + temp.Length.ToString("N0") + " bytes", tex);
				}
#endif
			}

			size = referencedMaterials.Count;
			i = 0;

			foreach (var pair in referencedMaterials)
			{
				++i;
				var mat = pair.Value;
				mat.Serialize(node.AddChild("Material", mat.GetUniqueID()), false);
			}

			size = referencedMeshes.Count;
			i = 0;

			foreach (var pair in referencedMeshes)
			{
				++i;
				var mesh = pair.Value;
#if UNITY_EDITOR
				if (progressBar && UnityEditor.EditorUtility.DisplayCancelableProgressBar("Working", "Exporting mesh " + mesh.name, i / (float)size))
				{
					mCanceled = true;
					return;
				}
#endif
				mesh.Serialize(node.AddChild("Mesh", mesh.GetUniqueID()));
			}

			size = referencedClips.Count;
			i = 0;

			foreach (var pair in referencedClips)
			{
				++i;
				var clip = pair.Value;
#if UNITY_EDITOR
				if (progressBar && UnityEditor.EditorUtility.DisplayCancelableProgressBar("Working", "Exporting audio " + clip.name, i / (float)size))
				{
					mCanceled = true;
					return;
				}
#endif
				var child = node.AddChild("Clip", clip.GetUniqueID());
				clip.Serialize(child);
#if UNITY_EDITOR
				if (progressBar)
				{
					var temp = child.GetChild<byte[]>("bytes");
					if (temp != null) Debug.Log(clip.name + " (AudioClip)\n" + temp.Length.ToString("N0") + " bytes", clip);
				}
#endif
			}
		}

		/// <summary>
		/// Serialize all of the referenced prefabs into the chosen node.
		/// </summary>

		static public void SerializeReferencedPrefabs (DataNode data, bool progressBar = true)
		{
			if (referencedPrefabs.Count == 0) return;

			var prefabs = data.GetChild("Prefabs", true);

			foreach (var pair in referencedPrefabs)
			{
				var go = pair.Value;
				var node = go.Serialize(true, false, progressBar);
				if (node != null) prefabs.children.Add(node);
			}
		}

		static public DataNode SerializeBundle ()
		{
			var data = new DataNode();
			SerializeReferencedResources(data, true);
			SerializeReferencedPrefabs(data, true);
			return data;
		}

		public delegate void OnPrefabDeserialize (int id, GameObject go);

		/// <summary>
		/// Whether this DataNode object can be instantiated as a game object.
		/// </summary>

		static public bool CanBeInstantiated (this DataNode node) { return node.GetChild("position") != null; }

		/// <summary>
		/// Example of the bundle being deserialized. Ideally this would be handled somewhere else, such as the prefab manager.
		/// </summary>

		static public void DeserializeBundle (this DataNode root, OnPrefabDeserialize onDeserialize = null)
		{
			var resNode = root.GetChild("Resources");

			if (resNode != null)
			{
				for (int i = 0; i < resNode.children.size; ++i)
				{
					var child = resNode.children.buffer[i];
					if (child.name == "Texture") child.DeserializeTexture();
					else if (child.name == "Material") child.DeserializeMaterial();
					else if (child.name == "Mesh") child.DeserializeMesh();
					else if (child.name == "Clip") child.DeserializeClip();
				}
			}

			var prefabs = root.GetChild("Prefabs");

			if (prefabs != null)
			{
				// First create all prefab game objects and add them to the list of known references
				for (int i = 0; i < prefabs.children.size; ++i)
				{
					var child = prefabs.children.buffer[i];
					var id = child.Get<int>();
					if (referencedPrefabs.ContainsKey(id)) continue;
					AddReference(new GameObject(child.name), id);
				}

				// Next run through all the newly created prefabs and actually deserialize them
				for (int i = 0; i < prefabs.children.size; ++i)
				{
					var child = prefabs.children.buffer[i];
					var id = child.Get<int>();
					var inst = referencedPrefabs[id];

					if (Application.isPlaying)
					{
						inst.SetActive(false);
						inst.hideFlags = HideFlags.DontSave;
						Object.DontDestroyOnLoad(inst);
						child.Instantiate(inst, false);
					}
					else child.Instantiate(inst, true);

					if (onDeserialize != null) onDeserialize(id, inst);
				}
			}
		}

		/// <summary>
		/// Serialize this game object into a DataNode.
		/// Note that the prefab references can only be resolved if serialized from within the Unity Editor.
		/// You can instantiate this game object directly from DataNode format by using DataNode.Instantiate().
		/// Ideal usage: save a game object hierarchy into a file. Serializing a game object will also serialize its
		/// mesh data, making it possible to export entire 3D models. Any references to prefabs or materials located
		/// in the Resources folder will be kept as references and their hierarchy won't be serialized.
		/// </summary>

		static public DataNode Serialize (this GameObject go, bool fullHierarchy = true, bool isRootNode = true, bool progressBar = false)
		{
			var root = new DataNode(go.name, go.GetUniqueID());

#if UNITY_EDITOR
			if (isRootNode) mCanceled = false;
			if (progressBar && UnityEditor.EditorUtility.DisplayCancelableProgressBar("Working", "Creating a DataNode...", 0f)) return null;
#endif
			// Save a reference to a prefab, if there is one
			var prefab = UnityTools.LocateResource(go, !isRootNode);
			if (!string.IsNullOrEmpty(prefab)) root.AddChild("prefab", prefab);

			// Save the transform and the object's layer
			var trans = go.transform;
			root.AddChild("position", trans.localPosition);
			root.AddChild("rotation", trans.localEulerAngles);
			root.AddChild("scale", trans.localScale);

			if (!go.activeSelf) root.AddChild("active", false);

			int layer = go.layer;
			if (layer != 0) root.AddChild("layer", go.layer);

			// If this was a prefab instance, don't do anything else
			if (!string.IsNullOrEmpty(prefab)) return root;

			// Collect all meshes
			if (isRootNode)
			{
#if UNITY_EDITOR
				if (progressBar && UnityEditor.EditorUtility.DisplayCancelableProgressBar("Working", "Exporting shared resources...", 0f))
				{
					UnityEditor.EditorUtility.ClearProgressBar();
					return null;
				}
#endif
				ClearReferences();
				CollectReferencedResources(go);

				if (HasReferencedResources())
				{
					SerializeReferencedResources(root, progressBar);
#if UNITY_EDITOR
					if (mCanceled) return null;
#endif
				}
			}

#if UNITY_EDITOR
			if (progressBar && UnityEditor.EditorUtility.DisplayCancelableProgressBar("Working", "Exporting hierarchy", 1f))
			{
				UnityEditor.EditorUtility.ClearProgressBar();
				return null;
			}
#endif
			mFullSerialization = false;
			var comps = go.GetComponents<Component>();
			DataNode compRoot = null;

			for (int i = 0, imax = comps.Length; i < imax; ++i)
			{
				var c = comps[i];
				var type = c.GetType();
				if (type == typeof(Transform)) continue;
				if (compRoot == null) compRoot = root.AddChild("Components");
				var child = compRoot.AddChild(Serialization.TypeToName(type), c.GetUniqueID());
				c.Serialize(child, type);
			}

			if (fullHierarchy && trans.childCount > 0)
			{
				var children = root.AddChild("Children");

				for (int i = 0, imax = trans.childCount; i < imax; ++i)
				{
					var child = trans.GetChild(i).gameObject;
					var c = child.Serialize(true, false);
					if (c != null) children.children.Add(c);
				}
			}

			mFullSerialization = true;

#if UNITY_EDITOR
			if (progressBar) UnityEditor.EditorUtility.ClearProgressBar();
#endif
			return root;
		}

		/// <summary>
		/// Used to convert object references to strings or instance IDs. Supports arrays of objects as well.
		/// </summary>

		static public object EncodeReference (GameObject go, object val)
		{
			if (val == null) return null;

			if (val is Object)
			{
				var obj = val as Object;
				if (obj == null) return null;

				if (val is Transform) return (val as Transform).gameObject.GetUniqueID();
				else if (obj is Texture) { if (referencedTextures.ContainsValue(obj as Texture)) return obj.GetUniqueID(); }
				else if (obj is Material) { if (referencedMaterials.ContainsValue(obj as Material)) return obj.GetUniqueID(); }
				else if (obj is Mesh) { if (referencedMeshes.ContainsValue(obj as Mesh)) return obj.GetUniqueID(); }
				else if (obj is AudioClip) { if (referencedClips.ContainsValue(obj as AudioClip)) return obj.GetUniqueID(); }
				else if (obj is GameObject) { if (referencedPrefabs.ContainsValue(obj as GameObject)) return obj.GetUniqueID(); }

				if (go != null)
				{
					var ret = go.ReferenceToString(obj);
					if (ret != null) return ret;
				}
				return obj.GetUniqueID();
			}
			else if (val is IList)
			{
				var list = val as IList;
				if (list.Count == 0) return null;
				var t = list.GetType();
				var elemType = t.GetElementType();
				if (elemType == null) elemType = t.GetGenericArgument();

				if (typeof(Object).IsAssignableFrom(elemType))
				{
					// First try to save references by their instance IDs in an integer-based array
					var intList = new List<int>();

					for (int d = 0, dmax = list.Count; d < dmax; ++d)
					{
						var o = list[d] as Object;
						if (!o) continue;
						var r = EncodeReference(go, o);
						if (r is int) intList.Add(r);
						else { intList.Clear(); break; }
					}

					if (intList.size != 0) return intList;

					// Failing that, save them as string-based references
					var strList = new List<string>();

					for (int d = 0, dmax = list.Count; d < dmax; ++d)
					{
						var o = list[d] as Object;
						if (!o) continue;
						var r = EncodeReference(go, o);
						var s = (r is int) ? ((int)r).ToString() : r as string;
						if (!string.IsNullOrEmpty(s)) strList.Add(s);
					}
					return strList;
				}
			}
			return val;
		}

		struct SerializationEntry
		{
			public Component comp;
			public DataNode node;
		}

		static List<SerializationEntry> mSerList = new List<SerializationEntry>();

		/// <summary>
		/// Deserialize a previously serialized game object.
		/// </summary>

		static public void Deserialize (this GameObject go, DataNode root, bool includeChildren = true)
		{
			var resNode = root.GetChild("Resources");

			if (resNode != null)
			{
				for (int i = 0; i < resNode.children.size; ++i)
				{
					var child = resNode.children.buffer[i];
					if (child.name == "Texture") child.DeserializeTexture();
					else if (child.name == "Material") child.DeserializeMaterial();
					else if (child.name == "Mesh") child.DeserializeMesh();
					else if (child.name == "Clip") child.DeserializeClip();
				}
			}

			if (includeChildren)
			{
				// Deserialize the hierarchy, creating all game objects and components
				go.DeserializeHierarchy(root);
			}
			else go.DeserializeComponents(root);

			// Finish deserializing the components now that all other components are in place
			for (int i = 0; i < mSerList.size; ++i)
			{
				var ent = mSerList.buffer[i];
				ent.comp.Deserialize(ent.node);
			}

			mSerList.Clear();
		}

		/// <summary>
		/// Deserialize a previously serialized game object.
		/// </summary>

		static void DeserializeHierarchy (this GameObject go, DataNode root)
		{
			var trans = go.transform;
			trans.localPosition = root.GetChild("position", trans.localPosition);
			trans.localEulerAngles = root.GetChild("rotation", trans.localEulerAngles);
			trans.localScale = root.GetChild("scale", trans.localScale);
			go.layer = root.GetChild("layer", go.layer);

			mLocalReferences[root.Get<int>()] = go;

			if (!root.GetChild<bool>("active", true)) go.SetActive(false);

			var childNode = root.GetChild("Children");

			if (childNode != null && childNode.children.size > 0)
			{
				for (int i = 0; i < childNode.children.size; ++i)
				{
					var node = childNode.children.buffer[i];
					GameObject child = null;
					var prefab = UnityTools.Load<GameObject>(node.GetChild<string>("prefab"));
					if (prefab != null) child = GameObject.Instantiate(prefab) as GameObject;
					if (child == null) child = new GameObject();
					child.name = node.name;

					var t = child.transform;
					t.parent = trans;
					t.localPosition = Vector3.zero;
					t.localRotation = Quaternion.identity;
					t.localScale = Vector3.one;

					AddReference(child, node.Get<int>());

					child.DeserializeHierarchy(node);
				}
			}

			go.DeserializeComponents(root);
		}

		/// <summary>
		/// Deserialize a previously serialized game object.
		/// </summary>

		static void DeserializeComponents (this GameObject go, DataNode root)
		{
			var scriptNode = root.GetChild("Components");
			if (scriptNode == null) return;

			for (int i = 0; i < scriptNode.children.size; ++i)
			{
				var node = scriptNode.children.buffer[i];
				var type = UnityTools.GetType(node.name);

				if (type != null && type.IsSubclassOf(typeof(Component)))
				{
					Component comp = null;
					if (type == typeof(ParticleSystemRenderer)) comp = go.GetComponent(type);
					if (comp == null) comp = go.AddComponent(type);
					if (comp == null) continue; // Can happen if two ParticleSystemRenderer get added to the same game object, for example

					AddReference(comp, node.Get<int>());

					var dc = new SerializationEntry();
					dc.comp = comp;
					dc.node = node;
					mSerList.Add(dc);
				}
			}
		}

		/// <summary>
		/// Instantiate a new game object given its previously serialized DataNode.
		/// You can serialize game objects by using GameObject.Serialize(), but be aware that serializing only
		/// works fully in the Unity Editor. Prefabs can't be located automatically outside of the Unity Editor.
		/// If a 'replace' object gets passed, the deserialized object will replace the provided one, if possible.
		/// </summary>

		static public GameObject Instantiate (this DataNode data, GameObject replace = null, bool setActive = true)
		{
			GameObject child = null;
			var assetBytes = data.GetChild<byte[]>("assetBundle");

			if (assetBytes != null)
			{
				var ab = UnityTools.LoadAssetBundle(assetBytes);

				if (ab != null)
				{
					var go = ab.mainAsset as GameObject;

					if (go != null)
					{
						child = GameObject.Instantiate(go) as GameObject;
						child.name = data.name;
					}
				}
			}
			else
			{
				var path = data.GetChild<string>("prefab");

				if (!string.IsNullOrEmpty(path))
				{
					var prefab = UnityTools.LoadPrefab(path);

					if (prefab != null)
					{
						child = GameObject.Instantiate(prefab) as GameObject;
						child.name = data.name;
					}
					else if (replace != null)
					{
						replace.name = data.name;
						child = replace;
					}
					else child = new GameObject(data.name);
				}
				else if (replace != null)
				{
					replace.name = data.name;
					child = replace;
				}
				else child = new GameObject(data.name);

				// In order to mimic Unity's prefabs, serialization should be performed on a disabled game object's hierarchy
				if (child.activeSelf) child.SetActive(false);
				child.Deserialize(data, true);
			}

			if (child != null && setActive != child.activeSelf) child.SetActive(setActive);
			return child;
		}
	}
}
#endif
