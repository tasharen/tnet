//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;
using TNet;

/// <summary>
/// This example script shows how to create a chat window powered by the Tasharen Network framework.
/// You can see it used in Example Chat.
/// </summary>

[ExecuteInEditMode]
public class ExampleChat : TNBehaviour
{
	Rect mRect;
	string mName = "Guest";
	string mInput = "";
	int mChannelID;

	struct ChatEntry
	{
		public string text;
		public Color color;
	}

	List<ChatEntry> mChatEntries = new List<ChatEntry>();

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
	/// Register event delegates.
	/// </summary>

	void OnEnable ()
	{
		TNManager.onJoinChannel += OnJoinChannel;
		TNManager.onPlayerJoin += OnPlayerJoin;
		TNManager.onPlayerLeave += OnPlayerLeave;
		TNManager.onRenamePlayer += OnRenamePlayer;
		TNManager.onSetServerData += OnSetServerData;
		TNManager.onSetChannelData += OnSetChannelData;
	}

	/// <summary>
	/// Unregister event delegates.
	/// </summary>

	void OnDisable ()
	{
		TNManager.onJoinChannel -= OnJoinChannel;
		TNManager.onPlayerJoin -= OnPlayerJoin;
		TNManager.onPlayerLeave -= OnPlayerLeave;
		TNManager.onRenamePlayer -= OnRenamePlayer;
		TNManager.onSetServerData -= OnSetServerData;
		TNManager.onSetChannelData -= OnSetChannelData;
	}

	void OnSetServerData (string path, DataNode node) { PrintServerData(path); }
	void OnSetChannelData (Channel ch, string path, DataNode node) { PrintChannelData(path); }

	/// <summary>
	/// The list of players in the channel is immediately available upon joining a room.
	/// </summary>

	void OnJoinChannel (int channelID, bool success, string error)
	{
		mChannelID = channelID;
		mName = TNManager.playerName;

		// Show the current configuration
		PrintServerData();
		PrintChannelData();

		var text = "Other players here: ";
		var players = TNManager.GetPlayers(channelID);

		for (int i = 0; i < players.size; ++i)
		{
			if (i > 0) text += ", ";
			text += players.buffer[i].name;
			if (players.buffer[i].id == TNManager.playerID) text += " (you)";
		}
		AddToChat(text, Color.black);
	}

	/// <summary>
	/// Notification of a new player joining the channel.
	/// </summary>

	void OnPlayerJoin (int channelID, Player p)
	{
		AddToChat(p.name + " has joined channel " + channelID, Color.black);
	}

	/// <summary>
	/// Notification of another player leaving the channel.
	/// </summary>

	void OnPlayerLeave (int channelID, Player p)
	{
		AddToChat(p.name + " has left channel " + channelID, Color.black);
	}

	/// <summary>
	/// Notification of a player changing their name.
	/// </summary>

	void OnRenamePlayer (Player p, string previous)
	{
		AddToChat(previous + " is now known as " + p.name, Color.black);
	}

	/// <summary>
	/// This is our chat callback. As messages arrive, they simply get added to the list.
	/// </summary>

	[RFC] void OnChat (int playerID, string text)
	{
		// Figure out who sent the message and add their name to the text
		Player player = TNManager.GetPlayer(playerID);
		Color color = (player.id == TNManager.playerID) ? Color.green : Color.white;
		AddToChat("[" + player.name + "]: " + text, color);
	}

	void SetChannelData (int channelID, string text)
	{
		if (!string.IsNullOrEmpty(text))
		{
			var parts = text.Split(new char[] { '=' }, 2);

			if (parts.Length == 2)
			{
				var key = parts[0].Trim();
				var val = parts[1].Trim();
				var node = new DataNode(key, val);
				if (node.ResolveValue()) TNManager.SetChannelData(channelID, node.name, node.value);
			}
			else Debug.LogWarning("Invalid syntax [" + text + "]. Expected [key = value].");
		}
	}

	/// <summary>
	/// Send the typed message to the server and clear the text.
	/// </summary>

	void Send ()
	{
		if (!string.IsNullOrEmpty(mInput))
		{
			mInput = mInput.Trim();

			if (mInput == "/getServer") PrintServerData();
			else if (mInput.StartsWith("/getServer ")) PrintServerData(mInput.Substring(5));
			else if (mInput.StartsWith("/setServer "))
			{
				if (TNManager.isAdmin) TNManager.SetServerData(mInput.Substring(5));
				else AddToChat("Only server administrators can set server data", Color.red);
			}
			else if (mInput == "/get") PrintChannelData();
			else if (mInput.StartsWith("/get ")) PrintChannelData(mInput.Substring(5));
			else if (mInput.StartsWith("/set ")) SetChannelData(mChannelID, mInput.Substring(5));
			else if (mInput.StartsWith("/exe "))
			{
				// Longer version, won't cause compile errors if RuntimeCode is not imported
				var type = System.Type.GetType("TNet.RuntimeCode");
				if (type != null) type.Invoke("Execute", mInput.Substring(5));
				else Debug.LogError("You need to import the RuntimeCode package first");

				// Shorter version:
				//RuntimeCode.Execute(mInput.Substring(5));
			}
			else tno.Send("OnChat", Target.All, TNManager.playerID, mInput);

			mInput = "";
		}
	}

