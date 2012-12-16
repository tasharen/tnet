using System;
using TNet;
using System.IO;

public class TNetTest
{
	static int Main ()
	{
		TcpServer server = new TcpServer();
		server.Start(5137, 5138);
		server.LoadFrom("server.dat");

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
		server.SaveTo("server.dat");
		server.Stop();
		return 0;
	}
}