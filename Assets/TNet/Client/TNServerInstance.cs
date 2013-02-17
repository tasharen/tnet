//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using UnityEngine;
using TNet;
using System.IO;
using System.Collections;
using System.Net;

/// <summary>
/// Tasharen Network server tailored for Unity.
/// </summary>

[AddComponentMenu("TNet/Network Server (internal)")]
public class TNServerInstance : MonoBehaviour
{
	static TNServerInstance mInstance;

	public enum State
	{
		Inactive,
		Starting,
		Active,
	}

	State mState = State.Inactive;
	GameServer mGame = new GameServer();
	LobbyServer mLobby;

	/// <summary>
	/// Instance access is internal only as all the functions are static for convenience purposes.
	/// </summary>

	static TNServerInstance instance
	{
		get
		{
			if (mInstance == null)
			{
				GameObject go = new GameObject("_Server");
				mInstance = go.AddComponent<TNServerInstance>();
				DontDestroyOnLoad(go);
			}
			return mInstance;
		}
	}

	/// <summary>
	/// Current state of the server instance, since the startup happens on a separate thread.
	/// This way the DNS queries / IP retrieval doesn't block the main thread.
	/// </summary>

	static public State state { get { return (mInstance != null) ? mInstance.mState : State.Inactive; } }

	/// <summary>
	/// Whether the server instance is currently active.
	/// </summary>

	static public bool isActive { get { return (mInstance != null) && mInstance.mGame.isActive; } }

	/// <summary>
	/// Whether the server is currently listening for incoming connections.
	/// </summary>

	static public bool isListening { get { return (mInstance != null) && mInstance.mGame.isListening; } }

	/// <summary>
	/// Port used to listen for incoming TCP connections.
	/// </summary>

	static public int listeningPort { get { return (mInstance != null) ? mInstance.mGame.tcpPort : 0; } }

	/// <summary>
	/// How many players are currently connected to the server.
	/// </summary>

	static public int playerCount { get { return (mInstance != null) ? mInstance.mGame.playerCount : 0; } }

	/// <summary>
	/// Active game server.
	/// </summary>

	static public GameServer game { get { return (mInstance != null) ? mInstance.mGame : null; } }

	/// <summary>
	/// Active lobby server.
	/// </summary>

	static public LobbyServer lobby { get { return (mInstance != null) ? mInstance.mLobby : null; } }

	/// <summary>
	/// Start a local server instance listening to the specified port.
	/// </summary>

	static public bool Start (int tcpPort)
	{
		return instance.StartLocal(tcpPort, 0, null, 0, false);
	}

	/// <summary>
	/// Start a local server instance listening to the specified port.
	/// </summary>

	static public bool Start (int tcpPort, int udpPort)
	{
		return instance.StartLocal(tcpPort, udpPort, null, 0, false);
	}

	/// <summary>
	/// Start a local server instance listening to the specified port and loading the saved data from the specified file.
	/// </summary>

	static public bool Start (int tcpPort, int udpPort, string fileName)
	{
		return instance.StartLocal(tcpPort, udpPort, fileName, 0, false);
	}

	/// <summary>
	/// Start a local game and lobby server instances.
	/// </summary>

	static public bool Start (int tcpPort, int udpPort, string fileName, int lobbyPort)
	{
		return instance.StartLocal(tcpPort, udpPort, fileName, lobbyPort, false);
	}

	/// <summary>
	/// Start a local game and lobby server instances.
	/// </summary>

	static public bool Start (int tcpPort, int udpPort, string fileName, int lobbyPort, bool useTcpLobby)
	{
		return instance.StartLocal(tcpPort, udpPort, fileName, lobbyPort, useTcpLobby);
	}

	/// <summary>
	/// Start a local game server and connect to a remote lobby server.
	/// </summary>

	static public bool Start (int tcpPort, int udpPort, string fileName, bool useTcpLobby, IPEndPoint remoteLobby)
	{
		return instance.StartRemote(tcpPort, udpPort, fileName, remoteLobby, useTcpLobby);
	}

	/// <summary>
	/// Start a new server.
	/// </summary>

	bool StartLocal (int tcpPort, int udpPort, string fileName, int lobbyPort, bool useTcpLobby)
	{
		// Ensure that everything has been stopped first
		OnDestroy();

		// If there is a lobby port, we should set up the lobby server and/or link first.
		// Doing so will let us inform the lobby that we are starting a new server.

		if (lobbyPort > 0)
		{
			// Create the appropriate lobby
			if (useTcpLobby) mLobby = new TcpLobbyServer();
			else mLobby = new UdpLobbyServer();

			// Start a local lobby server
			if (!mLobby.Start(lobbyPort, 0))
			{
				mLobby = null;
				return false;
			}

			// Create the local lobby link
			mGame.lobbyLink = new LobbyServerLink(mLobby);
		}

		// Start the game server
		if (mGame.Start(tcpPort, udpPort))
		{
			if (!string.IsNullOrEmpty(fileName)) mGame.LoadFrom(fileName);
			return false;
		}

		// Something went wrong -- stop everything
		OnDestroy();
		return true;
	}

	/// <summary>
	/// Start a new server.
	/// </summary>

	bool StartRemote (int tcpPort, int udpPort, string fileName, IPEndPoint remoteLobby, bool useTcpLobby)
	{
		OnDestroy();

		if (remoteLobby != null && remoteLobby.Port > 0)
		{
			if (useTcpLobby)
			{
				mLobby = new TcpLobbyServer();
				mGame.lobbyLink = new TcpLobbyServerLink(remoteLobby);
			}
			else
			{
				mLobby = new UdpLobbyServer();
				mGame.lobbyLink = new UdpLobbyServerLink(remoteLobby);
			}
		}

		if (mGame.Start(tcpPort, udpPort))
		{
			if (!string.IsNullOrEmpty(fileName)) mGame.LoadFrom(fileName);
			return false;
		}

		OnDestroy();
		return true;
	}

	/// <summary>
	/// Stop the server.
	/// </summary>

	static public void Stop () { if (mInstance != null) mInstance.OnDestroy(); }

	/// <summary>
	/// Stop the server, saving the current state to the specified file.
	/// </summary>

	static public void Stop (string fileName)
	{
		if (mInstance != null && mInstance.mGame.isActive)
		{
			mInstance.mGame.SaveTo(fileName);
			Stop();
		}
	}

	/// <summary>
	/// Make the server private by no longer accepting new connections.
	/// </summary>

	static public void MakePrivate () { if (mInstance != null) mInstance.mGame.MakePrivate(); }

	/// <summary>
	/// Make sure that the servers are stopped when the server instance is destroyed.
	/// </summary>

	void OnDestroy ()
	{
		mGame.Stop();

		if (mLobby != null)
		{
			mLobby.Stop();
			mLobby = null;
		}
	}
}
