using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wheel : MonoBehaviour {

    public float spinForce = 50f;
    public bool isDivisionOrderCCW = true;
    private Rigidbody rb;
    private bool spinning;

    public delegate void SpinIsDone();
    public SpinIsDone spinIsDone;

    public int divisions = 12;
    private float degreesPerWedge {
        get {
            return 360f / divisions;
        }
    }

    public int spindex {
        get {
            float ang = spinAngleDeg;
            if(ang < 0f) {
                ang += 360;
            }
            if(ang > 359f) {
                ang -= 360;
            }
            print(ang);

            int result = Mathf.FloorToInt(ang / degreesPerWedge);
            if(isDivisionOrderCCW) {
                return divisions - 1 - result;
            }
            return result;
        }
    }

    private float spinAngleDeg {
        get {
            return rb.transform.rotation.eulerAngles.z;
        }
    }

	// Use this for initialization
	void Start () {
        rb = GetComponent<Rigidbody>();
	}
	
	// Update is called once per frame
	void Update () {
        if(Input.GetKeyDown(KeyCode.Space)) {
            spin();
        }
	}

    private void spin() {
        if(spinning) {
            return;
        }

        StartCoroutine(takeASpin());
    }

    private IEnumerator takeASpin() {
        spinning = true;
        rb.AddTorque(rb.transform.forward * spinForce);
        
        while(rb.angularVelocity.sqrMagnitude > .2f) {
            yield return new WaitForFixedUpdate();
        }

        spinIsDone();
        spinning = false;
    }
}
