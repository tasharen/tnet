using UnityEngine;
using TNet;
using System.IO;

[RequireComponent(typeof(TNObject))]
public class Test : MonoBehaviour
{
	public string address = "127.0.0.1";
	public int port = 5127;
	public GameObject spawnObject;

	void Update ()
	{
		if (Input.GetKeyDown(KeyCode.C))
		{
			if (TNManager.isConnected)
			{
				TNManager.Disconnect();
			}
			else
			{
				TNManager.Connect(address, port);
			}
		}
		else if (Input.GetKeyDown(KeyCode.J))
		{
			if (TNManager.isInChannel)
			{
				TNManager.LeaveChannel();
			}
			else
			{
				TNManager.JoinChannel(123, "Game");
			}
		}
		else if (Input.GetKeyDown(KeyCode.I))
		{
			TNManager.Create(spawnObject);
		}
		else if (Input.GetKeyDown(KeyCode.R))
		{
			TNManager.LoadLevel("Red");
		}
	}

	void OnGUI ()
	{
		GUILayout.Label("Connected: " + TNManager.isConnected);
		GUILayout.Label("Hosting: " + TNManager.isHosting);
		GUILayout.Label("Ping: " + TNManager.ping + " ms");
	}

	/// <summary>
	/// Connection result notification.
	/// </summary>

	void OnNetworkConnect (bool success, string message)
	{
		Debug.Log("Connected: " + success);
	}

	/// <summary>
	/// Notification that happens when the client gets disconnected from the server.
	/// </summary>

	void OnNetworkDisconnect ()
	{
		Debug.Log("Disconnected");
	}

	/// <summary>
	/// Notification of changing channels. If 'isInChannel' is 'false', then the player is not in any channel.
	/// </summary>

	void OnNetworkJoinChannel (bool success, string message)
	{
		Debug.Log("Joined: " + success);
	}

	/// <summary>
	/// Notification of leaving the channel.
	/// </summary>

	void OnNetworkLeftChannel ()
	{
		Application.LoadLevel("Menu");
	}

	/// <summary>
	/// Notification of a new player joining the channel.
	/// </summary>

	void OnNetworkPlayerJoined (ClientPlayer p)
	{
		Debug.Log("Player joined: " + p.name);
	}

	/// <summary>
	/// Notification of another player leaving the channel.
	/// </summary>

	void OnNetworkPlayerLeft (ClientPlayer p)
	{
		Debug.Log("Player left: " + p.name);
	}

	/// <summary>
	/// Notification of a player being renamed.
	/// </summary>

	void OnNetworkRenamePlayer (ClientPlayer p, string previous)
	{
		Debug.Log(previous + " is now known as " + p.name);
	}

	/// <summary>
	/// Error notification.
	/// </summary>

	void OnNetworkError (string err) { Debug.LogError(err); }
}