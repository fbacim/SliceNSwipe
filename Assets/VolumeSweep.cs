using UnityEngine;
using System;
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

public class VolumeSweep {//}: MonoBehaviour {
	Transform cameraTransform;
	PointCloud pointCloud;
	
	enum state { MOVING_FINGER, SELECT_IN_OUT, SELECT_BUBBLE, NONE };
	state currentState = state.MOVING_FINGER;
	float timeSinceLastStateChange = 0.0F;
	float timeSinceLastClickCompleted = 0.0F;
	DateTime timeLastUpdate;
	float resetTimer = 0.0F;
	float velocityThreshold = 200.0F;
	float highVelocityThreshold = 150.0F;
	float lowVelocityThreshold = 5.0F;
	float stateChangeTimeThreshold = 0.7F;
	int updateCountSinceMovingSlashStarted = 0;
	bool crossedThreshold = false;

	List<Vector3> handPosition;
	List<Vector3> fingerPosition;
	List<float> fingerPositionTime;
	float lastFilteredVelocity = 0.0F;
	
	GameObject selectionVolume;
	List<GameObject> volumeTrail;
	List<Sphere> volumeTrailSpheres;
	
	bool select = false;

	bool isEnabled = true;
	bool needsClear = true;
	bool canSelect = false;
	
	Strategy strategy = Strategy.BOTH;
	
	public VolumeSweep(Strategy selectedStrategy) {
		strategy = selectedStrategy;

		pointCloud = GameObject.Find("Camera").GetComponent<PointCloud>();
		cameraTransform = GameObject.Find("Camera").GetComponent<Transform>();
		
		fingerPosition = new List<Vector3>();
		handPosition = new List<Vector3>();
		fingerPositionTime = new List<float>();
		
		selectionVolume = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		selectionVolume.name = "Bubble";
		selectionVolume.GetComponent<Renderer>().material = Resources.Load("DiffuseZ", typeof(Material)) as Material;
		selectionVolume.GetComponent<Renderer>().material.color = new Color(0.7F, 0.7F, 0.7F, 0.5F);
		selectionVolume.SetActive(false);

		volumeTrailSpheres = new List<Sphere>();
		
		timeLastUpdate = DateTime.Now;
	}
	
