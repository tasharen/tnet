//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2013 Tasharen Entertainment
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

#if !UNITY_WEBPLAYER
	/// <summary>
	/// Clean up the filename, ensuring that there is no funny business going on.
	/// </summary>

	protected string CleanupFilename (string fn) { return Path.GetFileName(fn); }
#endif

	/// <summary>
	/// Log an error message.
	/// </summary>

	protected void Error (string error)
	{
#if UNITY_EDITOR
		UnityEngine.Debug.LogError(error);
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
#if !UNITY_WEBPLAYER
		try
		{
			File.WriteAllBytes(CleanupFilename(fileName), data);
		}
		catch (System.Exception ex)
		{
			Error(fileName + ": " + ex.Message);
		}
#endif
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
#if !UNITY_WEBPLAYER
		string fn = CleanupFilename(fileName);

		if (File.Exists(fn))
		{
			try
			{
				byte[] bytes = File.ReadAllBytes(fn);

				if (bytes != null)
				{
					FileEntry fi = new FileEntry();
					fi.fileName = fileName;
					fi.data = bytes;
					mSavedFiles.Add(fi);
					return bytes;
				}
			}
			catch (System.Exception ex)
			{
				Error(fileName + ": " + ex.Message);
			}
		}
#endif
		return null;
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
#if !UNITY_WEBPLAYER
				File.Delete(CleanupFilename(fileName));
#endif
				break;
			}
		}
	}
}
}