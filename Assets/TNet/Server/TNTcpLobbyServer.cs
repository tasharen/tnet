//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2017 Tasharen Entertainment Inc
//-------------------------------------------------

using System;
using System.IO;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.Text;

namespace TNet
{
	/// <summary>
	/// Optional TCP-based listener that makes it possible for servers to
	/// register themselves with a central location for easy lobby by clients.
	/// </summary>

	public class TcpLobbyServer : LobbyServer
	{
		// List of servers that's currently being updated
		protected long mTime = 0;
		protected long mLastChange = 0;
		protected List<TcpProtocol> mTcp = new List<TcpProtocol>();
		protected ServerList mList = new ServerList();
		protected TcpListener mListener;
		protected int mPort = 0;
		protected Thread mThread;
		protected bool mInstantUpdates = true;
		protected Buffer mBuffer;

		/// <summary>
		/// If the number of simultaneous connected clients exceeds this number,
		/// server updates will no longer be instant, but rather delayed instead.
		/// </summary>

		public int instantUpdatesClientLimit = 50;

		/// <summary>
		/// Port used to listen for incoming packets.
		/// </summary>

		public override int port { get { return mPort; } }

		/// <summary>
		/// Whether the server is active.
		/// </summary>

		public override bool isActive { get { return (mListener != null); } }

		/// <summary>
		/// Start listening for incoming connections.
		/// </summary>

		public override bool Start (int listenPort)
		{
			Stop();

			Tools.LoadList("LobbyConfig/ban.txt", mBan);

#if FORCE_EN_US
			Tools.SetCurrentCultureToEnUS();
#endif
			try
			{
				mListener = new TcpListener(TNet.TcpProtocol.defaultListenerInterface, listenPort);
				mListener.Start(50);
				mPort = listenPort;
			}
#if STANDALONE
			catch (System.Exception ex)
			{
				Tools.LogError(ex.Message, ex.StackTrace);
				return false;
			}

			Tools.Print("Bans: " + mBan.Count);
			Tools.Print("TCP Lobby Server started on port " + listenPort);
#else
			catch (System.Exception) { return false; }
#endif
			mThread = Tools.CreateThread(ThreadFunction);
			mThread.Start();
			return true;
		}

		/// <summary>
		/// Stop listening for incoming packets.
		/// </summary>

		public override void Stop ()
		{
			if (mThread != null)
			{
				mThread.Interrupt();
				mThread.Join();
				mThread = null;
			}

			if (mListener != null)
			{
				mListener.Stop();
				mListener = null;

				Tools.LoadList("LobbyConfig/ban.txt", mBan);
			}
		}

		/// <summary>
		/// Start the sending process.
		/// </summary>

		BinaryWriter BeginSend (Packet type)
		{
			mBuffer = Buffer.Create();
			BinaryWriter writer = mBuffer.BeginPacket(type);
			return writer;
		}

		/// <summary>
		/// Send the outgoing buffer to the specified player.
		/// </summary>

		void EndSend (TcpProtocol tc)
		{
			mBuffer.EndPacket();
			tc.SendTcpPacket(mBuffer);
			mBuffer.Recycle();
			mBuffer = null;
		}

		/// <summary>
		/// Disconnect the specified protocol.
		/// </summary>

		void Disconnect (TcpProtocol tc)
		{
			tc.Disconnect();
			RemoveServer(tc);
		}

		/// <summary>
		/// Thread that will be processing incoming data.
		/// </summary>

