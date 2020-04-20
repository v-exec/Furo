using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Movement : MonoBehaviour {

	public Animator anim;
	public GameObject cam;
	public Animator ghostAnim;
	public Animator copyAnim;

	//priamry parameters
	private float rotationDegreePerSecond = 120f;
	private float directionDampTime = 0.2f;
	private float speedDampTime = 0.2f;
	private float directionAmplifier = 2f;

	private float speed;
	private float dir;
	private float angle;

	//input check
	private Coroutine inputCheck = null;
	private bool inputting = false;

	//parkour
	public bool jump = false;
	private Coroutine jumpRoutine = null;

	public bool vault = false;
	private Coroutine vaultRoutine = null;
	private float vaultType = 0f;

	public bool climb = false;
	private Coroutine climbRoutine = null;
	private float climbType = 0f;

	private bool falling = false;
	private Coroutine fallingRoutine = null;
	private float fallTimeThreshold = 0.2f;

	//target matching
	private float projectionDistance = 3f;
	private float projectionDownwardDistance = 6f;
	private int projectionSubdivisions = 4;

	public LayerMask ignoredLayers;

	public Transform tether;
	private Collider col;
	private Rigidbody rb;

	void Start() {
		col = GetComponent<Collider>();
		rb = GetComponent<Rigidbody>();
	}

	void Update() {
		//get input
		float horizontal = 0f;
		float vertical = 0f;
		if (Mathf.Abs(Input.GetAxis("Horizontal")) > 0.1f) horizontal = Input.GetAxis("Horizontal");
		if (Mathf.Abs(Input.GetAxis("Vertical")) > 0.1f) vertical = Input.GetAxis("Vertical");

		//create a vector direction from the inputs
		Vector3 direction = new Vector3(horizontal, 0.0f, vertical);

		//if there's an input from the player
		if (horizontal != 0f || vertical != 0f) {
			Transform newDir = cam.transform;
			newDir.Rotate(Vector3.left, cam.transform.localRotation.eulerAngles.x);

			direction = newDir.TransformDirection(direction);
			direction.y = 0.0f;

			//normalize input
			if (direction.magnitude > 1 || direction.magnitude < -1) direction = Vector3.Normalize(direction);

			//get angle
			angle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);

			//when not pivoting or parkouring, set angle and rotate character with code
			if (!isPivoting() && !isParkouring()) {
				anim.SetFloat("Angle", angle);

				int mod = 1;
				if (angle < 0) mod = -1;

				Vector3 rotationAmount = Vector3.Lerp(Vector3.zero, new Vector3(0f, rotationDegreePerSecond * mod, 0f), Mathf.Abs(angle));
				Quaternion deltaRotation = Quaternion.Euler(rotationAmount * Time.deltaTime);
				transform.rotation *= deltaRotation;
			}

			//move character
			dir = (angle / 180f) * directionAmplifier;
			anim.SetFloat("Direction", dir, directionDampTime, Time.deltaTime);

			//speed
			if (Input.GetKey(KeyCode.LeftShift)) speed = 2f;
			else speed = 1f;

			anim.SetFloat("Speed", speed, speedDampTime, Time.deltaTime);

			if (inputCheck != null) {
				StopCoroutine(inputCheck);
				inputCheck = null;
			}

			//parkour
			if (Input.GetKey(KeyCode.Space) && !isParkouring()) {
				findTarget();
			}
			
			inputting = true;
			anim.SetBool("Input", inputting);
		} else {
			speed = 0f;
			angle = 0f;

			anim.SetFloat("Speed", speed, speedDampTime, Time.deltaTime);
			anim.SetFloat("Angle", angle);

			inputCheck = StartCoroutine(NoInput());
		}

		matchAnimations();
		matchTargets();
		maintainVerticalRotation();

		if (!checkIfGrounded() && !isPivoting() && !isParkouring()) {
			if (fallingRoutine != null) {
				//
			} else {
				fallingRoutine = StartCoroutine(FallRoutine());
			}
		
		} else {
			if (fallingRoutine != null) {
				StopCoroutine(fallingRoutine);
				fallingRoutine = null;
				falling = false;
				anim.SetBool("Fall", falling);
			}
		}
	}

	//check if player is pivoting
	bool isPivoting() {
		return (anim.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.WalkPivotLeft") ||
				anim.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.WalkPivotRight") ||
				anim.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.SprintPivotLeft") ||
				anim.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.SprintPivotRight"));
	}

	//check if playing is doing parkour move
	bool isParkouring() {
		return (anim.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Vault") ||
				anim.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Climb") ||
				anim.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Jump"));
	}

	//linear map
	float remap(float val, float min1, float max1, float min2, float max2) {
		return (val - min1) / (max1 - min1) * (max2 - min2) + min2;
	}

	//check if the player is grounded
	bool checkIfGrounded() {
		return Physics.Raycast(new Vector3(transform.position.x, transform.position.y + 0.04f, transform.position.z), -Vector3.up, 0.5f);
	}

	//matches animations with ghost and copy of character
	void matchAnimations() {
		ghostAnim.SetFloat("Speed", speed, speedDampTime, Time.deltaTime);
		ghostAnim.SetFloat("Direction", dir, directionDampTime, Time.deltaTime);
		ghostAnim.SetFloat("Angle", angle);
		ghostAnim.SetBool("Input", inputting);
		ghostAnim.SetBool("Vault", vault);
		ghostAnim.SetBool("Jump", jump);
		ghostAnim.SetBool("Climb", climb);
		ghostAnim.SetFloat("Vault Type", vaultType);
		ghostAnim.SetFloat("Climb Type", climbType);

		copyAnim.SetFloat("Speed", speed, speedDampTime, Time.deltaTime);
		copyAnim.SetFloat("Direction", dir, directionDampTime, Time.deltaTime);
		copyAnim.SetFloat("Angle", angle);
		copyAnim.SetBool("Input", inputting);
		copyAnim.SetBool("Vault", vault);
		copyAnim.SetBool("Jump", jump);
		copyAnim.SetBool("Climb", climb);
		copyAnim.SetFloat("Vault Type", vaultType);
		copyAnim.SetFloat("Climb Type", climbType);

		//rotation is handled along with other transforms in phase script
	}

	void findTarget() {
		//raycast towards camera forward vector to check for walls (starting from player's location)
		RaycastHit initialCast;

		float raycastLimit = projectionDistance;

		if (Physics.Raycast((Camera.main.transform.position + (Camera.main.GetComponent<CameraControls>().forw * Camera.main.GetComponent<CameraControls>().targetDistance)), Camera.main.GetComponent<CameraControls>().forw, out initialCast, raycastLimit, ~ignoredLayers)) {
			raycastLimit = initialCast.distance;
			Debug.DrawRay((Camera.main.transform.position + (Camera.main.GetComponent<CameraControls>().forw * Camera.main.GetComponent<CameraControls>().targetDistance)), Camera.main.GetComponent<CameraControls>().forw * initialCast.distance, Color.red);
		}

		//make multiple downward raycasts at subdivided intervals along initial raycast (add extra element in array for initial raycast)
		RaycastHit[] hits = new RaycastHit[projectionSubdivisions + 1];

		for (int i = 0; i < projectionSubdivisions; i++) {
			Vector3 origin = (Camera.main.transform.position + (Camera.main.GetComponent<CameraControls>().forw * Camera.main.GetComponent<CameraControls>().targetDistance)) + ((Camera.main.GetComponent<CameraControls>().forw * ((raycastLimit / projectionSubdivisions) * i)));
			if (Physics.Raycast(origin, new Vector3(0, -1, 0), out hits[i], projectionDownwardDistance, ~ignoredLayers)) {
				Debug.DrawRay(origin, new Vector3(0, -1, 0) * hits[i].distance, Color.red);
			}
		}

		bool action = false;

		hits[projectionSubdivisions] = initialCast;

		for (int i = 0; i < projectionSubdivisions + 1; i++) {
			if (hits[i].collider != null && checkIfGrounded()) {
				switch (hits[i].collider.tag) {

					case "Vault_Tether":
						//don't start action if detected location is at/under player height
						if (hits[i].point.y > transform.position.y + 0.1f) {
							action = true;
							tether = hits[i].collider.gameObject.GetComponent<ParkourInteractable>().tether;

							if (vaultRoutine == null) vaultRoutine = StartCoroutine(Vault());
						}
						break;

					case "Climb_Tether":
						//don't start action if detected location is at/under player height
						if (hits[i].point.y > transform.position.y + 0.1f) {
							action = true;
							tether = hits[i].collider.gameObject.GetComponent<ParkourInteractable>().tether;

							if (hits[i].collider.gameObject.GetComponent<ParkourInteractable>().tallClimb) climbType = 1f;
							else climbType = 0f;

							if (climbRoutine == null) climbRoutine = StartCoroutine(Climb());
						}
						break;

					case "Jump_Tether":
						//only start action if y of player is more or less the same
						if (Mathf.Abs(hits[i].point.y - (transform.position.y + 0.1f)) < 0.5f) {
							action = true;
							tether = hits[i].collider.gameObject.GetComponent<ParkourInteractable>().tether;

							if (jumpRoutine == null) jumpRoutine = StartCoroutine(Jump());	
						}
						break;

					default:
						break;
				}

				if (action) break;
			}
		}
	}

	void matchTargets() {
		if (anim.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Vault")) {
			rb.useGravity = false;
			col.enabled = false;

			if (vaultType == 2f) anim.MatchTarget(tether.position, tether.rotation, AvatarTarget.LeftHand, new MatchTargetWeightMask(Vector3.one, 0f), 0.1f, 0.3f);
			else anim.MatchTarget(tether.position, tether.rotation, AvatarTarget.LeftHand, new MatchTargetWeightMask(Vector3.one, 0f), 0.1f, 0.5f);

		} else if (anim.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Climb")) {
			rb.useGravity = false;
			col.enabled = false;

			if (climbType == 1f) anim.MatchTarget(tether.position, tether.rotation, AvatarTarget.LeftHand, new MatchTargetWeightMask(Vector3.one, 1.0f), 0.1f, 0.45f);
			else anim.MatchTarget(tether.position, tether.rotation, AvatarTarget.RightFoot, new MatchTargetWeightMask(Vector3.one, 0.0f), 0.15f, 0.4f);

		} else if (anim.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Jump")) {
			rb.useGravity = false;
			col.enabled = false;

			anim.MatchTarget(tether.position, tether.rotation, AvatarTarget.LeftFoot, new MatchTargetWeightMask(Vector3.one, 0.0f), 0.3f, 0.65f);
		} else {
			rb.useGravity = true;
			col.enabled = true;
		}
	}

	void maintainVerticalRotation() {
		if (!isPivoting() && !isParkouring()) {
			transform.eulerAngles = new Vector3(0f, transform.eulerAngles.y, 0f);
		}
	}

	IEnumerator NoInput() {
		yield return new WaitForSeconds(0.1f);
		inputting = false;
		anim.SetBool("Input", inputting);
	}

	IEnumerator Vault() {
		vaultType = Random.Range(0, 3);
		anim.SetFloat("Vault Type", (float)vaultType);

		vault = true;
		anim.SetBool("Vault", vault);

		yield return new WaitForSeconds(1.0f);

		vault = false;
		anim.SetBool("Vault", vault);

		StopCoroutine(vaultRoutine);
		vaultRoutine = null;
	}

	IEnumerator Jump() {
		jump = true;
		anim.SetBool("Jump", jump);

		yield return new WaitForSeconds(0.5f);

		jump = false;
		anim.SetBool("Jump", jump);

		StopCoroutine(jumpRoutine);
		jumpRoutine = null;
	}

	IEnumerator Climb() {
		anim.SetFloat("Climb Type", climbType);

		climb = true;
		anim.SetBool("Climb", climb);

		yield return new WaitForSeconds(1.0f);

		climb = false;
		anim.SetBool("Climb", climb);

		StopCoroutine(climbRoutine);
		climbRoutine = null;
	}

	IEnumerator FallRoutine() {
		yield return new WaitForSeconds(fallTimeThreshold);
		falling = true;
		anim.SetBool("Fall", falling);
	}
}