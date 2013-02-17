//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using System.Net;
using System.Threading;

namespace TNet
{
/// <summary>
/// The game server cannot communicate directly with a lobby server because that server can be TCP or UDP based,
/// and may also be hosted either locally or on another computer. And so we use a different class to "link" them
/// together -- the LobbyServerLink. This class will link a game server with a local lobby server.
/// </summary>

public class LobbyServerLink
{
	LobbyServer mLobby;

	protected GameServer mGameServer;
	protected Thread mThread;
	protected IPEndPoint mInternal;
	protected IPEndPoint mExternal;

	// Thread-safe flag indicating that the server should shut down at the first available opportunity
	protected bool mShutdown = false;

	/// <summary>
	/// Create a new local lobby server link. Expects a local server to work with.
	/// </summary>

	public LobbyServerLink (LobbyServer lobbyServer) { mLobby = lobbyServer; }

	/// <summary>
	/// Whether the link is currently active.
	/// </summary>

	public virtual bool isActive { get { return (mLobby != null && mExternal != null); } }

	/// <summary>
	/// Start the lobby server link. Establish a connection, if one is required.
	/// </summary>

	public virtual void Start () { mShutdown = false; mGameServer = null; }

	/// <summary>
	/// Stopping the server should be delayed in order for it to be thread-safe.
	/// </summary>

	public void Stop () { mShutdown = true; mGameServer = null; }

	/// <summary>
	/// Send an update to the lobby server. Triggered by the game server.
	/// </summary>

	public virtual void SendUpdate (GameServer gameServer)
	{
		if (!mShutdown)
		{
			if (mExternal != null)
			{
				mLobby.AddServer(gameServer.name, gameServer.playerCount, mInternal, mExternal);
			}
			else if (mThread == null)
			{
				mThread = new Thread(SendThread);
				mThread.Start(gameServer);
			}
		}
	}

	void SendThread (object obj)
	{
		mInternal = new IPEndPoint(Tools.localAddress, mGameServer.tcpPort);
		mExternal = new IPEndPoint(Tools.externalAddress, mGameServer.tcpPort);

		GameServer gameServer = (GameServer)obj;

		if (gameServer != null && gameServer.isActive)
		{
			mLobby.AddServer(gameServer.name, gameServer.playerCount, mInternal, mExternal);
		}
	}
}
}
