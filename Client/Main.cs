using System;
using TNet;
using System.IO;
using System.Threading;

public class TNetTest
{
	static Client client;

	static void ThreadFunction ()
	{
		for (; ; )
		{
			client.ProcessPackets();
			Thread.Sleep(1);
		}
	}

	static int Main ()
	{
		client = new Client();
		client.onError = OnError;
		client.onConnect = OnConnect;
		client.onDisconnect = OnDisconnect;
		client.onPlayerJoined = OnPlayerJoined;
		client.onPlayerLeft = OnPlayerLeft;
		client.onChannelChanged = OnChannelChanged;
		client.onRenamePlayer = OnRenamePlayer;
		client.onCreate = OnCreateObject;
		client.onDestroy = OnDestroyView;
		client.onCustomPacket = OnCustomPacket;
		client.Connect("127.0.0.1", 5127);

		Thread thread = new Thread(ThreadFunction);
		thread.Start();

		for (; ; )
		{
			Console.WriteLine("Command: ");
			string command = Console.ReadLine();
			
			if (command == "q")
			{
				thread.Abort();
				break;
			}
		}
		Console.WriteLine("Shutting down...");
		client.Disconnect();
		return 0;
	}

	static void OnError (string err) { Console.WriteLine("Error: " + err); }

	static void OnConnect (bool success, string message)
	{
		Console.WriteLine("Connected: " + success + " (" + message + ")");
	}

	static void OnDisconnect ()
	{
		Console.WriteLine("Disconnected");
	}

	static void OnPlayerJoined (Player p)
	{
		Console.WriteLine("Player joined: " + p.name);
	}

	static void OnPlayerLeft (Player p)
	{
		Console.WriteLine("Player left: " + p.name);
	}

	static void OnChannelChanged (bool isInChannel, string message)
	{
		Console.WriteLine("Channel: " + isInChannel + " (" + message + ")");
	}

	static void OnRenamePlayer (Player p, string previous)
	{
		Console.WriteLine(previous + " is now known as " + p.name);
	}

	static void OnCreateObject (int objectID, int viewID, BinaryReader reader)
	{
		Console.WriteLine("Create " + objectID + " " + viewID);
	}

	static void OnDestroyView (int viewID)
	{
		Console.WriteLine("Destroy " + viewID);
	}

	static void OnCustomPacket (int packetID, BinaryReader reader)
	{
		Console.WriteLine("Custom: " + packetID);
	}
}