//------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//------------------------------------------

using UnityEngine;
using TNet;

/// <summary>
/// Barebone car script showing how to efficiently synchronize objects with very few network packets.
/// </summary>

[RequireComponent(typeof(Rigidbody))]
public class ExampleCar : TNBehaviour
{
	// Used by the ExampleChaseCamera
	static public ExampleCar mine;

	class Wheel
	{
		public Transform t;
		public WheelCollider col;
		public float rotation = 0f;

		public Wheel (Transform t)
		{
			this.t = t;
			col = t.GetComponent<WheelCollider>();
		}
	}

	public Transform centerOfMass;
	public Transform frontLeft;
	public Transform frontRight;
	public Transform rearLeft;
	public Transform rearRight;
	public float motorTorque = 3f;
	public float maxRPM = 300f;
	public bool showGUI = true;

	/// <summary>
	/// Maximum number of updates per second when synchronizing input axes. The actual number of updates may be less if nothing is changing.
	/// </summary>

	[Range(1f, 20f)]
	public float inputUpdates = 10f;

	/// <summary>
	/// Maximum number of updates per second when synchronizing the rigidbody.
	/// </summary>

	[Range(0.25f, 5f)]
	public float rigidbodyUpdates = 1f;

	Rigidbody mRb;
	Vector2 mInput;
	Vector2 mLastInput;
	float mLastInputSend = 0f;
	float mNextRB = 0f;
	Wheel mFL;
	Wheel mFR;
	Wheel mRL;
	Wheel mRR;

	/// <summary>
	/// Cache the local variables.
	/// </summary>

	void Awake ()
	{
		mRb = GetComponent<Rigidbody>();
		mFL = new Wheel(frontLeft);
		mFR = new Wheel(frontRight);
		mRL = new Wheel(rearLeft);
		mRR = new Wheel(rearRight);
	}

	/// <summary>
	/// If this is our car, we want to set a global static value, making it accessible from the outside.
	/// </summary>

	void Start () { if (tno.isMine) mine = this; }

	/// <summary>
	/// RFC for the input will be called several times per second.
	/// </summary>

	[RFC] void SetAxis (Vector2 v) { mInput = v; }
	
	/// <summary>
	/// RFC for the rigidbody will be called every couple of seconds.
	/// </summary>

	[RFC]
	void SetRB (Vector3 pos, Quaternion rot, Vector3 vel, Vector3 angVel)
	{
		mRb.position = pos;
		mRb.rotation = rot;
		mRb.velocity = vel;
		mRb.angularVelocity = angVel;
	}

	/// <summary>
	/// Handy function that checks to see if the two input values have actually changed.
	/// </summary>

	static bool Changed (float before, float after, float threshold)
	{
		if (before == after) return false;
		if (after == 0f || after == 1f) return true;
		if (Mathf.Abs(before - after) > threshold) return true;
		return false;
	}

	/// <summary>
	/// Only the car's owner should be updating the movement axes.
	/// </summary>

	void Update ()
	{
		if (tno.isMine)
		{
			float time = Time.time;
			mInput.x = Input.GetAxis("Horizontal");
			mInput.y = Input.GetAxis("Vertical");

			float delta = time - mLastInputSend;
			float delay = 1f / inputUpdates;

			// Don't send updates more than 20 times per second
			if (delta > 0.05f)
			{
				// The closer we are to the desired send time, the smaller is the deviation required to send an update.
				float threshold = Mathf.Clamp01(delta - delay) * 0.5f;

				if (Changed(mLastInput.x, mInput.x, threshold) || Changed(mLastInput.y, mInput.y, threshold))
				{
					mLastInputSend = time;
					mLastInput = mInput;
					tno.Send("SetAxis", Target.OthersSaved, mInput);
				}
			}

			// Since the input is sent frequently, rigidbody only needs to be corrected every couple of seconds.
			// Faster-paced games will require more frequent updates.
			if (mNextRB < time)
			{
				mNextRB = time + 1f / rigidbodyUpdates;
				tno.Send("SetRB", Target.OthersSaved, mRb.position, mRb.rotation, mRb.velocity, mRb.angularVelocity);
			}
		}
	}

	/// <summary>
	/// Update the input and update the wheels, moving the car.
	/// </summary>

	void FixedUpdate ()
	{
		// Keep the center of mass low to make it more difficult for the car to flip over
		mRb.centerOfMass = centerOfMass.localPosition;

		// Update the wheels: front wheels steer, all wheels drive
		UpdateWheel(mFL, 1f, 1f);
		UpdateWheel(mFR, 1f, 1f);
		UpdateWheel(mRL, 0f, 1f);
		UpdateWheel(mRR, 0f, 1f);

		// Handle the car falling off the edge of the world
		if (mRb.position.y < -10f)
		{
			transform.position = Vector3.up;
			mRb.velocity = Vector3.zero;
			mRb.angularVelocity = Vector3.zero;
		}
	}

	/// <summary>
	/// Update the specified wheel, applying torques and adjusting the renderer.
	/// </summary>

	void UpdateWheel (Wheel w, float steer, float drive)
	{
		Transform wheelRenderer = w.t.GetChild(0);
		float rpmFactor = Mathf.Clamp01(Mathf.Abs(w.col.rpm) / maxRPM);
		float torque = drive * motorTorque * mInput.y * (1f - rpmFactor * rpmFactor);
		w.col.brakeTorque = (1f - Mathf.Abs(mInput.y)) * motorTorque;

#if UNITY_4_3 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7
		w.col.motorTorque = torque;
#else
		w.col.motorTorque = torque * 3f;
#endif
		// Turn the wheel
		Vector3 euler = w.t.localEulerAngles;
		euler.y = steer * 20f * mInput.x;
#if UNITY_4_3 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7
		w.t.localEulerAngles = euler;

		// Spin the renderer
		w.rotation += w.col.rpm * Mathf.PI * 2f * Time.deltaTime;
		wheelRenderer.localRotation = Quaternion.Euler(w.rotation, 0f, 0f);

		// Adjust the visible suspension
		float suspension = w.col.suspensionDistance;

		if (suspension != 0f)
		{
			WheelHit hit;
			float currentSuspension = -w.col.suspensionDistance;

			if (w.col.GetGroundHit(out hit))
			{
				Vector3 hitPos = hit.point;
				float f = w.col.transform.InverseTransformPoint(hitPos).y;
				currentSuspension = f + w.col.radius;
			}

			wheelRenderer.localPosition = new Vector3(0f, currentSuspension, 0f);
		}
#else
		w.col.steerAngle = euler.y;

		// Position the renderer
		Vector3 pos;
		Quaternion rot;
		w.col.GetWorldPose(out pos, out rot);
		wheelRenderer.position = pos;
		wheelRenderer.rotation = rot;
#endif
	}

	/// <summary>
	/// Make the number of input and rigidbody updates configurable at run-time to see the effect.
	/// </summary>

	void OnGUI ()
	{
		if (showGUI && tno.isMine)
		{
			GUI.color = Color.black;

			Rect rect = new Rect(10f, 80f, 200f, 20f);
			GUI.Label(rect, "Input sync per second: " + inputUpdates);
			rect.y += 15f;
			inputUpdates = GUI.HorizontalSlider(rect, inputUpdates, 1f, 20f);

			rect.y += 20f;
			GUI.Label(rect, "RB sync per second: " + rigidbodyUpdates);
			rect.y += 15f;
			rigidbodyUpdates = GUI.HorizontalSlider(rect, rigidbodyUpdates, 0.25f, 5f);
		}
	}
}
