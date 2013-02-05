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
	// TODO: Fix the Unity project. Its discovery won't work anymore.

	/// <summary>
	/// Application entry point -- parse the parameters.
	/// </summary>

	static int Main (string[] args)
	{
		if (args == null || args.Length == 0)
		{
			Console.WriteLine("No arguments specified, assuming default values.");
			Console.WriteLine("In the future you can specify your own ports like so:");
			Console.WriteLine("  TNServer.exe \"Server Name\" 5127 5128      <-- TCP and UDP (default)");
			Console.WriteLine("  TNServer.exe \"Server Name\" 5127 5128 5129 <-- TCP, UDP, Discovery");
			Console.WriteLine("  TNServer.exe \"Server Name\" 5127           <-- TCP only");
			Console.WriteLine("  TNServer.exe \"Server Name\" 0 0 5129       <-- Discovery only\n");
			Console.WriteLine("To register with a remote discovery server, use this syntax:");
			Console.WriteLine("  TNServer.exe \"Server Name\" 5127 5128 some.server.com 5129\n");
			args = new string[] { "TNet Server", "5127", "5128", "5129" };
		}

		int tcpPort = 0;
		int udpPort = 0;
		int discoveryPort = 0;
		string name = "TNet Server";
		string discoveryAddress = null;

		if (args.Length > 0) name = args[0];
		if (args.Length > 1) int.TryParse(args[1], out tcpPort);
		if (args.Length > 2) int.TryParse(args[2], out udpPort);
		if (args.Length > 4)
		{
			if (int.TryParse(args[4], out discoveryPort))
			{
				discoveryAddress = args[3];
			}
		}
		else if (args.Length > 3)
		{
			int.TryParse(args[3], out discoveryPort);
		}
		Start(name, tcpPort, udpPort, discoveryPort, discoveryAddress);
		return 0;
	}

	/// <summary>
	/// Start the server.
	/// </summary>

	static void Start (string name, int tcpPort, int udpPort, int discoveryPort, string discoveryAddress)
	{
		Console.WriteLine("Locating the gateway...");
		{
			// Universal Plug & Play is used to determine the external IP address,
			// and to automatically open up ports on the router / gateway.
			UPnP up = new UPnP();

			// Wait for the gateway to be found
			while (up.status == UPnP.Status.Searching) Thread.Sleep(1);

			if (up.status == UPnP.Status.Failure)
			{
				Console.WriteLine("No gateway found");
				up = null;
			}
			else
			{
				Console.WriteLine("External IP address: " + up.externalAddress);

				// Open up ports on the gateway
				//if (tcpPort > 0) mUp.OpenTCP(tcpPort, OnPortOpened);
				//if (udpPort > 0) mUp.OpenUDP(udpPort, OnPortOpened);
				//if (disPort > 0) mUp.OpenUDP(disPort, OnPortOpened);
			}

			DiscoveryServer discoveryServer = null;

			if (discoveryPort > 0)
			{
				// Server discovery port should match the discovery port on the client
				discoveryServer = new TcpDiscoveryServer();
				discoveryServer.Start(discoveryPort);
			}

			GameServer gameServer = null;

			if (tcpPort > 0)
			{
				gameServer = new GameServer();
				gameServer.name = name;

				if (!string.IsNullOrEmpty(discoveryAddress))
				{
					gameServer.discoveryLink = new TcpDiscoveryServerLink();
					gameServer.discoveryLink.address = discoveryAddress;
					gameServer.discoveryLink.port = discoveryPort;
				}
				else if (discoveryPort > 0)
				{
					gameServer.discoveryLink = new TcpDiscoveryServerLink();
					gameServer.discoveryLink.address = "127.0.0.1";
					gameServer.discoveryLink.port = discoveryPort;
				}

				gameServer.Start(tcpPort, udpPort);
				gameServer.LoadFrom("server.dat");
			}

			for (; ; )
			{
				Console.WriteLine("Press 'q' followed by ENTER when you want to quit.");
				string command = Console.ReadLine();
				if (command == "q") break;
			}
			Console.WriteLine("Shutting down...");

			if (up != null)
			{
				// Close the ports we opened earlier
				//if (tcpPort > 0) mUp.CloseTCP(tcpPort, OnPortClosed);
				//if (udpPort > 0) mUp.CloseUDP(udpPort, OnPortClosed);
				//if (disPort > 0) mUp.CloseUDP(disPort, OnPortClosed);

				// Wait for the ports to get closed
				while (up.hasThreadsActive) Thread.Sleep(1);
				up = null;
			}

			// Save everything and stop the server
			if (discoveryServer != null)
			{
				discoveryServer.Stop();
				discoveryServer = null;
			}

			if (gameServer != null)
			{
				gameServer.SaveTo("server.dat");
				gameServer.Stop();
				gameServer = null;
			}
		}
		Console.WriteLine("There server has shut down. Press ENTER to terminate the application.");
		Console.ReadLine();
	}

	/// <summary>
	/// UPnP notification of a port being open.
	/// </summary>

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

	/// <summary>
	/// UPnP notification of a port closing.
	/// </summary>

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
}
