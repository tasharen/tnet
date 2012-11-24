using System;

#if !UNITY_WEB_PLAYER
using System.IO;
#endif

namespace TNet
{
/// <summary>
/// A channel contains one or more players.
/// All information broadcast by players is visible by others in the same channel.
/// </summary>

public class Channel
{
	public class RFC
	{
		public int id;
		public Buffer buffer;
	}

	public class CreatedObject
	{
		public short objectID;
		public int uniqueID;
		public Buffer buffer;
	}

	public class FileEntry
	{
		public string fileName;
		public byte[] data;
	};

	public int id;
	public string password;
	public bool persistent = false;
	public bool closed = false;
	public BetterList<Player> players = new BetterList<Player>();
	public BetterList<RFC> rfcs = new BetterList<RFC>();
	public BetterList<CreatedObject> created = new BetterList<CreatedObject>();
	public BetterList<int> destroyed = new BetterList<int>();
	public BetterList<FileEntry> savedFiles = new BetterList<FileEntry>();
	public Player host;
	public int viewCounter = 0;

	/// <summary>
	/// Remove the specified player from the channel.
	/// </summary>

	public void RemovePlayer (Player p)
	{
		if (p == host) host = null;
		if (players.Remove(p) && !persistent && players.size == 0) Close();
	}

	/// <summary>
	/// Create a new buffered remote function call.
	/// </summary>

	public void CreateRFC (int inID, Buffer buffer)
	{
		if (closed) return;
		buffer.MarkAsUsed();

		for (int i = 0; i < rfcs.size; ++i)
		{
			RFC r = rfcs[i];
			
			if (r.id == inID)
			{
				if (r.buffer != null && r.buffer.MarkAsUnused())
				{
					Connection.ReleaseBuffer(r.buffer);
				}
				r.buffer = buffer;
				return;
			}
		}

		RFC rpc = new RFC();
		rpc.id = inID;
		rpc.buffer = buffer;
		rfcs.Add(rpc);
	}

	/// <summary>
	/// Delete the specified remote function call.
	/// </summary>

	public void DeleteRFC (int inID)
	{
		for (int i = 0; i < rfcs.size; ++i)
		{
			RFC r = rfcs[i];

			if (r.id == inID)
			{
				rfcs.RemoveAt(i);
				if (r.buffer != null && r.buffer.MarkAsUnused()) Connection.ReleaseBuffer(r.buffer);
			}
		}
	}

	/// <summary>
	/// Delete the specified remote function call.
	/// </summary>

	public void DeleteObjectRFCs (int objectID)
	{
		objectID <<= 8;

		for (int i = 0; i < rfcs.size; )
		{
			RFC r = rfcs[i];

			if ((r.id & objectID) == objectID)
			{
				rfcs.RemoveAt(i);
				if (r.buffer != null && r.buffer.MarkAsUnused()) Connection.ReleaseBuffer(r.buffer);
				continue;
			}
			++i;
		}
	}

	/// <summary>
	/// Save the specified file.
	/// </summary>

	public void SaveFile (string fileName, byte[] data)
	{
		bool exists = false;

		for (int i = 0; i < savedFiles.size; ++i)
		{
			FileEntry fi = savedFiles[i];

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
			savedFiles.Add(fi);
		}
#if !UNITY_WEB_PLAYER
		try
		{
			File.WriteAllBytes(CleanupFilename(fileName), data);
		}
		catch (System.Exception ex)
		{
			Console.WriteLine(fileName + ": " + ex.Message);
		}
#endif
	}

	/// <summary>
	/// Load the specified file.
	/// </summary>

	public byte[] LoadFile (string fileName)
	{
		for (int i = 0; i < savedFiles.size; ++i)
		{
			FileEntry fi = savedFiles[i];

			if (fi.fileName == fileName)
			{
#if !UNITY_WEB_PLAYER
				if (fi.data == null)
				{
					try
					{
						fi.data = File.ReadAllBytes(CleanupFilename(fileName));
					}
					catch (System.Exception ex)
					{
						Console.WriteLine(fileName + ": " + ex.Message);
					}
				}
#endif
				return fi.data;
			}
		}
		return null;
	}

	/// <summary>
	/// Delete the specified file.
	/// </summary>

	public void DeleteFile (string fileName)
	{
		for (int i = 0; i < savedFiles.size; ++i)
		{
			FileEntry fi = savedFiles[i];

			if (fi.fileName == fileName)
			{
				savedFiles.RemoveAt(i);
#if !UNITY_WEB_PLAYER
				File.Delete(CleanupFilename(fileName));
#endif
				break;
			}
		}
	}

#if !UNITY_WEB_PLAYER
	/// <summary>
	/// Helper function that cleans up the specified path.
	/// </summary>

	string CleanupFilename (string path) { return id + "_" + Path.GetFileName(path); }

	/// <summary>
	/// Close the channel, preventing future join requests.
	/// </summary>

	public void Close ()
	{
		closed = true;
		for (int i = 0; i < savedFiles.size; ++i) File.Delete(CleanupFilename(savedFiles[i].fileName));
		savedFiles.Clear();

		for (int i = 0; i < rfcs.size; ++i)
		{
			RFC r = rfcs[i];
			if (r.buffer != null && r.buffer.MarkAsUnused()) Connection.ReleaseBuffer(r.buffer);
		}
		rfcs.Clear();
	}
#else
	public void Close ()
	{
		closed = true;
		savedFiles.Clear();

		for (int i = 0; i < rfcs.size; ++i)
		{
			RFC r = rfcs[i];
			if (r.buffer != null && r.buffer.MarkAsUnused()) Connection.ReleaseBuffer(r.buffer);
		}
		rfcs.Clear();
	}
#endif
}
}