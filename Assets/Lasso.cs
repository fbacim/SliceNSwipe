using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Leap;

public class Lasso {//}: MonoBehaviour {
	Transform cameraTransform;
	PointCloud pointCloud;
	
	enum state { NONE, DRAW, SELECT_IN_OUT, SELECT };
	state currentState = state.NONE;
	float timeSinceLastStateChange = 0.0F;
	float timeSinceLastClickCompleted = 0.0F;
	DateTime timeLastUpdate;
	float resetTimer = 0.0F;
	float highVelocityThreshold = 400.0F;
	float lowVelocityThreshold = 20.0F;
	float lastFilteredVelocity = 0.0F;
	float stateChangeTimeThreshold = 0.7F;
	int updateCountSinceMovingSlashStarted = 0;

	GameObject goFingerLineRenderer;
	LineRenderer fingerLineRenderer;
	List<Vector3> handPosition;
	List<Vector3> fingerPosition;
	List<float> fingerPositionTime;
	
	bool isEnabled = true;
	bool needsClear = true;
	bool canSelect = false;

	Strategy strategy = Strategy.BOTH;

	public Lasso(Strategy selectedStrategy) {
		strategy = selectedStrategy;

		pointCloud = GameObject.Find("Camera").GetComponent<PointCloud>();
		cameraTransform = GameObject.Find("Camera").GetComponent<Transform>();
		
		fingerPosition = new List<Vector3>();
		handPosition = new List<Vector3>();
		fingerPositionTime = new List<float>();

		goFingerLineRenderer = new GameObject("Finger Line Renderer");
		fingerLineRenderer = goFingerLineRenderer.AddComponent<LineRenderer>();
		fingerLineRenderer.material = Resources.Load("DiffuseZTop", typeof(Material)) as Material;
		fingerLineRenderer.SetColors(new Color(0.8F,0.1F,0.1F), new Color(0.1F,0.1F,0.8F));
		fingerLineRenderer.SetWidth(0.2F,0.2F);
		
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
		if(timeSinceLastClickCompleted < 2.0F) 
			return locked; // avoid detecting two clicks in one
		if(currentState == state.DRAW)
			timeSinceLastStateChange = 0.0F;
		else
			timeSinceLastStateChange += timeSinceLastUpdate;
		
		// get reference to the index finger
		FingerList indexFingers = frame.Fingers.FingerType(Finger.FingerType.TYPE_INDEX);
		Finger indexFinger = null;
		if(indexFingers.Count > 0)
		{
			indexFinger = (indexFingers[0].Hand.IsRight || indexFingers.Count == 1) ? indexFingers[0] : indexFingers[1]; // right hand is preferred, but left is used if right hand is not detected.
		}

		// calculate velocity
		float scalarVelocity;
		float filteredVelocity;
		if(indexFinger != null && indexFinger.TimeVisible > 1.0 && !pointCloud.animating)// && distance != fl[0].StabilizedTipPosition.Magnitude ) 
		{
			scalarVelocity = indexFinger.TipVelocity.Magnitude;
			filteredVelocity = 0.7F*lastFilteredVelocity + 0.3F*scalarVelocity;
		}
		else if(timeSinceLastStateChange <= stateChangeTimeThreshold)
		{
			filteredVelocity = 0.0f;
		}
		else
		{
			scalarVelocity = 0;
			filteredVelocity = 0;
			if(currentState != state.SELECT_IN_OUT)
				currentState = state.NONE;
		}
		lastFilteredVelocity = filteredVelocity;

		// update internal structures and visual feedback 		
		if(currentState == state.DRAW)
		{
			pointCloud.TriggerSeparation(false,0);
			fingerPosition.Add(goFingerList[1+5*System.Convert.ToInt32(indexFinger.Hand.IsRight)].transform.position);
			handPosition.Add(goHandList[System.Convert.ToInt32(indexFinger.Hand.IsRight)].transform.position);
			fingerPositionTime.Add(currentTime.Ticks);
			
			fingerLineRenderer.SetVertexCount(fingerPosition.Count+1);
			for(int i = 0; i < fingerPosition.Count; i++)
				fingerLineRenderer.SetPosition(i,fingerPosition[i]);
			fingerLineRenderer.SetPosition(fingerPosition.Count,fingerPosition[0]);
			goFingerLineRenderer.SetActive(true);
			
			List<Vector3> lasso = new List<Vector3>(fingerPosition);
			lasso.Add(fingerPosition[0]);
			canSelect = pointCloud.SetLasso(lasso);
			
			updateCountSinceMovingSlashStarted = 0;
		}
		else
		{
			goFingerLineRenderer.SetActive(false);
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
		}

		// state machine 
		if(currentState == state.NONE && ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) || (filteredVelocity > highVelocityThreshold && strategy != Strategy.PRECISE)) && timeSinceLastStateChange > stateChangeTimeThreshold)
		{
			pointCloud.TriggerSeparation(false,0);
			currentState = state.DRAW;
			
			fingerPosition.Clear();
			handPosition.Clear();
			fingerPositionTime.Clear();
			fingerLineRenderer.SetVertexCount(0);
		}
		else if(currentState == state.DRAW && strategy != Strategy.PRECISE && !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && filteredVelocity < lowVelocityThreshold && timeSinceLastStateChange > stateChangeTimeThreshold)
		{
			pointCloud.currentStrategy = Strategy.FAST;
			if(canSelect)
			{
				currentState = state.SELECT_IN_OUT;
			}
			else
			{
				pointCloud.TriggerSeparation(false,0);
				currentState = state.NONE;
				timeSinceLastStateChange = 0.0F;
				Clear();
				pointCloud.ResetSelected();
			}
		}
		else if(currentState == state.SELECT_IN_OUT && indexFinger != null && filteredVelocity < lowVelocityThreshold && timeSinceLastStateChange > stateChangeTimeThreshold) 
		{
			locked = true;
			goFingerLineRenderer.SetActive(false);
			pointCloud.TriggerSeparation(true,0);
			
			//Debug.Log(filteredVelocity);
			if(filteredVelocity > highVelocityThreshold)
			{
				updateCountSinceMovingSlashStarted++;
			}
			else if(filteredVelocity < lowVelocityThreshold && updateCountSinceMovingSlashStarted > 0)
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
				
				//Debug.Log(""+tmp.normal+direction.normalized+Mathf.Abs(Vector3.Angle(tmp.normal,direction.normalized)));
				
				fingerPosition.Add(fingerPosition[0]);
				pointCloud.SelectLasso(fingerPosition,(Mathf.Abs(Vector3.Angle(tmp.normal,direction.normalized)) > 90.0F));
				pointCloud.TriggerSeparation(false,(Mathf.Abs(Vector3.Angle(tmp.normal,direction.normalized)) > 90.0F) ? 1 : 2);
				
				timeSinceLastStateChange = 0.0F;
				updateCountSinceMovingSlashStarted = 0;
				currentState = state.NONE;
				fingerPosition.Clear();
				handPosition.Clear();
				fingerPositionTime.Clear();
				fingerLineRenderer.SetVertexCount(0);
				lastFilteredVelocity = 0;
			}
		}

		ProcessKeys ();

		if(currentState == state.SELECT_IN_OUT)
			locked = true;
		
		return locked;
	}
	
	public void SetEnabled(bool enable)
	{
		isEnabled = enable;
		goFingerLineRenderer.SetActive(isEnabled);
	}
	
	public void Clear()
	{
		if(!needsClear)
			return;
		needsClear = false;
		//Debug.Log("LASSO");

		currentState = state.NONE;
		goFingerLineRenderer.SetActive(false);
		
		fingerPosition.Clear();
		handPosition.Clear();
		fingerPositionTime.Clear();
		fingerLineRenderer.SetVertexCount(0);

		pointCloud.ResetSelected();
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
			Clear();
			pointCloud.ResetAll();
			resetTimer = 0;
		}
		else if(Input.GetKeyUp(KeyCode.Escape))
		{
			pointCloud.TriggerSeparation(false,0);
			if(currentState == state.NONE || currentState == state.DRAW)//timeSinceLastStateChange > 4.0F)
				pointCloud.Undo();
			currentState = state.NONE;
			timeSinceLastStateChange = 0.0F;
			Clear();
			pointCloud.ResetSelected();
		}

		// selection
		if(Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift)) // ready to select
		{
			pointCloud.currentStrategy = Strategy.PRECISE;
			if(canSelect)
				currentState = state.SELECT_IN_OUT;
			else
				currentState = state.SELECT;
		}
		else if(currentState != state.NONE && currentState != state.DRAW && (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.LeftShift))) // reset?
		{
			Clear();
			currentState = state.DRAW;
		}
	}
}
