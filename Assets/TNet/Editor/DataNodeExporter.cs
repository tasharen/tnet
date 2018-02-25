//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;
using UnityEditor;
using TNet;
using Tools = TNet.Tools;

/// <summary>
/// DataNode export/import menu options, found under Assets/TNet.
/// </summary>

static internal class DataNodeExporter
{
	/// <summary>
	/// Save the data under the specified filename.
	/// </summary>

	static public void Save (DataNode data, string path, DataNode.SaveType type)
	{
		if (data == null || string.IsNullOrEmpty(path)) return;

		EditorUtility.DisplayCancelableProgressBar("Working", "Saving...", 1f);
		data.Write(path, type);
		EditorUtility.ClearProgressBar();

		AssetDatabase.Refresh(ImportAssetOptions.Default);
		var asset = AssetDatabase.LoadAssetAtPath(FileUtil.GetProjectRelativePath(path), typeof(TextAsset)) as TextAsset;

		if (asset != null)
		{
			// Saved in the project folder -- select the saved asset
			Selection.activeObject = asset;
		}
		else if (System.IO.File.Exists(path))
		{
			System.Diagnostics.Process.Start("explorer.exe", "/select," + path.Replace('/', '\\'));
		}
	}

	static bool IsSelectingSingleGameObject ()
	{
		var objects = Selection.objects;
		if (objects == null || objects.Length > 1) return false;
		return (Selection.activeGameObject != null);
	}

	[MenuItem("Assets/DataNode/Export Selected/as Text", true)]
	static internal bool ExportA0 () { return (Selection.activeObject != null); }

	[MenuItem("Assets/DataNode/Export Selected/as Text", false, 0)]
	static internal void ExportA ()
	{
		if (IsSelectingSingleGameObject())
		{
			var go = Selection.activeGameObject;
			var path = UnityEditorExtensions.ShowExportDialog("Export to DataNode", go.name);

			if (!string.IsNullOrEmpty(path))
			{
				EditorUtility.DisplayCancelableProgressBar("Working", "Creating a DataNode...", 0f);
				ComponentSerialization.ClearReferences();
				var data = go.Serialize(true, true, true);
				if (data != null) Save(data, path, DataNode.SaveType.Text);
				ComponentSerialization.ClearReferences();
				EditorUtility.ClearProgressBar();
			}
		}
		else ExportAssets(DataNode.SaveType.Text);
	}

	[MenuItem("Assets/DataNode/Export Selected/as Binary", true)]
	static internal bool ExportB0 () { return (Selection.activeObject != null); }

	[MenuItem("Assets/DataNode/Export Selected/as Binary", false, 0)]
	static internal void ExportB ()
	{
		if (IsSelectingSingleGameObject())
		{
			var go = Selection.activeGameObject;
			var path = UnityEditorExtensions.ShowExportDialog("Export to DataNode", go.name);

			if (!string.IsNullOrEmpty(path))
			{
				EditorUtility.DisplayCancelableProgressBar("Working", "Creating a DataNode...", 0f);
				ComponentSerialization.ClearReferences();
				var data = go.Serialize(true, true, true);
				if (data != null) Save(data, path, DataNode.SaveType.Binary);
				ComponentSerialization.ClearReferences();
				EditorUtility.ClearProgressBar();
			}
		}
		else ExportAssets(DataNode.SaveType.Binary);
	}

	[MenuItem("Assets/DataNode/Export Selected/as Compressed", true)]
	static internal bool ExportC0 () { return (Selection.activeObject != null); }

	[MenuItem("Assets/DataNode/Export Selected/as Compressed", false, 0)]
	static internal void ExportC ()
	{
		if (IsSelectingSingleGameObject())
		{
			var go = Selection.activeGameObject;
			var path = UnityEditorExtensions.ShowExportDialog("Export to DataNode", go.name);

			if (!string.IsNullOrEmpty(path))
			{
				EditorUtility.DisplayCancelableProgressBar("Working", "Creating a DataNode...", 0f);
				ComponentSerialization.ClearReferences();
				var data = go.Serialize(true, true, true);
				if (data != null) Save(data, path, DataNode.SaveType.Compressed);
				ComponentSerialization.ClearReferences();
				EditorUtility.ClearProgressBar();
			}
		}
		else ExportAssets(DataNode.SaveType.Compressed);
	}

#if UNITY_4_3 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7 || ASSET_BUNDLE_EXPORT
	[MenuItem("Assets/DataNode/Export Selected/as AssetBundle", true)]
	static internal bool ExportD0 () { return (Selection.activeObject != null); }

