//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using System.IO;
using UnityEngine;
using TNet;
using System.Net;

/// <summary>
/// Tasharen Network Manager tailored for Unity.
/// </summary>

[AddComponentMenu("TNet/Network Manager")]
public class TNManager : MonoBehaviour
{
	/// <summary>
	/// List of objects that can be instantiated by the network.
	/// </summary>

	public GameObject[] objects;

	// Network client
	GameClient mClient = new GameClient();

	// Static player, here just for convenience so that GetPlayer() works the same even if instance is missing.
	static Player mPlayer = new Player("Guest");

	// Player list that will contain only the player in it. Here for the same reason as 'mPlayer'.
	static List<Player> mPlayers;

	// Instance pointer
	static TNManager mInstance;
	static int mObjectOwner = 1;

	/// <summary>
	/// TNet Client used for communication.
	/// </summary>

	static public GameClient client { get { return (mInstance != null) ? mInstance.mClient : null; } }

	/// <summary>
	/// Whether we're currently connected.
	/// </summary>

	static public bool isConnected { get { return mInstance != null && mInstance.mClient.isConnected; } }

	/// <summary>
	/// Whether we're currently hosting.
	/// </summary>

	static public bool isHosting { get { return mInstance == null || mInstance.mClient.isHosting; } }

	/// <summary>
	/// Whether the player is currently in a channel.
	/// </summary>

	static public bool isInChannel { get { return mInstance == null || mInstance.mClient.isInChannel; } }

	/// <summary>
	/// Enable or disable the Nagle's buffering algorithm (aka NO_DELAY flag).
	/// Enabling this flag will improve latency at the cost of increased bandwidth.
	/// http://en.wikipedia.org/wiki/Nagle's_algorithm
	/// </summary>

	static public bool noDelay { get { return mInstance != null && mInstance.mClient.noDelay; } set { if (mInstance != null) mInstance.mClient.noDelay = value; } }

	/// <summary>
	/// Current ping to the server.
	/// </summary>

	static public int ping { get { return mInstance != null ? mInstance.mClient.ping : 0; } }

	/// <summary>
	/// Whether we can use unreliable packets (UDP) to communicate with the server.
	/// </summary>

	static public bool canUseUDP { get { return mInstance != null && mInstance.mClient.canUseUDP; } }

	/// <summary>
	/// Listening port for incoming UDP packets. Set via TNManager.Start().
	/// </summary>

	static public int listeningPort { get { return mInstance != null ? mInstance.mClient.listeningPort : 0; } }

	/// <summary>
	/// ID of the player that wanted an object to be created.
	/// Check this in a script's Awake() attached to an network-instantiated object.
	/// </summary>

	static public int objectOwnerID { get { return mObjectOwner; } }

	/// <summary>
	/// Call this function in the script's Awake() to determine if this object
	/// was created as a result of the player's TNManager.Create() call.
	/// </summary>

	static public bool isThisMyObject { get { return mObjectOwner == playerID; } }

	/// <summary>
	/// Address from which the packet was received. Only available during packet processing callbacks.
	/// If null, then the packet arrived via the active connection (TCP).
	/// If the return value is not null, then the last packet arrived via UDP.
	/// </summary>

	static public IPEndPoint packetSource { get { return (mInstance != null) ? mInstance.mClient.packetSource : null; } }

	/// <summary>
	/// The player's unique identifier.
	/// </summary>

	static public int playerID
	{
		get
		{
			return (isConnected) ? mInstance.mClient.playerID : mPlayer.id;
		}
	}

	/// <summary>
	/// Get or set the player's name as everyone sees him on the network.
	/// </summary>

	static public string playerName
	{
		get
		{
			return (isConnected) ? mInstance.mClient.playerName : mPlayer.name;
		}
		set
		{
			if (playerName != value)
			{
				mPlayer.name = value;
				if (isConnected) mInstance.mClient.playerName = value;
			}
		}
	}

	/// <summary>
	/// List of players in the same channel as the client.
	/// </summary>

