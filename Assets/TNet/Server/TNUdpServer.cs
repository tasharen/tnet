/*using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;

namespace TNet
{
/// <summary>
/// UDP class makes it possible to broadcast messages to players on the same network prior to establishing a connection.
/// </summary>

public class UdpServer : UdpProtocol
{
	// Each UDP socket can service any number of players, so we need to keep track of them
	List<UdpPlayer> mPlayers = new List<UdpPlayer>();
	Dictionary<IPEndPoint, UdpPlayer> mDictionary = new Dictionary<IPEndPoint, UdpPlayer>();

	/// <summary>
	/// Retrieve the specified player.
	/// </summary>

	public UdpPlayer GetPlayer (IPEndPoint ip) { return GetPlayer(ip, false); }

	/// <summary>
	/// Get or create a player given the end point.
	/// </summary>

	UdpPlayer GetPlayer (IPEndPoint ip, bool createIfMissing)
	{
		UdpPlayer player = null;

		if (!mDictionary.TryGetValue(ip, out player) && createIfMissing)
		{
			player = new UdpPlayer(this, ip);
			mPlayers.Add(player);
			mDictionary[ip] = player;
		}
		return player;
	}

	/// <summary>
	/// Remove the specified player from the list.
	/// This player should be already disconnected (received Packet.Disconnect).
	/// If not, use Disconnect() instead.
	/// </summary>

	public void RemovePlayer (IPEndPoint ip)
	{
		if (mDictionary.Remove(ip))
		{
			for (int i = 0; i < mPlayers.size; ++i)
			{
				if (mPlayers[i].endPoint.Equals(ip))
				{
					mPlayers.RemoveAt(i);
					break;
				}
			}
		}
	}

	/// <summary>
	/// Disconnect all players.
	/// </summary>

	public void Disconnect ()
	{
		for (int i = 0; i < mPlayers.size; ++i)
		{
			UdpPlayer player = mPlayers[i];

			if (player.stage != UdpPlayer.Stage.NotConnected)
			{
				player.stage = UdpPlayer.Stage.NotConnected;
				SendDisconnect(mIn, player.endPoint);
				SendDisconnect(mOut, player.endPoint);
			}
		}
	}

	/// <summary>
	/// Disconnect the specified player.
	/// </summary>

	public void Disconnect (IPEndPoint ip, bool createLocalPacket)
	{
		UdpPlayer player = GetPlayer(ip);

		if (player != null && player.stage != UdpPlayer.Stage.NotConnected)
		{
			player.stage = UdpPlayer.Stage.NotConnected;
			if (createLocalPacket) SendDisconnect(mIn, ip);
			SendDisconnect(mOut, ip);
		}
	}

	/// <summary>
	/// Send a disconnect notification packet to the specified end point.
	/// </summary>

	void SendDisconnect (Queue<Datagram> queue, IPEndPoint ip)
	{
		Datagram dg = Datagram.Create();
		dg.endPoint = ip;
		dg.buffer = Buffer.Create();
		dg.buffer.BeginTcpPacket(Packet.Disconnect);
		dg.buffer.EndTcpPacket();
		lock (queue) queue.Enqueue(dg);
	}
}
}*/