using UnityEngine;
using TNet;

/// <summary>
/// This is NOT how networking should be done. NEVER send packets in every Update(). Packets should only be sent infrequently.
/// In this case, this is is an absolute stress test meant to simulate an absurd scenario, which is why I send packets in every Update().
/// </summary>

public class ExampleStressObj : TNBehaviour
{
	[System.NonSerialized] static System.Collections.Generic.List<ExampleStressObj> mList = new System.Collections.Generic.List<ExampleStressObj>();
	[System.NonSerialized] static int mUpdateCount = 0;

	public bool debug = false;

	static public int count { get { return mList.Count; } }

	static public int GetUpdateCount() { var c = mUpdateCount; mUpdateCount = 0; return c; }

	[System.NonSerialized] bool mCanSend = true;
	[System.NonSerialized] bool mDestroySent = false;

	void OnEnable () { mList.Add(this); tno.onDestroy += OnDestroyNotification; }

	void OnDestroyNotification ()
	{
		if (debug && ExampleStressTest.onAddToChat != null) ExampleStressTest.onAddToChat("Receiving destroy packets", Color.green);
	}

	void OnDisable () { mList.Remove(this); }

	void Update ()
	{
		if (!tno.canSend) return;

		if (tno.isMine && !ExampleStressTest.instance)
		{
			if (debug && ExampleStressTest.onAddToChat != null) ExampleStressTest.onAddToChat("Sending destroy packets", Color.green);
			DestroySelf();
			mDestroySent = true;
			return;
		}

		if (mDestroySent)
		{
			if (debug && ExampleStressTest.onAddToChat != null) ExampleStressTest.onAddToChat("Should not be here", Color.red);
			return;
		}

		if (ExampleStressTest.mute) return;

		//if (!mCanSend) return;

		mCanSend = false;
		++mUpdateCount;

		if (Random.value < 0.9f) tno.Send("NoArgFunc", Target.OthersSaved);
		else if (tno.isMine) tno.RemoveSavedRFC("NoArgFunc");

		if (Random.value < 0.9f) tno.Send("OneArgFunc", Target.OthersSaved, TNManager.playerID);
		else if (tno.isMine) tno.RemoveSavedRFC("OneArgFunc");
		
		if (Random.value < 0.9f) tno.Send("TwoArgFunc", Target.OthersSaved, TNManager.playerID, TNManager.serverTime);
		else if (tno.isMine) tno.RemoveSavedRFC("TwoArgFunc");
		
		if (Random.value < 0.9f) tno.Send("ThreeArgFunc", Target.OthersSaved, TNManager.playerID, "Third", true);
		else if (tno.isMine) tno.RemoveSavedRFC("ThreeArgFunc");

		tno.Send("UnlockSending", Target.All, TNManager.playerID, TNManager.serverTime);
	}

	[RFC] void NoArgFunc () { }

	[RFC] void OneArgFunc (int pid) { }

	[RFC] void TwoArgFunc (int pid, long serverTime) { }

	[RFC] void ThreeArgFunc (int pid, string test, bool b) { }

	[RFC] void UnlockSending (int pid, long time)
	{
		if (debug && ExampleStressTest.onAddToChat != null) 
		{
			var delta = (float)((TNManager.serverTime - time) * 0.001);
			ExampleStressTest.onAddToChat($"Send complete by player {pid}: sent {delta} seconds ago", Color.white);
			#if UNITY_EDITOR
			Debug.Log("Send complete by " + pid, this);
			#endif
		}

		if (pid == TNManager.playerID) mCanSend = true;
	}
}