	static public List<Player> players
	{
		get
		{
			if (isConnected) return mInstance.mClient.players;
			
			if (mPlayers == null)
			{
				mPlayers = new List<Player>();
				mPlayers.Add(mPlayer);
			}
			return mPlayers;
		}
	}

	/// <summary>
	/// Get the local player.
	/// </summary>

	static public Player player { get { return (mInstance != null) ? mInstance.mClient.player : mPlayer; } }

	/// <summary>
	/// Get the player associated with the specified ID.
	/// </summary>

	static public Player GetPlayer (int id)
	{
		if (isConnected) return mInstance.mClient.GetPlayer(id);
		if (id == mPlayer.id) return mPlayer;
		return null;
	}

	/// <summary>
	/// Set the following function to handle this type of packets.
	/// </summary>

	static public void SetPacketHandler (byte packetID, GameClient.OnPacket callback)
	{
		if (mInstance != null)
		{
			mInstance.mClient.packetHandlers[packetID] = callback;
		}
	}

	/// <summary>
	/// Set the following function to handle this type of packets.
	/// </summary>

	static public void SetPacketHandler (Packet packet, GameClient.OnPacket callback)
	{
		if (mInstance != null)
		{
			mInstance.mClient.packetHandlers[(byte)packet] = callback;
		}
	}

	/// <summary>
	/// Start listening for incoming UDP packets on the specified port.
	/// </summary>

	static public bool StartUDP (int udpPort) { return (mInstance != null) ? mInstance.mClient.StartUDP(udpPort) : false; }

	/// <summary>
	/// Stop listening to incoming UDP packets.
	/// </summary>

	static public void StopUDP () { if (mInstance != null) mInstance.mClient.StopUDP(); }

	/// <summary>
	/// Connect to the specified destination.
	/// </summary>

	static public void Connect (string address, int port)
	{
		if (mInstance != null)
		{
			mInstance.mClient.playerName = mPlayer.name;
			mInstance.mClient.Connect(address, port);
		}
	}

	/// <summary>
	/// Connect to the specified destination with the address and port specified as "255.255.255.255:255".
	/// </summary>

	static public void Connect (string address)
	{
		if (mInstance != null)
		{
			string[] split = address.Split(new char[] { ':' });
			int port = 5127;
			if (split.Length > 1) int.TryParse(split[1], out port);
			Connect(split[0], port);
		}
	}

	/// <summary>
	/// Disconnect from the specified destination.
	/// </summary>

	static public void Disconnect () { if (mInstance != null) mInstance.mClient.Disconnect(); }

	/// <summary>
	/// Join the specified channel. This channel will be marked as persistent, meaning it will
	/// stay open even when the last player leaves, unless explicitly closed first.
	/// </summary>
	/// <param name="channelID">ID of the channel. Every player joining this channel will see one another.</param>
	/// <param name="levelName">Level that will be loaded first.</param>

	static public void JoinChannel (int channelID, string levelName)
	{
		if (mInstance != null) mInstance.mClient.JoinChannel(channelID, levelName, true, null);
	}

	/// <summary>
	/// Join the specified channel.
	/// </summary>
	/// <param name="channelID">ID of the channel. Every player joining this channel will see one another.</param>
	/// <param name="levelName">Level that will be loaded first.</param>
	/// <param name="persistent">Whether the channel will remain active even when the last player leaves.</param>
	/// <param name="password">Password for the channel. First player sets the password.</param>

	static public void JoinChannel (int channelID, string levelName, bool persistent, string password)
	{
		if (mInstance != null) mInstance.mClient.JoinChannel(channelID, levelName, persistent, password);
	}

	/// <summary>
	/// Leave the channel we're in.
	/// </summary>

	static public void LeaveChannel () { if (mInstance != null) mInstance.mClient.LeaveChannel(); }

	/// <summary>
	/// Load the specified level.
	/// </summary>

