namespace TNet
{
/// <summary>
/// A channel contains one or more players. All information broadcast by players is visible by others in the same channel.
/// </summary>

public class Channel
{
	public class RFC
	{
		public int id;
		public Buffer buffer;
	}

	public class CreatedObject
	{
		public short objectID;
		public int uniqueID;
		public Buffer buffer;
	}

	public int id;
	public string password;
	public BetterList<Player> players = new BetterList<Player>();
	public BetterList<RFC> rfcs = new BetterList<RFC>();
	public BetterList<CreatedObject> created = new BetterList<CreatedObject>();
	public BetterList<int> destroyed = new BetterList<int>();
	public Player host;
	public int viewCounter = 0;

	/// <summary>
	/// Create a new buffered remote function call.
	/// </summary>

	public RFC CreateRFC (int inID)
	{
		for (int i = 0; i < rfcs.size; ++i)
		{
			RFC r = rfcs[i];
			if (r.id == inID) return r;
		}

		RFC rpc = new RFC();
		rpc.id = inID;
		rfcs.Add(rpc);
		return rpc;
	}

	/// <summary>
	/// Delete the specified remote function call.
	/// </summary>

	public void DeleteRFC (int inID)
	{
		for (int i = 0; i < rfcs.size; ++i)
		{
			RFC r = rfcs[i];

			if (r.id == inID)
			{
				rfcs.RemoveAt(i);
				if (r.buffer != null && r.buffer.MarkAsUnused()) Connection.ReleaseBuffer(r.buffer);
			}
		}
	}

	/// <summary>
	/// Delete the specified remote function call.
	/// </summary>

	public void DeleteObjectRFCs (int objectID)
	{
		objectID <<= 8;

		for (int i = 0; i < rfcs.size; )
		{
			RFC r = rfcs[i];

			if ((r.id & objectID) == objectID)
			{
				rfcs.RemoveAt(i);
				if (r.buffer != null && r.buffer.MarkAsUnused()) Connection.ReleaseBuffer(r.buffer);
				continue;
			}
			++i;
		}
	}
}
}