using UnityEngine;
using TNet;
using System.IO;

public class Test : MonoBehaviour
{
	public string address = "127.0.0.1";
	public int port = 5127;

	void Update ()
	{
		if (Input.GetKeyDown(KeyCode.C))
		{
			if (TNManager.isConnected)
			{
				TNManager.instance.client.Disconnect();
			}
			else
			{
				TNManager.instance.client.Connect(address, port);
			}
		}
		else if (Input.GetKeyDown(KeyCode.J))
		{
			TNManager.instance.client.JoinChannel(123, null, true);
		}
	}
}