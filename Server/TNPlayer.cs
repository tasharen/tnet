using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Collections.Generic;
using System.Threading;

namespace TNet
{
/// <summary>
/// Class containing information about connected players.
/// </summary>

public class Player : Connection
{
	/// <summary>
	/// Server protocol version. Must match the client.
	/// </summary>

	public const int version = 1;

	/// <summary>
	/// Connection ID.
	/// </summary>

	public int id = 0;

	/// <summary>
	/// Whether the connection has been verified to use the correct protocol version.
	/// </summary>

	public bool verified = false;

	/// <summary>
	/// Player's name.
	/// </summary>

	public string name;

	/// <summary>
	/// Channel that the player is currently in.
	/// </summary>

	public Channel channel;

	/// <summary>
	/// Start receiving incoming messages.
	/// </summary>

	public override void StartReceiving ()
	{
		if (socket != null && socket.Connected)
		{
			base.StartReceiving();
		}
	}
}
}