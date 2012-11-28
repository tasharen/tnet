//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using UnityEngine;
using TNet;

/// <summary>
/// This example script shows how to create a chat window powered by the Tasharen Network framework.
/// You can see it used in Example 2.
/// </summary>

[ExecuteInEditMode]
public class ExampleChat : TNBehaviour
{
	Rect mRect;
	string mName = "Guest";
	string mInput = "";

	struct ChatEntry
	{
		public string text;
		public Color color;
	}
	BetterList<ChatEntry> mChatEntries = new BetterList<ChatEntry>();

	/// <summary>
	/// Add a new chat entry.
	/// </summary>

	void AddToChat (string text, Color color)
	{
		ChatEntry ent = new ChatEntry();
		ent.text = text;
		ent.color = color;
		mChatEntries.Add(ent);
	}

	/// <summary>
	/// The list of players in the channel is immediately available upon joining a room.
	/// </summary>

	void OnNetworkJoinChannel (bool success, string error)
	{
		mName = TNManager.playerName;

		string text = "Players here: ";
		BetterList<ClientPlayer> players = TNManager.players;
		
		for (int i = 0; i < players.size; ++i)
		{
			if (i > 0) text += ", ";
			text += players[i].name;
			if (players[i].id == TNManager.playerID) text += " (you)";
		}
		AddToChat(text, Color.grey);
	}

	/// <summary>
	/// Notification of a new player joining the channel.
	/// </summary>

	void OnNetworkPlayerJoined (ClientPlayer p)
	{
		AddToChat(p.name + " has joined the channel.", Color.grey);
	}

	/// <summary>
	/// Notification of another player leaving the channel.
	/// </summary>

	void OnNetworkPlayerLeft (ClientPlayer p)
	{
		AddToChat(p.name + " has left the channel.", Color.grey);
	}

	/// <summary>
	/// Notification of a player changing their name.
	/// </summary>

	void OnNetworkPlayerRenamed (ClientPlayer p, string previous)
	{
		AddToChat(previous + " is now known as " + p.name, Color.grey);
	}

	/// <summary>
	/// This is our chat callback. As messages arrive, they simply get added to the list.
	/// </summary>

	[RFC] void OnChat (int playerID, string text)
	{
		// Figure out who sent the message and add their name to the text
		ClientPlayer player = TNManager.GetPlayer(playerID);
		Color color = (player.id == TNManager.playerID) ? Color.green : Color.white;
		AddToChat("[" + player.name + "]: " + text, color);
	}

	/// <summary>
	/// This function draws the chat window.
	/// </summary>

	void OnGUI ()
	{
		float cx = Screen.width * 0.5f;
		float cy = Screen.height * 0.5f;

		GUI.Label(new Rect(cx - 140f, cy - 200f, 80f, 24f), "Nickname");
		GUI.SetNextControlName("Nickname");
		mName = GUI.TextField(new Rect(cx - 70f, cy - 200f, 120f, 24f), mName);

		if (GUI.Button(new Rect(cx + 60f, cy - 200f, 80f, 24f), "Change"))
		{
			// Change the player's name when the button gets clicked.
			TNManager.playerName = mName;
		}

		GUI.SetNextControlName("Chat Window");
		mRect = new Rect(cx - 200f, cy - 150f, 400f, 300f);
		GUI.Window(0, mRect, OnGUIWindow, "Chat Window");

		if (Event.current.keyCode == KeyCode.Return && Event.current.type == EventType.KeyUp)
		{
			string ctrl = GUI.GetNameOfFocusedControl();

			if (ctrl == "Nickname")
			{
				// Enter key pressed on the input field for the player's nickname -- change the player's name.
				TNManager.playerName = mName;
				GUI.FocusControl("Chat Window");
			}
			else if (ctrl == "Chat Input")
			{
				// Enter key pressed while typing a chat message -- send it to the server
				tno.Send("OnChat", Target.All, TNManager.playerID, mInput);
				GUI.FocusControl("Chat Window");
				mInput = "";
			}
			else
			{
				// Enter key pressed -- give focus to the chat input
				GUI.FocusControl("Chat Input");
			}
		}
	}

	/// <summary>
	/// This function draws the chat window and the chat messages.
	/// </summary>

	void OnGUIWindow (int id)
	{
		GUI.SetNextControlName("Chat Input");
		mInput = GUI.TextField(new Rect(6f, mRect.height - 30f, 388f, 24f), mInput);

		GUI.BeginGroup(new Rect(2f, 20f, 382f, 254f));
		{
			Rect rect = new Rect(4f, 244f, 382f, 300f);

			for (int i = mChatEntries.size; i > 0; )
			{
				ChatEntry ent = mChatEntries[--i];
				rect.y -= GUI.skin.label.CalcHeight(new GUIContent(ent.text), 382f);
				GUI.color = ent.color;
				GUI.Label(rect, ent.text, GUI.skin.label);
				if (rect.y < 0f) break;
			}
			GUI.color = Color.white;
		}
		GUI.EndGroup();
	}
}