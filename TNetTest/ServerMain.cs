//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

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

public class Application : IDisposable
{
	string mFilename;
	UPnP mUPnP = null;
	GameServer mGameServer = null;
	LobbyServer mLobbyServer = null;

	public delegate bool HandlerRoutine (int type);
	[System.Runtime.InteropServices.DllImport("Kernel32")]
	static extern bool SetConsoleCtrlHandler (HandlerRoutine Handler, bool Add);

	/// <summary>
	/// Function executed by kernel32 when the application exits. This is the only way to reliably detect a closed app in Windows.
	/// </summary>

	bool OnExit (int type) { Dispose(); return true; }

	/// <summary>
	/// Start the server.
	/// </summary>

	public void Start (string serverName, int tcpPort, int udpPort, string lobbyAddress, int lobbyPort, bool useTcp, bool service, string fn = "server.dat")
	{
		mFilename = fn;
		List<IPAddress> ips = Tools.localAddresses;
		string text = "\nLocal IPs: " + ips.size;
		var ipv6 = (TNet.Tools.localAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6);

		for (int i = 0; i < ips.size; ++i)
		{
			var ip = ips[i];
			text += "\n  " + (i + 1) + ": " + ips[i];

			if (ip == TNet.Tools.localAddress)
			{
				text += " (LAN)";
			}
			else if (ipv6 && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
			{
				if (ip == TNet.Tools.externalAddress)
					text += " (WAN)";
			}
		}

		Console.WriteLine(text + "\n");
		{
			// You don't have to Start() and WaitForThreads(). TNet will do it for you when you try to open a port via UPnP.
			// It is good practice to have UPnP resolved before trying to use it though. This way there is no delay when
			// you try to open a port.
			mUPnP = new UPnP();
			mUPnP.Start();
			mUPnP.WaitForThreads();

			Tools.Print("External IP: " + Tools.externalAddress);

			if (tcpPort > 0)
			{
				mGameServer = new GameServer();
				mGameServer.name = serverName;

				if (!string.IsNullOrEmpty(lobbyAddress))
				{
					// Remote lobby address specified, so the lobby link should point to a remote location
					IPEndPoint ip = Tools.ResolveEndPoint(lobbyAddress, lobbyPort);
					if (useTcp) mGameServer.lobbyLink = new TcpLobbyServerLink(ip);
					else mGameServer.lobbyLink = new UdpLobbyServerLink(ip);

				}
				else if (lobbyPort > 0)
				{
					// Server lobby port should match the lobby port on the client
					if (useTcp)
					{
						mLobbyServer = new TcpLobbyServer();
						mLobbyServer.Start(lobbyPort);
						if (mUPnP.status != UPnP.Status.Failure) mUPnP.OpenTCP(lobbyPort, OnPortOpened);
					}
					else
					{
						mLobbyServer = new UdpLobbyServer();
						mLobbyServer.Start(lobbyPort);
						if (mUPnP.status != UPnP.Status.Failure) mUPnP.OpenUDP(lobbyPort, OnPortOpened);
					}

					// Local lobby server
					mGameServer.lobbyLink = new LobbyServerLink(mLobbyServer);
				}

				// Start the actual game server and load the save file
				mGameServer.Start(tcpPort, udpPort);
				mGameServer.Load(mFilename);
				Tools.Print("Loaded " + mFilename);
			}
			else if (lobbyPort > 0)
			{
				if (useTcp)
				{
					if (mUPnP.status != UPnP.Status.Failure) mUPnP.OpenTCP(lobbyPort, OnPortOpened);
					mLobbyServer = new TcpLobbyServer();
					mLobbyServer.Start(lobbyPort);
				}
				else
				{
					if (mUPnP.status != UPnP.Status.Failure) mUPnP.OpenUDP(lobbyPort, OnPortOpened);
					mLobbyServer = new UdpLobbyServer();
					mLobbyServer.Start(lobbyPort);
				}
			}

			// Open up ports on the router / gateway
			if (mUPnP.status != UPnP.Status.Failure)
			{
				if (tcpPort > 0) mUPnP.OpenTCP(tcpPort, OnPortOpened);
				if (udpPort > 0) mUPnP.OpenUDP(udpPort, OnPortOpened);
				mUPnP.WaitForThreads();
			}

			// This approach doesn't work on Windows 7 and higher.
			AppDomain.CurrentDomain.ProcessExit += new EventHandler(delegate(object sender, EventArgs e) { Dispose(); });

			// This approach works only on Windows
			try { SetConsoleCtrlHandler(new HandlerRoutine(OnExit), true); }
			catch (Exception) { }

			bool showInfo = true;

			for (; ; )
			{
				if (!service)
				{
					if (showInfo)
					{
						showInfo = false;
						Console.WriteLine("[TNet] Server is now active. Optional command list:");
						Console.WriteLine("  q -- Quit the application");
						Console.WriteLine("  r -- Reload the server configuration");
						Console.WriteLine("  c -- Release all unused memory");
						Console.WriteLine("  ban <keyword> -- Ban this player, alias or IP");
						Console.WriteLine("  unban <keyword> -- Unban this keyword");
						Console.WriteLine("  http -- Enable or disable HTTP support");
					}

					string command = Console.ReadLine();
					if (command == "q") break;

					if (command.StartsWith("ban "))
					{
						if (mLobbyServer != null) mLobbyServer.Ban(command.Substring(4));
						if (mGameServer != null) mGameServer.Ban(command.Substring(4));
					}
					else if (command.StartsWith("unban "))
					{
						if (mLobbyServer != null) mLobbyServer.Unban(command.Substring(6));
						if (mGameServer != null) mGameServer.Unban(command.Substring(6));
					}
					else if (command == "c")
					{
						TNet.Buffer.ReleaseUnusedMemory();
					}
					else if (command == "r")
					{
						if (mLobbyServer != null) mLobbyServer.LoadBanList();

						if (mGameServer != null)
						{
							mGameServer.LoadBanList();
							mGameServer.LoadAdminList();
							mGameServer.LoadConfig();
						}
					}
					else if (command == "http")
					{
						TcpProtocol.httpGetSupport = !TcpProtocol.httpGetSupport;
						Tools.Print("HTTP support: " + TcpProtocol.httpGetSupport + "\n");
					}
					else showInfo = true;
				}
				else Thread.Sleep(10000);
			}
			Tools.Print("Shutting down...");
			Dispose();
		}

		if (!service)
		{
			Tools.Print("The server has shut down. Press ENTER to terminate the application.");
			Console.ReadLine();
		}
	}

	/// <summary>
	/// Stop the server.
	/// </summary>

	public void Dispose ()
	{
		// Stop the game server
		if (mGameServer != null)
		{
			mGameServer.Stop();
			mGameServer = null;
		}

		// Stop the lobby server
		if (mLobbyServer != null)
		{
			mLobbyServer.Stop();
			mLobbyServer = null;
		}

		// Close all opened ports
		if (mUPnP != null)
		{
			mUPnP.Close();
			mUPnP.WaitForThreads();
			mUPnP = null;
		}
	}

	/// <summary>
	/// UPnP notification of a port being open.
	/// </summary>

	void OnPortOpened (UPnP up, int port, ProtocolType protocol, bool success)
	{
		if (success)
		{
			Tools.Print("UPnP: " + protocol.ToString().ToUpper() + " port " + port + " was opened successfully.");
		}
		else
		{
			Tools.Print("UPnP: Unable to open " + protocol.ToString().ToUpper() + " port " + port);
		}
	}

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
			Console.WriteLine("   -service                    <-- Run it as a service");
			Console.WriteLine("   -http                       <-- Respond to HTTP requests");
			Console.WriteLine("   -ipv6                       <-- Use IPv6");
			Console.WriteLine("   -fn [filename]              <-- Use this save instead of server.dat");
			Console.WriteLine("   -app [name]                 <-- Set the application name");
			Console.WriteLine("\nFor example:");
			Console.WriteLine("  TNServer -name \"My Server\" -tcp 5127 -udp 5128 -udpLobby 5129");

			args = new string[] { "-name", "TNet Server", "-tcp", "5127", "-udp", "5128", "-udpLobby", "5129" };
		}

