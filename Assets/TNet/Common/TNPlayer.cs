//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using System;
using System.Net.Sockets;
using System.IO;
using System.Net;

namespace TNet
{
/// <summary>
/// Class containing basic information about a remote player.
/// </summary>

public class Player
{
	/// <summary>
	/// Protocol version.
	/// </summary>

	public const int version = 1;

	/// <summary>
	/// All players have a unique identifier given by the server.
	/// </summary>

	public int id = 1;

	/// <summary>
	/// All players have a name that they chose for themselves.
	/// </summary>

	public string name = "Guest";

	public Player () { }
	public Player (string playerName) { name = playerName; }

	/// <summary>
	/// Helper function that resolves the remote address.
	/// </summary>

	static public IPAddress ResolveAddress (string address)
	{
		IPAddress ip;
		if (IPAddress.TryParse(address, out ip))
			return ip;

		IPAddress[] ips = Dns.GetHostAddresses(address);

		for (int i = 0; i < ips.Length; ++i)
			if (!IPAddress.IsLoopback(ips[i]))
				return ips[i];

		return null;
	}

	/// <summary>
	/// Given the specified address and port, get the end point class.
	/// </summary>

	static public IPEndPoint ResolveEndPoint (string address, int port)
	{
		IPAddress ad = ResolveAddress(address);
		return (ad != null) ? new IPEndPoint(ad, port) : null;
	}
}
}