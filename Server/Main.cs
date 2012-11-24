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
			else if (command == "g")
			{
				GC.Collect();
				Console.WriteLine("Recycled: " + TNet.Buffer.recycleQueue);
			}
		}
		Console.WriteLine("Shutting down...");
		server.Stop();
		return 0;
	}
}