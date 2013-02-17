//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

// Note on the UDP lobby: Although it's a better choice than TCP (and plus it allows LAN broadcasts),
// it doesn't seem to work with the Amazon EC2 cloud-hosted servers. They don't seem to accept inbound UDP traffic
// without an active TPC connection from the same source... so it's your choice which protocol to use.

#define UDP_LOBBY

using System;
using TNet;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Net;

/// <summary>
/// This is an example of a stand-alone server. You don't need Unity in order to compile and run it.
/// Running it as-is will start a Game Server on ports 5127 (TCP) and 5128 (UDP), as well as a Lobby Server on port 5129.
/// </summary>

public class ServerMain
{
	/// <summary>
	/// Application entry point -- parse the parameters.
	/// </summary>

	static int Main (string[] args)
	{
		// TODO: Make it possible for the lobby servers to save & load files.
		// - Client connects to the lobby.
		// - Client gets a list of servers.
		// - Client may request an AccountID if one was not found.
		// - Lobby will provide an ever-incrementing AccountID for that player. This needs to be saved on server shutdown.
		// - Player will pass this AccountID back to the server in order to save their progress (achievements and such).
		// - This AccountID identifier makes it possible to send PMs, mail, and /friend the player.

		if (args == null || args.Length == 0)
		{
			Console.WriteLine("No arguments specified, assuming default values.");
			Console.WriteLine("In the future you can specify your own ports like so:");
			Console.WriteLine("  TNServer.exe \"Server Name\" 5127           <-- TCP only");
			Console.WriteLine("  TNServer.exe \"Server Name\" 5127 5128      <-- TCP and UDP");
			Console.WriteLine("  TNServer.exe \"Server Name\" 5127 5128 5129 <-- TCP, UDP, Lobby");
			Console.WriteLine("  TNServer.exe \"Server Name\" 0 0 5129       <-- Lobby only\n");
			Console.WriteLine("To register with a remote lobby server, use this syntax:");
			Console.WriteLine("  TNServer.exe \"Server Name\" 5127 5128 some.server.com 5129\n");
			args = new string[] { "TNet Server", "5127", "5128", "5129" };
			//args = new string[] { "TNet Server", "5127", "5128", "server.tasharen.com", "5129" };
			//args = new string[] { "TNet Server", "0", "0", "5129" };
		}

		int tcpPort = 0;
		int udpPort = 0;
		int lobbyPort = 0;
		string name = "TNet Server";
		string lobbyAddress = null;

		if (args.Length > 0) name = args[0];
		if (args.Length > 1) int.TryParse(args[1], out tcpPort);
		if (args.Length > 2) int.TryParse(args[2], out udpPort);
		if (args.Length > 4)
		{
			if (int.TryParse(args[4], out lobbyPort))
			{
				lobbyAddress = args[3];
			}
		}
		else if (args.Length > 3)
		{
			int.TryParse(args[3], out lobbyPort);
		}
		Start(name, tcpPort, udpPort, lobbyPort, lobbyAddress);
		return 0;
	}

	/// <summary>
	/// Start the server.
	/// </summary>

	static void Start (string name, int tcpPort, int udpPort, int lobbyPort, string lobbyAddress)
	{
		Console.WriteLine("IP Addresses\n------------");
		Console.WriteLine("External: " + Tools.externalAddress);
		Console.WriteLine("Internal: " + Tools.localAddress);
		{
			// Universal Plug & Play is used to determine the external IP address,
			// and to automatically open up ports on the router / gateway.
			UPnP up = new UPnP();
			up.WaitForThreads();

			if (up.status == UPnP.Status.Success)
			{
				Console.WriteLine("Gateway:  " + up.gatewayAddress + "\n");
			}
			else
			{
				Console.WriteLine("Gateway:  None found\n");
				up = null;
			}

			GameServer gameServer = null;
			LobbyServer lobbyServer = null;

			if (tcpPort > 0)
			{
				gameServer = new GameServer();
				gameServer.name = name;

				if (!string.IsNullOrEmpty(lobbyAddress))
				{
					// Remote lobby address specified, so the lobby link should point to a remote location
					IPEndPoint ip = Tools.ResolveEndPoint(lobbyAddress, lobbyPort);
#if UDP_LOBBY
					gameServer.lobbyLink = new UdpLobbyServerLink(ip);
#else
					gameServer.lobbyLink = new TcpLobbyServerLink(ip);
#endif
				}
				else if (lobbyPort > 0)
				{
					// Server lobby port should match the lobby port on the client
#if UDP_LOBBY
					lobbyServer = new UdpLobbyServer();
					lobbyServer.Start(lobbyPort, udpPort);
					if (up != null) up.OpenUDP(lobbyPort, OnPortOpened);
#else
					lobbyServer = new TcpLobbyServer();
					lobbyServer.Start(lobbyPort);
					if (up != null) up.OpenTCP(lobbyPort, OnPortOpened);
#endif
					// Local lobby server
					gameServer.lobbyLink = new LobbyServerLink(lobbyServer);
				}

				// Start the actual game server and load the save file
				gameServer.Start(tcpPort, udpPort);
				gameServer.LoadFrom("server.dat");
			}
			else if (lobbyPort > 0)
			{
#if UDP_LOBBY
				if (up != null) up.OpenUDP(lobbyPort, OnPortOpened);
				lobbyServer = new UdpLobbyServer();
				lobbyServer.Start(lobbyPort, udpPort);
#else
				if (up != null) up.OpenTCP(lobbyPort, OnPortOpened);
				lobbyServer = new TcpLobbyServer();
				lobbyServer.Start(lobbyPort);
#endif
			}

			// Open up ports on the router / gateway
			if (up != null)
			{
				if (tcpPort > 0) up.OpenTCP(tcpPort, OnPortOpened);
				if (udpPort > 0) up.OpenUDP(udpPort, OnPortOpened);
			}

			for (; ; )
			{
				Console.WriteLine("Press 'q' followed by ENTER when you want to quit.");
				string command = Console.ReadLine();
				if (command == "q") break;
			}
			Console.WriteLine("Shutting down...");

			// Close all opened ports
			if (up != null)
			{
				up.Close();
				up.WaitForThreads();
				up = null;
			}

			// Stop the game server
			if (gameServer != null)
			{
				gameServer.SaveTo("server.dat");
				gameServer.Stop();
				gameServer = null;
			}

			// Stop the lobby server
			if (lobbyServer != null)
			{
				lobbyServer.Stop();
				lobbyServer = null;
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
			Console.WriteLine("UPnP: " + protocol.ToString().ToUpper() + " port " + port + " was opened successfully.");
		}
		else
		{
			Console.WriteLine("UPnP: Unable to open " + protocol.ToString().ToUpper() + " port " + port);
		}
	}
}
