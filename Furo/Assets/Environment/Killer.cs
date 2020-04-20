using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Killer : MonoBehaviour {

	public Transform respawn;
	public Transform target;

	private void OnTriggerEnter(Collider other) {
    	other.gameObject.transform.position = respawn.position;
    	Camera.main.transform.position = respawn.position;
    	target.position = respawn.position;
    }
}