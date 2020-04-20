using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraTarget : MonoBehaviour {

	public GameObject target;
	public float ease = 10f;

    void FixedUpdate() {
        transform.position = new Vector3(CameraControls.ease(transform.position.x, target.transform.position.x, ease), CameraControls.ease(transform.position.y, target.transform.position.y, ease), CameraControls.ease(transform.position.z, target.transform.position.z, ease)); 
    }
}