//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using System.Net;

namespace TNet
{
/// <summary>
/// The game server cannot communicate directly with a lobby server because that server can be TCP or UDP based,
/// and may also be hosted either locally or on another computer. And so we use a different class to "link" them
/// together -- the LobbyServerLink. This class will link a game server with a local lobby server.
/// </summary>

public class LobbyServerLink
{
	/// <summary>
	/// Game server's internal address, sent to the lobby server.
	/// </summary>

	public IPEndPoint internalAddress;

	/// <summary>
	/// Game server's external address, sent to the lobby server.
	/// </summary>

	public IPEndPoint externalAddress;

	// Local lobby server
	LobbyServer mServer;

	// Thread-safe flag indicating that the server should shut down at the first available opportunity
	protected bool mShutdown = false;

	/// <summary>
	/// Create a new local lobby server link. Expects a local server to work with.
	/// </summary>

	public LobbyServerLink (LobbyServer lobbyServer) { mServer = lobbyServer; }

	/// <summary>
	/// Whether the link is currently active.
	/// </summary>

	public virtual bool isActive { get { return (mServer != null && externalAddress != null); } }

	/// <summary>
	/// Start the lobby server link. Establish a connection, if one is required.
	/// </summary>

	public virtual void Start () { mShutdown = false; }

	/// <summary>
	/// Stopping the server should be delayed in order for it to be thread-safe.
	/// </summary>

	public void Stop () { mShutdown = true; }

	/// <summary>
	/// Send an update to the lobby server. Triggered by the game server.
	/// </summary>

	public virtual void SendUpdate (GameServer gameServer)
	{
		if (!mShutdown)
		{
			mServer.AddServer(gameServer.name, gameServer.playerCount, internalAddress, externalAddress);
		}
	}
}
}
