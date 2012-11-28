using UnityEngine;

/// <summary>
/// A very simple example showing how to use Tasharen Networking.
/// </summary>

public class Example1 : MonoBehaviour
{
	/// <summary>
	/// As soon as the game starts, we want to connect to the server.
	/// </summary>

	void Start ()
	{
		TNManager.Connect("127.0.0.1", 5127);
	}

	/// <summary>
	/// If connection succeeds, join the first channel.
	/// </summary>

	void OnNetworkConnect (bool success, string message)
	{
		if (success) TNManager.JoinChannel(1, "Example1");
		else Debug.LogError(message);
	}

	/// <summary>
	/// We want to return to the menu when we leave the channel.
	/// This message is also sent out when we get disconnected.
	/// </summary>

	void OnNetworkLeftChannel ()
	{
		Application.LoadLevel("Menu");
	}
}