//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

// Note on the UDP lobby: Although it's a better choice than TCP (and plus it allows LAN broadcasts),
// it doesn't seem to work with the Amazon EC2 cloud-hosted servers. They don't seem to accept inbound UDP traffic
// without an active TPC connection from the same source... so it's your choice which protocol to use.

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
		if (args == null || args.Length == 0)
		{
			Console.WriteLine("No arguments specified, assuming default values.");
			Console.WriteLine("In the future you can specify your own ports like so:\n");
			Console.WriteLine("   -name \"Your Server\"         <-- Name your server");
			Console.WriteLine("   -tcp [port]                 <-- TCP port for clients to connect to");
			Console.WriteLine("   -udp [port]                 <-- UDP port used for communication");
			Console.WriteLine("   -udpLobby [address] [port]  <-- Start or connect to a UDP lobby");
			Console.WriteLine("   -tcpLobby [address] [port]  <-- Start or connect to a TCP lobby");
			Console.WriteLine("   -ip [ip]                    <-- Choose a specific network interface");
			Console.WriteLine("\nFor example:");
			Console.WriteLine("  TNServer -name \"My Server\" -tcp 5127 -udp 5128 -udpLobby 5129");
			
			args = new string[] { "TNet Server", "-tcp", "5127", "-udp", "5128", "-udpLobby", "5129" };
		}

		string name = "TNet Server";
		int tcpPort = 0;
		int udpPort = 0;
		string lobbyAddress = null;
		int lobbyPort = 0;
		bool tcpLobby = false;

		for (int i = 0; i < args.Length; )
		{
			string param = args[i];
			string val0 = (i + 1 < args.Length) ? args[i + 1] : null;
			string val1 = (i + 2 < args.Length) ? args[i + 2] : null;

			if (val0 != null && val0.StartsWith("-"))
			{
				val0 = null;
				val1 = null;
			}
			else if (val1 != null && val1.StartsWith("-"))
			{
				val1 = null;
			}

			if (param == "-name")
			{
				if (val0 != null) name = val0;
			}
			else if (param == "-tcp")
			{
				if (val0 != null) int.TryParse(val0, out tcpPort);
			}
			else if (param == "-udp")
			{
				if (val0 != null) int.TryParse(val0, out udpPort);
			}
			else if (param == "-ip")
			{
				if (val0 != null) UdpProtocol.defaultNetworkInterface = Tools.ResolveAddress(val0);
			}
			else if (param == "-tcpLobby")
			{
				if (val1 != null)
				{
					lobbyAddress = val0;
					int.TryParse(val1, out lobbyPort);
				}
				else int.TryParse(val0, out lobbyPort);
				tcpLobby = true;
			}
			else if (param == "-udpLobby")
			{
				if (val1 != null)
				{
					lobbyAddress = val0;
					int.TryParse(val1, out lobbyPort);
				}
				else int.TryParse(val0, out lobbyPort);
				tcpLobby = false;
			}
			else if (param == "-lobby")
			{
				if (val0 != null) lobbyAddress = val0;
			}

			if (val1 != null) i += 3;
			else if (val0 != null) i += 2;
			else ++i;
		}

		Start(name, tcpPort, udpPort, lobbyAddress, lobbyPort, tcpLobby);
		return 0;
	}

	/// <summary>
	/// Start the server.
	/// </summary>

	static void Start (string name, int tcpPort, int udpPort, string lobbyAddress, int lobbyPort, bool useTcp)
	{
		List<IPAddress> ips = Tools.localAddresses;
		string text = "\nLocal IPs: " + ips.size;

		for (int i = 0; i < ips.size; ++i)
		{
			text += "\n  " + (i + 1) + ": " + ips[i];
			if (ips[i] == TNet.Tools.localAddress) text += " (Primary)";
		}
		Console.WriteLine(text + "\n");
		{
			// Universal Plug & Play is used to determine the external IP address,
			// and to automatically open up ports on the router / gateway.
			UPnP up = new UPnP();
			up.WaitForThreads();

			if (up.status == UPnP.Status.Success)
			{
				Console.WriteLine("Gateway IP:  " + up.gatewayAddress);
			}
			else
			{
				Console.WriteLine("Gateway IP:  None found");
				up = null;
			}

			Console.WriteLine("External IP: " + Tools.externalAddress);
			Console.WriteLine("");

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
					if (useTcp) gameServer.lobbyLink = new TcpLobbyServerLink(ip);
					else gameServer.lobbyLink = new UdpLobbyServerLink(ip);

				}
				else if (lobbyPort > 0)
				{
					// Server lobby port should match the lobby port on the client
					if (useTcp)
					{
						lobbyServer = new TcpLobbyServer();
						lobbyServer.Start(lobbyPort);
						if (up != null) up.OpenTCP(lobbyPort, OnPortOpened);
					}
					else
					{
						lobbyServer = new UdpLobbyServer();
						lobbyServer.Start(lobbyPort);
						if (up != null) up.OpenUDP(lobbyPort, OnPortOpened);
					}
					
					// Local lobby server
					gameServer.lobbyLink = new LobbyServerLink(lobbyServer);
				}

				// Start the actual game server and load the save file
				gameServer.Start(tcpPort, udpPort);
				gameServer.LoadFrom("server.dat");
			}
			else if (lobbyPort > 0)
			{
				if (useTcp)
				{
					if (up != null) up.OpenTCP(lobbyPort, OnPortOpened);
					lobbyServer = new TcpLobbyServer();
					lobbyServer.Start(lobbyPort);
				}
				else
				{
					if (up != null) up.OpenUDP(lobbyPort, OnPortOpened);
					lobbyServer = new UdpLobbyServer();
					lobbyServer.Start(lobbyPort);
				}
			}

			// Open up ports on the router / gateway
			if (up != null)
			{
				if (tcpPort > 0) up.OpenTCP(tcpPort, OnPortOpened);
				if (udpPort > 0) up.OpenUDP(udpPort, OnPortOpened);
			}

			for (; ; )
			{
				Console.WriteLine("Press 'q' followed by ENTER when you want to quit.\n");
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
		Console.WriteLine("The server has shut down. Press ENTER to terminate the application.");
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
