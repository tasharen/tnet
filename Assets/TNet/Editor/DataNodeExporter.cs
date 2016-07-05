//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;
using UnityEditor;
using TNet;

/// <summary>
/// DataNode export/import menu options, found under Assets/TNet.
/// </summary>

static internal class DataNodeExporter
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
	/// Save the data under the specified filename.
	/// </summary>

	static public void Save (DataNode data, string path, DataNode.SaveType type)
	{
		if (data == null || string.IsNullOrEmpty(path)) return;

		data.Write(path, type);

		AssetDatabase.Refresh(ImportAssetOptions.Default);
		TextAsset asset = AssetDatabase.LoadAssetAtPath(FileUtil.GetProjectRelativePath(path), typeof(TextAsset)) as TextAsset;

		if (asset != null)
		{
			// Saved in the project folder -- select the saved asset
			Selection.activeObject = asset;
			if (asset != null) Debug.Log("Saved as " + path + " (" + asset.bytes.Length.ToString("N0") + " bytes)", asset);
		}
		else
		{
			// Saved outside of the project folder -- simply print its size
			System.IO.FileStream fs = System.IO.File.OpenRead(path);

			if (fs != null)
			{
				long pos = fs.Seek(0, System.IO.SeekOrigin.End);
				Debug.Log("Saved as " + path + " (" + pos.ToString("N0") + " bytes)");
				fs.Close();
			}
		}
	}

	[MenuItem("Assets/DataNode/Export Selected/as Text", true)]
	static internal bool ExportA0 () { return (Selection.activeGameObject != null); }

	[MenuItem("Assets/DataNode/Export Selected/as Text", false, 0)]
	static internal void ExportA ()
	{
		GameObject go = Selection.activeGameObject;
		DataNode node = go.Serialize(true);
		string path = ShowExportDialog("Export to DataNode", go.name);
		Save(node, path, DataNode.SaveType.Text);
	}

	[MenuItem("Assets/DataNode/Export Selected/as Binary", true)]
	static internal bool ExportB0 () { return (Selection.activeGameObject != null); }

	[MenuItem("Assets/DataNode/Export Selected/as Binary", false, 0)]
	static internal void ExportB ()
	{
		GameObject go = Selection.activeGameObject;
		DataNode node = go.Serialize(true);
		string path = ShowExportDialog("Export to DataNode", go.name);
		Save(node, path, DataNode.SaveType.Binary);
	}

	[MenuItem("Assets/DataNode/Export Selected/as Compressed", true)]
	static internal bool ExportC0 () { return (Selection.activeGameObject != null); }

	[MenuItem("Assets/DataNode/Export Selected/as Compressed", false, 0)]
	static internal void ExportC ()
	{
		GameObject go = Selection.activeGameObject;
		DataNode node = go.Serialize(true);
		string path = ShowExportDialog("Export to DataNode", go.name);
		Save(node, path, DataNode.SaveType.Compressed);
	}

#if UNITY_4_3 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7 || ASSET_BUNDLE_EXPORT
	[MenuItem("Assets/DataNode/Export Selected/as AssetBundle", true)]
	static internal bool ExportD0 () { return (Selection.activeGameObject != null); }

	[MenuItem("Assets/DataNode/Export Selected/as AssetBundle", false, 0)]
	static internal void ExportD ()
	{
		GameObject go = Selection.activeGameObject;
		DataNode node = new DataNode(go.name, go.GetInstanceID());
		string path = ShowExportDialog("Export AssetBundle", go.name);

		if (!string.IsNullOrEmpty(path))
		{
			Object[] selection = Selection.GetFiltered(typeof(Object), SelectionMode.DeepAssets);

			if (BuildPipeline.BuildAssetBundle(Selection.activeObject, selection, path,
				BuildAssetBundleOptions.CollectDependencies |
				BuildAssetBundleOptions.CompleteAssets,
				BuildTarget.StandaloneWindows))
			{
				node.AddChild("assetBundle", System.IO.File.ReadAllBytes(path));
			}
		}

		Save(node, path, DataNode.SaveType.Binary);
	}
#endif

	[MenuItem("Assets/DataNode/Convert/to Text", false, 30)]
	static internal void ConvertA ()
	{
		string path = ShowImportDialog("Convert DataNode");

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
		string path = ShowImportDialog("Convert DataNode");

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
		string path = ShowImportDialog("Convert DataNode");

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
		string path = ShowImportDialog("Import DataNode");

		if (!string.IsNullOrEmpty(path))
		{
			DataNode node = DataNode.Read(path, true);
			if (node != null) Selection.activeGameObject = node.Instantiate();
			else Debug.LogError("Failed to parse " + path + " as DataNode");
		}
	}
}
