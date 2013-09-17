//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2013 Tasharen Entertainment
//---------------------------------------------

namespace TNet
{
/// <summary>
/// Class containing basic information about a remote player.
/// </summary>

public class Player
{
	static protected int mPlayerCounter = 0;

	/// <summary>
	/// Protocol version.
	/// </summary>

	public const int version = 9;

	/// <summary>
	/// All players have a unique identifier given by the server.
	/// </summary>

	public int id = 1;

	/// <summary>
	/// All players have a name that they chose for themselves.
	/// </summary>

	public string name = "Guest";

	/// <summary>
	/// Any custom data you may want to associate with the player.
	/// </summary>

	public object data = null;

	public Player () { }
	public Player (string playerName) { name = playerName; }
}
}
