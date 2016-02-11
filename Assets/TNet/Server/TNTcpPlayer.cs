//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Text;

namespace TNet
{
/// <summary>
/// Class containing information about connected players.
/// </summary>

public class TcpPlayer : TcpProtocol
{
	[System.Obsolete("Players can now subscribe to multiple channels at once, making the singular 'channel' obsolete.")]
	public Channel channel { get { return (channels.size != 0) ? channels[0] : null; } }

	/// <summary>
	/// Channel that the player is currently in.
	/// </summary>

	public List<Channel> channels = new List<Channel>();

	/// <summary>
	/// Whether the player is in the specified channel.
	/// </summary>

	public bool IsInChannel (int id)
	{
		for (int i = 0; i < channels.size; ++i)
			if (channels[i].id == id) return true;
		return false;
	}

	/// <summary>
	/// Return the specified channel if the player is currently within it, null otherwise.
	/// </summary>

	public Channel GetChannel (int id)
	{
		for (int i = 0; i < channels.size; ++i)
			if (channels[i].id == id) return channels[i];
		return null;
	}

	/// <summary>
	/// UDP end point if the player has one open.
	/// </summary>

	public IPEndPoint udpEndPoint;

	/// <summary>
	/// Whether the UDP has been confirmed as active and usable.
	/// </summary>

	public bool udpIsUsable = false;

	/// <summary>
	/// Whether this player has authenticated as an administrator.
	/// </summary>

	public bool isAdmin = false;

	/// <summary>
	/// Path where the player's data gets saved, if any.
	/// </summary>

	public string savePath;

	/// <summary>
	/// Next time the player data will be saved.
	/// </summary>

	public bool saveNeeded = false;

	/// <summary>
	/// Type of the saved data.
	/// </summary>

	public DataNode.SaveType saveType = DataNode.SaveType.Binary;

	/// <summary>
	/// Time of the next possible broadcast, used to catch spammers.
	/// </summary>

	public long nextBroadcast = 0;

	/// <summary>
	/// Count broadcasts done per second.
	/// </summary>

	public int broadcastCount = 0;

	/// <summary>
	/// Whether the specified player is already known to this one.
	/// </summary>

	public bool IsKnownTo (Player p, Channel ignoreChannel = null)
	{
		for (int i = 0; i < channels.size; ++i)
		{
			Channel ch = channels[i];
			if (ch == ignoreChannel) continue;
			if (ch.players.Contains(p)) return true;
		}
		return false;
	}
}
}