	static public void LoadLevel (string levelName)
	{
		if (isConnected) mInstance.mClient.LoadLevel(levelName);
		else Application.LoadLevel(levelName);
	}

	/// <summary>
	/// Change the hosting player.
	/// </summary>

	static public void SetHost (Player player)
	{
		if (mInstance != null) mInstance.mClient.SetHost(player);
	}

	/// <summary>
	/// Create the specified game object on all connected clients.
	/// Note that the object must be present in the TNManager's list of objects.
	/// </summary>

	static public void Create (GameObject go)
	{
		if (mInstance != null)
		{
			int index = mInstance.IndexOf(go);

			if (index != -1)
			{
				if (mInstance.mClient.isConnected)
				{
					BinaryWriter writer = mInstance.mClient.BeginSend(Packet.RequestCreate);
					writer.Write((ushort)index);
					writer.Write(go.GetComponent<TNObject>() != null ? (byte)1 : (byte)0);
					writer.Write((byte)0);
					mInstance.mClient.EndSend();
					return;
				}
			}
			else
			{
				Debug.LogError("You must add the object you're trying to create to the TNManager's list of objects", go);
			}
		}
		Instantiate(go);
	}

	/// <summary>
	/// Create a new object at the specified position and rotation.
	/// Note that the object must be present in the TNManager's list of objects.
	/// </summary>

	static public void Create (GameObject go, Vector3 pos, Quaternion rot)
	{
		if (mInstance != null)
		{
			int index = mInstance.IndexOf(go);

			if (index != -1)
			{
				if (mInstance.mClient.isConnected)
				{
					BinaryWriter writer = mInstance.mClient.BeginSend(Packet.RequestCreate);
					writer.Write((ushort)index);
					writer.Write(go.GetComponent<TNObject>() != null ? (byte)1 : (byte)0);
					writer.Write((byte)1);
					writer.Write(pos.x);
					writer.Write(pos.y);
					writer.Write(pos.z);
					writer.Write(rot.x);
					writer.Write(rot.y);
					writer.Write(rot.z);
					writer.Write(rot.w);
					mInstance.mClient.EndSend();
					return;
				}
			}
			else
			{
				Debug.LogError("You must add the object you're trying to create to the TNManager's list of objects", go);
			}
		}
		Instantiate(go, pos, rot);
	}

	/// <summary>
	/// Create a new object at the specified position and rotation.
	/// Note that the object must be present in the TNManager's list of objects.
	/// </summary>

	static public void Create (GameObject go, Vector3 pos, Quaternion rot, Vector3 vel, Vector3 angVel)
	{
		if (mInstance != null)
		{
			int index = mInstance.IndexOf(go);

			if (index != -1)
			{
				if (mInstance.mClient.isConnected)
				{
					BinaryWriter writer = mInstance.mClient.BeginSend(Packet.RequestCreate);
					writer.Write((ushort)index);
					writer.Write(go.GetComponent<TNObject>() != null ? (byte)1 : (byte)0);
					writer.Write((byte)2);
					writer.Write(pos.x);
					writer.Write(pos.y);
					writer.Write(pos.z);
					writer.Write(rot.x);
					writer.Write(rot.y);
					writer.Write(rot.z);
					writer.Write(rot.w);
					writer.Write(vel.x);
					writer.Write(vel.y);
					writer.Write(vel.z);
					writer.Write(angVel.x);
					writer.Write(angVel.y);
					writer.Write(angVel.z);
					mInstance.mClient.EndSend();
					return;
				}
			}
			else
			{
				Debug.LogError("You must add the object you're trying to create to the TNManager's list of objects", go);
			}
		}

		go = Instantiate(go, pos, rot) as GameObject;
		Rigidbody rb = go.rigidbody;

		if (rb != null)
		{
			if (rb.isKinematic)
			{
				rb.isKinematic = false;
				rb.velocity = vel;
				rb.angularVelocity = angVel;
				rb.isKinematic = true;
			}
			else
			{
				rb.velocity = vel;
				rb.angularVelocity = angVel;
			}
		}
	}

