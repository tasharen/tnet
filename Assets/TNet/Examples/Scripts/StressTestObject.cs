using UnityEngine;
using System.Collections;
using TNet;

public class StressTestObject : TNBehaviour
{
	void Start ()
	{
		StartCoroutine(PeriodicUpdate());
		StartCoroutine(DestroyAfterDelay());
	}

	IEnumerator PeriodicUpdate ()
	{
		for (; ; )
		{
			yield return new WaitForSeconds(Random.Range(0.04f, 0.06f));
			if (tno.isMine) tno.Send("STO", Target.OthersSaved, "Testing STO");
		}
	}

	[RFC]
	void STO (string data) { }

	IEnumerator DestroyAfterDelay ()
	{
		yield return new WaitForSeconds(Random.Range(0.3f, 0.4f));

		for (; ; )
		{
			if (tno.isMine)
			{
				tno.DestroySelf();
				break;
			}
			yield return null;
		}
	}
}
