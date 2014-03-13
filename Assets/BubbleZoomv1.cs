using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Leap;

public class BubbleZoomv1 {
	Transform cameraTransform;
	PointCloud pointCloud;
	
	enum state { MOVING_FINGER, FINGER_TRIGGER, SELECT_BUBBLE, NONE };
	state currentState = state.NONE;
	float timeSinceLastStateChange = 0.0F;
	float timeSinceLastClickCompleted = 0.0F;
	float timeLastUpdate = 0.0F;
	float lastScalarVelocity = 0.0F;
	float velocityThreshold = 500.0F;
	float stateChangeTimeThreshold = 0.2F;
	int updateCountSinceMovingSlashStarted = 0;
	float angularTriggerThreshold = 25.0F; // 10 degrees
	
	LineRenderer fingerLineRenderer;
	List<Vector3> handPosition;
	List<Vector3> fingerPosition;
	List<float> fingerPositionTime;
	float traceTime = 0.3F;

	GameObject selectionVolume;
	
	public BubbleZoomv1() {
		pointCloud = GameObject.Find("Point Cloud").GetComponent<PointCloud>();
		cameraTransform = GameObject.Find("Camera").GetComponent<Transform>();
		
		fingerPosition = new List<Vector3>();
		handPosition = new List<Vector3>();
		fingerPositionTime = new List<float>();
		
		fingerLineRenderer = (new GameObject("Finger Line Renderer")).AddComponent<LineRenderer>();
		fingerLineRenderer.material.shader = Shader.Find("Sprites/Default");
		fingerLineRenderer.SetColors(new Color(0.8F,0.1F,0.1F), new Color(0.1F,0.1F,0.8F));
		fingerLineRenderer.SetWidth(0.001F,1.0F);

		selectionVolume = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		selectionVolume.renderer.material = Resources.Load("DiffuseZ", typeof(Material)) as Material;
		selectionVolume.renderer.material.color = new Color(0.1F, 0.3F, 0.8F, 0.3F);
	}
	
	public bool Update (Frame frame, List<GameObject> goHandList, List<GameObject> goFingerList) {
		// calculate how much time has passed since last update
		float currentTime = Time.time;
		float timeSinceLastUpdate = currentTime - timeLastUpdate;
		timeLastUpdate = currentTime;
		
		float selectionVolumeScale = Mathf.Max(new float[]{pointCloud.Size().x,pointCloud.Size().y,pointCloud.Size().z}) / 4.0F;
		selectionVolume.transform.localScale = new Vector3(selectionVolumeScale,selectionVolumeScale,selectionVolumeScale);

		// update selection volume position
		selectionVolume.transform.position = goFingerList[0].transform.position;

		// update finger trace
		if(frame.Fingers.Count > 0)
		{
			fingerPosition.Add(goFingerList[0].transform.position);
			handPosition.Add(goHandList[0].transform.position);
			fingerPositionTime.Add(currentTime);
			while(currentTime-fingerPositionTime[0] > traceTime)
			{
				fingerPosition.RemoveAt(0);
				handPosition.RemoveAt(0);
				fingerPositionTime.RemoveAt(0);
			}
			
			fingerLineRenderer.SetVertexCount(fingerPosition.Count);
			for(int i = 0; i < fingerPosition.Count; i++)
				fingerLineRenderer.SetPosition(i,fingerPosition[i]);
		}
		else if(fingerPosition.Count > 0)
		{
			fingerPosition.RemoveAt(0);
			handPosition.RemoveAt(0);
			fingerPositionTime.RemoveAt(0);
		}
		
		timeSinceLastClickCompleted += timeSinceLastUpdate;
		if(timeSinceLastClickCompleted < 0.5F) return false; // avoid detecting two clicks in one

		timeSinceLastStateChange += timeSinceLastUpdate;
		
		HandList hl = frame.Hands;
		FingerList fl = frame.Fingers;
		
		// reset state machine
		if(hl.Count == 0 || (hl.Count >= 1 && fl.Count < 1)) 
		{
			currentState = state.NONE;
			timeSinceLastStateChange = 0.0F;
		}
		// change state to moving finger (initial state) if there are two fingers 
		else if(currentState == state.NONE && hl.Count >= 1 && fl.Count == 2) 
		{
			currentState = state.MOVING_FINGER;
			timeSinceLastStateChange = 0.0F;
		}
		// if angle between two hand-finger vectors is smaller than angle trigger threshold, change to 
		else if(currentState == state.MOVING_FINGER && hl.Count >= 1 && fl.Count == 2 && ((fl[0].TipPosition-hl[0].PalmPosition).AngleTo(fl[1].TipPosition-hl[0].PalmPosition)*57.2957795F) < angularTriggerThreshold && timeSinceLastStateChange > stateChangeTimeThreshold) 
		{
			//Debug.Log("Angle:"+(fl[0].TipPosition.AngleTo(fl[1].TipPosition)*57.2957795F));
			currentState = state.FINGER_TRIGGER;
			timeSinceLastStateChange = 0.0F;
		}
		// if velocity goes below the threshold again, change state to cut slash, where the cut has been made
		else if(currentState == state.FINGER_TRIGGER && hl.Count >= 1 && fl.Count == 2 && ((fl[0].TipPosition-hl[0].PalmPosition).AngleTo(fl[1].TipPosition-hl[0].PalmPosition)*57.2957795F) > angularTriggerThreshold && timeSinceLastStateChange > stateChangeTimeThreshold) 
		{
			// radius of the bubble is determined by the size of the selection box
			pointCloud.SelectSphere(goFingerList[0].transform.position,selectionVolumeScale/2.0F,true);
			currentState = state.SELECT_BUBBLE;
			timeSinceLastStateChange = 0.0F;
		}

		// if two seconds have passed without a state change, reset state machine
		if(currentState != state.NONE && currentState != state.MOVING_FINGER && timeSinceLastStateChange > 4.0F)
		{
			currentState = state.NONE;
			timeSinceLastStateChange = 0.0F;
		}

		// if things have been selected, reset state machine
		if(currentState == state.SELECT_BUBBLE)
		{
			currentState = state.NONE;
			timeSinceLastClickCompleted = 0.0F;
			return true;
		}
		return false;
	}
}

