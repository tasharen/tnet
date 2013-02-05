//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

namespace TNet
{
/// <summary>
/// Abstract class for a discovery communicator.
/// You can use this class to register your game server with a remote discovery server.
/// </summary>

public abstract class DiscoveryServerLink
{
	/// <summary>
	/// Address of the discovery server.
	/// </summary>

	public string address;

	/// <summary>
	/// Port used by the discovery server.
	/// </summary>

	public int port = 5129;

	/// <summary>
	/// Whether the link is currently active.
	/// </summary>

	public abstract bool isActive { get; }

	/// <summary>
	/// Start the discovery server link.
	/// </summary>

	public abstract void Start ();

	/// <summary>
	/// Stop informing the discovery server of any changes.
	/// </summary>

	public abstract void Stop ();

	/// <summary>
	/// Send an update to the discovery server.
	/// </summary>

	public abstract void Update (GameServer server);
}
}
