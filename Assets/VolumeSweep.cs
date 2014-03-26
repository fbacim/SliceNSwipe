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

public class VolumeSweep : MonoBehaviour {
	Transform cameraTransform;
	PointCloud pointCloud;
	
	enum state { MOVING_FINGER, SELECT_IN_OUT, SELECT_BUBBLE, NONE };
	state currentState = state.NONE;
	float timeSinceLastStateChange = 0.0F;
	float timeSinceLastClickCompleted = 0.0F;
	float timeLastUpdate = 0.0F;
	float resetTimer = 0.0F;
	float velocityThreshold = 300.0F;
	int updateCountSinceMovingSlashStarted = 0;

	List<Vector3> handPosition;
	List<Vector3> fingerPosition;
	List<float> fingerPositionTime;
	float pinchVelocity = 0.0F;
	float lastPinchVelocity = 0.0F;
	float lastFingerDistance = 0.0F;
	float lastScalarVelocity = 0.0F;
	
	GameObject selectionVolume;
	List<GameObject> volumeTrail;
	List<Sphere> volumeTrailSpheres;
	
	bool select = false;

	bool isEnabled = true;
	bool needsClear = true;
	bool canSelect = false;
	
	public VolumeSweep() {
		pointCloud = GameObject.Find("Camera").GetComponent<PointCloud>();
		cameraTransform = GameObject.Find("Camera").GetComponent<Transform>();
		
		fingerPosition = new List<Vector3>();
		handPosition = new List<Vector3>();
		fingerPositionTime = new List<float>();
		
		selectionVolume = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		selectionVolume.name = "Bubble";
		selectionVolume.renderer.material = Resources.Load("DiffuseZ", typeof(Material)) as Material;
		selectionVolume.renderer.material.color = new Color(0.3F, 0.3F, 0.3F, 0.5F);

		volumeTrailSpheres = new List<Sphere>();
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
		if(timeSinceLastClickCompleted < 0.5F) return false; // avoid detecting two clicks in one
		
		timeSinceLastStateChange += timeSinceLastUpdate;

		//Debug.Log(currentState);

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
		
		HandList hl = frame.Hands;
		FingerList fl = frame.Fingers;
		
		float scalarVelocity;
		float filteredVelocity;
		if(fl.Count > 0 && fl[0].TimeVisible > 0.1)// && distance != fl[0].StabilizedTipPosition.Magnitude ) 
		{
			scalarVelocity = fl[0].TipVelocity.Magnitude;
			filteredVelocity = 0.5F*lastScalarVelocity + 0.5F*scalarVelocity;
		}
		else
		{
			scalarVelocity = 0;
			filteredVelocity = 0;
			currentState = state.NONE;
			Clear();
		}
		lastScalarVelocity = scalarVelocity;

		// distance between fingers
		if(fl.Count == 2)
		{
			float fingerDistance = fl[0].StabilizedTipPosition.DistanceTo(fl[1].StabilizedTipPosition);
			pinchVelocity = ((fingerDistance-lastFingerDistance)/timeSinceLastUpdate);//0.6F*lastPinchVelocity + 0.4F*((fingerDistance-lastFingerDistance)/timeSinceLastUpdate);
			lastPinchVelocity = pinchVelocity;
			lastFingerDistance = fingerDistance;
		}
		
		// reset state machine
		/*if(hl.Count == 0 || (hl.Count >= 1 && fl.Count < 1)) 
		{
			pointCloud.TriggerSeparation(false);
			currentState = state.NONE;
			timeSinceLastStateChange = 0.0F;
		}
		// change state to moving finger (initial state) if there are two fingers 
		else */if(currentState == state.NONE && hl.Count >= 1 && fl.Count >= 2) 
		{
			pointCloud.TriggerSeparation(false,0);
			currentState = state.MOVING_FINGER;
			timeSinceLastStateChange = 0.0F;
			updateCountSinceMovingSlashStarted = 0;
		}
		// if angle between two hand-finger vectors is smaller than angle trigger threshold, change to 
		else if(currentState == state.SELECT_IN_OUT && hl.Count >= 1 && fl.Count >= 1) 
		{
			pointCloud.TriggerSeparation(true,0);
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
					{
						direction += fingerPosition[i];
						print(direction);
					}
					direction /= fingerPosition.Count-initialPosition;
					
					Plane tmp = new Plane();
					tmp.Set3Points(cameraTransform.position+cameraTransform.forward,
					               cameraTransform.position,
					               cameraTransform.position+cameraTransform.up);

					Debug.Log(""+tmp.normal+direction.normalized+Mathf.Abs(Vector3.Angle(tmp.normal,direction.normalized)));
					
					pointCloud.SelectSphereTrail(volumeTrailSpheres,(Mathf.Abs(Vector3.Angle(tmp.normal,direction.normalized)) < 90.0F));
					pointCloud.TriggerSeparation(false,(Mathf.Abs(Vector3.Angle(tmp.normal,direction.normalized)) > 90.0F) ? 1 : 2);
					
					selectionVolume.SetActive(true);
					currentState = state.SELECT_BUBBLE;
					timeSinceLastStateChange = 0.0F;
				}
				else
				{
					updateCountSinceMovingSlashStarted = 0;
					selectionVolume.SetActive(false);
				}
			}
			else
			{
				if(pinchVelocity > velocityThreshold)
				{
					pointCloud.SelectSphereTrail(volumeTrailSpheres,true);
					selectionVolume.SetActive(true);
					currentState = state.SELECT_BUBBLE;
				}
				else if(pinchVelocity < -velocityThreshold)
				{
					pointCloud.SelectSphereTrail(volumeTrailSpheres,false);
					selectionVolume.SetActive(true);
					currentState = state.SELECT_BUBBLE;
				}
				else
				{
					selectionVolume.SetActive(false);
				}
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
			if(volumeTrailSpheres.Count == 0 || !canSelect)
				canSelect = pointCloud.SetSphere(selectionVolume.transform.position,selectionVolume.transform.localScale.x/2.0F,true);
			else if(currentState == state.MOVING_FINGER)
				canSelect = pointCloud.SetSphere(volumeTrailSpheres[volumeTrailSpheres.Count-1].center,volumeTrailSpheres[volumeTrailSpheres.Count-1].radius,false);//pointCloud.SetSphereTrail(volumeTrailSpheres);
		}

		//hold key for bubble sweep
		
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
			volumeTrailSpheres.Clear();
			pointCloud.ResetAll();
			resetTimer = 0;
		}
		else if(Input.GetKeyUp(KeyCode.Escape) && resetTimer > 0.0f && currentTime-resetTimer < 2.0f)
		{
			pointCloud.TriggerSeparation(false,0);
			if(currentState == state.NONE || currentState == state.MOVING_FINGER)//timeSinceLastStateChange > 4.0F)
				pointCloud.Undo();
			pointCloud.ResetSelected();
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
			if(canSelect)
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
		isEnabled = enable;
		selectionVolume.SetActive(false);
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
		}
	}

	public void RenderTransparentObjects()
	{
		if(selectionVolume != null && isEnabled)
		{
			selectionVolume.renderer.material.SetPass(0);
			Graphics.DrawMeshNow(selectionVolume.GetComponent<MeshFilter>().mesh,selectionVolume.transform.localToWorldMatrix);
		}

	}
}

