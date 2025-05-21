using Gen = System.Collections.Generic;
using UnityEngine;
using TNet;
using System;

/// <summary>
/// Once the test is started by typing 'start', this example creates a lot of objects on the server, and all objects start spamming the server with various packets.
/// </summary>

[ExecuteInEditMode]
public class ExampleStressTest : MonoBehaviour
{
	static public ExampleStressTest instance { get; private set; }

	[NonSerialized] Rect mRect;
	[NonSerialized] string mInput = "";

	struct ChatEntry
	{
		public string text;
		public Color color;
	}

	[NonSerialized] List<ChatEntry> mChatEntries = new List<ChatEntry>();
	[NonSerialized] int mChannelID = 0;

	/// <summary>
	/// Add a new chat entry.
	/// </summary>

	void AddToChat (string text, Color color)
	{
		var ent = new ChatEntry();
		ent.text = text;
		ent.color = color;
		lock (mChatEntries) mChatEntries.Add(ent);
	}

	/// <summary>
	/// This function draws the chat window.
	/// </summary>

	void OnGUI ()
	{
		float cx = Screen.width * 0.5f;
		float cy = Screen.height * 0.5f;

		// Change the player's name when the button gets clicked.
		if (Application.isPlaying) GUI.SetNextControlName("Events");
		mRect = new Rect(cx - 400f, cy - 300f, 800f, 600f);
		GUI.Window(0, mRect, OnGUIWindow, "Events");

		if (Event.current.type == EventType.KeyUp)
		{
			var keyCode = Event.current.keyCode;
			string ctrl = GUI.GetNameOfFocusedControl();

			if (ctrl == "Chat Input")
			{
				if (keyCode == KeyCode.Return)
				{
					Send();
					if (Application.isPlaying) GUI.FocusControl("Events");
				}
			}
			else if (keyCode == KeyCode.Return)
			{
				// Enter key pressed -- give focus to the chat input
				if (Application.isPlaying) GUI.FocusControl("Chat Input");
			}
		}
	}

	void Send ()
	{
		if (mInput == "start")
		{
			if (!instance)
			{
				instance = this;
				AddToChat("Test started", Color.yellow);
				for (int i = 0; i < 1000; ++i) TNManager.Instantiate(mChannelID, "OnStressObj", null, false);
			}
		}
		else if (mInput == "stop")
		{
			instance = null;
			AddToChat("Test stopped", Color.yellow);
		}
		else AddToChat(mInput, Color.white);

		mInput = "";
	}

	/// <summary>
	/// This function draws the chat window and the chat messages.
	/// </summary>

	void OnGUIWindow (int id)
	{
		if (Application.isPlaying) GUI.SetNextControlName("Chat Input");
		mInput = GUI.TextField(new Rect(6f, mRect.height - 30f, 728f, 24f), mInput);

		if (GUI.Button(new Rect(734f, mRect.height - 31f, 60f, 26f), "Send"))
		{
			Send();
			if (Application.isPlaying) GUI.FocusControl("Events");
		}

		GUI.BeginGroup(new Rect(2f, 20f, 782f, 554f));
		{
			var rect = new Rect(4f, 544f, 782f, 600f);

			for (int i = mChatEntries.size; i > 0;)
			{
				var ent = mChatEntries.buffer[--i];
				rect.y -= GUI.skin.label.CalcHeight(new GUIContent(ent.text), 782f);
				GUI.color = ent.color;
				GUI.Label(rect, ent.text, GUI.skin.label);
				if (rect.y < 0f) break;
			}
			GUI.color = Color.white;
		}
		GUI.EndGroup();
	}

	void OnEnable ()
	{
		if (Application.isPlaying)
		{
			TNManager.onJoinChannel += OnJoinChannel;
			TNManager.onError += OnError;
			Application.logMessageReceivedThreaded += LogCallback;
		}
	}

	void OnDisable ()
	{
		if (Application.isPlaying)
		{
			TNManager.onJoinChannel -= OnJoinChannel;
			TNManager.onError -= OnError;
			Application.logMessageReceivedThreaded -= LogCallback;
		}
	}

	void OnError (string msg) { AddToChat(msg, Color.red); }

	void LogCallback (string condition, string stackTrace, LogType type)
	{
		if (type != LogType.Log)
		{
			var c = (type == LogType.Error) ? Color.red : Color.yellow;
			AddToChat(type.ToString() + ": " + condition, c);
			var lines = stackTrace.Split('\n');
			foreach (var line in lines) AddToChat(line, c);
		}
	}

	void OnJoinChannel (int channelID, bool success, string message)
	{
		mChannelID = channelID;
	}

	void Start ()
	{
		InvokeRepeating("InfrequentUpdate", 1f, 1f);
	}

	void InfrequentUpdate ()
	{
		var c = ExampleStressObj.count;
		if (c > 0) AddToChat($"{c} objects. {TNManager.sentBytes} sent, {TNManager.receivedBytes} received. {ExampleStressObj.GetUpdateCount()} updates.", Color.white);
	}

	[System.NonSerialized] static int mID = 0;

	[RCC]
	static GameObject OnStressObj (GameObject prefab)
	{
		var go = prefab.Instantiate();
		go.name = "Obj #" + (mID++) + " (player " + TNManager.packetSourceID + ")";
		go.AddComponent<ExampleStressObj>();
		return go;
	}
}
