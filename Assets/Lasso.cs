using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Leap;

public class Lasso : MonoBehaviour {
	Transform cameraTransform;
	PointCloud pointCloud;
	
	enum state { DRAW, WAITING_TWO_FINGERS, SELECT_IN_OUT, SELECT };
	state currentState = state.DRAW;
	float timeSinceLastStateChange = 0.0F;
	float timeSinceLastClickCompleted = 0.0F;
	float timeLastUpdate = 0.0F;
	float velocityThreshold = 300.0F;

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

		// distance between fingers
		if(fl.Count >= 2)
		{
			float fingerDistance = fl[0].TipPosition.DistanceTo(fl[1].TipPosition);
			pinchVelocity = ((fingerDistance-lastFingerDistance)/timeSinceLastUpdate);//0.6F*lastPinchVelocity + 0.4F*((fingerDistance-lastFingerDistance)/timeSinceLastUpdate);
			lastPinchVelocity = pinchVelocity;
			lastFingerDistance = fingerDistance;
			Debug.Log("pinchVelocity:"+pinchVelocity);
		}

		locked = true;

		if(currentState == state.DRAW)
		{
			fingerPosition.Add(goFingerList[0].transform.position);
			handPosition.Add(goHandList[0].transform.position);
			fingerPositionTime.Add(currentTime);
				
			fingerLineRenderer.SetVertexCount(fingerPosition.Count+1);
			for(int i = 0; i < fingerPosition.Count; i++)
				fingerLineRenderer.SetPosition(i,fingerPosition[i]);
			fingerLineRenderer.SetPosition(fingerPosition.Count,fingerPosition[0]);

			List<Vector3> lasso = new List<Vector3>(fingerPosition);
			lasso.Add(fingerPosition[0]);
			pointCloud.setLasso(lasso);
		}
		else if(currentState == state.WAITING_TWO_FINGERS && hl.Count >= 1 && fl.Count >= 2 && timeSinceLastStateChange >= 1.0F && Mathf.Abs(pinchVelocity) < velocityThreshold)
		{
			currentState = state.SELECT_IN_OUT;
		}
		// if angle between two hand-finger vectors is smaller than angle trigger threshold, change to 
		else if(currentState == state.SELECT_IN_OUT && hl.Count >= 1 && fl.Count >= 2) 
		{
			if(pinchVelocity > velocityThreshold)
			{
				// close the loop
				fingerPosition.Add(fingerPosition[0]);
				pointCloud.Lasso(fingerPosition,true);
				currentState = state.SELECT;
			}
			else if(pinchVelocity < -velocityThreshold)
			{
				// close the loop
				fingerPosition.Add(fingerPosition[0]);
				pointCloud.Lasso(fingerPosition,false);
				currentState = state.SELECT;
			}
		}
		
		if(currentState == state.SELECT)
		{
			currentState = state.DRAW;
			locked = false;
		}

		if(Input.GetKeyUp(KeyCode.LeftControl) || Input.GetKeyUp(KeyCode.RightControl)) // ready to select
		{
			currentState = state.WAITING_TWO_FINGERS;
			locked = true;
		}
		else if(currentState != state.DRAW && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))) // reset?
		{
			Clear();
			currentState = state.DRAW;
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
		Debug.Log("LASSO");

		currentState = state.DRAW;

		fingerPosition.Clear();
		handPosition.Clear();
		fingerPositionTime.Clear();
		fingerLineRenderer.SetVertexCount(0);

		pointCloud.ResetSelected();
	}
}
