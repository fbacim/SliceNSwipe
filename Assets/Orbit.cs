using UnityEngine;
using System.Collections;

public class Orbit : MonoBehaviour {
	public Transform target;

	public float distance = 15.0F;
	public float xSpeed = 50.0F;
	public float ySpeed = 50.0F;
	
	private float x = 0.0F;
	private float y = 0.0F;

	// Use this for initialization
	void Start () {
		Vector3 angles = transform.eulerAngles;
		x = angles.y;
		y = angles.x;
	}
	
	// Update is called once per frame
	void Update () {
		if (target) {
			x += Input.GetAxis("Mouse X") * xSpeed * 0.02F;
			y -= Input.GetAxis("Mouse Y") * ySpeed * 0.02F; 
			Quaternion rotation = Quaternion.Euler(y, x, 0);
			Vector3 position = rotation * (new Vector3(0.0F, 0.0F, -distance)) + target.position;
			transform.rotation = rotation;
			transform.position = position;
		}
	}
}