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

public class VolumeSweep {//}: MonoBehaviour {
	Transform cameraTransform;
	PointCloud pointCloud;
	
	enum state { MOVING_FINGER, SELECT_IN_OUT, SELECT_BUBBLE, NONE };
	state currentState = state.MOVING_FINGER;
	float timeSinceLastStateChange = 0.0F;
	float timeSinceLastClickCompleted = 0.0F;
	float timeLastUpdate = 0.0F;
	float resetTimer = 0.0F;
	float velocityThreshold = 500.0F;
	int updateCountSinceMovingSlashStarted = 0;

	List<Vector3> handPosition;
	List<Vector3> fingerPosition;
	List<float> fingerPositionTime;
	float lastScalarVelocity = 0.0F;
	
	GameObject selectionVolume;
	List<GameObject> volumeTrail;
	List<Sphere> volumeTrailSpheres;
	
	bool select = false;

	bool isEnabled = true;
	bool needsClear = true;
	bool canSelect = false;

	bool shiftToggle = false;
	
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
		if(timeSinceLastClickCompleted < 0.5F) 
			return false; // avoid detecting two clicks in one
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
			fingerPositionTime.Add(currentTime);

			while(fingerPosition.Count > 60)
			{
				fingerPosition.RemoveAt(0);
				handPosition.RemoveAt(0);
				fingerPositionTime.RemoveAt(0);
			}
		}
		
		// calculate velocity
		float scalarVelocity;// = fl[0].TipVelocity.Magnitude; 
		float filteredVelocity;
		if(indexFinger != null && indexFinger.TimeVisible > 0.2)// && distance != fl[0].StabilizedTipPosition.Magnitude ) 
		{
			scalarVelocity = indexFinger.TipVelocity.Magnitude;
			filteredVelocity = scalarVelocity;//0.5F*lastScalarVelocity + 0.5F*scalarVelocity;
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
		lastScalarVelocity = scalarVelocity;
		
		// change state to moving finger (initial state) if there are two fingers 
		if(currentState == state.NONE && thumbFinger != null && indexFinger != null) 
		{
			pointCloud.TriggerSeparation(false,0);
			currentState = state.MOVING_FINGER;
			timeSinceLastStateChange = 0.0F;
			updateCountSinceMovingSlashStarted = 0;
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

			timeSinceLastStateChange = 0.0F;
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
					}
					direction /= fingerPosition.Count-initialPosition;
					
					Plane tmp = new Plane();
					tmp.Set3Points(cameraTransform.position+cameraTransform.forward,
					               cameraTransform.position,
					               cameraTransform.position+cameraTransform.up);

					//Debug.Log(""+tmp.normal+direction.normalized+Mathf.Abs(Vector3.Angle(tmp.normal,direction.normalized)));
					
					pointCloud.SelectSphereTrail(volumeTrailSpheres,(Mathf.Abs(Vector3.Angle(tmp.normal,direction.normalized)) < 90.0F));
					pointCloud.TriggerSeparation(false,(Mathf.Abs(Vector3.Angle(tmp.normal,direction.normalized)) > 90.0F) ? 1 : 2);

					currentState = state.SELECT_BUBBLE;
					timeSinceLastStateChange = 0.0F;
				}
				else
				{
					updateCountSinceMovingSlashStarted = 0;
				}
			}
		}

		// if things have been selected, reset state machine
		if(currentState == state.SELECT_BUBBLE)
		{
			currentState = state.NONE;
			timeSinceLastClickCompleted = 0.0F;
			volumeTrailSpheres.Clear();
		}

		// if in any state other than NONE, need to update pointcloud state for rendering
		if(currentState != state.NONE)
		{
			if(volumeTrailSpheres.Count == 0 || !canSelect)
			{
				canSelect = pointCloud.SetSphere(selectionVolume.transform.position,selectionVolume.transform.localScale.x/2.0F,true);
			}
			else if(currentState == state.MOVING_FINGER)
			{
				canSelect = pointCloud.SetSphere(volumeTrailSpheres[volumeTrailSpheres.Count-1].center,volumeTrailSpheres[volumeTrailSpheres.Count-1].radius,false);//pointCloud.SetSphereTrail(volumeTrailSpheres);
			}
		}

		ProcessKeys();

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

		//hold SHIFT key for bubble sweep
		if(strategy == Strategy.BOTH)
		{
			if(Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift)) // reset
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
			else if(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) // ready to select
			{
				Sphere s = new Sphere(selectionVolume.transform.position,selectionVolume.transform.localScale.x/2.0F);
				volumeTrailSpheres.Add(s);
			}
		}
		else if(strategy == Strategy.FAST) 
		{
			if(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) // reset
			{
				Sphere s = new Sphere(selectionVolume.transform.position,selectionVolume.transform.localScale.x/2.0F);
				volumeTrailSpheres.Add(s);
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
		}
		else if(strategy == Strategy.PRECISE) 
		{
			if(shiftToggle) // if selection started
			{
				Sphere s = new Sphere(selectionVolume.transform.position,selectionVolume.transform.localScale.x/2.0F);
				volumeTrailSpheres.Add(s);
			}

			if(Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift)) 
			{
				shiftToggle = !shiftToggle;
				if(!shiftToggle)
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
			}
		}
	}
}

