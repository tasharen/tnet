//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using System;
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

	/// <summary>
	/// Socket that is used for communication.
	/// </summary>

	public override Socket socket
	{
		get
		{
			return mSocket;
		}
		set
		{
			if (mSocket != value)
			{
				Disconnect();
				mSocket = value;
			}
		}
	}

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
#if UNITY_EDITOR
		catch (System.Exception ex)
		{
			UnityEngine.Debug.LogError(ex.Message);
#else
		catch (System.Exception)
		{
#endif
		}
	}

	/// <summary>
	/// Start receiving for incoming data.
	/// </summary>

	public override void StartReceiving ()
	{
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
		int bytes = 0;

		try
		{
			bytes = mSocket.EndReceiveFrom(result, ref mEndPoint);
		}
		catch (System.Exception)
		{
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

			IPEndPoint ip = (IPEndPoint)mEndPoint;
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

	public override void SendPacket (Buffer buffer, bool immediate)
	{
		throw new NotImplementedException();
	}
}
}
