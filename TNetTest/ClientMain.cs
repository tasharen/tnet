using System;
using TNet;
using System.IO;
using System.Threading;

public class ClientMain
{
	static TcpClient client;
	static int test = 0;

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
				
				BinaryWriter writer = client.BeginSend(Packet.ForwardToAllSaved);
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
		client = new TcpClient();
		client.onError = OnError;
		client.onConnect = OnConnect;
		client.onDisconnect = OnDisconnect;
		client.onPlayerJoined = OnPlayerJoined;
		client.onPlayerLeft = OnPlayerLeft;
		client.onSetHost = OnSetHost;
		client.onJoinChannel = OnJoinChannel;
		client.onRenamePlayer = OnRenamePlayer;
		client.onCreate = OnCreateObject;
		client.onDestroy = OnDestroyObject;
		client.onForwardedPacket = OnForwardedPacket;
		//client.Start(5129);
		client.Connect("127.0.0.1", 5137);

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
			else if (command == "j")
			{
				client.JoinChannel(123, "Some Level", true, null);
			}
			else if (command == "l")
			{
				client.LeaveChannel();
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

	static void OnSetHost (bool hosting)
	{
		Console.WriteLine("Hosting: " + hosting);
	}

	static void OnJoinChannel (bool isInChannel, string message)
	{
		Console.WriteLine("Channel: " + isInChannel + " (" + message + ")");
	}

	static void OnRenamePlayer (Player p, string previous)
	{
		Console.WriteLine(previous + " is now known as " + p.name);
	}

	static void OnCreateObject (int objectID, uint objID, BinaryReader reader)
	{
		Console.WriteLine("Create " + objectID + " " + objID);
	}

	static void OnDestroyObject (uint objID)
	{
		Console.WriteLine("Destroy " + objID);
	}

	static void OnForwardedPacket (BinaryReader reader)
	{
		Console.WriteLine("Custom (" + reader.BaseStream.Length + " bytes)");
	}
}