	/// <summary>
	/// Destroy the specified game object.
	/// </summary>

	static public void Destroy (GameObject go)
	{
		if (isConnected)
		{
			TNObject obj = go.GetComponent<TNObject>();

			if (obj != null)
			{
				BinaryWriter writer = mInstance.mClient.BeginSend(Packet.RequestDestroy);
				writer.Write(obj.id);
				mInstance.mClient.EndSend();
				return;
			}
		}
		GameObject.Destroy(go);
	}

	/// <summary>
	/// Begin sending a new packet to the server.
	/// </summary>

	static public BinaryWriter BeginSend (Packet type) { return mInstance.mClient.BeginSend(type); }

	/// <summary>
	/// Begin sending a new packet to the server.
	/// </summary>

	static public BinaryWriter BeginSend (byte packetID) { return mInstance.mClient.BeginSend(packetID); }

	/// <summary>
	/// Send the outgoing buffer.
	/// </summary>

	static public void EndSend () { mInstance.mClient.EndSend(true); }

	/// <summary>
	/// Send the outgoing buffer.
	/// </summary>

	static public void EndSend (bool reliable) { mInstance.mClient.EndSend(reliable); }

	/// <summary>
	/// Broadcast the packet to everyone on the LAN.
	/// </summary>

	static public void EndSend (int port) { mInstance.mClient.EndSend(port); }

	/// <summary>
	/// Broadcast the packet to the specified endpoint via UDP.
	/// </summary>

	static public void EndSend (IPEndPoint target) { mInstance.mClient.EndSend(target); }


#region MonoBehaviour Functions -- it's unlikely that you will need to modify these

	/// <summary>
	/// Ensure that there is only one instance of this class present.
	/// </summary>

	void Awake ()
	{
		if (mInstance != null)
		{
			GameObject.Destroy(gameObject);
		}
		else
		{
			mInstance = this;
			DontDestroyOnLoad(gameObject);

			mClient.onError = OnError;
			mClient.onConnect = OnConnect;
			mClient.onDisconnect = OnDisconnect;
			mClient.onJoinChannel = OnJoinChannel;
			mClient.onLeftChannel = OnLeftChannel;
			mClient.onLoadLevel = OnLoadLevel;
			mClient.onPlayerJoined = OnPlayerJoined;
			mClient.onPlayerLeft = OnPlayerLeft;
			mClient.onRenamePlayer = OnRenamePlayer;
			mClient.onCreate = OnCreateObject;
			mClient.onDestroy = OnDestroyObject;
			mClient.onForwardedPacket = OnForwardedPacket;
		}
	}

	/// <summary>
	/// Make sure we disconnect on exit.
	/// </summary>

	void OnDestroy ()
	{
		if (mInstance == this)
		{
			if (isConnected) mClient.Disconnect();
			mClient.StopUDP();
			mInstance = null;
		}
	}

	/// <summary>
	/// Find the index of the specified game object.
	/// </summary>

	int IndexOf (GameObject go)
	{
		for (int i = 0, imax = objects.Length; i < imax; ++i) if (objects[i] == go) return i;
		return -1;
	}

	/// <summary>
	/// Notification of a new object being created.
	/// </summary>

