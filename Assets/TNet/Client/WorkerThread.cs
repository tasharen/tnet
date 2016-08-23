//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;
using System.Threading;
using System.Collections.Generic;

namespace TNet
{
/// <summary>
/// Worker thread is a convenience class that can execute specified code on a separate thread.
/// The worker thread class takes care of creating multiple threads for concurrent code execution.
/// </summary>

public class WorkerThread : MonoBehaviour
{
	// Recommended setting is 2 threads per CPU, but I am setting it to 1 by default since I don't want to assume 100% CPU usage
	const int threadsPerCore = 1;
	
	// How much time the worker thread's update function is allowed to take per frame.
	// Note that this value simply means that if it's exceeded, no more functions will be executed on this frame, not that it will pause mid-execution.
	const long MAX_MILLISECONDS_PER_FRAME = 4;

	static WorkerThread mInstance = null;

	public delegate bool BoolFunc ();
	public delegate void VoidFunc ();

	// Actual worker thread
	Thread[] mThreads = null;
	int[] mLoad = null;

	class Entry
	{
		public VoidFunc main;
		public VoidFunc finished;
		public BoolFunc mainBool;		// Return 'true' when done, 'false' to execute again next update
		public BoolFunc finishedBool;	// Return 'true' when done, 'false' to execute again next update
	}

	// List of callbacks executed in order by the worker thread
	Queue<Entry> mNew = new Queue<Entry>();
	Queue<Entry> mFinished = new Queue<Entry>();
	List<Entry> mUnused = new List<Entry>();
	System.Diagnostics.Stopwatch mStopwatch = new System.Diagnostics.Stopwatch();

	/// <summary>
	/// Whether the update timer has been exceeded for this frame. Can check this inside delegates executed on the main thread and exit out early.
	/// </summary>

	static public bool mainThreadTimeExceeded
	{
		get
		{
			if (mInstance == null) return false;
			return mInstance.mStopwatch.ElapsedMilliseconds > MAX_MILLISECONDS_PER_FRAME;
		}
	}

	/// <summary>
	/// Create the worker thread.
	/// </summary>

	void OnEnable ()
	{
		if (mInstance == null)
		{
			mInstance = this;
			StartThreads();
		}
		else Destroy(this);
	}

	/// <summary>
	/// Release the mutex and destroy the worker thread.
	/// </summary>

	void OnDisable ()
	{
		if (mInstance == this)
		{
			StopThreads();
			mInstance = null;
		}
	}

	/// <summary>
	/// Start worker threads.
	/// </summary>

	void StartThreads ()
	{
		if (mThreads != null) return;

		int maxThreads = System.Environment.ProcessorCount * threadsPerCore;
		if (maxThreads < 1) maxThreads = 1;
		if (maxThreads > 32) maxThreads = 32;

		mThreads = new Thread[maxThreads];
		mLoad = new int[maxThreads];

		// Create the threads
		for (int i = 0; i < maxThreads; ++i)
		{
			int threadID = i;

			mThreads[threadID] = new Thread(delegate()
			{
				List<Entry> active = new List<Entry>();

				for (; ; )
				{
					// Check without locking first as it's faster
					if (mNew.Count > 0)
					{
						bool grab = true;

						// If this thread is not idling, check to see if others are
						if (active.size != 0)
						{
							for (int b = 0; b < maxThreads; ++b)
							{
								if (b != threadID && mLoad[b] == 0)
								{
									grab = false;
									break;
								}
							}
						}

						// No threads are idling -- grab the first queued item
						if (grab)
						{
							lock (mNew)
							{
								if (mNew.Count > 0)
								{
									active.Add(mNew.Dequeue());
									mLoad[threadID] = active.size;
								}
							}
						}
					}

					// If we are working on something, run another update
					if (active.Count > 0)
					{
						for (int b = active.size; b > 0; )
						{
							var ent = active[--b];

							try
							{
								if (ent.main != null)
								{
									ent.main();
									active.RemoveAt(b);
									if (ent.finished != null || ent.finishedBool != null) lock (mFinished) mFinished.Enqueue(ent);
									else lock (mUnused) mUnused.Add(ent);
									mLoad[threadID] = active.size;
								}
								else if (ent.mainBool != null && ent.mainBool())
								{
									active.RemoveAt(b);
									if (ent.finished != null || ent.finishedBool != null) lock (mFinished) mFinished.Enqueue(ent);
									else lock (mUnused) mUnused.Add(ent);
									mLoad[threadID] = active.size;
								}
							}
							catch (System.Exception ex)
							{
								Debug.LogError(ex.Message + "\n" + ex.StackTrace);
								active.RemoveAt(b);
							}
						}
					}
					
					// Sleep for a short amount
					try { Thread.Sleep(1); }
					catch (System.Threading.ThreadInterruptedException) { return; }
				}
			});
		}
		
		// Now that all threads have been created, start them all at once
		for (int i = 0; i < maxThreads; ++i) mThreads[i].Start();
	}

	/// <summary>
	/// Stop all active threads.
	/// </summary>

	void StopThreads ()
	{
		if (mThreads != null)
		{
			for (int i = 0; i < mThreads.Length; ++i)
			{
				Thread thread = mThreads[i];

				if (thread != null)
				{
					thread.Interrupt();
					thread.Join();
				}
			}
			mThreads = null;
		}
	}

