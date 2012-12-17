//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using UnityEngine;
using TNet;
using System.IO;

/// <summary>
/// Tasharen Network server tailored for Unity.
/// </summary>

[AddComponentMenu("TNet/Network Server (internal)")]
public class TNServerInstance : MonoBehaviour
{
	static TNServerInstance mInstance;

	TcpServer mServer = new TcpServer();

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
	/// Server name is static and always available.
	/// </summary>

	static public string serverName = "Server";

	/// <summary>
	/// Whether the server instance is currently active.
	/// </summary>

	static public bool isActive { get { return (mInstance != null) && mInstance.mServer.isActive; } }

	/// <summary>
	/// Whether the server is currently listening for incoming connections.
	/// </summary>

	static public bool isListening { get { return (mInstance != null) && mInstance.mServer.isListening; } }

	/// <summary>
	/// Port used to listen for incoming TCP connections.
	/// </summary>

	static public int listeningPort { get { return (mInstance != null) ? mInstance.mServer.listeningPort : 0; } }

	/// <summary>
	/// How many players are currently connected to the server.
	/// </summary>

	static public int playerCount { get { return (mInstance != null) ? mInstance.mServer.playerCount : 0; } }

	/// <summary>
	/// Server's local address on the network. For example: 192.168.1.10
	/// </summary>

	static public string localAddress { get { return (mInstance != null) ? mInstance.mServer.localAddress : "0.0.0.0:0"; } }

	/// <summary>
	/// Start a local server instance listening to the specified port.
	/// </summary>

	static public bool Start (int tcpPort) { return instance.mServer.Start(tcpPort, 0); }

	/// <summary>
	/// Start a local server instance listening to the specified port.
	/// </summary>

	static public bool Start (int tcpPort, int udpPort) { return instance.mServer.Start(tcpPort, udpPort); }

	/// <summary>
	/// Start a local server instance listening to the specified port and loading the saved data from the specified file.
	/// </summary>

	static public bool Start (int tcpPort, int udpPort, string fileName)
	{
		if (instance.mServer.Start(tcpPort, udpPort))
		{
			instance.mServer.LoadFrom(fileName);
			return true;
		}
		return false;
	}

	/// <summary>
	/// Stop the server.
	/// </summary>

	static public void Stop ()
	{
		if (mInstance != null)
		{
			mInstance.mServer.Stop();
		}
	}

	/// <summary>
	/// Stop the server, saving the current state to the specified file.
	/// </summary>

	static public void Stop (string fileName)
	{
		if (mInstance != null && mInstance.mServer.isActive)
		{
			mInstance.mServer.SaveTo(fileName);
			mInstance.mServer.Stop();
		}
	}

	/// <summary>
	/// Make the server private by no longer accepting new connections.
	/// </summary>

	static public void MakePrivate () { if (mInstance != null) mInstance.mServer.MakePrivate(); }
}