	/// <summary>
	/// This function draws the chat window.
	/// </summary>

	void OnGUI ()
	{
		float cx = Screen.width * 0.5f;
		float cy = Screen.height * 0.5f;

		GUI.Box(new Rect(Screen.width * 0.5f - 270f, Screen.height * 0.5f - 200f, 540f, 410f), "");

		GUI.Label(new Rect(cx - 140f, cy - 170f, 80f, 24f), "Name");
		if (Application.isPlaying) GUI.SetNextControlName("Name");
		mName = GUI.TextField(new Rect(cx - 70f, cy - 170f, 120f, 24f), mName);

		// Change the player's name when the button gets clicked.
		if (GUI.Button(new Rect(cx + 60f, cy - 170f, 80f, 24f), "Change"))
			TNManager.playerName = mName;

		if (Application.isPlaying) GUI.SetNextControlName("Chat Window");
		mRect = new Rect(cx - 200f, cy - 120f, 400f, 300f);
		GUI.Window(0, mRect, OnGUIWindow, "Chat Window");

		if (Event.current.type == EventType.KeyUp)
		{
			var keyCode = Event.current.keyCode;
			string ctrl = GUI.GetNameOfFocusedControl();

			if (ctrl == "Name")
			{
				if (keyCode == KeyCode.Return)
				{
					// Enter key pressed on the input field for the player's nickname -- change the player's name.
					TNManager.playerName = mName;
					if (Application.isPlaying) GUI.FocusControl("Chat Window");
				}
			}
			else if (ctrl == "Chat Input")
			{
				if (keyCode == KeyCode.Return)
				{
					Send();
					if (Application.isPlaying) GUI.FocusControl("Chat Window");
				}
			}
			else if (keyCode == KeyCode.Return)
			{
				// Enter key pressed -- give focus to the chat input
				if (Application.isPlaying) GUI.FocusControl("Chat Input");
			}
			else if (keyCode == KeyCode.Slash)
			{
				mInput = "/";
				if (Application.isPlaying) GUI.FocusControl("Chat Input");
			}
		}

#if UNITY_4_3 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7
		// Unity bug: http://forum.unity3d.com/threads/dest-m_multiframeguistate-m_namedkeycontrollist.158676/page-2
		GUI.SetNextControlName(gameObject.GetHashCode().ToString());
		Rect bounds = new Rect(-10, -10, 0, 0);
		GUI.TextField(bounds, "", 0);
#endif
	}

	/// <summary>
	/// This function draws the chat window and the chat messages.
	/// </summary>

	void OnGUIWindow (int id)
	{
		if (Application.isPlaying) GUI.SetNextControlName("Chat Input");
		mInput = GUI.TextField(new Rect(6f, mRect.height - 30f, 328f, 24f), mInput);

		if (GUI.Button(new Rect(334f, mRect.height - 31f, 60f, 26f), "Send"))
		{
			Send();
			if (Application.isPlaying) GUI.FocusControl("Chat Window");
		}

		GUI.BeginGroup(new Rect(2f, 20f, 382f, 254f));
		{
			Rect rect = new Rect(4f, 244f, 382f, 300f);

			for (int i = mChatEntries.size; i > 0; )
			{
				var ent = mChatEntries.buffer[--i];
				rect.y -= GUI.skin.label.CalcHeight(new GUIContent(ent.text), 382f);
				GUI.color = ent.color;
				GUI.Label(rect, ent.text, GUI.skin.label);
				if (rect.y < 0f) break;
			}
			GUI.color = Color.white;
		}
		GUI.EndGroup();
	}

	/// <summary>
	/// Helper function that prints the specified config node and its children.
	/// </summary>

	void PrintConfig (string path, DataNode node, Color color)
	{
		if (!string.IsNullOrEmpty(path)) node = node.GetHierarchy(path);

		if (node != null)
		{
			var lines = node.ToString().Split(new char[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
			foreach (string s in lines) AddToChat(s.Replace("\t", "    "), color);
		}
		else AddToChat("[" + path + "] has not been set", color);
	}

	void PrintServerData (string path = "")
	{
		AddToChat("Server Data (" + path + "):", Color.yellow);
		PrintConfig(path, TNManager.serverData, Color.yellow);
	}

	void PrintChannelData (string path = "")
	{
		var ch = TNManager.GetChannel(mChannelID);
		AddToChat("Channel #" + ch.id + " Data (" + path + "):", Color.green);
		PrintConfig(path, ch.dataNode, Color.green);
	}
}
