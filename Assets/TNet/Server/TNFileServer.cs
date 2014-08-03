//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2014 Tasharen Entertainment
//---------------------------------------------

using System;
using System.IO;

namespace TNet
{
/// <summary>
/// Base class for Game and Lobby servers capable of saving and loading files.
/// </summary>

public class FileServer
{
	/// <summary>
	/// You can save files on the server, such as player inventory, Fog of War map updates, player avatars, etc.
	/// </summary>

	struct FileEntry
	{
		public string fileName;
		public byte[] data;
	}

	List<FileEntry> mSavedFiles = new List<FileEntry>();

	/// <summary>
	/// Log an error message.
	/// </summary>

	protected void Error (string error)
	{
#if UNITY_EDITOR
		UnityEngine.Debug.LogError("[TNet] " + error);
#elif STANDALONE
		Console.WriteLine("ERROR: " + error);
#endif
	}

	/// <summary>
	/// Save the specified file.
	/// </summary>

	public void SaveFile (string fileName, byte[] data)
	{
		bool exists = false;

		for (int i = 0; i < mSavedFiles.size; ++i)
		{
			FileEntry fi = mSavedFiles[i];

			if (fi.fileName == fileName)
			{
				fi.data = data;
				exists = true;
				break;
			}
		}

		if (!exists)
		{
			FileEntry fi = new FileEntry();
			fi.fileName = fileName;
			fi.data = data;
			mSavedFiles.Add(fi);
		}
		Tools.WriteFile(fileName, data);
	}

	/// <summary>
	/// Load the specified file.
	/// </summary>

	public byte[] LoadFile (string fileName)
	{
		for (int i = 0; i < mSavedFiles.size; ++i)
		{
			FileEntry fi = mSavedFiles[i];
			if (fi.fileName == fileName) return fi.data;
		}
		return Tools.ReadFile(fileName);
	}

	/// <summary>
	/// Delete the specified file.
	/// </summary>

	public void DeleteFile (string fileName)
	{
		for (int i = 0; i < mSavedFiles.size; ++i)
		{
			FileEntry fi = mSavedFiles[i];

			if (fi.fileName == fileName)
			{
				mSavedFiles.RemoveAt(i);
				Tools.DeleteFile(fileName);
				break;
			}
		}
	}
}
}
