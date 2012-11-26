using UnityEngine;
using TNet;
using System.IO;

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
				TNManager.JoinChannel(123, null, true);
			}
		}
		else if (Input.GetKeyDown(KeyCode.I))
		{
			TNManager.Create(spawnObject);
		}
	}

	void OnGUI ()
	{
		GUILayout.Label("Connected: " + TNManager.isConnected);
		GUILayout.Label("Hosting: " + TNManager.isHosting);
		GUILayout.Label("Ping: " + TNManager.ping + " ms");
	}
}