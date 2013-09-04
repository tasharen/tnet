//---------------------------------------------
//            Tasharen Network
// Copyright © 2012-2013 Tasharen Entertainment
//---------------------------------------------

using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Threading;

#if UNITY_IPHONE
using System.Net.NetworkInformation;
#endif

namespace TNet
{
/// <summary>
/// Generic sets of helper functions used within TNet.
/// </summary>

static public class Tools
{
	static string mChecker = "http://checkip.dyndns.org";

	/// <summary>
	/// Get or set the URL that will perform the IP check. The URL-returned value should be in the format of:
	/// "Current IP Address: 255.255.255.255".
	/// Note that the server must have a valid policy XML if it's accessed from a Unity web player build.
	/// </summary>

	static public string ipCheckerUrl
	{
		get
		{
			return mChecker;
		}
		set
		{
			if (mChecker != value)
			{
				mChecker = value;
				mLocalAddress = null;
				mExternalAddress = null;
			}
		}
	}

	static IPAddress mLocalAddress;
	static IPAddress mExternalAddress;

	/// <summary>
	/// Generate a random port from 10,000 to 60,000.
	/// </summary>

	static public int randomPort { get { return 10000 + (int)(System.DateTime.Now.Ticks % 50000); } }

	/// <summary>
	/// Local IP address.
	/// </summary>

	static public IPAddress localAddress
	{
		get
		{
			if (mLocalAddress == null)
			{
#if UNITY_IPHONE
				NetworkInterface[] nis = NetworkInterface.GetAllNetworkInterfaces();

				foreach (NetworkInterface ni in nis)
				{
					IPInterfaceProperties IPInterfaceProperties = ni.GetIPProperties();
					UnicastIPAddressInformationCollection UnicastIPAddressInformationCollection = IPInterfaceProperties.UnicastAddresses;

					foreach (UnicastIPAddressInformation UnicastIPAddressInformation in UnicastIPAddressInformationCollection)
					{
						if (UnicastIPAddressInformation.Address.AddressFamily == AddressFamily.InterNetwork)
						{
							mLocalAddress = UnicastIPAddressInformation.Address;
							break;
						}
					}
				}
#else
				mLocalAddress = IPAddress.Loopback;

				try
				{
					IPHostEntry ent = Dns.GetHostEntry(Dns.GetHostName());

					foreach (IPAddress ip in ent.AddressList)
					{
						if (IsValidAddress(ip))
						{
							mLocalAddress = ip;
							break;
						}
					}
				}
#if DEBUG
				catch (System.Exception ex) { System.Console.WriteLine("TNTools.LocalAddress: " + ex.Message); }
#else
				catch (System.Exception) {}
#endif
#endif
			}
			return mLocalAddress;
		}
	}

	/// <summary>
	/// Immediately retrieve the external IP address.
	/// Note that if the external address is not yet known, this operation may hold up the application.
	/// </summary>

	static public IPAddress externalAddress
	{
		get
		{
			if (mExternalAddress == null)
				mExternalAddress = GetExternalAddress();
			return mExternalAddress != null ? mExternalAddress : localAddress;
		}
	}

	public delegate void OnResolvedIPs (IPAddress local, IPAddress ext);
	
	/// <summary>
	/// Since calling "localAddress" and "externalAddress" would lock up the application, it's better to do it asynchronously.
	/// </summary>

	static public void ResolveIPs (OnResolvedIPs del)
	{
		Thread th = new Thread(ResolveThread);
		th.Start(del);
	}

	/// <summary>
	/// Thread function that resolves IP addresses.
	/// </summary>

	static void ResolveThread (object obj)
	{
		OnResolvedIPs callback = (OnResolvedIPs)obj;
		IPAddress local = localAddress;
		IPAddress ext = externalAddress;
		if (callback != null) callback(local, ext);
	}

	/// <summary>
	/// Determine the external IP address by accessing an external web site.
	/// </summary>

	static IPAddress GetExternalAddress ()
	{
#if UNITY_WEBPLAYER
		// HttpWebRequest.Create is not supported in the Unity web player
		return localAddress;
#else
		WebRequest web = HttpWebRequest.Create(mChecker);
		web.Timeout = 3000;

		// "Current IP Address: xxx.xxx.xxx.xxx"
		string response = GetResponse(web);
		if (string.IsNullOrEmpty(response)) return localAddress;

		string[] split1 = response.Split(':');
		if (split1.Length < 2) return localAddress;

		string[] split2 = split1[1].Trim().Split('<');
		return ResolveAddress(split2[0]);
#endif
	}

