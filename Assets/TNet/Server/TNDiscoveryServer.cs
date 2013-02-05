//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

namespace TNet
{
/// <summary>
/// Abstract class for a discovery server.
/// </summary>

public abstract class DiscoveryServer
{
	/// <summary>
	/// Port used to listen for incoming packets.
	/// </summary>

	public abstract int port { get; }

	/// <summary>
	/// Whether the server is active.
	/// </summary>

	public abstract bool isActive { get; }

	/// <summary>
	/// Mark the list as having changed.
	/// </summary>

	public abstract void MarkAsDirty ();

	/// <summary>
	/// Start listening for incoming connections.
	/// </summary>

	public abstract bool Start (int listenPort);

	/// <summary>
	/// Stop listening for incoming packets.
	/// </summary>

	public abstract void Stop ();
}
}