		string serverName = "TNet Server";
		int tcpPort = 0;
		int udpPort = 0;
		string lobbyAddress = null;
		int lobbyPort = 0;
		bool tcpLobby = false;
		bool service = false;
		bool http = false;
		bool useIPv6 = false;
		string fn = "server.dat";

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
				if (val0 != null) serverName = val0;
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
			else if (param == "-service")
			{
				service = true;
			}
			else if (param == "-id")
			{
				if (val0 != null) ushort.TryParse(val0, out GameServer.gameID);
			}
			else if (param == "-http")
			{
				http = true;
			}
			else if (param == "-app")
			{
				Tools.applicationDirectory = val0;
			}
			else if (param == "-fn")
			{
				fn = val0;
			}
			else if (param == "-ipv6" || param == "-IPv6")
			{
				useIPv6 = true;
			}

			if (val1 != null) i += 3;
			else if (val0 != null) i += 2;
			else ++i;
		}

		TcpProtocol.defaultListenerInterface = useIPv6 ? System.Net.IPAddress.IPv6Any : System.Net.IPAddress.Any;
		TcpProtocol.httpGetSupport = http;
		Application app = new Application();
		app.Start(serverName, tcpPort, udpPort, lobbyAddress, lobbyPort, tcpLobby, service, fn);
		return 0;
	}
}
