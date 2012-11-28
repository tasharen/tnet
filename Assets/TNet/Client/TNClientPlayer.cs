//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using System;
using System.Net.Sockets;
using System.IO;

namespace TNet
{
/// <summary>
/// Class containing information about connected players.
/// </summary>

public class ClientPlayer
{
	public int id = 0;
	public string name;

	public ClientPlayer () { }
	public ClientPlayer (string playerName) { name = playerName; }
}
}