	[MenuItem("Assets/DataNode/Export Selected/as AssetBundle", false, 0)]
	static internal void ExportD ()
	{
		var go = Selection.activeGameObject;
		var path = UnityEditorExtensions.ShowExportDialog("Export AssetBundle", go.name);

		if (!string.IsNullOrEmpty(path))
		{
			var node = new DataNode(go.name, go.GetInstanceID());
			var selection = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);

			if (BuildPipeline.BuildAssetBundle(Selection.activeObject, selection, path,
				BuildAssetBundleOptions.CollectDependencies |
				BuildAssetBundleOptions.CompleteAssets,
				BuildTarget.StandaloneWindows))
			{
				node.AddChild("assetBundle", System.IO.File.ReadAllBytes(path));
				Save(node, path, DataNode.SaveType.Binary);
			}
		}
	}
#endif

	/// <summary>
	/// Asset Bundle-like export, except using DataNode's deep serialization.
	/// </summary>

	static internal void ExportAssets (DataNode.SaveType type)
	{
		var path = UnityEditorExtensions.ShowExportDialog("Export to DataNode", "Assets");
		if (!string.IsNullOrEmpty(path)) ExportAssets(type, path);
	}

	/// <summary>
	/// Asset Bundle-like export, except using DataNode's deep serialization.
	/// </summary>

	static public void ExportAssets (DataNode.SaveType type, string path)
	{
		EditorUtility.DisplayCancelableProgressBar("Working", "Collecting references...", 0f);

		ComponentSerialization.ClearReferences();

		var objects = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);
		var components = new System.Collections.Generic.HashSet<string>();

		foreach (var obj in objects)
		{
			if (obj is MonoScript)
			{
				var s = obj.name;
				if (!components.Contains(s)) components.Add(s);
				continue;
			}

			var go = obj as GameObject;

			if (go)
			{
				go.CollectReferencedPrefabs(true);
				var comps = go.GetComponentsInChildren<MonoBehaviour>(true);

				foreach (var comp in comps)
				{
					var t = comp.GetType().ToString();
					if (!components.Contains(t)) components.Add(t);
				}
			}
			else ComponentSerialization.AddReference(obj);
		}

		EditorUtility.DisplayCancelableProgressBar("Working", "Copying scripts...", 0f);

		var dir = Tools.GetDirectoryFromPath(path);

		// Copy the scripts
		foreach (var c in components)
		{
			var fn = c + ".cs";
			var p = Tools.FindFile(Application.dataPath, fn);
			if (!string.IsNullOrEmpty(p)) System.IO.File.Copy(p, System.IO.Path.Combine(dir, fn), true);
		}

		EditorUtility.DisplayCancelableProgressBar("Working", "Creating a DataNode...", 0f);

		foreach (var pair in ComponentSerialization.referencedPrefabs) pair.Value.CollectReferencedResources();

		var data = ComponentSerialization.SerializeBundle();
		if (data != null && data.children.size > 0) Save(data, path, type);
		else Debug.LogWarning("No assets found to serialize");

		ComponentSerialization.ClearReferences();
		EditorUtility.ClearProgressBar();
	}

	[MenuItem("Assets/DataNode/Convert/to Text", false, 30)]
	static internal void ConvertA ()
	{
		string path = UnityEditorExtensions.ShowImportDialog("Convert DataNode");

		if (!string.IsNullOrEmpty(path))
		{
			DataNode node = DataNode.Read(path, true);
			if (node != null) Save(node, path, DataNode.SaveType.Text);
			else Debug.LogError("Failed to parse " + path + " as DataNode");
		}
	}

	[MenuItem("Assets/DataNode/Convert/to Binary", false, 30)]
	static internal void ConvertB ()
	{
		string path = UnityEditorExtensions.ShowImportDialog("Convert DataNode");

		if (!string.IsNullOrEmpty(path))
		{
			DataNode node = DataNode.Read(path, true);
			if (node != null) Save(node, path, DataNode.SaveType.Binary);
			else Debug.LogError("Failed to parse " + path + " as DataNode");
		}
	}

	[MenuItem("Assets/DataNode/Convert/to Compressed", false, 30)]
	static internal void ConvertC ()
	{
		string path = UnityEditorExtensions.ShowImportDialog("Convert DataNode");

		if (!string.IsNullOrEmpty(path))
		{
			DataNode node = DataNode.Read(path, true);
			if (node != null) Save(node, path, DataNode.SaveType.Compressed);
			else Debug.LogError("Failed to parse " + path + " as DataNode");
		}
	}

	[MenuItem("Assets/DataNode/Import", false, 60)]
	static internal void ImportSelected ()
	{
		string path = UnityEditorExtensions.ShowImportDialog("Import DataNode");

		if (!string.IsNullOrEmpty(path))
		{
			var node = DataNode.Read(path, true);

			if (node != null)
			{
				if (node.GetChild("Prefabs") != null) ComponentSerialization.DeserializeBundle(node);
				else Selection.activeGameObject = node.Instantiate();
			}
			else Debug.LogError("Failed to parse " + path + " as DataNode");
		}
	}
}
