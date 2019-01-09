//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2018 Tasharen Entertainment Inc
//-------------------------------------------------

using System.Net;

namespace TNet
{
	/// <summary>
	/// Simple datagram container -- contains a data buffer and the address of where it came from (or where it's going).
	/// </summary>

	public struct Datagram
	{
		public IPEndPoint ip;
		public Buffer data;

		public void Recycle (bool threadSafe = true) { if (data != null) { data.Recycle(threadSafe); data = null; } }
	}
}