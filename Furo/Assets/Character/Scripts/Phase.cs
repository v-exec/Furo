using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Phase : MonoBehaviour {

	public GameObject player;
	public GameObject ghost;
	public GameObject copy;

	public Movement mov;

	public float projectionDistance = 15f;
	public float projectionDownwardDistance = 8f;
	public int projectionSubdivisions = 10;
	public float cooldownSeconds = 0.5f;

	public LayerMask ignoredLayers;

	private Material playerMat;
	private Material copyMat;

	private bool phasing = false;
	private Transform projection;

	private bool vault = false;
	private bool climb = false;
	private bool jump = false;
	private Transform falseTether;
	private Vector3 positionInRelationToTether;

	void Start() {
		playerMat = player.transform.Find("Model").GetComponent<Renderer>().material;
		copyMat = copy.transform.Find("Model").GetComponent<Renderer>().material;
	}

	void Update() {
		vault = mov.vault;
		climb = mov.climb;
		jump = mov.jump;
	}

	void LateUpdate() {
		ghost.transform.position = new Vector3(0,0,0);
		if (!phasing) copy.transform.position = new Vector3(0,0,0);

		if (Input.GetMouseButton(1) && !phasing) {
			projectToLocation();
			Time.timeScale = 0.2f;
		} else if (Time.timeScale != 1.0f) Time.timeScale = 1.0f;
	}

	void projectToLocation() {
		//raycast towards camera forward vector to check for walls (starting from player's location)
		RaycastHit hit;

		float raycastLimit = projectionDistance;

		if (Physics.Raycast((transform.position + (transform.TransformDirection(Vector3.forward) * GetComponent<CameraControls>().targetDistance)), transform.TransformDirection(Vector3.forward), out hit, raycastLimit, ~ignoredLayers)) {
			raycastLimit = hit.distance;
			Debug.DrawRay((transform.position + (transform.TransformDirection(Vector3.forward) * GetComponent<CameraControls>().targetDistance)), transform.TransformDirection(Vector3.forward) * hit.distance, Color.blue);
		}

		//make multiple downward raycasts at subdivided intervals along initial raycast (ignore initial downward raycast at player location)
		RaycastHit[] hits = new RaycastHit[projectionSubdivisions];

		for (int i = 1; i < projectionSubdivisions; i++) {
			Vector3 origin = (transform.position + (transform.TransformDirection(Vector3.forward) * GetComponent<CameraControls>().targetDistance)) + ((transform.TransformDirection(Vector3.forward) * ((raycastLimit / projectionSubdivisions) * i)));
			if (Physics.Raycast(origin, new Vector3(0, -1, 0), out hits[i], projectionDownwardDistance, ~ignoredLayers)) {
				Debug.DrawRay(origin, new Vector3(0, -1, 0) * hits[i].distance, Color.blue);
			}
		}

		Vector3 projectionSpot = new Vector3(0,0,0);

		//project ghost to downward raycast closest to ground if not parkouring
		if (!vault && !climb && !jump) {
			
			float smallestDistance = projectionDownwardDistance;

			for (int i = 1; i < projectionSubdivisions; i++) {
				if (hits[i].point != null) {
					if (hits[i].distance < smallestDistance) {
						smallestDistance = hits[i].distance;
						projectionSpot = hits[i].point;
					}
				}
			}

			if (smallestDistance < projectionDownwardDistance) ghost.transform.position = projectionSpot;

		//project ghost to tether of same type of current parkour move
		} else {
			hits[0] = hit;
			positionInRelationToTether = new Vector3(mov.gameObject.transform.position.x - mov.tether.position.x, mov.gameObject.transform.position.y - mov.tether.position.y, mov.gameObject.transform.position.z - mov.tether.position.z);

			for (int i = 0; i < projectionSubdivisions; i++) {
				if (hits[i].collider != null) {
					if (vault && hits[i].collider.tag == "Vault_Tether") {
						falseTether = hits[i].collider.gameObject.GetComponent<ParkourInteractable>().tether;
						projectionSpot = falseTether.position;
					} else if (climb && hits[i].collider.tag == "Climb_Tether") {
						falseTether = hits[i].collider.gameObject.GetComponent<ParkourInteractable>().tether;
						projectionSpot = falseTether.position;
					} else if (jump && hits[i].collider.tag == "Jump_Tether") {
						falseTether = hits[i].collider.gameObject.GetComponent<ParkourInteractable>().tether;
						projectionSpot = falseTether.position;
					}
				}
			}

			ghost.transform.position = projectionSpot + positionInRelationToTether;
		}

		if (Input.GetMouseButton(0) && projectionSpot != new Vector3(0,0,0)) {
			StartCoroutine(doPhase(projectionSpot));
		}

		//rotate to match player
		ghost.transform.rotation = player.transform.rotation;
		copy.transform.rotation = player.transform.rotation;
	}

	private IEnumerator doPhase(Vector3 target) {
		phasing = true;

		playerMat.SetFloat("Dissolve_Direction", 1f);
		copyMat.SetFloat("Dissolve_Direction", 0f);

		float playerDissolve = 0f;
		float copyDissolve = 1f;

		for (int i = 0; i < 20; i++) {
			//move copy to projection spot
			if (vault || climb || jump) {
				positionInRelationToTether = new Vector3(mov.gameObject.transform.position.x - mov.tether.position.x, mov.gameObject.transform.position.y - mov.tether.position.y, mov.gameObject.transform.position.z - mov.tether.position.z);
				copy.transform.position = target + positionInRelationToTether;
			} else copy.transform.position = target;
			
			//lerp dissolve
			playerMat.SetFloat("Dissolve", playerDissolve += 0.025f);
			copyMat.SetFloat("Dissolve", copyDissolve -= 0.025f);

			yield return new WaitForSeconds(0.002f);
		}

		//swap places between character and copy
		playerMat.SetFloat("Dissolve_Direction", 0f);
		copyMat.SetFloat("Dissolve_Direction", 1f);

		if (vault || climb || jump) {
			Transform temp = mov.tether;
			mov.tether = falseTether;
			falseTether = temp;
		}

		Vector3 oldPlayerPosition = player.transform.position;

		player.transform.position = copy.transform.position;
		copy.transform.position = oldPlayerPosition;


		for (int i = 0; i < 20; i++) {
			copy.transform.position = oldPlayerPosition;

			playerMat.SetFloat("Dissolve", playerDissolve -= 0.025f);
			copyMat.SetFloat("Dissolve", copyDissolve += 0.025f);

			yield return new WaitForSeconds(0.002f);
		}

		yield return new WaitForSeconds(cooldownSeconds);
		phasing = false;
	}
}