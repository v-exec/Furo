using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraControls : MonoBehaviour {

	public Transform target;
	
	public float xSpeed = 660.0f;
	public float ySpeed = 480.0f;
	public float zSpeed = 5.0f;
 
	public float yMinLimit = -30f;
	public float yMaxLimit = 80f;
	public float cameraHeight = 0.5f;
	public float minZoom = 1f;
	public float maxZoom = 4f;

	private float setDistance = 6f;
	public float targetDistance = 6f;
	private bool collidingWithWall = false;

	public LayerMask ignoredLayers;
 
	private float x = 0.0f;
	private float y = 0.0f;

	public Vector3 forw;
 
	void Start ()  {
		Vector3 angles = transform.eulerAngles;
		x = angles.y;
		y = angles.x;

		Cursor.visible = false;
		Cursor.lockState = CursorLockMode.Locked;
	}

	void Update() {
		//saves previous frame's forward vector for use in raycasting
		forw = transform.forward;
	}

	void LateUpdate() {
		if (Input.GetKey("escape")) {
			Application.Quit();
		}

		 if (target) {
			//input
			x += Input.GetAxis("Mouse X") * xSpeed * 0.02f;
			y -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;
 
			y = ClampAngle(y, yMinLimit, yMaxLimit);
 
			Quaternion rotation = Quaternion.Euler(y, x, 0);
 
			float distance = targetDistance;
			collidingWithWall = false;
 
 			//move camera forward when colliding
			RaycastHit hit;
			if (Physics.Linecast(target.position, transform.position, out hit, ~ignoredLayers)) {
				float hitDistance = Vector3.Distance(hit.point, transform.position);
				distance -= hitDistance;
				collidingWithWall = true;
			}

			//don't zoom camera in when wall is directly behind it (to avoid bouncing between inside wall and outside)
			if (Physics.Linecast(transform.position, transform.position + transform.TransformDirection(-Vector3.forward), out hit, ~ignoredLayers)) {
				collidingWithWall = true;
			}

			//set zoom distance
			setDistance -= Input.mouseScrollDelta.y;
			setDistance = Mathf.Clamp(setDistance, minZoom, maxZoom);
			if (collidingWithWall) targetDistance = distance;
			else if (targetDistance != setDistance) targetDistance = Mathf.Lerp(targetDistance, setDistance, 2.0f * Time.deltaTime);

			Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
			Vector3 position = rotation * negDistance + target.position + new Vector3(0.0f, cameraHeight, 0.0f);
 
			transform.rotation = rotation;
			transform.position = position;
		}
	}
 
	public static float ClampAngle(float angle, float min, float max) {
		if (angle < -360F)
			angle += 360F;
		if (angle > 360F)
			angle -= 360F;
		return Mathf.Clamp(angle, min, max);
	}

	public static float ease(float val, float target, float ease) {
		if (val == target) return val;

		float difference = target - val;
		return val += ((difference * ease) * Time.deltaTime);
	}

	public static float remap(float val, float min1, float max1, float min2, float max2) {
		if (val < min1) val = min1;
		if (val > max1) val = max1;

		return (val - min1) / (max1 - min1) * (max2 - min2) + min2;
	}
}