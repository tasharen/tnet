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

	static int Main (string[] args)
	{
		Console.WriteLine("Locating the gateway...");
		{
			if (args == null || args.Length == 0)
			{
				Console.WriteLine("No arguments specified, assuming default values.");
				Console.WriteLine("In the future you can specify your own ports like so:");
				Console.WriteLine("  TNServer.exe \"Server Name\" 5127 5128 5129 <-- TCP, UDP, Discovery");
				Console.WriteLine("  TNServer.exe \"Server Name\" 5127 5128      <-- TCP and UDP (default)");
				Console.WriteLine("  TNServer.exe \"Server Name\" 5127           <-- TCP only");
				Console.WriteLine("  TNServer.exe \"Server Name\" 0 0 5129       <-- Discovery only\n");
				args = new string[] { "TNet Server", "5127", "5128" };
			}

			int tcpPort = 0;
			int udpPort = 0;
			int disPort = 0;
			string name = "TNet Server";

			if (args.Length > 0) name = args[0];
			if (args.Length > 1) int.TryParse(args[1], out tcpPort);
			if (args.Length > 2) int.TryParse(args[2], out udpPort);
			if (args.Length > 3) int.TryParse(args[3], out disPort);

			UPnP up = new UPnP();

			// Open up ports on the gateway
			if (tcpPort > 0) up.OpenTCP(tcpPort, OnPortOpened);
			if (udpPort > 0) up.OpenUDP(udpPort, OnPortOpened);
			if (disPort > 0) up.OpenUDP(disPort, OnPortOpened);

			// Wait for the gateway to be found
			while (up.status == UPnP.Status.Searching) Thread.Sleep(1);

			Console.WriteLine("External IP address: " + up.externalAddress);

			GameServer server = null;
			DiscoveryServer discovery = null;

			if (tcpPort > 0)
			{
				server = new GameServer();
				server.name = name;
				//server.discoveryAddress = "127.0.0.1";
				//server.discoveryPort = 5129;
				server.Start(tcpPort, udpPort);
				server.LoadFrom("server.dat");
			}

			if (disPort > 0)
			{
				// Server discovery port should match the discovery port on the client (TNDiscoveryClient).
				discovery = new DiscoveryServer();
				discovery.localServer = server;
				discovery.Start(disPort);
			}

			for (; ; )
			{
				Console.WriteLine("Press 'q' followed by ENTER when you want to quit.");
				string command = Console.ReadLine();
				if (command == "q") break;
			}
			Console.WriteLine("Shutting down...");

			// Here we close the ports manually -- although this is not required,
			// as UPnP class will automatically close the ports when it's destroyed.
			if (tcpPort > 0) up.CloseTCP(tcpPort, OnPortClosed);
			if (udpPort > 0) up.CloseUDP(udpPort, OnPortClosed);
			if (disPort > 0) up.CloseUDP(disPort, OnPortClosed);

			// Wait for the ports to get closed
			while (up.hasThreadsActive) Thread.Sleep(1);

			// Save everything and stop the server
			if (discovery != null) discovery.Stop();

			if (server != null)
			{
				server.SaveTo("server.dat");
				server.Stop();
			}
		}
		Console.WriteLine("There server has shut down. Press ENTER to terminate the application.");
		Console.ReadLine();
		return 0;
	}
}