//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
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
		/// Path to the ban file. Can generally be left untouched, unless you really want to change it.
		/// </summary>

		public string banFilePath = "ServerConfig/ban.txt";

		/// <summary>
		/// You can save files on the server, such as player inventory, Fog of War map updates, player avatars, etc.
		/// </summary>

		protected Dictionary<string, byte[]> mSavedFiles = new Dictionary<string, byte[]>();

		// List of banned keywords
		protected HashSet<string> mBan = new HashSet<string>();

		/// <summary>
		/// Root directory that will be used for all file operations.
		/// </summary>

		public string rootDirectory;

		/// <summary>
		/// Save the specified file.
		/// </summary>

		public bool SaveFile (string fileName, byte[] data)
		{
			if (fileName.Contains("..")) return false;

			if (Tools.WriteFile(string.IsNullOrEmpty(rootDirectory) ? fileName : Path.Combine(rootDirectory, fileName), data, true))
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
			if (fileName.Contains("..")) return null;

			byte[] data;

			if (!mSavedFiles.TryGetValue(fileName, out data))
			{
				data = Tools.ReadFile(string.IsNullOrEmpty(rootDirectory) ? fileName : Path.Combine(rootDirectory, fileName));
				mSavedFiles[fileName] = data;
			}
			return data;
		}

		/// <summary>
		/// Delete the specified file.
		/// </summary>

		public bool DeleteFile (string fileName)
		{
			if (fileName.Contains("..")) return false;

			if (Tools.DeleteFile(string.IsNullOrEmpty(rootDirectory) ? fileName : Path.Combine(rootDirectory, fileName)))
			{
				mSavedFiles.Remove(fileName);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Load a previously saved ban list.
		/// </summary>

		public void LoadBanList ()
		{
			Tools.Print("Bans: " + (Tools.LoadList(string.IsNullOrEmpty(rootDirectory) ? banFilePath : Path.Combine(rootDirectory, banFilePath), mBan) ? mBan.Count.ToString() : "file not found"));
		}

		/// <summary>
		/// Save the current ban list to a file.
		/// </summary>

		public void SaveBanList () { Tools.SaveList(string.IsNullOrEmpty(rootDirectory) ? banFilePath : Path.Combine(rootDirectory, banFilePath), mBan); }

		/// <summary>
		/// Add the specified keyword to the ban list.
		/// </summary>

		public virtual void Ban (string keyword)
		{
			if (string.IsNullOrEmpty(keyword)) return;

			if (!mBan.Contains(keyword))
			{
				mBan.Add(keyword);
				Tools.Log("Added a banned keyword (" + keyword + ")");
				SaveBanList();
			}
			else Tools.Log("Ban already exists (" + keyword + ")");
		}

		/// <summary>
		/// Remove the specified keyword from the ban list.
		/// </summary>

		public virtual void Unban (string keyword)
		{
			if (string.IsNullOrEmpty(keyword)) return;

			if (mBan.Remove(keyword))
			{
				Tools.Log("Removed a banned keyword (" + keyword + ")");
				SaveBanList();
			}
		}

		/// <summary>
		/// Whether the specified keyword is banned.
		/// </summary>

		public bool IsBanned (string keyword)
		{
			if (string.IsNullOrEmpty(keyword)) return false;
			if (mBan.Contains(keyword)) return true;
			foreach (var s in mBan) if (keyword.Contains(s)) return true;
			return false;
		}
	}
}
