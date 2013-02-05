//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using System.Net;
using System.IO;

namespace TNet
{
/// <summary>
/// TCP-based discovery server link. Designed to communicate with a remote TcpDiscoveryServer.
/// You can use this class to register your game server with a remote discovery server.
/// </summary>

public class TcpDiscoveryServerLink : DiscoveryServerLink
{
	TcpProtocol mTcp;

	/// <summary>
	/// Whether the link is currently active.
	/// </summary>

	public override bool isActive { get { return mTcp.isConnected; } }

	/// <summary>
	/// Start the discovery server link.
	/// </summary>

	public override void Start ()
	{
		IPEndPoint ip = Player.ResolveEndPoint(address, port);

		if (ip != null)
		{
			if (mTcp == null) mTcp = new TcpProtocol();
			mTcp.Connect(ip);
		}
	}

	/// <summary>
	/// Stop informing the discovery server of any changes.
	/// </summary>

	public override void Stop ()
	{
		if (mTcp != null)
		{
			mTcp.Disconnect();
			mTcp = null;
		}
	}

	/// <summary>
	/// Send a server update.
	/// </summary>

	public override void Update (GameServer server)
	{
		if (mTcp.isConnected)
		{
			BinaryWriter writer = mTcp.BeginSend(Packet.RequestAddServer);
			writer.Write(GameServer.gameID);
			writer.Write(server.name);
			writer.Write((ushort)server.tcpPort);
			writer.Write((short)server.playerCount);
			mTcp.EndSend();
		}
	}
}
}