	/// <summary>
	/// Abort the worker thread on application quit.
	/// </summary>

	void OnApplicationQuit ()
	{
		StopThreads();
		mNew.Clear();
	}

	List<Entry> mTemp = new List<Entry>();

	/// <summary>
	/// Call finished delegates on the main thread.
	/// </summary>

	void Update ()
	{
		if (mFinished.Count > 0)
		{
			mStopwatch.Reset();
			mStopwatch.Start();

			lock (mFinished)
			{
				while (mFinished.Count > 0)
				{
					var ent = mFinished.Dequeue();

					if (ent.finished != null)
					{
						ent.finished();
					}
					else if (ent.finishedBool != null && !ent.finishedBool())
					{
						mTemp.Add(ent);
						if (mStopwatch.ElapsedMilliseconds > MAX_MILLISECONDS_PER_FRAME) break;
						continue;
					}

					ent.main = null;
					ent.finished = null;
					ent.mainBool = null;
					ent.finishedBool = null;
					lock (mUnused) mUnused.Add(ent);

					if (mStopwatch.ElapsedMilliseconds > MAX_MILLISECONDS_PER_FRAME) break;
				}

				// Re-queue the conditionals
				for (int i = 0; i < mTemp.size; ++i) mFinished.Enqueue(mTemp[i]);
				mTemp.Clear();
			}
		}
	}

	/// <summary>
	/// Add a new callback function to the worker thread.
	/// </summary>

	static public void Create (VoidFunc main, VoidFunc finished = null)
	{
		if (mInstance == null)
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				if (main != null) main();
				if (finished != null) finished();
				return;
			}
#endif
			GameObject go = new GameObject("Worker Thread");
			mInstance = go.AddComponent<WorkerThread>();
		}

		Entry ent;

		if (mInstance.mUnused.size != 0)
		{
			lock (mInstance.mUnused) { ent = (mInstance.mUnused.size != 0) ? mInstance.mUnused.Pop() : new Entry(); }
		}
		else ent = new Entry();

		ent.main = main;
		ent.finished = finished;

		if (main != null) lock (mInstance.mNew) mInstance.mNew.Enqueue(ent);
		else lock (mInstance.mFinished) mInstance.mFinished.Enqueue(ent);
	}

	/// <summary>
	/// Add a new callback function to the worker thread.
	/// Return 'false' if you want the same delegate to execute again in the next Update(), or 'true' if you're done.
	/// </summary>

	static public void CreateMultiStageCompletion (VoidFunc main, BoolFunc finished = null)
	{
		if (mInstance == null)
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				if (main != null) main();
				if (finished != null) while (!finished()) { };
				return;
			}
#endif
			GameObject go = new GameObject("Worker Thread");
			mInstance = go.AddComponent<WorkerThread>();
		}

		Entry ent;

		if (mInstance.mUnused.size != 0)
		{
			lock (mInstance.mUnused) { ent = (mInstance.mUnused.size != 0) ? mInstance.mUnused.Pop() : new Entry(); }
		}
		else ent = new Entry();

		ent.main = main;
		ent.finishedBool = finished;

		if (main != null) lock (mInstance.mNew) mInstance.mNew.Enqueue(ent);
		else lock (mInstance.mFinished) mInstance.mFinished.Enqueue(ent);
	}

	/// <summary>
	/// Add a new callback function to the worker thread.
	/// The 'main' delegate will run on a secondary thread, while the 'finished' delegate will run in Update().
	/// Return 'false' if you want the same delegate to execute again next time, or 'true' if you're done.
	/// </summary>

	static public void CreateMultiStageExecution (BoolFunc main, VoidFunc finished = null)
	{
		if (mInstance == null)
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				if (main != null) { while (!main()) { } }
				if (finished != null) finished();
				return;
			}
#endif
			GameObject go = new GameObject("Worker Thread");
			mInstance = go.AddComponent<WorkerThread>();
		}

		Entry ent;

		if (mInstance.mUnused.Count != 0)
		{
			lock (mInstance.mUnused) { ent = (mInstance.mUnused.size != 0) ? mInstance.mUnused.Pop() : new Entry(); }
		}
		else ent = new Entry();

		ent.mainBool = main;
		ent.finished = finished;
		lock (mInstance.mNew) mInstance.mNew.Enqueue(ent);
	}

	/// <summary>
	/// Add a new callback function to the worker thread.
	/// The 'main' delegate will run on a secondary thread, while the 'finished' delegate will run in Update().
	/// Return 'false' if you want the same delegates to execute again next time, or 'true' if you're done.
	/// </summary>

	static public void CreateMultiStage (BoolFunc main, BoolFunc finished = null)
	{
		if (mInstance == null)
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				if (main != null) { while (!main()) { } }
				if (finished != null) { while (!finished()) { } }
				return;
			}
#endif
			GameObject go = new GameObject("Worker Thread");
			mInstance = go.AddComponent<WorkerThread>();
		}

		Entry ent;

		if (mInstance.mUnused.Count != 0)
		{
			lock (mInstance.mUnused) { ent = (mInstance.mUnused.size != 0) ? mInstance.mUnused.Pop() : new Entry(); }
		}
		else ent = new Entry();

		ent.mainBool = main;
		ent.finishedBool = finished;
		lock (mInstance.mNew) mInstance.mNew.Enqueue(ent);
	}
}
}
