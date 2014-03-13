using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Leap;

public struct Sphere {
	public Vector3 center;
	public float radius;
	
	public Sphere(Vector3 pos, float rad)
	{
		center = pos;
		radius = rad;
	}
}

public class BubbleZoomv4 : MonoBehaviour {
	Transform cameraTransform;
	PointCloud pointCloud;
	
	enum state { MOVING_FINGER, SELECT_IN_OUT, SELECT_BUBBLE, NONE };
	state currentState = state.NONE;
	float timeSinceLastStateChange = 0.0F;
	float timeSinceLastClickCompleted = 0.0F;
	float timeLastUpdate = 0.0F;
	float lastScalarVelocity = 0.0F;
	float velocityThreshold = 300.0F;
	float stateChangeTimeThreshold = 0.2F;
	int updateCountSinceMovingSlashStarted = 0;
	float angularTriggerThreshold = 25.0F; // 10 degrees

	List<Vector3> handPosition;
	List<Vector3> fingerPosition;
	List<float> fingerPositionTime;
	float traceTime = 0.3F;
	float pinchVelocity = 0.0F;
	float lastPinchVelocity = 0.0F;
	float lastFingerDistance = 0.0F;
	
	GameObject selectionVolume;
	List<GameObject> volumeTrail;
	List<Sphere> volumeTrailSpheres;
	
	bool select = false;

	bool enabled = true;
	bool needsClear = true;
	
	public BubbleZoomv4() {
		pointCloud = GameObject.Find("Point Cloud").GetComponent<PointCloud>();
		cameraTransform = GameObject.Find("Camera").GetComponent<Transform>();
		
		fingerPosition = new List<Vector3>();
		handPosition = new List<Vector3>();
		fingerPositionTime = new List<float>();
		
		selectionVolume = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		selectionVolume.renderer.material = Resources.Load("DiffuseZ", typeof(Material)) as Material;
		selectionVolume.renderer.material.color = new Color(0.7F, 0.7F, 0.8F, 0.5F);

		volumeTrailSpheres = new List<Sphere>();
	}
	