	/// <summary>
	/// Helper function that determines if this is a valid address.
	/// </summary>

	static public bool IsValidAddress (IPAddress address)
	{
		if (address.AddressFamily != AddressFamily.InterNetwork) return false;
		if (address.Equals(IPAddress.Loopback)) return false;
		if (address.Equals(IPAddress.None)) return false;
		if (address.Equals(IPAddress.Any)) return false;
		return true;
	}

	/// <summary>
	/// Helper function that resolves the remote address.
	/// </summary>

	static public IPAddress ResolveAddress (string address)
	{
		if (string.IsNullOrEmpty(address))
			return null;

		IPAddress ip;
		if (IPAddress.TryParse(address, out ip))
			return ip;

		try
		{
			IPAddress[] ips = Dns.GetHostAddresses(address);

			for (int i = 0; i < ips.Length; ++i)
				if (!IPAddress.IsLoopback(ips[i]))
					return ips[i];
		}
#if UNITY_EDITOR
		catch (System.Exception ex)
		{
			UnityEngine.Debug.LogWarning(ex.Message + " (" + address + ")");
		}
#else
		catch (System.Exception) {}
#endif
		return null;
	}

	/// <summary>
	/// Given the specified address and port, get the end point class.
	/// </summary>

	static public IPEndPoint ResolveEndPoint (string address, int port)
	{
		IPEndPoint ip = ResolveEndPoint(address);
		if (ip != null) ip.Port = port;
		return ip;
	}

	/// <summary>
	/// Given the specified address, get the end point class.
	/// </summary>

	static public IPEndPoint ResolveEndPoint (string address)
	{
		int port = 0;
		string[] split = address.Split(':');

		// Automatically try to parse the port
		if (split.Length > 1)
		{
			address = split[0];
			int.TryParse(split[1], out port);
		}

		IPAddress ad = ResolveAddress(address);
		return (ad != null) ? new IPEndPoint(ad, port) : null;
	}

	/// <summary>
	/// Converts 192.168.1.1 to 192.168.1.
	/// </summary>

	static public string GetSubnet (IPAddress ip)
	{
		if (ip == null) return null;
		string addr = ip.ToString();
		int last = addr.LastIndexOf('.');
		if (last == -1) return null;
		return addr.Substring(0, last);
	}

	/// <summary>
	/// Helper function that returns the response of the specified web request.
	/// </summary>

	static public string GetResponse (WebRequest request)
	{
		string response = "";

		try
		{
			WebResponse webResponse = request.GetResponse();
			Stream stream = webResponse.GetResponseStream();

			byte[] bytes = new byte[2048];

			for (; ; )
			{
				int count = stream.Read(bytes, 0, bytes.Length);
				if (count > 0) response += Encoding.ASCII.GetString(bytes, 0, count);
				else break;
			}
		}
		catch (System.Exception)
		{
			return null;
		}
		return response;
	}

	/// <summary>
	/// Serialize the IP end point.
	/// </summary>

	static public void Serialize (BinaryWriter writer, IPEndPoint ip)
	{
		byte[] bytes = ip.Address.GetAddressBytes();
		writer.Write((byte)bytes.Length);
		writer.Write(bytes);
		writer.Write((ushort)ip.Port);
	}

	/// <summary>
	/// Deserialize the IP end point.
	/// </summary>

	static public void Serialize (BinaryReader reader, out IPEndPoint ip)
	{
		byte[] bytes = reader.ReadBytes(reader.ReadByte());
		int port = reader.ReadUInt16();
		ip = new IPEndPoint(new IPAddress(bytes), port);
	}

	/// <summary>
	/// Write the channel's data into the specified writer.
	/// </summary>

	/*static public void Serialize (BinaryWriter writer, byte[] data)
	{
		int count = (data != null) ? data.Length : 0;

		if (count < 255)
		{
			writer.Write((byte)count);
		}
		else
		{
			writer.Write((byte)255);
			writer.Write(count);
		}
		if (count > 0) writer.Write(data);
	}

	/// <summary>
	/// Read the channel's data from the specified reader.
	/// </summary>

	static public void Serialize (BinaryReader reader, out byte[] data)
	{
		int count = reader.ReadByte();
		if (count == 255) count = reader.ReadInt32();
		data = (count > 0) ? reader.ReadBytes(count) : null;
	}*/
}
}