	public bool ProcessFrame (Frame frame, List<GameObject> goHandList, List<GameObject> goFingerList) {
		bool locked = false; // return true if in any other state other than the initial one

		// if its not enabled, simply clear and return
		if(!isEnabled)
		{
			Clear();
			return locked;
		}
		
		needsClear = true;

		// calculate how much time has passed since last update
		DateTime currentTime = DateTime.Now;
		float timeSinceLastUpdate = (float)(currentTime - timeLastUpdate).TotalSeconds;
		timeLastUpdate = currentTime;
		timeSinceLastClickCompleted += timeSinceLastUpdate;
		timeSinceLastStateChange += timeSinceLastUpdate;

		// get reference to the index and thumb fingers
		FingerList indexFingers = frame.Fingers.FingerType(Finger.FingerType.TYPE_INDEX);
		Finger indexFinger = null;
		if(indexFingers.Count > 0)
		{
			indexFinger = (indexFingers[0].Hand.IsRight || indexFingers.Count == 1) ? indexFingers[0] : indexFingers[1]; // right hand is preferred, but left is used if right hand is not detected.
		}
		FingerList thumbFingers = frame.Fingers.FingerType(Finger.FingerType.TYPE_THUMB);
		Finger thumbFinger = null;
		if(thumbFingers.Count > 0)
		{
			thumbFinger = (thumbFingers[0].Hand == indexFinger.Hand) ? thumbFingers[0] : thumbFingers[1];
		}

		// if there are fingers in the current frame, add them to the local structures
		if(frame.Fingers.Count > 0)
		{
			fingerPosition.Add(goFingerList[1+5*System.Convert.ToInt32(indexFinger.Hand.IsRight)].transform.position);
			handPosition.Add(goHandList[System.Convert.ToInt32(indexFinger.Hand.IsRight)].transform.position);
			fingerPositionTime.Add(currentTime.Ticks);

			while(fingerPosition.Count > 60)
			{
				fingerPosition.RemoveAt(0);
				handPosition.RemoveAt(0);
				fingerPositionTime.RemoveAt(0);
			}
		}

		if((indexFinger == null || thumbFinger == null) && !pointCloud.animating && !pointCloud.separate)
			pointCloud.ResetSelected();
		
		// calculate velocity
		float scalarVelocity;// = fl[0].TipVelocity.Magnitude; 
		float filteredVelocity;
		if(indexFinger != null && indexFinger.TimeVisible > 1 && !pointCloud.animating && timeSinceLastClickCompleted > 2.0F)// && distance != fl[0].StabilizedTipPosition.Magnitude ) 
		{
			scalarVelocity = ((indexFinger.TipVelocity+thumbFinger.TipVelocity)/2.0f).Magnitude;
			// only use smoothing after first cut
			if(strategy != Strategy.PRECISE && currentState == state.MOVING_FINGER && !crossedThreshold)
			{
				filteredVelocity = scalarVelocity;
			}
			else
			{
				filteredVelocity = 0.7F*lastFilteredVelocity + 0.3F*scalarVelocity;
			}
		}
		else if(timeSinceLastStateChange <= stateChangeTimeThreshold)
		{
			filteredVelocity = 0.0f;
		}
		else
		{
			scalarVelocity = 0;
			filteredVelocity = 0;
			if(currentState != state.SELECT_IN_OUT && !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
			{
				currentState = state.NONE;
			}
		}
		lastFilteredVelocity = filteredVelocity;

		if(GameObject.Find("VelocityBar") != null)
			GameObject.Find("VelocityBar").GetComponent<ProgressBar>().scale = filteredVelocity/50.0f;

		// change state to moving finger (initial state) if there are two fingers 
		if(currentState == state.NONE && thumbFinger != null && indexFinger != null) 
		{
			pointCloud.TriggerSeparation(false,0);
			pointCloud.ResetSelected();
			currentState = state.MOVING_FINGER;
			timeSinceLastStateChange = 0.0F;
			updateCountSinceMovingSlashStarted = 0;
			volumeTrailSpheres.Clear();
		}
		// if moving fingers, update volume object position and size
		else if(currentState == state.MOVING_FINGER && thumbFinger != null && indexFinger != null) 
		{
			// update selection volume position
			selectionVolume.transform.position = (goFingerList[5*System.Convert.ToInt32(thumbFinger.Hand.IsRight)].transform.position+goFingerList[1+5*System.Convert.ToInt32(indexFinger.Hand.IsRight)].transform.position)/2.0F;
			// radius of the bubble is determined by the distance between the two fingers
			float distance = Vector3.Distance(goFingerList[5*System.Convert.ToInt32(thumbFinger.Hand.IsRight)].transform.position,goFingerList[1+5*System.Convert.ToInt32(indexFinger.Hand.IsRight)].transform.position);
			float selectionVolumeScale = distance;
			selectionVolume.transform.localScale = new Vector3(selectionVolumeScale,selectionVolumeScale,selectionVolumeScale);
		}
		// if angle between two hand-finger vectors is smaller than angle trigger threshold, change to 
		else if(currentState == state.SELECT_IN_OUT) 
		{
			// can't change techniques in this state
			locked = true;

			// trigger separation of two volume parts
			pointCloud.TriggerSeparation(true,0);

			if(indexFinger != null)
			{
				// check finger velocity against velocity threshold for selection of side in swipe phase
				if(filteredVelocity > velocityThreshold && timeSinceLastStateChange > stateChangeTimeThreshold)
				{
					updateCountSinceMovingSlashStarted++;
				}
				else if(filteredVelocity < velocityThreshold && updateCountSinceMovingSlashStarted > 1)
				{
					int initialPosition = (fingerPosition.Count-1-updateCountSinceMovingSlashStarted < 0) ? 0 : (fingerPosition.Count-1-updateCountSinceMovingSlashStarted);
					Vector3 direction = new Vector3();
					for(int i = initialPosition; i < fingerPosition.Count; i++)
					{
						direction += fingerPosition[i];
					}
					direction /= fingerPosition.Count-initialPosition;
					
					Plane tmp = new Plane();
					tmp.Set3Points(cameraTransform.position+cameraTransform.forward,
					               cameraTransform.position,
					               cameraTransform.position+cameraTransform.up);

					Debug.Log(""+tmp.normal+direction.normalized+Mathf.Abs(Vector3.Angle(tmp.normal,direction.normalized)));
					pointCloud.SelectSphereTrail(volumeTrailSpheres,(Mathf.Abs(Vector3.Angle(tmp.normal,direction.normalized)) < 90.0F));
					pointCloud.TriggerSeparation(false,(Mathf.Abs(Vector3.Angle(tmp.normal,direction.normalized)) > 90.0F) ? 1 : 2);

					timeSinceLastStateChange = 0.0F;
					timeSinceLastClickCompleted = 0.0F;
					updateCountSinceMovingSlashStarted = 0;
					lastFilteredVelocity = 0;
					currentState = state.NONE;
				}
				else if(timeSinceLastStateChange < stateChangeTimeThreshold)
				{
					lastFilteredVelocity = 0.0F;
				}
			}
			else
			{
				updateCountSinceMovingSlashStarted = 0;
			}
		}

		// if in any state other than NONE, need to update pointcloud state for rendering
		if(currentState != state.NONE)
		{
			if(volumeTrailSpheres.Count == 0)
			{
				canSelect = pointCloud.SetSphere(selectionVolume.transform.position,selectionVolume.transform.localScale.x/2.0F,true);
			}
			else if(currentState == state.MOVING_FINGER)
			{
				canSelect = pointCloud.SetSphere(volumeTrailSpheres[volumeTrailSpheres.Count-1].center,volumeTrailSpheres[volumeTrailSpheres.Count-1].radius,false);//pointCloud.SetSphereTrail(volumeTrailSpheres);
			}
		}

		ProcessKeys();

		// FAST STRATEGY
		if(strategy != Strategy.PRECISE && currentState == state.MOVING_FINGER && thumbFinger != null && indexFinger != null && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift) && timeSinceLastStateChange > stateChangeTimeThreshold)
		{
			if(filteredVelocity > highVelocityThreshold || crossedThreshold) // ready to select
			{
				AddCurrentBubbleToSweep();
				crossedThreshold = true;
			}
			if(filteredVelocity <= lowVelocityThreshold && crossedThreshold) // reset
			{
				pointCloud.currentStrategy = Strategy.FAST;
				CompleteBubbleSweep();
				crossedThreshold = false;
				timeSinceLastStateChange = 0.0F;
			}
		}

		// if select key has been pressed, transition to swipe phase
		if(select) 
		{
			currentState = state.SELECT_IN_OUT;
			select = false;
		}

		return locked;
	}
	
