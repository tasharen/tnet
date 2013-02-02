//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using System;
using TNet;
using System.IO;
using System.Net.Sockets;
using System.Threading;

/// <summary>
/// This is an example of a stand-alone server. You don't need Unity in order to compile and run it.
/// </summary>

public class ServerMain
{
	static void OnPortOpened (UPnP up, int port, ProtocolType protocol, bool success)
	{
		if (success)
		{
			Console.WriteLine(protocol.ToString().ToUpper() + " port " + port + " was opened successfully.");
		}
		else
		{
			Console.WriteLine("Unable to open " + protocol.ToString().ToUpper() + " port " + port);
		}
	}

	static void OnPortClosed (UPnP up, int port, ProtocolType protocol, bool success)
	{
		if (success)
		{
			Console.WriteLine(protocol.ToString().ToUpper() + " port " + port + " was closed successfully.");
		}
		else
		{
			Console.WriteLine("Unable to close " + protocol.ToString().ToUpper() + " port " + port);
		}
	}

	static int Main ()
	{
		Console.WriteLine("Locating the gateway...");
		{
			// Open up ports on the gateway
			UPnP up = new UPnP();
			up.OpenTCP(5127, OnPortOpened);
			up.OpenUDP(5128, OnPortOpened);

			// Wait for the gateway to be found
			while (up.status == UPnP.Status.Searching) Thread.Sleep(1);

			Console.WriteLine("External IP address: " + up.externalAddress);

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
				Console.WriteLine("Press 'q' followed by ENTER when you want to quit.");
				string command = Console.ReadLine();
				if (command == "q") break;
			}
			Console.WriteLine("Shutting down...");

			// Here we close the ports manually -- although this is not required,
			// as UPnP class will automatically close the ports when it's destroyed.
			up.CloseTCP(5127, OnPortClosed);
			up.CloseUDP(5128, OnPortClosed);

			// Wait for the ports to get closed
			while (up.hasThreadsActive) Thread.Sleep(1);

			//discovery.Stop();
			server.SaveTo("server.dat");
			server.Stop();
		}
		Console.WriteLine("There server has shut down. Press ENTER to exit.");
		Console.ReadLine();
		return 0;
	}
}