	void OnCreateObject (int creator, int index, uint objectID, BinaryReader reader)
	{
		if (index >= objects.Length)
		{
			Debug.LogError("Attempting to create an invalid object. Index: " + index);
			return;
		}
		GameObject go = null;

		mObjectOwner = creator;
		int type = reader.ReadByte();

		if (type == 2)
		{
			Vector3 pos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
			Quaternion rot = new Quaternion(reader.ReadSingle(), reader.ReadSingle(),
				reader.ReadSingle(), reader.ReadSingle());
			Vector3 vel = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
			Vector3 ang = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
			go = Instantiate(objects[index], pos, rot) as GameObject;
			Rigidbody rb = go.rigidbody;

			if (rb != null)
			{
				if (rb.isKinematic)
				{
					rb.isKinematic = false;
					rb.velocity = vel;
					rb.angularVelocity = ang;
					rb.isKinematic = true;
				}
				else
				{
					rb.velocity = vel;
					rb.angularVelocity = ang;
				}
			}
		}
		else if (type == 1)
		{
			Vector3 pos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
			Quaternion rot = new Quaternion(reader.ReadSingle(), reader.ReadSingle(),
				reader.ReadSingle(), reader.ReadSingle());
			go = Instantiate(objects[index], pos, rot) as GameObject;
		}
		else
		{
			go = Instantiate(objects[index]) as GameObject;
		}

		mObjectOwner = playerID;
		
		if (go != null && objectID != 0)
		{
			TNObject obj = go.GetComponent<TNObject>();

			if (obj != null)
			{
				obj.id = (int)objectID;
				obj.Register();
			}
			else
			{
				Debug.LogWarning("The instantiated object has no TNObject component. Don't request a ObjectID when creating it.", go);
			}
		}
	}

	/// <summary>
	/// Notification of a network object being destroyed.
	/// </summary>

	void OnDestroyObject (uint objID)
	{
		TNObject obj = TNObject.Find(objID);
		if (obj) GameObject.Destroy(obj.gameObject);
	}

	/// <summary>
	/// If custom functionality is needed, all unrecognized packets will arrive here.
	/// </summary>

	void OnForwardedPacket (BinaryReader reader)
	{
		uint val = reader.ReadUInt32();
		uint objID = (val >> 8);
		byte funcID = (byte)(val & 0xFF);

		if (funcID == 0)
		{
			TNObject.FindAndExecute(objID, reader.ReadString(), Tools.Read(reader));
		}
		else
		{
			TNObject.FindAndExecute(objID, funcID, Tools.Read(reader));
		}
	}

	/// <summary>
	/// Process incoming packets in the update function.
	/// </summary>

	void Update () { mClient.ProcessPackets(); }

#endregion
#region Callbacks -- Modify these if you don't like the broadcast approach

	/// <summary>
	/// Error notification.
	/// </summary>

	void OnError (string err) { Tools.Broadcast("OnNetworkError", err); }

	/// <summary>
	/// Connection result notification.
	/// </summary>

	void OnConnect (bool success, string message) { Tools.Broadcast("OnNetworkConnect", success, message); }

	/// <summary>
	/// Notification that happens when the client gets disconnected from the server.
	/// </summary>

	void OnDisconnect () { Tools.Broadcast("OnNetworkDisconnect"); }

	/// <summary>
	/// Notification sent when attempting to join a channel, indicating a success or failure.
	/// </summary>

	void OnJoinChannel (bool success, string message) { Tools.Broadcast("OnNetworkJoinChannel", success, message); }

	/// <summary>
	/// Notification sent when leaving a channel.
	/// Also sent just before a disconnect (if inside a channel when it happens).
	/// </summary>

	void OnLeftChannel () { Tools.Broadcast("OnNetworkLeaveChannel"); }

	/// <summary>
	/// Notification sent when a level is changing.
	/// </summary>

	void OnLoadLevel (string levelName)
	{
		if (!string.IsNullOrEmpty(levelName))
		{
			Application.LoadLevel(levelName);
		}
	}

	/// <summary>
	/// Notification of a new player joining the channel.
	/// </summary>

	void OnPlayerJoined (Player p) { Tools.Broadcast("OnNetworkPlayerJoin", p); }

	/// <summary>
	/// Notification of another player leaving the channel.
	/// </summary>

	void OnPlayerLeft (Player p) { Tools.Broadcast("OnNetworkPlayerLeave", p); }

	/// <summary>
	/// Notification of a player being renamed.
	/// </summary>

	void OnRenamePlayer (Player p, string previous)
	{
		mPlayer.name = p.name;
		Tools.Broadcast("OnNetworkPlayerRenamed", p, previous);
	}
#endregion
}
