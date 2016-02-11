//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

namespace TNet
{
/// <summary>
/// Simple container class holding a dataNode with convenient Get and Set functions.
/// </summary>

public class DataNodeContainer
{
	/// <summary>
	/// Custom data easily accessible via Get and Set functions.
	/// </summary>

	public DataNode dataNode;

	/// <summary>
	/// Set the specified value.
	/// Note that on the client side only the player's owner should be setting the values.
	/// </summary>

	public DataNode Set (string path, object val)
	{
		if (dataNode == null)
		{
			if (val == null) return null;
			dataNode = new DataNode("Version", Player.version);
		}
		return dataNode.SetHierarchy(path, val);
	}

	/// <summary>
	/// Get the specified child within the DataNode.
	/// </summary>

	public DataNode Get (string path) { return (dataNode != null) ? dataNode.GetHierarchy(path) : null; }

	/// <summary>
	/// Get the specified value from the DataNode.
	/// </summary>

	public T Get<T> (string path)
	{
		return (dataNode != null) ? dataNode.GetHierarchy<T>(path) : default(T);
	}

	/// <summary>
	/// Get the specified value from the DataNode.
	/// </summary>

	public T Get<T> (string path, T defaultVal)
	{
		return (dataNode != null) ? dataNode.GetHierarchy<T>(path, defaultVal) : defaultVal;
	}
}

/// <summary>
/// Class containing basic information about a remote player.
/// </summary>

public class Player : DataNodeContainer
{
	static protected int mPlayerCounter = 0;
	static protected object mLock = new int();

	/// <summary>
	/// Protocol version.
	/// </summary>

	public const int version = 20160207;

	/// <summary>
	/// All players have a unique identifier given by the server.
	/// </summary>

	public int id = 1;

	/// <summary>
	/// All players have a name that they chose for themselves.
	/// </summary>

#if UNITY_EDITOR
	public string name = "Editor";
#else
	public string name = "Guest";
#endif

	/// <summary>
	/// Player's known aliases. These will be checked against the ban list.
	/// Ideal usage: Steam ID, computer ID, account ID, etc.
	/// </summary>

	public List<string> aliases = null;

	/// <summary>
	/// Add a new alias to work with.
	/// </summary>

	public bool AddAlias (string s)
	{
		if (!string.IsNullOrEmpty(s))
		{
			if (aliases == null)
			{
				aliases = new List<string>();
				aliases.Add(s);
				return true;
			}
			else if (!aliases.Contains(s))
			{
				aliases.Add(s);
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Does the player have this alias?
	/// </summary>

	public bool HasAlias (string s)
	{
		if (aliases == null) return false;
		for (int i = 0; i < aliases.size; ++i)
			if (aliases[i] == s)
				return true;
		return false;
	}

	public Player () { }
	public Player (string playerName) { name = playerName; }

	/// <summary>
	/// Call after shutting down the server.
	/// </summary>

	static public void ResetPlayerCounter () { mPlayerCounter = 0; }
}
}
