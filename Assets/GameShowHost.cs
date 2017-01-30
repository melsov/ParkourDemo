using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameShowHost : MonoBehaviour {

    public Wheel wheel;
    public Text spindexText;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        spindexText.text = string.Format("{0}", wheel.spindex);
	}
}
