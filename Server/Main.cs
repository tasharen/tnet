using System;
using TNet;
using System.IO;

public class TNetTest
{
	static int Main ()
	{
		Server server = new Server();
		server.Start(5127);

		for (; ; )
		{
			Console.WriteLine("Command: ");
			string command = Console.ReadLine();
			if (command == "q") break;
		}
		Console.WriteLine("Shutting down...");
		server.Stop();
		return 0;
	}
}