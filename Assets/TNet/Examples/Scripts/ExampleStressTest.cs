using UnityEngine;
using TNet;
using System.Collections;

[ExecuteInEditMode]
public class ExampleStressTest : TNBehaviour
{
	Rect mRect;

	struct LogEntry
	{
		public string text;
		public Color color;
	}
	List<LogEntry> mChatEntries = new List<LogEntry>();

	void OnNetworkError (string err) { Debug.LogError(err); }

	void OnNetworkJoinChannel (bool success, string msg)
	{
		if (success) Debug.Log("Joined the channel");
		else Debug.LogError(msg);
	}

	void OnNetworkDisconnect () { Debug.LogWarning("Disconnected!"); }

	void Awake ()
	{
		Application.RegisterLogCallback(MyCallback);
	}

	void MyCallback (string condition, string stacktrace, UnityEngine.LogType type)
	{
		if (type == LogType.Error)
		{
			Log(condition, Color.red);
		}
		else if (type == LogType.Warning)
		{
			Log(condition, Color.yellow);
		}
		else Log(condition, Color.white);
	}

	void Start ()
	{
		if (Application.isPlaying)
		{
			StartCoroutine(PeriodicCreate());
			StartCoroutine(Send1());
			StartCoroutine(Send2());
			StartCoroutine(Send3());
			StartCoroutine(Send4());
		}
	}

	/// <summary>
	/// Add a new chat entry.
	/// </summary>

	void Log (string text, Color color)
	{
		LogEntry ent = new LogEntry();
		ent.text = text;
		ent.color = color;
		mChatEntries.Add(ent);
		if (mChatEntries.size > 50)
			mChatEntries.RemoveAt(0);
	}

	/// <summary>
	/// This function draws the chat window.
	/// </summary>

	void OnGUI ()
	{
		GUI.SetNextControlName("Log Window");
		mRect = new Rect(Screen.width * 0.5f - 270f, Screen.height * 0.5f - 200f, 540f, 410f);
		GUI.Window(0, mRect, OnGUIWindow, "Log Window");
	}

	/// <summary>
	/// This function draws the chat window and the chat messages.
	/// </summary>

	void OnGUIWindow (int id)
	{
		for (int i = mChatEntries.size; i > 0; )
		{
			LogEntry ent = mChatEntries[--i];
			GUI.color = ent.color;
			GUILayout.Label(ent.text);
			GUILayout.Space(-8f);
		}
	}

	[RFC] void First (string a) { if (TNManager.isJoiningChannel) Debug.Log("First: " + a); }
	[RFC] void Second (string a, int b) { if (TNManager.isJoiningChannel) Debug.Log("Second: " + a + ", " + b); }
	[RFC] void Third (byte ch) { if (TNManager.isJoiningChannel) Debug.Log("Third: " + ch); }
	[RFC] void Fourth (string a, string b, string c) { if (TNManager.isJoiningChannel) Debug.Log("Fourth: " + c); }

	IEnumerator PeriodicCreate ()
	{
		while (TNManager.isConnected)
		{
			if (!TNManager.isJoiningChannel)
				TNManager.Create("Stress Test Object", true);

			yield return new WaitForSeconds(Random.Range(0.04f, 0.06f));

			if (!TNManager.isJoiningChannel)
				TNManager.Create("Stress Test Object", false);

			yield return new WaitForSeconds(Random.Range(0.04f, 0.06f));
		}
	}

	IEnumerator Send1 ()
	{
		while (TNManager.isConnected)
		{
			if (!TNManager.isJoiningChannel)
				tno.Send("First", Target.Others, "Testing1");
			yield return new WaitForSeconds(Random.Range(0.04f, 0.06f));
		}
	}

	IEnumerator Send2 ()
	{
		while (TNManager.isConnected)
		{
			if (!TNManager.isJoiningChannel)
				tno.Send("Second", Target.OthersSaved, "Testing Function 2", TNManager.playerID);
			yield return new WaitForSeconds(Random.Range(0.04f, 0.06f));
		}
	}

	IEnumerator Send3 ()
	{
		while (TNManager.isConnected)
		{
			if (!TNManager.isJoiningChannel)
				tno.Send("Third", Target.AllSaved, (byte)3);
			yield return new WaitForSeconds(Random.Range(0.04f, 0.06f));
		}
	}

	IEnumerator Send4 ()
	{
		while (TNManager.isConnected)
		{
			if (!TNManager.isJoiningChannel)
				tno.Send("Fourth", Target.AllSaved, "Long String 1", "Longer String 2", "Still longer string 3");
			yield return new WaitForSeconds(Random.Range(0.04f, 0.06f));
		}
	}
}
