//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using UnityEngine;
using TNet;

/// <summary>
/// Tasharen Network server tailored for Unity.
/// </summary>

[AddComponentMenu("TNet/Network Server (internal)")]
public class TNServerInstance : MonoBehaviour
{
	static TNServerInstance mInstance;

	Server mServer = new Server();

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
	/// Whether the server instance is currently active.
	/// </summary>

	static public bool isActive { get { return (mInstance != null) && mInstance.mServer.isActive; } }

	/// <summary>
	/// Whether the server is currently listening for incoming connections.
	/// </summary>

	static public bool isListening { get { return (mInstance != null) && mInstance.mServer.isListening; } }

	/// <summary>
	/// Start a local server instance listening to the specified port.
	/// </summary>

	static public bool Start (int port) { return instance.mServer.Start(port); }

	/// <summary>
	/// Start a local server instance listening to the specified port and loading the saved data from the specified file.
	/// </summary>

	static public bool Start (int port, string fileName)
	{
		if (instance.mServer.Start(port))
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