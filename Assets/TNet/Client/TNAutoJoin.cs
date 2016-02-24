//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;
using UnityTools = TNet.UnityTools;

namespace TNet
{
/// <summary>
/// Extremely simplified "join a server" functionality. Attaching this script will
/// make it possible to automatically join a remote server when the game starts.
/// It's best to place this script in a clean scene with a message that displays
/// a "Connecting, please wait..." message.
/// </summary>

public class TNAutoJoin : MonoBehaviour
{
	static public TNAutoJoin instance;

	public string serverAddress = "127.0.0.1";
	public int serverPort = 5127;
	public string firstLevel = "Example 1";
	public int channelID = 1;
	public bool persistent = false;
	public string disconnectLevel;
	public bool allowUDP = true;
	public bool connectOnStart = true;

	/// <summary>
	/// Set the instance so this script can be easily found.
	/// </summary>

	void Awake ()
	{
		if (instance == null)
		{
			instance = this;
			DontDestroyOnLoad(gameObject);
		}
	}

	void OnEnable ()
	{
		TNManager.onConnect += OnConnect;
		TNManager.onDisconnect += OnDisconnect;
	}

	void OnDisable ()
	{
		TNManager.onConnect -= OnConnect;
		TNManager.onDisconnect -= OnDisconnect;
	}

	/// <summary>
	/// Connect to the server if requested.
	/// </summary>

	void Start () { if (connectOnStart) Connect(); }

	/// <summary>
	/// Connect to the server.
	/// </summary>

	public void Connect ()
	{
		// We don't want mobile devices to dim their screen and go to sleep while the app is running
		Screen.sleepTimeout = SleepTimeout.NeverSleep;

		// Connect to the remote server
		TNManager.Connect(serverAddress, serverPort);
	}

	/// <summary>
	/// On success -- join a channel.
	/// </summary>

	void OnConnect (bool result, string message)
	{
		if (result)
		{
			// Make it possible to use UDP using a random port
			if (allowUDP) TNManager.StartUDP(Random.Range(10000, 50000));
			TNManager.JoinChannel(channelID, firstLevel, persistent, 10000, null);
		}
		else Debug.LogError(message);
	}

	/// <summary>
	/// Disconnected? Go back to the menu.
	/// </summary>

	void OnDisconnect ()
	{
#if UNITY_4_6 || UNITY_4_7 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2
		if (!string.IsNullOrEmpty(disconnectLevel) && Application.loadedLevelName != disconnectLevel)
			Application.LoadLevel(disconnectLevel);
#else
		if (!string.IsNullOrEmpty(disconnectLevel) &&
			UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != disconnectLevel)
			UnityEngine.SceneManagement.SceneManager.LoadScene(disconnectLevel);
#endif
	}
}
}
