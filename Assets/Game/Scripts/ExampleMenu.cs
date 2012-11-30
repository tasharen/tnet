//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using UnityEngine;
using TNet;

/// <summary>
/// This script provides a main menu for all examples.
/// The menu is created in Unity's built-in Immediate Mode GUI system.
/// The menu makes use of the following TNet functions:
/// - TNManager.Connect
/// - TNManager.JoinChannel
/// - TNManager.LeaveChannel
/// - TNManager.Disconnect
/// </summary>

[ExecuteInEditMode]
public class ExampleMenu : MonoBehaviour
{
	const float buttonWidth = 150f;
	const float buttonHeight = 30f;

	public string address = "127.0.0.1";
	public int port = 5127;
	public string mainMenu = "Example Menu";
	public string[] examples;

	string mError = "";

	/// <summary>
	/// Show the GUI for the examples.
	/// </summary>

	void OnGUI ()
	{
		if (!TNManager.isConnected)
		{
			ShowConnectMenu();
		}
		else
		{
			if (!Application.isPlaying || Application.loadedLevelName == mainMenu)
			{
				ShowSelectionMenu();
			}
			else if (TNManager.isInChannel)
			{
				ShowExampleMenu();
			}
			ShowDisconnectButton();
		}
	}

	/// <summary>
	/// This menu is shown if the client has not yet connected to the server.
	/// </summary>

	void ShowConnectMenu ()
	{
		Rect rect = new Rect(Screen.width * 0.5f - 200f * 0.5f, Screen.height * 0.5f - 60f, 200f, 170f);

		GUILayout.BeginArea(rect);
		{
			GUILayout.Label("Server Address");
			address = GUILayout.TextField(address, GUILayout.Width(200f));

			if (GUILayout.Button("Connect", GUILayout.Height(30f)))
			{
				// We want to connect to the specified destination when the button is clicked on.
				// "OnNetworkConnect" function will be called sometime later with the result.
				TNManager.Connect(address, port);
				mError = "Connecting...";
			}

			if (!string.IsNullOrEmpty(mError))
			{
				GUI.color = Color.yellow;
				GUILayout.Label(mError);
				GUI.color = Color.white;
			}
		}
		GUILayout.EndArea();
	}

	/// <summary>
	/// This function is called when a connection is either established or it fails to connect.
	/// Connecting to a server doesn't mean that the connected players are now immediately able
	/// to see each other, as they have not yet joined a channel. Only players that have joined
	/// some channel are able to see and interact with other players in the same channel.
	/// You can call TNManager.JoinChannel here if you like, but in this example we let the player choose.
	/// </summary>

	void OnNetworkConnect (bool success, string message)
	{
		mError = message;
	}

	/// <summary>
	/// This menu is shown when a connection has been established and the player has not yet joined any channel.
	/// </summary>

	void ShowSelectionMenu ()
	{
		int count = examples.Length;

		Rect rect = new Rect(
			Screen.width * 0.5f - buttonWidth * 0.5f,
			Screen.height * 0.5f - buttonHeight * 0.5f * count,
			buttonWidth, buttonHeight);

		for (int i = 0; i < count; ++i)
		{
			string sceneName = examples[i];

			if (GUI.Button(rect, sceneName))
			{
				// When a button is clicked, join the specified channel.
				// Whoever creates the channel also sets the scene that will be loaded by everyone who joins.
				// In this case, we are specifying the name of the scene we've just clicked on.
				TNManager.JoinChannel(i + 1, sceneName);
			}
			rect.y += buttonHeight;
		}

		rect.y += 20f;
	}

	/// <summary>
	/// This menu is shown if the player has joined a channel.
	/// </summary>

	void ShowExampleMenu ()
	{
		Rect rect = new Rect(0f, Screen.height - buttonHeight, buttonWidth, buttonHeight);
		
		if (GUI.Button(rect, "Return to Main Menu"))
		{
			// Leaving the channel will cause the "OnNetworkLeftChannel" to be sent out.
			TNManager.LeaveChannel();
		}
	}

	/// <summary>
	/// We want to return to the menu when we leave the channel.
	/// This message is also sent out when we get disconnected.
	/// </summary>

	void OnNetworkLeftChannel ()
	{
		Application.LoadLevel(mainMenu);
	}

	/// <summary>
	/// The disconnect button is only shown if we are currently connected.
	/// </summary>

	void ShowDisconnectButton ()
	{
		Rect rect = new Rect(Screen.width - buttonWidth, Screen.height - buttonHeight, buttonWidth, buttonHeight);

		if (GUI.Button(rect, "Disconnect"))
		{
			// Disconnecting while in some channel will cause "OnNetworkLeftChannel" to be sent out first,
			// followed by "OnNetworkDisconnect". Disconnecting while not in a channel will only trigger
			// "OnNetworkDisconnect".
			TNManager.Disconnect();
		}
	}
}