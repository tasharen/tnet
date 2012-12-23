//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using System;
using TNet;
using System.IO;

/// <summary>
/// This is an example of a stand-alone server. You don't need Unity in order to compile and run it.
/// </summary>

public class ServerMain
{
	static int Main ()
	{
		// The game server's ports don't really matter if you use the discovery server.
		// If you're not, you will want to remember the first value (TCP port).
		GameServer server = new GameServer();
		server.name = "Stand-alone Server";
		server.Start(5127, 5128);
		server.LoadFrom("server.dat");

		// Server discovery port should match the discovery port on the client (TNDiscoveryClient).
		//DiscoveryServer discovery = new DiscoveryServer();
		//discovery.localServer = server;
		//discovery.Start(5129);

		for (; ; )
		{
			Console.WriteLine("Press 'q' followed by 'Enter' when you want to quit.");
			string command = Console.ReadLine();
			if (command == "q") break;
		}
		Console.WriteLine("Shutting down...");
		//discovery.Stop();
		server.SaveTo("server.dat");
		server.Stop();
		return 0;
	}
}