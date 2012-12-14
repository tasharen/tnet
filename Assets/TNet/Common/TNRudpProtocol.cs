//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

/*using System;
using System.IO;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;
using System.Net;

namespace TNet
{
/// <summary>
/// Reliable UDP protocol.
/// </summary>

public class RudpProtocol : ConnectedProtocol
{
	Socket mSocket;
	int mPort = 0;
	EndPoint mEndPoint = new IPEndPoint(IPAddress.Any, 0);
	EndPoint mRemote = new IPEndPoint(IPAddress.None, 0);

	// The index of the last packet to arrive successfully with all previous packets to arrive successfully as well
	//ushort mLastFullSequenceID = 0;

	/// <summary>
	/// Try to establish a connection with the specified address.
	/// </summary>

	public override void Connect (string addr, int port)
	{
		Disconnect();
		mPort = port;

		IPAddress destination = null;

		if (!IPAddress.TryParse(addr, out destination))
		{
			IPAddress[] ips = Dns.GetHostAddresses(addr);
			if (ips.Length > 0) destination = ips[0];
		}

		address = addr + ":" + port;
		mSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

		try
		{
			mSocket.Bind(new IPEndPoint(IPAddress.Any, mPort));
			mSocket.BeginReceiveFrom(mTemp, 0, mTemp.Length, SocketFlags.None, ref mEndPoint, OnReceive, null);

			BinaryWriter writer = BeginSend(Packet.RequestID);
			writer.Write(Player.version);
			writer.Write(string.IsNullOrEmpty(name) ? "Guest" : name);
			EndSend();
		}
		catch (System.Exception ex)
		{
			Error(ex.Message);
		}
	}

	/// <summary>
	/// Start receiving for incoming data.
	/// </summary>

	public override void StartReceiving (Socket socket)
	{
		if (socket != null)
		{
			Close(false);
			mSocket = socket;
		}

		if (mSocket != null)
		{
			mSocket.BeginReceiveFrom(mTemp, 0, mTemp.Length, SocketFlags.None, ref mEndPoint, OnReceive, null);
		}
	}

	/// <summary>
	/// Receive incoming data.
	/// </summary>

	void OnReceive (IAsyncResult result)
	{
		// TODO:
		// rUDP can completely replace TCP as it makes NAT punchthrough trivial.
		// Each socket would need to keep a bunch of "source" lists though.
		// Packet arrives -- add it to the list associated with its IP:port. When processing packets,
		// process each list as if it was a separate connection.

		// UDP port 1234 sends a packet to a remote client's port 2345 (it gets blocked).
		// Remote client on port 2345 sends a packet to this client's 1234 (it may go through).
		// Neither are actively listening.
		// Both close the sockets.
		// Bind TCP listener to 1234.
		// Remote client binds a TCP socket to port 2345 and tries to connect to 1234. Connection succeeds.

		int bytes = 0;

		try
		{
			bytes = mSocket.EndReceiveFrom(result, ref mEndPoint);
		}
#if UNITY_EDITOR
		catch (System.Exception ex)
		{
			UnityEngine.Debug.LogWarning(ex.Message);
#else
		catch (System.Exception)
		{
#endif
			if (mSocket != null)
			{
				mSocket.Close();
				mSocket = null;
			}
			Close(true);
			return;
		}

		// If the packet didn't come from our target destination, simply ignore it
		if (bytes > 4 && mRemote.Equals(mEndPoint))
		{
			// Read the packet. UDP packets always arrive whole. They don't get fragmented like TCP.
			Buffer buffer = Buffer.Create();
			BinaryWriter writer = buffer.BeginWriting(false);

			//IPEndPoint ip = (IPEndPoint)mEndPoint;
			writer.Write(mTemp, 0, bytes);
			buffer.BeginReading(4);

			lock (mIn) mIn.Enqueue(buffer);

			// Queue up the next receive operation
			mSocket.BeginReceiveFrom(mTemp, 0, mTemp.Length, SocketFlags.None, ref mEndPoint, OnReceive, null);
		}
	}

	/// <summary>
	/// Disconnect the player, freeing all resources.
	/// </summary>

	public override void Disconnect () { if (mSocket != null) Close(isConnected); }

	/// <summary>
	/// Close the connection.
	/// </summary>

	public override void Close (bool notify)
	{
		base.Close(notify);

		if (mSocket != null)
		{
			if (mSocket != null)
			{
				if (notify)
				{
					try
					{
						BeginSend(Packet.Disconnect);
						EndSend(true);
						mSocket.Close();
					}
					catch (System.Exception) { }
				}
				mSocket = null;
			}

			if (notify)
			{
				Buffer buff = Buffer.Create();
				buff.BeginWriting(false).Write((byte)Packet.Disconnect);
				lock (mIn) mIn.Enqueue(buff);
			}
		}
		mPort = 0;
	}
	
	/// <summary>
	/// Send the specified packet to the connected client.
	/// </summary>

	protected override void SendPacket (Buffer buffer, bool immediate)
	{
		throw new NotImplementedException();
	}
}
}
*/