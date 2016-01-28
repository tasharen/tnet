//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2016 Tasharen Entertainment
//---------------------------------------------

namespace TNet
{
/// <summary>
/// Class containing basic information about a remote player.
/// </summary>

public class Player
{
	static protected int mPlayerCounter = 0;
	static protected object mLock = new int();

	/// <summary>
	/// Protocol version.
	/// </summary>

	public const int version = 20160128;

	/// <summary>
	/// All players have a unique identifier given by the server.
	/// </summary>

	public int id = 1;

	/// <summary>
	/// All players have a name that they chose for themselves.
	/// </summary>

	public string name = "Guest";

	/// <summary>
	/// Player's custom data. Set via TNManger.playerData.
	/// Player's data is always kept as byte[] on the server.
	/// </summary>

	public object data = null;

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

#if !STANDALONE
	static DataNode mDummy = new DataNode("Version", version);

	/// <summary>
	/// Player's data converted to DataNode form. Always returns a valid DataNode.
	/// It's a convenience property, intended for read-only purposes.
	/// </summary>

	public DataNode dataNode
	{
		get
		{
			DataNode node = data as DataNode;
			return node ?? mDummy;
		}
	}
#endif

	public Player () { }
	public Player (string playerName) { name = playerName; }

	/// <summary>
	/// Call after shutting down the server.
	/// </summary>

	static public void ResetPlayerCounter () { mPlayerCounter = 0; }
}
}
