//-------------------------------------------------
//                    TNet 3
// Copyright © 2012-2016 Tasharen Entertainment Inc
//-------------------------------------------------

using UnityEngine;

/// <summary>
/// This script is used by the ExampleChaseCamera. It's attached to camera's parent object.
/// The ExampleChaseCamera script needs a reference to what it's working with, and this script
/// is used to provide just that. The ExampleChaseCamera script is attached to a dynamically
/// instantiated object (the example car), so it's not possible to reference this transform directly.
/// </summary>

public class ExampleChaseCameraTarget : MonoBehaviour
{
	void Awake () { ExampleChaseCamera.target = transform; }
}
