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
		public byte[] buffer;
	}

	public int id;
	public string password;
	public BetterList<Player> players = new BetterList<Player>();
	public BetterList<RFC> rpcs = new BetterList<RFC>();
	public Player host;

	/// <summary>
	/// Create a new buffered remote function call.
	/// ID should be: 16 bits of viewID and 16 bits of RFC ID.
	/// </summary>

	public RFC CreateRFC (int id)
	{
		for (int i = 0; i < rpcs.size; ++i)
		{
			RFC r = rpcs[i];
			if (r.id == id) return r;
		}

		RFC rpc = new RFC();
		rpc.id = id;
		rpcs.Add(rpc);
		return rpc;
	}

	/// <summary>
	/// Delete the specified remote function call.
	/// </summary>

	public void DeleteRFC (int id)
	{
		for (int i = 0; i < rpcs.size; ++i)
		{
			RFC r = rpcs[i];

			if (r.id == id)
			{
				rpcs.RemoveAt(i);
				return;
			}
		}
	}
}
}