	public void SetEnabled(bool enable)
	{
		isEnabled = enable;
	}

	public void Clear()
	{
		if(!needsClear)
			return;
		needsClear = false;

		// if two seconds have passed without a state change, reset state machine
		if((currentState != state.NONE && currentState != state.MOVING_FINGER) || !isEnabled)
		{
			pointCloud.ResetSelected();
			currentState = state.NONE;
			timeSinceLastStateChange = 0.0F;
			volumeTrailSpheres.Clear();
		}
	}

	public void RenderTransparentObjects()
	{
		if(selectionVolume != null && isEnabled && currentState != state.SELECT_IN_OUT)
		{
			selectionVolume.SetActive(true);
			selectionVolume.GetComponent<Renderer>().material.SetVector("_LightPosition",new Vector4(cameraTransform.position.x,cameraTransform.position.y,cameraTransform.position.z,1.0F));
			for (int pass = 0; pass < selectionVolume.GetComponent<Renderer>().material.passCount; pass++)
				if(selectionVolume.GetComponent<Renderer>().material.SetPass(pass))
					Graphics.DrawMeshNow(selectionVolume.GetComponent<MeshFilter>().mesh,selectionVolume.transform.localToWorldMatrix);
			selectionVolume.SetActive(false);
		}
	}

	public void CompleteBubbleSweep()
	{
		if(pointCloud.ValidateSets())
		{
			select = true;
		}
		else
		{
			pointCloud.TriggerSeparation(false,0);
			pointCloud.ResetSelected();
			currentState = state.NONE;
			timeSinceLastStateChange = 0.0F;
			volumeTrailSpheres.Clear();
		}
	}

	public void AddCurrentBubbleToSweep()
	{
		Sphere s = new Sphere(selectionVolume.transform.position,selectionVolume.transform.localScale.x/2.0F);
		volumeTrailSpheres.Add(s);
	}

	public void ProcessKeys()
	{
		float currentTime = Time.timeSinceLevelLoad;

		// CHECK FOR CANCEL/RESET
		// if two seconds have passed without a state change, reset state machine
		if(Input.GetKeyDown(KeyCode.Escape))
		{
			resetTimer = currentTime;
		}
		else if(Input.GetKey(KeyCode.F8))
		{
			pointCloud.TriggerSeparation(false,0);
			currentState = state.NONE;
			timeSinceLastStateChange = 0.0F;
			volumeTrailSpheres.Clear();
			pointCloud.ResetAll();
			resetTimer = 0;
		}
		else if(Input.GetKeyUp(KeyCode.Escape) && !pointCloud.animating)
		{
			pointCloud.TriggerSeparation(false,0);
			if(strategy != Strategy.PRECISE && currentState == state.MOVING_FINGER && crossedThreshold) 
				crossedThreshold = false;
			else if(currentState == state.NONE || currentState == state.MOVING_FINGER)//timeSinceLastStateChange > 4.0F)
				pointCloud.Undo();
			pointCloud.ResetSelected();
			currentState = state.NONE;
			timeSinceLastStateChange = 0.0F;
			volumeTrailSpheres.Clear();
		}

		//hold SHIFT key for bubble sweep
		if(strategy != Strategy.FAST)
		{
			if(Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift)) // reset
			{
				pointCloud.currentStrategy = Strategy.PRECISE;
				CompleteBubbleSweep();
				timeSinceLastStateChange = 0.0F;
			}
			else if(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) // ready to select
			{
				AddCurrentBubbleToSweep();
			}
		}
	}
}

