using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotate : MonoBehaviour {

	// Use this for initialization
	void Start () {
	}
	
	// Update is called once per frame
	void Update () {
		var tr = gameObject.transform;
		tr.Rotate (1.5f, 0.0f, 0.0f);
	}
}
