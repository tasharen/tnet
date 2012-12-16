//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

/*using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace TNet
{
/// <summary>
/// Class containing basic information about a remote player communicating via UDP.
/// </summary>

public class UdpPlayer : Player
{
	public enum Stage
	{
		NotConnected,
		Verifying,
		Connected,
	}

	/// <summary>
	/// Current connection stage.
	/// </summary>

	public Stage stage = Stage.Verifying;

	/// <summary>
	/// Channel that the player is currently in.
	/// </summary>

	public TcpChannel channel;

	/// <summary>
	/// IP end point of the connected player.
	/// </summary>

	public IPEndPoint endPoint;

	// Temporary buffer used for writing
	Datagram mDatagram;
	UdpProtocol mUdp;

	/// <summary>
	/// UDP player must always be associated with a UDP protocol used for communication.
	/// </summary>

	public UdpPlayer (UdpProtocol udp, IPEndPoint ep)
	{
		mUdp = udp;

		// Create a copy of the end point
		endPoint = new IPEndPoint(new IPAddress(ep.Address.GetAddressBytes()), ep.Port);
	}

	/// <summary>
	/// Begin sending a new packet to the server.
	/// </summary>

	public BinaryWriter BeginSend (Packet type)
	{
		mDatagram = mUdp.Datagram.Create();
		mDatagram.endPoint = endPoint;
		return mDatagram.buffer.BeginUdpPacket(type);
	}

	/// <summary>
	/// Begin sending a new packet to the server.
	/// </summary>

	public BinaryWriter BeginSend (byte packetID)
	{
		mDatagram = mUdp.Datagram.Create();
		mDatagram.endPoint = endPoint;
		return mDatagram.buffer.BeginUdpPacket(packetID);
	}

	/// Send the outgoing buffer.
	/// </summary>

	public void EndSend ()
	{
		mDatagram.buffer.EndUdpPacket();
		SendDatagram(mDatagram);
		mDatagram = null;
	}

	/// <summary>
	/// Send the specified datagram. Marks the buffer as used.
	/// </summary>

	public void SendDatagram (Datagram dg) { mUdp.Send(dg); }
}
}*/