		void ThreadFunction ()
		{
			for (;;)
			{
#if !STANDALONE
				if (TNManager.isPaused)
				{
					Thread.Sleep(500);
					continue;
				}
#endif
				mTime = DateTime.UtcNow.Ticks / 10000;

				// Accept incoming connections
				while (mListener != null && mListener.Pending())
				{
					Socket socket = mListener.AcceptSocket();

					try
					{
						if (socket != null && socket.Connected)
						{
							IPEndPoint remote = socket.RemoteEndPoint as IPEndPoint;

							if (remote == null || mBan.Contains(remote.Address.ToString()))
							{
								socket.Close();
							}
							else
							{
								TcpProtocol tc = new TcpProtocol();
								tc.StartReceiving(socket);
								mTcp.Add(tc);
							}
						}
					}
					catch (Exception)
					{
						if (socket != null)
						{
							try { socket.Close(); }
							catch (Exception) { }
						}
					}
				}

				Buffer buffer = null;

				// Process incoming TCP packets
				for (int i = 0; i < mTcp.size;)
				{
					TcpProtocol tc = mTcp[i];

					if (!tc.isSocketConnected)
					{
						RemoveServer(tc);
						mTcp.RemoveAt(i);
						var se = tc.Get<ServerList.Entry>("data");
						if (se != null) Tools.Print("Warning: Orphaned connection detected. Removing " + se.name);
						continue;
					}
#if STANDALONE
					else
					{
						var ent = tc.Get<ServerList.Entry>("data");
					
						if (ent != null && ent.recordTime + 30000 < mTime)
						{
							Disconnect(tc);
							mTcp.RemoveAt(i);
							Tools.Print("Warning: Time out detected. Removing " + ent.name);
							continue;
						}
					}
#endif
					while (tc.ReceivePacket(out buffer))
					{
						try { if (!ProcessPacket(buffer, tc)) Disconnect(tc); }
#if STANDALONE
						catch (System.Exception ex)
						{
							tc.LogError(ex.Message, ex.StackTrace);
							Disconnect(tc);
						}
#else
						catch (System.Exception) { Disconnect(tc); }
#endif
						if (buffer != null)
						{
							buffer.Recycle();
							buffer = null;
						}
					}

					if (tc.stage == TcpProtocol.Stage.NotConnected)
					{
						RemoveServer(tc);
						mTcp.RemoveAt(i);
					}
					else ++i;
				}

				if (buffer != null)
				{
					buffer.Recycle();
					buffer = null;
				}

				// We only want to send instant updates if the number of players is under a specific threshold
				if (mTcp.size > instantUpdatesClientLimit) mInstantUpdates = false;

				// Send the server list to all connected clients
				for (int i = 0; i < mTcp.size; ++i)
				{
					var tc = mTcp[i];

					// Skip clients that have not yet verified themselves
					if (tc.stage != TcpProtocol.Stage.Connected) continue;

					// Skip server links
					long lastSendTime = tc.Get<long>("lastSend");
					if (lastSendTime == 0) continue;

					// List hasn't changed -- do nothing
					if (lastSendTime >= mLastChange) continue;

					// Too many clients: we want the updates to be infrequent
					if (!mInstantUpdates && lastSendTime + 4000 > mTime) continue;

					// Create the server list packet
					if (buffer == null)
					{
						lock (mList.list)
						{
							buffer = Buffer.Create();
							BinaryWriter writer = buffer.BeginPacket(Packet.ResponseServerList);

							int serverCount = mList.list.size;

							for (int b = 0; b < mTcp.size; ++b)
							{
								if (!mTcp[b].isConnected) continue;
								var ent = mTcp[b].Get<ServerList.Entry>("data");
								if (ent != null) ++serverCount;
							}

							writer.Write(GameServer.gameID);
							writer.Write((ushort)serverCount);

							for (int b = 0; b < mList.list.size; ++b)
								mList.list[b].WriteTo(writer);

							for (int b = 0; b < mTcp.size; ++b)
							{
								if (!mTcp[b].isConnected) continue;
								var ent = mTcp[b].Get<ServerList.Entry>("data");
								if (ent != null) ent.WriteTo(writer);
							}
							buffer.EndPacket();
						}
					}

					tc.SendTcpPacket(buffer);
					tc.Set("lastSend", mTime);
				}

				if (buffer != null)
				{
					buffer.Recycle();
					buffer = null;
				}

				try { Thread.Sleep(1); }
				catch (System.Threading.ThreadInterruptedException) { return; }
			}
		}

		/// <summary>
		/// Process an incoming packet.
		/// </summary>

