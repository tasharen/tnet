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

	static public int count { get { return mList.Count; } }

	static public int GetUpdateCount() { var c = mUpdateCount; mUpdateCount = 0; return c; }

	public float lastUpdateTime = 0f;

	void OnEnable () { mList.Add(this); }

	void OnDisable () { mList.Remove(this); }

	void Update ()
	{
		if (!tno.canSend) return;
		if (tno.isMine && !ExampleStressTest.instance) { DestroySelf(); return; }

		++mUpdateCount;

		if (Random.value > 0.9f) tno.Send("NoArgFunc", Target.AllSaved);
		else if (tno.isMine) tno.RemoveSavedRFC("NoArgFunc");

		if (Random.value > 0.9f) tno.Send("OneArgFunc", Target.AllSaved, Time.time);
		else if (tno.isMine) tno.RemoveSavedRFC("OneArgFunc");
		
		if (Random.value > 0.9f) tno.Send("TwoArgFunc", Target.AllSaved, Time.time, TNManager.serverTime);
		else if (tno.isMine) tno.RemoveSavedRFC("TwoArgFunc");
		
		if (Random.value > 0.9f) tno.Send("ThreeArgFunc", Target.AllSaved, Time.time, "Third", true);
		else if (tno.isMine) tno.RemoveSavedRFC("ThreeArgFunc");
	}

	[RFC] void NoArgFunc () { }

	[RFC] void OneArgFunc (float val) { lastUpdateTime = val; }

	[RFC] void TwoArgFunc (float val, long serverTime) { lastUpdateTime = val; }

	[RFC] void ThreeArgFunc (float val, string test, bool b) { lastUpdateTime = val; }
}
