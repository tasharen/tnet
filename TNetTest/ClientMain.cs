#if !UNITY_EDITOR

using System;
using TNet;
using System.IO;
using System.Threading;

public class TNetTest
{
	static Client client;
	static int test = 0;

	// TODO: Now that the server is ready, it's time to work on TNObject in Unity.

	static void ThreadFunction ()
	{
		for (; ; )
		{
			client.ProcessPackets();

			if (test == 1)
			{
				Console.WriteLine("Large data test...");

				FileStream stream = new FileStream("../../sig.png", FileMode.Open);
				byte[] data = new byte[stream.Length];
				stream.Read(data, 0, (int)stream.Length);
				stream.Close();
				stream.Dispose();
				
				BinaryWriter writer = client.BeginSend(Packet.ForwardToAllBuffered);
				writer.Write(0);
				writer.Write(data);
				client.EndSend();
			}
			else if (test == 2)
			{
				client.BeginSend(Packet.RequestCloseChannel);
				client.EndSend();
			}
			test = 0;
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
		client.onSetHost = OnSetHost;
		client.onChannelChanged = OnChannelChanged;
		client.onRenamePlayer = OnRenamePlayer;
		client.onCreate = OnCreateObject;
		client.onDestroy = OnDestroyObject;
		client.onForwardedPacket = OnForwardedPacket;
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
			else if (command == "s")
			{
				test = 1;
			}
			else if (command == "c")
			{
				test = 2;
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
		client.JoinChannel(123, null, true);
	}

	static void OnDisconnect ()
	{
		Console.WriteLine("Disconnected");
	}

	static void OnPlayerJoined (ClientPlayer p)
	{
		Console.WriteLine("Player joined: " + p.name);
	}

	static void OnPlayerLeft (ClientPlayer p)
	{
		Console.WriteLine("Player left: " + p.name);
	}

	static void OnSetHost (bool hosting)
	{
		Console.WriteLine("Hosting: " + hosting);
	}

	static void OnChannelChanged (bool isInChannel, string message)
	{
		Console.WriteLine("Channel: " + isInChannel + " (" + message + ")");
	}

	static void OnRenamePlayer (ClientPlayer p, string previous)
	{
		Console.WriteLine(previous + " is now known as " + p.name);
	}

	static void OnCreateObject (int objectID, int objID, BinaryReader reader)
	{
		Console.WriteLine("Create " + objectID + " " + objID);
	}

	static void OnDestroyObject (int objID)
	{
		Console.WriteLine("Destroy " + objID);
	}

	static void OnForwardedPacket (BinaryReader reader)
	{
		Console.WriteLine("Custom (" + reader.BaseStream.Length + " bytes)");
	}
}
#endif