using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Leap;

public class ClickZoom {
	Transform cameraTransform;
	PointCloud pointCloud;
	
	enum state { OPEN, CLOSED, CLICK, NONE };
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
	
	public ClickZoom() {
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
		selectionVolume.renderer.material.color = new Color(0.1F, 0.3F, 0.8F, 0.3F);
	}
	
	public bool Update (Frame frame, List<GameObject> goHandList, List<GameObject> goFingerList) {
		// calculate how much time has passed since last update
		float currentTime = Time.timeSinceLevelLoad;
		float timeSinceLastUpdate = currentTime - timeLastUpdate;
		timeLastUpdate = currentTime;
		
		timeSinceLastClickCompleted += timeSinceLastUpdate;
		if(timeSinceLastClickCompleted < 0.2) return false; // avoid detecting two clicks in one
		
		if(currentState == state.OPEN || currentState == state.NONE)
			timeSinceLastStateChange = 0.0F;
		else
			timeSinceLastStateChange += timeSinceLastUpdate;

		float selectionVolumeScale = Mathf.Max(new float[]{pointCloud.Size().x,pointCloud.Size().y,pointCloud.Size().z}) / 4.0F;
		selectionVolume.transform.localScale = new Vector3(selectionVolumeScale,selectionVolumeScale,selectionVolumeScale);
		
		// update selection volume position
		selectionVolume.transform.position = goHandList[0].transform.position;
				
		HandList hl = frame.Hands;
		FingerList fl = frame.Fingers;
		
		if(hl.Count == 0)
			currentState = state.NONE;
		else if((currentState == state.NONE || currentState == state.OPEN) && hl.Count >= 1 && fl.Count >= 2)
			currentState = state.OPEN;
		else if((currentState == state.OPEN || (currentState == state.CLOSED && timeSinceLastStateChange < 0.3)) && hl.Count >= 1 && fl.Count <= 1)
			currentState = state.CLOSED;
		else if(currentState == state.CLOSED && hl.Count >= 1 && fl.Count >= 2 && timeSinceLastStateChange < 0.3)
			currentState = state.CLICK;
		else 
			currentState = state.NONE;
		
		//Debug.Log("Current state: "+currentState);
		
		if(currentState == state.CLICK)
		{
			// radius of the bubble is determined by the size of the selection box
			pointCloud.SelectSphere(goHandList[0].transform.position,selectionVolumeScale/2.0F,true);

			currentState = state.NONE;
			timeSinceLastClickCompleted = 0.0F;
			return true;
		}
		return false;
	}
}