		bool ProcessPacket (Buffer buffer, TcpProtocol tc)
		{
			var reader = buffer.BeginReading();

			// TCP connections must be verified first to ensure that they are using the correct protocol
			if (tc.stage == TcpProtocol.Stage.Verifying)
			{
				if (tc.VerifyRequestID(reader, buffer, false))
				{
					BinaryWriter writer = tc.BeginSend(Packet.ResponseID);
					writer.Write(TcpPlayer.version);
					writer.Write(tc.id);
					writer.Write((Int64)(System.DateTime.UtcNow.Ticks / 10000));
					tc.EndSend();
					return true;
				}
				Tools.Print(tc.address + " has failed the verification step");
				return false;
			}

			var request = (Packet)reader.ReadByte();

			switch (request)
			{
				case Packet.RequestPing:
				{
					var writer = BeginSend(Packet.ResponsePing);
					writer.Write(mTime);
					writer.Write((ushort)mList.list.size);
					EndSend(tc);
					break;
				}
				case Packet.RequestServerList:
				{
					if (reader.ReadUInt16() != GameServer.gameID) return false;
					tc.Set("lastSend", 1);
					return true;
				}
				case Packet.RequestAddServer:
				{
					if (reader.ReadUInt16() != GameServer.gameID) return false;
					ServerList.Entry ent = new ServerList.Entry();
					ent.ReadFrom(reader);

					if (mBan.Count != 0 && (mBan.Contains(ent.externalAddress.Address.ToString()) || IsBanned(ent.name))) return false;

					if (ent.externalAddress.Address.Equals(IPAddress.None) ||
						ent.externalAddress.Address.Equals(IPAddress.IPv6None))
						ent.externalAddress = tc.tcpEndPoint;

					AddServer(ent, tc);
					return true;
				}
				case Packet.RequestRemoveServer:
				{
					if (reader.ReadUInt16() != GameServer.gameID) return false;
					IPEndPoint internalAddress, externalAddress;
					Tools.Serialize(reader, out internalAddress);
					Tools.Serialize(reader, out externalAddress);
					RemoveServer(tc);
					return true;
				}
				case Packet.Disconnect:
				{
					return false;
				}
				case Packet.RequestSaveFile:
				{
					string fileName = reader.ReadString();
					byte[] data = reader.ReadBytes(reader.ReadInt32());
					SaveFile(fileName, data);
					break;
				}
				case Packet.RequestLoadFile:
				{
					string fn = reader.ReadString();
					byte[] data = LoadFile(fn);

					BinaryWriter writer = BeginSend(Packet.ResponseLoadFile);
					writer.Write(fn);

					if (data != null)
					{
						writer.Write(data.Length);
						writer.Write(data);
					}
					else writer.Write(0);
					EndSend(tc);
					break;
				}
				case Packet.RequestDeleteFile:
				{
					DeleteFile(reader.ReadString());
					break;
				}
				case Packet.RequestHTTPGet:
				{
					if (tc.stage == TcpProtocol.Stage.WebBrowser)
					{
						// string requestText = reader.ReadString();
						// Example of an HTTP request:
						// GET / HTTP/1.1
						// Host: 127.0.0.1:5127
						// Connection: keep-alive
						// User-Agent: Chrome/47.0.2526.80
						int serverCount = 0, playerCount = 0;

						// Detailed list of clients
						for (int i = 0; i < mTcp.size; ++i)
						{
							TcpProtocol p = mTcp[i];

							if (p.stage == TcpProtocol.Stage.Connected)
							{
								var ent = p.Get<ServerList.Entry>("data");

								if (ent != null)
								{
									++serverCount;
									playerCount += ent.playerCount;
								}
							}
						}

						// Number of connected clients
						StringBuilder sb = new StringBuilder();
						sb.Append("Servers: ");
						sb.AppendLine(serverCount.ToString());
						sb.Append("Players: ");
						sb.AppendLine(playerCount.ToString());

						// Detailed list of clients
						for (int i = 0; i < mTcp.size; ++i)
						{
							TcpProtocol p = mTcp[i];

							if (p.stage == TcpProtocol.Stage.Connected)
							{
								var ent = p.Get<ServerList.Entry>("data");

								if (ent != null)
								{
									sb.Append(ent.playerCount);
									sb.Append(" ");
									sb.AppendLine(ent.name);
								}
							}
						}

						// Create the header indicating that the connection should be severed after receiving the data
						string text = sb.ToString();
						sb = new StringBuilder();
						sb.AppendLine("HTTP/1.1 200 OK");
						sb.AppendLine("Server: TNet 3");
						sb.AppendLine("Content-Length: " + text.Length);
						sb.AppendLine("Content-Type: text/plain");
						sb.AppendLine("Connection: Closed\n");
						sb.Append(text);

						// Send the response
						mBuffer = Buffer.Create();
						BinaryWriter bw = mBuffer.BeginWriting(false);
						bw.Write(Encoding.ASCII.GetBytes(sb.ToString()));
						tc.SendTcpPacket(mBuffer);
						mBuffer.Recycle();
						mBuffer = null;
					}
					break;
				}
				case Packet.Error:
				{
					return false;
				}
			}
#if STANDALONE
		Tools.Print(tc.address + " sent a packet not handled by the lobby server: " + request);
#endif
			return false;
		}

		/// <summary>
		/// Add a new server to the list.
		/// </summary>

		void AddServer (ServerList.Entry ent, TcpProtocol tcp)
		{
			ent.recordTime = mTime;
			tcp.Set("data", ent);
			AddServer(ent.name, ent.playerCount, ent.internalAddress, ent.externalAddress);
		}

		/// <summary>
		/// Remove all entries added by the specified client.
		/// </summary>

		bool RemoveServer (TcpProtocol tcp)
		{
			var ent = tcp.Get<ServerList.Entry>("data");
			if (ent != null) { RemoveServer(ent.internalAddress, ent.externalAddress); return true; }
			return false;
		}

		/// <summary>
		/// Add a new server to the list.
		/// </summary>

		public override void AddServer (string name, int playerCount, IPEndPoint internalAddress, IPEndPoint externalAddress)
		{
			mLastChange = mTime;
#if STANDALONE
			var ent = mList.Add(name, playerCount, internalAddress, externalAddress, mTime);
			Tools.Print("[+] " + ent.name + " (" + ent.playerCount + ")");
#else
			mList.Add(name, playerCount, internalAddress, externalAddress, mTime);
#endif
		}

		/// <summary>
		/// Remove an existing server from the list.
		/// </summary>

		public override void RemoveServer (IPEndPoint internalAddress, IPEndPoint externalAddress)
		{
			var ent = mList.Remove(internalAddress, externalAddress);

			if (ent != null)
			{
				mLastChange = mTime;
#if STANDALONE
				Tools.Print("[-] " + ent.name);
#endif
			}
		}
	}
}
