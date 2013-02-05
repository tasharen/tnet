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
	static protected int mPlayerCounter = 0;

	/// <summary>
	/// Protocol version.
	/// </summary>

	public const int version = 5;

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

	static IPAddress mLocalAddress;

	/// <summary>
	/// Local IP address.
	/// </summary>

	static public IPAddress localAddress
	{
		get
		{
			if (mLocalAddress == null)
			{
				IPAddress[] ips = Dns.GetHostAddresses(Dns.GetHostName());

				for (int i = 0; i < ips.Length; ++i)
				{
					IPAddress addr = ips[i];

					if (IsValidAddress(addr))
					{
						mLocalAddress = addr;
						break;
					}
				}
			}
			return mLocalAddress;
		}
	}

	/// <summary>
	/// Helper function that determines if this is a valid address.
	/// </summary>

	static public bool IsValidAddress (IPAddress address)
	{
		if (address.AddressFamily != AddressFamily.InterNetwork) return false;
		if (address.Equals(IPAddress.Loopback)) return false;
		if (address.Equals(IPAddress.None)) return false;
		if (address.Equals(IPAddress.Any)) return false;
		return true;
	}

	/// <summary>
	/// Helper function that resolves the remote address.
	/// </summary>

	static public IPAddress ResolveAddress (string address)
	{
		if (string.IsNullOrEmpty(address)) return null;

		IPAddress ip;
		if (IPAddress.TryParse(address, out ip))
			return ip;

		try
		{
			IPAddress[] ips = Dns.GetHostAddresses(address);

			for (int i = 0; i < ips.Length; ++i)
				if (!IPAddress.IsLoopback(ips[i]))
					return ips[i];
		}
#if UNITY_EDITOR
		catch (System.Exception ex)
		{
			UnityEngine.Debug.LogError(ex.Message + " (" + address + ")");
		}
#else
		catch (System.Exception) {}
#endif
		return null;
	}

	/// <summary>
	/// Given the specified address and port, get the end point class.
	/// </summary>

	static public IPEndPoint ResolveEndPoint (string address, int port)
	{
		IPEndPoint ip = ResolveEndPoint(address);
		if (ip != null) ip.Port = port;
		return ip;
	}

	/// <summary>
	/// Given the specified address, get the end point class.
	/// </summary>

	static public IPEndPoint ResolveEndPoint (string address)
	{
		int port = 0;
		string[] split = address.Split(new char[':']);

		// Automatically try to parse the port
		if (split.Length > 1)
		{
			address = split[0];
			int.TryParse(split[1], out port);
		}

		IPAddress ad = ResolveAddress(address);
		return (ad != null) ? new IPEndPoint(ad, port) : null;
	}
}
}
