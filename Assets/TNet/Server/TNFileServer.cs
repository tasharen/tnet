//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using System;
using System.IO;
using System.Collections.Generic;

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

	Dictionary<string, byte[]> mSavedFiles = new Dictionary<string, byte[]>();

	/// <summary>
	/// Save the specified file.
	/// </summary>

	public bool SaveFile (string fileName, byte[] data)
	{
		if (Tools.WriteFile(fileName, data, true))
		{
			mSavedFiles[fileName] = data;
			return true;
		}
		return false;
	}

	/// <summary>
	/// Load the specified file.
	/// </summary>

	public byte[] LoadFile (string fileName)
	{
		byte[] data;

		if (!mSavedFiles.TryGetValue(fileName, out data))
		{
			data = Tools.ReadFile(fileName);
			mSavedFiles[fileName] = data;
		}
		return data;
	}

	/// <summary>
	/// Delete the specified file.
	/// </summary>

	public bool DeleteFile (string fileName)
	{
		if (Tools.DeleteFile(fileName))
		{
			mSavedFiles.Remove(fileName);
			return true;
		}
		return false;
	}
}
}
