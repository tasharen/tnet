//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;

/// <summary>
/// Simple single-player version of the car example that doesn't have TNet-based multiplayer.
/// </summary>

[RequireComponent(typeof(Rigidbody))]
public class ExampleCarNoNetworking : MonoBehaviour
{
	protected class Wheel
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

	protected Rigidbody mRb;
	protected Vector2 mInput;
	protected Wheel mFL;
	protected Wheel mFR;
	protected Wheel mRL;
	protected Wheel mRR;

	/// <summary>
	/// Cache the local variables.
	/// </summary>

	protected virtual void Awake ()
	{
		mRb = GetComponent<Rigidbody>();
		mFL = new Wheel(frontLeft);
		mFR = new Wheel(frontRight);
		mRL = new Wheel(rearLeft);
		mRR = new Wheel(rearRight);
	}

	/// <summary>
	/// Update the movement axes based on user input.
	/// </summary>

	protected virtual void Update ()
	{
		mInput.x = Input.GetAxis("Horizontal");
		mInput.y = Input.GetAxis("Vertical");
	}

	/// <summary>
	/// Update the input and update the wheels, moving the car.
	/// </summary>

	protected virtual void FixedUpdate ()
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

#if UNITY_4_3 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7
	protected void UpdateWheel (Wheel w, float steer, float drive)
	{
		Transform wheelRenderer = w.t.GetChild(0);
		float rpmFactor = Mathf.Clamp01(Mathf.Abs(w.col.rpm) / maxRPM);
		float torque = drive * motorTorque * mInput.y * (1f - rpmFactor * rpmFactor);
		w.col.brakeTorque = (1f - Mathf.Abs(mInput.y)) * motorTorque;
		w.col.motorTorque = torque;

		// Turn the wheel
		Vector3 euler = w.t.localEulerAngles;
		euler.y = steer * 20f * mInput.x;
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
	}
#else // Unity 5+
	protected void UpdateWheel (Wheel w, float steer, float drive)
	{
		Transform wheelRenderer = w.t.GetChild(0);
		float rpmFactor = Mathf.Clamp01(Mathf.Abs(w.col.rpm) / maxRPM);
		float torque = drive * motorTorque * mInput.y * (1f - rpmFactor * rpmFactor);
		w.col.brakeTorque = (1f - Mathf.Abs(mInput.y)) * motorTorque;
		w.col.motorTorque = torque * 3f;

		// Turn the wheel
		Vector3 euler = w.t.localEulerAngles;
		euler.y = steer * 20f * mInput.x;
		w.col.steerAngle = euler.y;

		// Position the renderer
		Vector3 pos;
		Quaternion rot;
		w.col.GetWorldPose(out pos, out rot);
		wheelRenderer.position = pos;
		wheelRenderer.rotation = rot;
	}
#endif
}
