//------------------------------------------
//            Tasharen Network
// Copyright © 2012 Tasharen Entertainment
//------------------------------------------

using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;

namespace TNet
{
/// <summary>
/// Generic sets of helper functions used within TNet.
/// </summary>

static public class Tools
{
	static IPAddress mLocalAddress;
	static IPAddress mExternalAddress;

	/// <summary>
	/// Local IP address.
	/// </summary>

	static public IPAddress localAddress
	{
		get
		{
			if (mLocalAddress == null)
			{
				IPAddress[] ips = Dns.GetHostAddresses(Dns.GetHostName());

				for (int i = 0; i < ips.Length; ++i)
				{
					IPAddress addr = ips[i];

					if (IsValidAddress(addr))
					{
						mLocalAddress = addr;
						break;
					}
				}
			}
			return mLocalAddress;
		}
	}

	/// <summary>
	/// External IP address.
	/// </summary>

	static public IPAddress externalAddress
	{
		get
		{
			if (mExternalAddress == null) mExternalAddress = GetExternalAddress();
			return mExternalAddress;
		}
	}

	/// <summary>
	/// Determine the external IP address by accessing an external web site.
	/// </summary>

	static IPAddress GetExternalAddress ()
	{
		WebRequest web = HttpWebRequest.Create("http://checkip.dyndns.org");
		web.Timeout = 3000;

		// "Current IP Address: xxx.xxx.xxx.xxx"
		string response = GetResponse(web);
		if (string.IsNullOrEmpty(response)) return localAddress;

		string[] split1 = response.Split(':');
		if (split1.Length < 2) return localAddress;

		string[] split2 = split1[1].Trim().Split('<');
		return ResolveAddress(split2[0]);
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
		if (string.IsNullOrEmpty(address)) return null;

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
			UnityEngine.Debug.LogError(ex.Message + " (" + address + ")");
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
		string[] split = address.Split(new char[':']);

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
}
}
