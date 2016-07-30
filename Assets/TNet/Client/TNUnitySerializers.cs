//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

#if !STANDALONE && (UNITY_EDITOR || (!UNITY_FLASH && !NETFX_CORE && !UNITY_WP8 && !UNITY_WP_8_1))
#define REFLECTION_SUPPORT

using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System;
using System.Reflection;
using System.Globalization;

namespace TNet
{
/// <summary>
/// This class contains DataNode serialization methods for Unity components that make it possible
/// to serialize behaviours and game objects.
/// </summary>

public static class ComponentSerialization
{
#region Component Serialization

	// Whether mesh and texture data will be serialized or not. Set automatically. Don't change it.
	static bool mFullSerialization = true;

	/// <summary>
	/// Generic component serialization function. You can add custom serialization
	/// to any component by adding an extension with this signature:
	/// static public void Serialize (this YourComponentType, DataNode);
	/// </summary>

	static public void Serialize (this Component c, DataNode node, Type type = null)
	{
		// The 'enabled' flag should only be written down if the behavior is actually disabled
		Behaviour b = c as Behaviour;
		
		if (b != null)
		{
			if (!b.enabled) node.AddChild("enabled", b.enabled);
		}
		else
		{
			Collider cd = c as Collider;
			if (cd != null && !cd.enabled) node.AddChild("enabled", cd.enabled);
		}

		// Try custom serialization first
		if (c.Invoke("Serialize", node)) return;

		GameObject go = c.gameObject;
		if (type == null) type = c.GetType();
		MonoBehaviour mb = c as MonoBehaviour;

		if (mb != null)
		{
			// For MonoBehaviours we want to serialize serializable fields
			List<FieldInfo> fields = type.GetSerializableFields();

			for (int f = 0; f < fields.size; ++f)
			{
				FieldInfo field = fields[f];

				object val = field.GetValue(c);
				if (val == null) continue;

				val = EncodeReference(go, val);
				if (val == null) continue;

				node.AddChild(field.Name, val);
			}
		}
		else
		{
			// Unity components don't have fields, so we should serialize properties instead.
			List<PropertyInfo> props = type.GetSerializableProperties();

			for (int f = 0; f < props.size; ++f)
			{
				PropertyInfo prop = props[f];

				if (prop.Name == "name" ||
					prop.Name == "tag" ||
					prop.Name == "hideFlags" ||
					prop.Name == "enabled" ||
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
		Behaviour b = c as Behaviour;

		if (b != null)
		{
			b.enabled = node.GetChild<bool>("enabled", b.enabled);
		}
		else
		{
			Collider cd = c as Collider;
			if (cd != null) cd.enabled = node.GetChild<bool>("enabled", cd.enabled);
		}

		// Try calling the custom function first
		if (c.Invoke("Deserialize", node)) return;

		GameObject go = c.gameObject;

		// Fallback -- just set the appropriate fields/properties
		for (int i = 0; i < node.children.size; ++i)
		{
			DataNode child = node.children[i];
			if (child.value != null) c.SetFieldOrPropertyValue(child.name, child.value, go);
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
		node.AddChild("hdr", cam.hdr);
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
		Transform[] bones = ren.bones;
		string[] boneList = new string[bones.Length];
		for (int i = 0; i < bones.Length; ++i)
			boneList[i] = ren.gameObject.ReferenceToString(bones[i]);

		root.AddChild("root", ren.gameObject.ReferenceToString(ren.rootBone));
		root.AddChild("bones", boneList);

		Mesh sm = ren.sharedMesh;

		if (sm != null)
		{
			DataNode sub = root.AddChild("Mesh", sm.GetInstanceID());
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
		GameObject go = ren.gameObject;
		ren.rootBone = go.StringToReference(data.GetChild<string>("root")) as Transform;

		string[] boneList = data.GetChild<string[]>("bones");

		if (boneList != null)
		{
			Transform[] bones = new Transform[boneList.Length];

			for (int i = 0; i < bones.Length; ++i)
			{
				bones[i] = go.StringToReference(boneList[i]) as Transform;
				if (bones[i] == null) Debug.LogWarning("Bone not found: " + boneList[i], go);
			}
			ren.bones = bones;
		}

		DataNode meshNode = data.GetChild("Mesh");
		if (meshNode != null) ren.sharedMesh = meshNode.DeserializeMesh();

		ren.quality = data.GetChild<SkinQuality>("quality", ren.quality);
		ren.updateWhenOffscreen = data.GetChild<bool>("offscreen", ren.updateWhenOffscreen);

		Vector3 center = data.GetChild<Vector3>("center", ren.localBounds.center);
		Vector3 size = data.GetChild<Vector3>("size", ren.localBounds.size);
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
		if (ren.lightProbeAnchor != null) root.AddChild("probeAnchor", ren.gameObject.ReferenceToString(ren.lightProbeAnchor));
#else
		var sm = ren.shadowCastingMode;
		if (sm == UnityEngine.Rendering.ShadowCastingMode.Off) root.AddChild("castShadows", false);
		else if (sm == UnityEngine.Rendering.ShadowCastingMode.On) root.AddChild("castShadows", true);
		else root.AddChild("shadowCasting", ren.shadowCastingMode);
		root.AddChild("reflectionProbes", ren.reflectionProbeUsage);
		if (ren.probeAnchor != null) root.AddChild("probeAnchor", ren.gameObject.ReferenceToString(ren.probeAnchor));
#endif
		root.AddChild("receiveShadows", ren.receiveShadows);
#if UNITY_4_7 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2 || UNITY_5_3
		root.AddChild("useLightProbes", ren.useLightProbes);
#else
		root.AddChild("lightProbeUsage", (byte)ren.lightProbeUsage);
#endif

		Material[] mats = ren.sharedMaterials;
		if (mats == null || mats.Length == 0) return;

		DataNode matNode = root.AddChild("Materials", mats.Length);

		for (int i = 0; i < mats.Length; ++i)
		{
			Material mat = mats[i];

			if (mat != null)
			{
				DataNode node = matNode.AddChild("Material", mat.GetInstanceID());
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
					Texture tex = mat.GetTexture(propName);

					if (tex != null)
					{
						DataNode sub = new DataNode(propName, tex.GetInstanceID());
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
		else ren.shadowCastingMode = data.GetChild<UnityEngine.Rendering.ShadowCastingMode>("shadowCastingMode", ren.shadowCastingMode);

		ren.reflectionProbeUsage = data.GetChild<UnityEngine.Rendering.ReflectionProbeUsage>("reflectionProbes", ren.reflectionProbeUsage);
#endif
		ren.receiveShadows = data.GetChild<bool>("receiveShadows", ren.receiveShadows);
#if UNITY_4_7 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2 || UNITY_5_3
		ren.useLightProbes = data.GetChild<bool>("useLightProbes", ren.useLightProbes);
#else
		var lpu = data.GetChild("lightProbeUsage");

		if (lpu != null)
		{
			ren.lightProbeUsage = (UnityEngine.Rendering.LightProbeUsage)lpu.Get<byte>((byte)ren.lightProbeUsage);
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
				DataNode matNode = matRoot.children[i];
				mats[i] = matNode.DeserializeMaterial();
			}
			ren.sharedMaterials = mats;
		}
	}

	static Dictionary<int, Material> mMaterials = new Dictionary<int, Material>();

	/// <summary>
	/// Deserialize a previously serialized material.
	/// </summary>

	static public Material DeserializeMaterial (this DataNode matNode)
	{
		Material mat = null;
		int id = matNode.Get<int>();
		if (mMaterials.TryGetValue(id, out mat) && mat != null) return mat;

		// Try to load this material
		string name = matNode.GetChild<string>("name", "Unnamed");
		string path = matNode.GetChild<string>("path");

		if (id == 0)
		{
			id = (path + name).GetHashCode();
			if (mMaterials.TryGetValue(id, out mat) && mat != null) return mat;
		}

		if (!string.IsNullOrEmpty(path))
		{
			mat = UnityTools.Load<Material>(path);

			if (mat != null)
			{
				mMaterials[id] = mat;
				return mat;
			}
		}

		// Material can only be created if there is a shader to work with
		string shaderName = matNode.GetChild<string>("shader");
		Shader shader = Shader.Find(shaderName);

		if (shader == null)
		{
			Debug.LogWarning("Shader '" + shaderName + "' was not found");
			shader = Shader.Find("Diffuse");
		}

		// Create a new material
		mat = new Material(shader);
		mat.name = name;
		mMaterials[id] = mat;

		// Restore material properties
		for (int b = 0; b < matNode.children.size; ++b)
		{
			DataNode prop = matNode.children[b];
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
		Mesh sm = filter.sharedMesh;

		if (sm != null)
		{
			DataNode child = data.AddChild("Mesh", sm.GetInstanceID());
			sm.Serialize(child);
		}
	}

	/// <summary>
	/// Restore a previously serialized Mesh Filter component.
	/// </summary>

	static public void Deserialize (this MeshFilter filter, DataNode data)
	{
		DataNode mesh = data.GetChild("Mesh");
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

	static Dictionary<int, Mesh> mCachedMeshes = new Dictionary<int, Mesh>();

	/// <summary>
	/// Set the mesh from the specified DataNode.
	/// </summary>

	static public Mesh DeserializeMesh (this DataNode node)
	{
		Mesh mesh = null;
		int id = node.Get<int>();
		if (id != 0 && mCachedMeshes.TryGetValue(id, out mesh) && mesh != null) return mesh;

		string name = node.GetChild<string>("name");
		string path = node.GetChild<string>("path");

		if (id == 0)
		{
			id = (path + name).GetHashCode();
			if (mCachedMeshes.TryGetValue(id, out mesh) && mesh != null) return mesh;
		}

		if (!string.IsNullOrEmpty(path))
		{
			mesh = UnityTools.Load<Mesh>(path, name);
#if UNITY_EDITOR
			if (mesh == null) Debug.LogWarning("Unable to find mesh '" + name + "' in " + path);
#endif
		}
		else
		{
			mesh = new Mesh();
			mesh.name = name;

			Vector3[] verts = node.GetChild<Vector3[]>("vertices");
			if (verts != null) mesh.vertices = verts;

			Vector3[] normals = node.GetChild<Vector3[]>("normals");
			if (normals != null) mesh.normals = normals;

			Vector2[] uv1 = node.GetChild<Vector2[]>("uv1");
			if (uv1 != null) mesh.uv = uv1;

			Vector2[] uv2 = node.GetChild<Vector2[]>("uv2");
			if (uv2 != null) mesh.uv2 = uv2;

			Vector4[] tangents = node.GetChild<Vector4[]>("tangents");
			if (tangents != null) mesh.tangents = tangents;

			Color32[] colors = node.GetChild<Color32[]>("colors");
			if (colors != null) mesh.colors32 = colors;

			BoneWeight[] weights = node.GetChild<BoneWeight[]>("weights");
			if (weights != null) mesh.boneWeights = weights;

			Matrix4x4[] poses = node.GetChild<Matrix4x4[]>("poses");
			if (poses != null) mesh.bindposes = poses;

			int[] triangles = node.GetChild<int[]>("triangles");
			if (triangles != null) mesh.triangles = triangles;

			mesh.RecalculateBounds();
		}
		mCachedMeshes[id] = mesh;
		return mesh;
	}

	/// <summary>
	/// Serialize the entire texture into the specified DataNode.
	/// </summary>

	static public void Serialize (this Texture tex, DataNode node)
	{
		if (!mFullSerialization) return;

		node.AddChild("name", tex.name);
		string path = UnityTools.LocateResource(tex);

		if (!string.IsNullOrEmpty(path))
		{
			node.AddChild("path", path);
			return;
		}

		if (tex is Texture2D)
		{
			Texture2D t2 = tex as Texture2D;

#if UNITY_EDITOR
			try
			{
				byte[] bytes = t2.EncodeToPNG();
				if (bytes != null) node.AddChild("bytes", bytes);
				else Debug.Log(t2.name + " (" + t2.format + ")", tex);
			}
			catch (Exception)
			{
				string assetPath = UnityEditor.AssetDatabase.GetAssetPath(tex);

				if (!string.IsNullOrEmpty(assetPath))
				{
					UnityEditor.TextureImporter ti = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;
					ti.isReadable = true;
					UnityEditor.AssetDatabase.ImportAsset(assetPath);
					byte[] bytes = t2.EncodeToPNG();
					if (bytes != null) node.AddChild("bytes", bytes);
					else Debug.Log(t2.name + " (" + t2.format + ")", tex);
				}
			}
#else
			node.AddChild("bytes", t2.EncodeToPNG());
#endif
			node.AddChild("filter", (int)t2.filterMode);
			node.AddChild("wrap", (int)t2.wrapMode);
			node.AddChild("af", t2.anisoLevel);
			return;
		}

		Debug.LogWarning("Unable to save a reference to texture '" + tex.name + "' because it's not in the Resources folder.", tex);
	}

	static Dictionary<int, Texture> mTextures = new Dictionary<int, Texture>();

	/// <summary>
	/// Deserialize the texture that was previously serialized into the DataNode format.
	/// </summary>

	static public Texture DeserializeTexture (this DataNode node)
	{
		// First try the cache
		Texture tex = null;
		int id = node.Get<int>();
		if (id != 0 && mTextures.TryGetValue(id, out tex) && tex != null) return tex;

		// If the texture's ID is unknown, make a dummy one and try going through cache again
		string name = node.GetChild<string>("name", "Unnamed");
		string path = node.GetChild<string>("path");

		if (id == 0)
		{
			id = (path + name).GetHashCode();
			if (mTextures.TryGetValue(id, out tex) && tex != null) return tex;
		}

		// Next try to load the texture
		if (!string.IsNullOrEmpty(path))
		{
			tex = UnityTools.Load<Texture>(path);

			if (tex != null)
			{
				mTextures[id] = tex;
				return tex;
			}
		}

		// Lastly, create a new texture
		Texture2D t2 = new Texture2D(2, 2);
		t2.name = name;

		// Try to load the texture's data
		byte[] bytes = node.GetChild<byte[]>("bytes");

		if (bytes != null)
		{
			t2.LoadImage(bytes);
			t2.filterMode = (FilterMode)node.GetChild<int>("filter", (int)t2.filterMode);
			t2.wrapMode = (TextureWrapMode)node.GetChild<int>("wrap", (int)t2.wrapMode);
			t2.anisoLevel = node.GetChild<int>("af", t2.anisoLevel);
			t2.Apply();
		}
		else
		{
#if UNITY_EDITOR
			Debug.LogWarning("Creating a dummy texture: " + t2.name, t2);
#endif
			t2.SetPixels(new Color[] { Color.clear, Color.clear, Color.clear, Color.clear });
			t2.Apply();
		}

		// Add it to cache
		tex = t2;
		mTextures[id] = tex;
		return tex;
	}

	/// <summary>
	/// Collect all meshes, materials and textures underneath the specified object and serialize them into the DataNode.
	/// </summary>

	static public void SerializeSharedResources (this GameObject go, DataNode node, bool includeInactive = false)
	{
		mFullSerialization = true;

		MeshFilter[] filters = go.GetComponentsInChildren<MeshFilter>(includeInactive);
		MeshRenderer[] rens = go.GetComponentsInChildren<MeshRenderer>(includeInactive);
		SkinnedMeshRenderer[] sks = go.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive);

		List<Material> materials = new List<Material>();
		List<Mesh> meshes = new List<Mesh>();

		foreach (MeshFilter f in filters)
		{
			Mesh m = f.sharedMesh;
			if (!meshes.Contains(m)) meshes.Add(m);
		}

		foreach (SkinnedMeshRenderer sk in sks)
		{
			Mesh m = sk.sharedMesh;
			if (!meshes.Contains(m)) meshes.Add(m);

			Material[] mats = sk.sharedMaterials;
			foreach (Material mt in mats)
				if (!materials.Contains(mt))
					materials.Add(mt);
		}

		foreach (MeshRenderer r in rens)
		{
			Material[] mats = r.sharedMaterials;
			foreach (Material m in mats)
				if (!materials.Contains(m))
					materials.Add(m);
		}

		if (materials.size == 0 && meshes.size == 0) return;

#if UNITY_EDITOR
		List<Texture> textures = new List<Texture>();

		for (int i = 0; i < materials.size; ++i)
		{
			Material mat = materials[i];
			Shader s = mat.shader;
			if (s == null) continue;

			string matPath = UnityTools.LocateResource(mat);
			if (!string.IsNullOrEmpty(matPath)) continue;

			int props = UnityEditor.ShaderUtil.GetPropertyCount(s);

			for (int b = 0; b < props; ++b)
			{
				string propName = UnityEditor.ShaderUtil.GetPropertyName(s, b);
				UnityEditor.ShaderUtil.ShaderPropertyType type = UnityEditor.ShaderUtil.GetPropertyType(s, b);
				if (type != UnityEditor.ShaderUtil.ShaderPropertyType.TexEnv) continue;
				Texture tex = mat.GetTexture(propName);
				if (tex != null && !textures.Contains(tex)) textures.Add(tex);
			}
		}

		for (int i = 0; i < textures.size; ++i)
		{
			Texture tex = textures[i];
			tex.Serialize(node.AddChild("Texture", tex.GetInstanceID()));
		}
#endif

		for (int i = 0; i < materials.size; ++i)
		{
			Material mat = materials[i];
			mat.Serialize(node.AddChild("Material", mat.GetInstanceID()), false);
		}

		for (int i = 0; i < meshes.size; ++i)
		{
			Mesh mesh = meshes[i];
			mesh.Serialize(node.AddChild("Mesh", mesh.GetInstanceID()));
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

	static public DataNode Serialize (this GameObject go, bool fullHierarchy = true, bool isRootNode = true)
	{
		DataNode root = new DataNode(go.name, go.GetInstanceID());

		// Save a reference to a prefab, if there is one
		string prefab = UnityTools.LocateResource(go, !isRootNode);
		if (!string.IsNullOrEmpty(prefab)) root.AddChild("prefab", prefab);

		// Save the transform and the object's layer
		Transform trans = go.transform;
		root.AddChild("position", trans.localPosition);
		root.AddChild("rotation", trans.localEulerAngles);
		root.AddChild("scale", trans.localScale);

		int layer = go.layer;
		if (layer != 0) root.AddChild("layer", go.layer);

		// If this was a prefab instance, don't do anything else
		if (!string.IsNullOrEmpty(prefab)) return root;

		// Collect all meshes
		if (isRootNode)
		{
			DataNode child = new DataNode("Resources");
#if UNITY_EDITOR
			go.SerializeSharedResources(child, UnityEditor.PrefabUtility.GetPrefabType(go) == UnityEditor.PrefabType.Prefab);
#else
			go.SerializeSharedResources(child);
#endif
			if (child.children.size != 0) root.children.Add(child);
			mFullSerialization = false;
		}

		Component[] comps = go.GetComponents<Component>();
		DataNode compRoot = null;

		for (int i = 0, imax = comps.Length; i < imax; ++i)
		{
			Component c = comps[i];

			System.Type type = c.GetType();
			if (type == typeof(Transform)) continue;

			if (compRoot == null) compRoot = root.AddChild("Components");
			DataNode child = compRoot.AddChild(Serialization.TypeToName(type), c.GetInstanceID());
			c.Serialize(child, type);
		}

		if (fullHierarchy && trans.childCount > 0)
		{
			DataNode children = root.AddChild("Children");

			for (int i = 0, imax = trans.childCount; i < imax; ++i)
			{
				GameObject child = trans.GetChild(i).gameObject;
				if (child.activeInHierarchy)
					children.children.Add(child.Serialize(true, false));
			}
		}
		if (isRootNode) mFullSerialization = true;
		return root;
	}

	/// <summary>
	/// Used to convert object references to strings.
	/// </summary>

	static object EncodeReference (GameObject go, object val)
	{
		if (val is UnityEngine.Object)
		{
#if UNITY_EDITOR
			return go.ReferenceToString(val as UnityEngine.Object);
#else
			return null;
#endif
		}
		else if (val is System.Collections.IList)
		{
			System.Collections.IList list = val as System.Collections.IList;
			if (list.Count == 0) return null;
			System.Type t = list.GetType();
			System.Type elemType = t.GetElementType();
			if (elemType == null) elemType = t.GetGenericArgument();

			if (typeof(UnityEngine.Object).IsAssignableFrom(elemType))
			{
#if UNITY_EDITOR
				string[] strList = new string[list.Count];
				for (int d = 0, dmax = list.Count; d < dmax; ++d)
					strList[d] = go.ReferenceToString(list[d] as UnityEngine.Object);
				val = strList;
#else
				return null;
#endif
			}
		}
		return val;
	}

	class SerializationEntry
	{
		public GameObject go;
		public DataNode node;
	}

	static List<SerializationEntry> mSerList = new List<SerializationEntry>();

	/// <summary>
	/// Deserialize a previously serialized game object.
	/// </summary>

	static public void Deserialize (this GameObject go, DataNode root, bool includeChildren = true)
	{
		DataNode resNode = root.GetChild("Resources");

		if (resNode != null)
		{
			for (int i = 0; i < resNode.children.size; ++i)
			{
				DataNode child = resNode.children[i];
				if (child.name == "Texture") child.DeserializeTexture();
				else if (child.name == "Material") child.DeserializeMaterial();
				else if (child.name == "Mesh") child.DeserializeMesh();
			}
		}

		if (includeChildren)
		{
			go.DeserializeHierarchy(root);
			for (int i = 0; i < mSerList.size; ++i)
				mSerList[i].go.DeserializeComponents(mSerList[i].node);
			mSerList.Clear();
		}
		else go.DeserializeComponents(root);
	}

	/// <summary>
	/// Deserialize a previously serialized game object.
	/// </summary>

	static void DeserializeHierarchy (this GameObject go, DataNode root)
	{
		SerializationEntry ent = new SerializationEntry();
		ent.go = go;
		ent.node = root;
		mSerList.Add(ent);

		Transform trans = go.transform;
		trans.localPosition = root.GetChild<Vector3>("position", trans.localPosition);
		trans.localEulerAngles = root.GetChild<Vector3>("rotation", trans.localEulerAngles);
		trans.localScale = root.GetChild<Vector3>("scale", trans.localScale);
		go.layer = root.GetChild<int>("layer", go.layer);

		DataNode childNode = root.GetChild("Children");

		if (childNode != null && childNode.children.size > 0)
		{
			for (int i = 0; i < childNode.children.size; ++i)
			{
				DataNode node = childNode.children[i];
				GameObject child = null;
				GameObject prefab = UnityTools.Load<GameObject>(node.GetChild<string>("prefab"));
				if (prefab != null) child = GameObject.Instantiate(prefab) as GameObject;
				if (child == null) child = new GameObject();
				child.name = node.name;

				Transform t = child.transform;
				t.parent = trans;
				t.localPosition = Vector3.zero;
				t.localRotation = Quaternion.identity;
				t.localScale = Vector3.one;

				child.DeserializeHierarchy(node);
			}
		}
	}

	/// <summary>
	/// Deserialize a previously serialized game object.
	/// </summary>

	static void DeserializeComponents (this GameObject go, DataNode root)
	{
		DataNode scriptNode = root.GetChild("Components");
		if (scriptNode == null) return;

		for (int i = 0; i < scriptNode.children.size; ++i)
		{
			DataNode node = scriptNode.children[i];
			System.Type type = UnityTools.GetType(node.name);
 
			if (type != null && type.IsSubclassOf(typeof(Component)))
			{
				Component comp = go.GetComponent(type);
				if (comp == null) comp = go.AddComponent(type);
				comp.Deserialize(node);
			}
		}
	}

	/// <summary>
	/// Instantiate a new game object given its previously serialized DataNode.
	/// You can serialize game objects by using GameObject.Serialize(), but be aware that serializing only
	/// works fully in the Unity Editor. Prefabs can't be located automatically outside of the Unity Editor.
	/// </summary>

	static public GameObject Instantiate (this DataNode data)
	{
		GameObject child = null;
		byte[] assetBytes = data.GetChild<byte[]>("assetBundle");

		if (assetBytes != null)
		{
			AssetBundle ab = UnityTools.LoadAssetBundle(assetBytes);

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
			string path = data.GetChild<string>("prefab");

			if (!string.IsNullOrEmpty(path))
			{
				GameObject prefab = UnityTools.LoadPrefab(path);

				if (prefab != null)
				{
					child = GameObject.Instantiate(prefab) as GameObject;
					child.name = data.name;
					child.SetActive(true);
				}
				else child = new GameObject(data.name);
			}
			else child = new GameObject(data.name);

			child.Deserialize(data, true);
		}
		return child;
	}
}
}
#endif
