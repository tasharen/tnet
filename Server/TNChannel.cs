namespace TNet
{
/// <summary>
/// A channel contains one or more players. All information broadcast by players is visible by others in the same channel.
/// </summary>

public class Channel
{
	public class RFC
	{
		public int viewID;
		public short rfcID;
		public byte[] buffer;
	}

	public class CreatedObject
	{
		public short objectID;
		public int uniqueID;
		public byte[] buffer;
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

	public RFC CreateRFC (int viewID, short rfcID)
	{
		for (int i = 0; i < rfcs.size; ++i)
		{
			RFC r = rfcs[i];
			if (r.viewID == viewID && r.rfcID == rfcID) return r;
		}

		RFC rpc = new RFC();
		rpc.viewID = viewID;
		rpc.rfcID = rfcID;
		rfcs.Add(rpc);
		return rpc;
	}

	/// <summary>
	/// Delete the specified remote function call.
	/// </summary>

	public void DeleteRFC (int viewID, short rfcID)
	{
		for (int i = 0; i < rfcs.size; ++i)
		{
			RFC r = rfcs[i];

			if (r.viewID == viewID && r.rfcID == rfcID)
			{
				rfcs.RemoveAt(i);
				return;
			}
		}
	}
}
}