	public bool ProcessFrame (Frame frame, List<GameObject> goHandList, List<GameObject> goFingerList) {
		bool locked = false; // return true if in any other state other than the initial one

		// if its not enabled, simply clear and return
		if(!enabled)
		{
			Clear();
			return locked;
		}
		
		needsClear = true;

		// calculate how much time has passed since last update
		float currentTime = Time.timeSinceLevelLoad;
		float timeSinceLastUpdate = currentTime - timeLastUpdate;
		timeLastUpdate = currentTime;
		
		timeSinceLastClickCompleted += timeSinceLastUpdate;
		if(timeSinceLastClickCompleted < 0.5F) return false; // avoid detecting two clicks in one
		
		timeSinceLastStateChange += timeSinceLastUpdate;

		Debug.Log(currentState);
		
		HandList hl = frame.Hands;
		FingerList fl = frame.Fingers;

		// distance between fingers
		if(fl.Count == 2)
		{
			float fingerDistance = fl[0].TipPosition.DistanceTo(fl[1].TipPosition);
			pinchVelocity = ((fingerDistance-lastFingerDistance)/timeSinceLastUpdate);//0.6F*lastPinchVelocity + 0.4F*((fingerDistance-lastFingerDistance)/timeSinceLastUpdate);
			lastPinchVelocity = pinchVelocity;
			lastFingerDistance = fingerDistance;
		}
		
		// reset state machine
		if(hl.Count == 0 || (hl.Count >= 1 && fl.Count < 1)) 
		{
			currentState = state.NONE;
			timeSinceLastStateChange = 0.0F;
		}
		// change state to moving finger (initial state) if there are two fingers 
		else if(currentState == state.NONE && hl.Count >= 1 && fl.Count >= 2) 
		{
			currentState = state.MOVING_FINGER;
			timeSinceLastStateChange = 0.0F;
		}
		// if angle between two hand-finger vectors is smaller than angle trigger threshold, change to 
		else if(currentState == state.SELECT_IN_OUT && hl.Count >= 1 && fl.Count >= 1) 
		{
			if(pinchVelocity > velocityThreshold)
			{
				if(volumeTrailSpheres.Count == 0)
					pointCloud.SelectSphere(selectionVolume.transform.position,selectionVolume.transform.localScale.x/2.0F,true);
				else
					pointCloud.SelectSphereTrail(volumeTrailSpheres,true);
				selectionVolume.SetActive(true);
				currentState = state.SELECT_BUBBLE;
			}
			else if(pinchVelocity < -velocityThreshold)
			{
				if(volumeTrailSpheres.Count == 0)
					pointCloud.SelectSphere(selectionVolume.transform.position,selectionVolume.transform.localScale.x/2.0F,false);
				else
					pointCloud.SelectSphereTrail(volumeTrailSpheres,false);
				selectionVolume.SetActive(true);
				currentState = state.SELECT_BUBBLE;
			}
			else
			{
				selectionVolume.SetActive(false);
			}
		}

		// if angle between two hand-finger vectors is smaller than angle trigger threshold, change to 
		if(currentState == state.MOVING_FINGER && hl.Count >= 1 && fl.Count >= 2) 
		{
			// update selection volume position
			selectionVolume.transform.position = (goFingerList[0].transform.position+goFingerList[1].transform.position)/2.0F;
			// radius of the bubble is determined by the distance between the two fingers
			float distance = Vector3.Distance(goFingerList[0].transform.position,goFingerList[1].transform.position);
			float selectionVolumeScale = distance;
			selectionVolume.transform.localScale = new Vector3(selectionVolumeScale,selectionVolumeScale,selectionVolumeScale);
			
			timeSinceLastStateChange = 0.0F;
		}
		
		if(select) 
		{
			currentState = state.SELECT_IN_OUT;
			selectionVolume.SetActive(false);
			select = false;
			locked = true;
		}

		if(currentState != state.NONE)
		{
			if(volumeTrailSpheres.Count == 0)
				pointCloud.SetSphere(selectionVolume.transform.position,selectionVolume.transform.localScale.x/2.0F);
			else if(currentState == state.MOVING_FINGER)
				pointCloud.SetSphereTrail(volumeTrailSpheres);
		}

		//hold key for bubble sweep
		
		// if two seconds have passed without a state change, reset state machine
		if(currentState != state.NONE && currentState != state.MOVING_FINGER && timeSinceLastStateChange > 4.0F)
		{
			currentState = state.NONE;
			timeSinceLastStateChange = 0.0F;
			volumeTrailSpheres.Clear();
		}
		
		// if things have been selected, reset state machine
		if(currentState == state.SELECT_BUBBLE)
		{
			currentState = state.NONE;
			timeSinceLastClickCompleted = 0.0F;
			volumeTrailSpheres.Clear();
		}


		if(Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift)) // reset
		{
			SelectBubble();
			locked = true;
		}
		else if(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) // ready to select
		{
			//volumeTrail.Add(Instantiate(selectionVolume) as GameObject);
			//volumeTrail[volumeTrail.Count-1].renderer.material.color = new Color(0.7F, 0.7F, 0.7F, 0.5F);
			Sphere s = new Sphere(selectionVolume.transform.position,selectionVolume.transform.localScale.x/2.0F);
			volumeTrailSpheres.Add(s);

			locked = true;
		}

		if(Input.GetKeyDown(KeyCode.Slash) || Input.GetKeyUp(KeyCode.Slash))
			SelectBubble();

		if(currentState != state.NONE && currentState != state.MOVING_FINGER)
			locked = true;

		return locked;
	}
	
	public void SelectBubble()
	{
		select = true;
	}
	
	public void SetEnabled(bool enable)
	{
		enabled = enable;
		selectionVolume.SetActive(enabled);
	}
	
	public void Clear()
	{
		if(!needsClear)
			return;
		needsClear = false;
		Debug.Log("BUBBLE");

		// if two seconds have passed without a state change, reset state machine
		if((currentState != state.NONE && currentState != state.MOVING_FINGER) || !enabled)
		{
			pointCloud.ResetSelected();
			currentState = state.NONE;
			timeSinceLastStateChange = 0.0F;
		}
	}
}

