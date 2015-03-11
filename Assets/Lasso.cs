using UnityEngine;
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
	float timeLastUpdate = 0.0F;
	float resetTimer = 0.0F;
	float highVelocityThreshold = 500.0F;
	float lowVelocityThreshold = 100.0F;
	float lastScalarVelocity = 0.0F;
	int updateCountSinceMovingSlashStarted = 0;

	GameObject goFingerLineRenderer;
	LineRenderer fingerLineRenderer;
	List<Vector3> handPosition;
	List<Vector3> fingerPosition;
	List<float> fingerPositionTime;
	
	bool isEnabled = true;
	bool needsClear = true;
	bool canSelect = false;

	enum Strategy { FAST, PRECISE, BOTH };
	Strategy strategy = Strategy.BOTH;

	public Lasso(int selectedStrategy) {
		strategy = (Strategy)selectedStrategy;

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
		float currentTime = Time.timeSinceLevelLoad;
		float timeSinceLastUpdate = currentTime - timeLastUpdate;
		timeLastUpdate = currentTime;
		
		timeSinceLastClickCompleted += timeSinceLastUpdate;
		if(timeSinceLastClickCompleted < 0.2) return locked; // avoid detecting two clicks in one
		
		if(currentState == state.DRAW)
			timeSinceLastStateChange = 0.0F;
		else
			timeSinceLastStateChange += timeSinceLastUpdate;
		
		HandList hl = frame.Hands;
		FingerList fl = frame.Fingers;
		
		float scalarVelocity;
		float filteredVelocity;
		if(fl.Count > 0 && fl[0].TimeVisible > 0.2)// && distance != fl[0].StabilizedTipPosition.Magnitude ) 
		{
			scalarVelocity = fl[0].TipVelocity.Magnitude;
			filteredVelocity = 0.5F*lastScalarVelocity + 0.5F*scalarVelocity;
		}
		else
		{
			scalarVelocity = 0;
			filteredVelocity = 0;
			if(currentState != state.SELECT_IN_OUT)
				currentState = state.NONE;
		}
		lastScalarVelocity = scalarVelocity;

		// s
		if(currentState == state.NONE && ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) || (filteredVelocity > highVelocityThreshold && strategy != Strategy.PRECISE)))
		{
			pointCloud.TriggerSeparation(false,0);
			currentState = state.DRAW;
			
			fingerPosition.Clear();
			handPosition.Clear();
			fingerPositionTime.Clear();
			fingerLineRenderer.SetVertexCount(0);
		}
		else if(currentState == state.DRAW && strategy != Strategy.PRECISE && !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && filteredVelocity < lowVelocityThreshold)
		{
			currentState = state.SELECT_IN_OUT;
		}

		if(currentState == state.DRAW)
		{
			pointCloud.TriggerSeparation(false,0);
			fingerPosition.Add(goFingerList[0].transform.position);
			handPosition.Add(goHandList[0].transform.position);
			fingerPositionTime.Add(currentTime);
				
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
				fingerPosition.Add(goFingerList[0].transform.position);
				handPosition.Add(goHandList[0].transform.position);
				fingerPositionTime.Add(currentTime);
				
				while(fingerPosition.Count > 60)
				{
					fingerPosition.RemoveAt(0);
					handPosition.RemoveAt(0);
					fingerPositionTime.RemoveAt(0);
				}
			}
		}

		if(currentState == state.SELECT_IN_OUT && hl.Count >= 1 && fl.Count >= 1) 
		{
			locked = true;
			goFingerLineRenderer.SetActive(false);
			pointCloud.TriggerSeparation(true,0);

			//Debug.Log(filteredVelocity);
			if(filteredVelocity > highVelocityThreshold)
			{
				updateCountSinceMovingSlashStarted++;
			}
			else if(filteredVelocity < highVelocityThreshold && updateCountSinceMovingSlashStarted > 0)
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

				currentState = state.SELECT;
				timeSinceLastStateChange = 0.0F;
			}
			else
			{
				updateCountSinceMovingSlashStarted = 0;
			}
		}

		ProcessKeys ();
		
		if(currentState == state.SELECT)
		{
			currentState = state.NONE;
			fingerPosition.Clear();
			handPosition.Clear();
			fingerPositionTime.Clear();
			fingerLineRenderer.SetVertexCount(0);
		}

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
		else if(Input.GetKey(KeyCode.Escape) && resetTimer > 0.0f && currentTime-resetTimer > 2.0f)
		{
			pointCloud.TriggerSeparation(false,0);
			currentState = state.NONE;
			timeSinceLastStateChange = 0.0F;
			Clear();
			pointCloud.ResetAll();
			resetTimer = 0;
		}
		else if(Input.GetKeyUp(KeyCode.Escape) && resetTimer > 0.0f && currentTime-resetTimer < 2.0f)
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
