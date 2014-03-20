using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Leap;

public class Lasso : MonoBehaviour {
	Transform cameraTransform;
	PointCloud pointCloud;
	
	enum state { NONE, DRAW, WAITING_TWO_FINGERS, SELECT_IN_OUT, SELECT };
	state currentState = state.NONE;
	float timeSinceLastStateChange = 0.0F;
	float timeSinceLastClickCompleted = 0.0F;
	float timeLastUpdate = 0.0F;
	float velocityThreshold = 300.0F;
	float lastScalarVelocity = 0.0F;
	int updateCountSinceMovingSlashStarted = 0;

	GameObject goFingerLineRenderer;
	LineRenderer fingerLineRenderer;
	List<Vector3> handPosition;
	List<Vector3> fingerPosition;
	List<float> fingerPositionTime;
	float pinchVelocity = 0.0F;
	float lastPinchVelocity = 0.0F;
	float lastFingerDistance = 0.0F;
	
	bool isEnabled = true;
	bool needsClear = true;

	public Lasso() {
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
		
		float scalarVelocity = fl[0].TipVelocity.Magnitude; 
		float filteredVelocity = 0.5F*lastScalarVelocity + 0.5F*scalarVelocity;
		lastScalarVelocity = scalarVelocity;

		// distance between fingers
		if(fl.Count >= 2)
		{
			float fingerDistance = fl[0].TipPosition.DistanceTo(fl[1].TipPosition);
			pinchVelocity = ((fingerDistance-lastFingerDistance)/timeSinceLastUpdate);//0.6F*lastPinchVelocity + 0.4F*((fingerDistance-lastFingerDistance)/timeSinceLastUpdate);
			lastPinchVelocity = pinchVelocity;
			lastFingerDistance = fingerDistance;
			//Debug.Log("pinchVelocity:"+pinchVelocity);
		}

		locked = true;

		if(currentState == state.NONE && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
		{
			pointCloud.TriggerSeparation(false);
			currentState = state.DRAW;
		}
		if(currentState == state.DRAW)
		{
			pointCloud.TriggerSeparation(false);
			fingerPosition.Add(goFingerList[0].transform.position);
			handPosition.Add(goHandList[0].transform.position);
			fingerPositionTime.Add(currentTime);
				
			fingerLineRenderer.SetVertexCount(fingerPosition.Count+1);
			for(int i = 0; i < fingerPosition.Count; i++)
				fingerLineRenderer.SetPosition(i,fingerPosition[i]);
			fingerLineRenderer.SetPosition(fingerPosition.Count,fingerPosition[0]);

			List<Vector3> lasso = new List<Vector3>(fingerPosition);
			lasso.Add(fingerPosition[0]);
			pointCloud.SetLasso(lasso);

			updateCountSinceMovingSlashStarted = 0;
		}
		else if(currentState == state.WAITING_TWO_FINGERS && hl.Count >= 1 && fl.Count >= 2 && timeSinceLastStateChange >= 1.0F && Mathf.Abs(pinchVelocity) < velocityThreshold)
		{
			pointCloud.TriggerSeparation(true);
			currentState = state.SELECT_IN_OUT;
		}
		// if angle between two hand-finger vectors is smaller than angle trigger threshold, change to 
		else if(currentState == state.SELECT_IN_OUT && hl.Count >= 1 && fl.Count >= 1) 
		{
			pointCloud.TriggerSeparation(true);
			if(pointCloud.useSeparation)
			{
				//Debug.Log(filteredVelocity);
				if(filteredVelocity > velocityThreshold)
				{
					updateCountSinceMovingSlashStarted++;
				}
				else if(filteredVelocity < velocityThreshold && updateCountSinceMovingSlashStarted > 0)
				{
					int initialPosition = (fingerPosition.Count-1-updateCountSinceMovingSlashStarted < 0) ? 0 : (fingerPosition.Count-1-updateCountSinceMovingSlashStarted);
					Vector3 direction = new Vector3();
					for(int i = initialPosition; i < fingerPosition.Count; i++)
						direction += fingerPosition[i];
					direction /= fingerPosition.Count-initialPosition;
					
					Plane tmp = new Plane();
					tmp.Set3Points(cameraTransform.position+cameraTransform.forward,
					               cameraTransform.position,
					               cameraTransform.position+cameraTransform.up);
					//Debug.Log(Mathf.Abs(Vector3.Angle(tmp.normal,direction.normalized)));
					
					fingerPosition.Add(fingerPosition[0]);
					pointCloud.SelectLasso(fingerPosition,(Mathf.Abs(Vector3.Angle(tmp.normal,direction.normalized)) < 90.0F));

					currentState = state.SELECT;
					timeSinceLastStateChange = 0.0F;
				}
				else
				{
					updateCountSinceMovingSlashStarted = 0;
				}
			}
			else
			{
				if(pinchVelocity > velocityThreshold)
				{
					// close the loop
					fingerPosition.Add(fingerPosition[0]);
					pointCloud.SelectLasso(fingerPosition,true);
					currentState = state.SELECT;
				}
				else if(pinchVelocity < -velocityThreshold)
				{
					// close the loop
					fingerPosition.Add(fingerPosition[0]);
					pointCloud.SelectLasso(fingerPosition,false);
					currentState = state.SELECT;
				}
			}
		}
		
		if(currentState == state.SELECT)
		{
			pointCloud.TriggerSeparation(false);
			currentState = state.NONE;
			fingerPosition.Clear();
			handPosition.Clear();
			fingerPositionTime.Clear();
			fingerLineRenderer.SetVertexCount(0);
			locked = false;
		}

		if(Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift)) // ready to select
		{
			if(pointCloud.useSeparation)
				currentState = state.SELECT_IN_OUT;
			else
				currentState = state.WAITING_TWO_FINGERS;
			locked = true;
		}
		else if(currentState != state.NONE && currentState != state.DRAW && (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.LeftShift))) // reset?
		{
			Clear();
			currentState = state.DRAW;
		}

		// if two seconds have passed without a state change, reset state machine
		if(Input.GetKeyDown(KeyCode.Escape))
		{
			pointCloud.TriggerSeparation(false);
			if(currentState == state.NONE || currentState == state.DRAW)//timeSinceLastStateChange > 4.0F)
				pointCloud.Undo();
			currentState = state.NONE;
			timeSinceLastStateChange = 0.0F;
			Clear();
		}
		
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

		fingerPosition.Clear();
		handPosition.Clear();
		fingerPositionTime.Clear();
		fingerLineRenderer.SetVertexCount(0);

		pointCloud.ResetSelected();
	}
}
