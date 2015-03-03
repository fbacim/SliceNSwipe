using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Leap;

public class SlicenSwipe {
	Transform cameraTransform;
	PointCloud pointCloud;
	
	enum state { MOVING_FINGER, MOVING_CUT, CUT_SLASH, MOVING_SELECT, SELECT_SLASH, NONE };
	state currentState = state.NONE;
	float timeSinceLastStateChange = 0.0F;
	float timeSinceLastClickCompleted = 0.0F;
	float timeLastUpdate = 0.0F;
	float resetTimer = 0.0F;
	float lastScalarVelocity = 0.0F;
	float velocityThreshold = 500.0F;
	float stateChangeTimeThreshold = 0.2F;
	int updateCountSinceMovingSlashStarted = 0;
	Plane slashPlane;
	
	GameObject go1, go2, go3;

	GameObject fingerHandTrail;
	Mesh fingerHandTrailMesh;
	
	LineRenderer fingerLineRenderer;
	LineRenderer handLineRenderer;
	LineRenderer fingerHandLineRenderer;//GameObject fingerHandLineRenderer;
	List<Vector3> handPosition;
	List<Vector3> fingerPosition;
	List<float> fingerPositionTime;
	public float traceTime = 0.3F;

	bool useRubberBand = false;
	bool rubberBandActive = false;
	
	bool isEnabled = true;
	bool needsClear = true;
	
	enum Strategy { FAST, PRECISE, BOTH };
	Strategy strategy = Strategy.BOTH;
	
	public SlicenSwipe(int selectedStrategy) {
		strategy = (Strategy)selectedStrategy;

		pointCloud = GameObject.Find("Camera").GetComponent<PointCloud>();
		
		fingerPosition = new List<Vector3>();
		handPosition = new List<Vector3>();
		fingerPositionTime = new List<float>();
		
		fingerLineRenderer = (new GameObject("Finger Line Renderer")).AddComponent<LineRenderer>();
		fingerLineRenderer.material = Resources.Load("Trail", typeof(Material)) as Material;
		fingerLineRenderer.SetColors(new Color(0.8F,0.1F,0.1F,0.0F), new Color(0.1F,0.1F,0.8F,1.0F));
		fingerLineRenderer.SetWidth(0.2F,0.2F);

		handLineRenderer = (new GameObject("Hand Line Renderer")).AddComponent<LineRenderer>();
		handLineRenderer.material = Resources.Load("Trail", typeof(Material)) as Material;
		handLineRenderer.SetColors(new Color(0.8F,0.1F,0.1F,0.0F), new Color(0.1F,0.1F,0.8F,1.0F));
		handLineRenderer.SetWidth(0.2F,0.2F);
		
		fingerHandLineRenderer = (new GameObject("Finger-Hand Line Renderer")).AddComponent<LineRenderer>();//GameObject.CreatePrimitive(PrimitiveType.Cube);
		fingerHandLineRenderer.material.shader = Shader.Find("Sprites/Default");
		fingerHandLineRenderer.SetColors(new Color(0.8F,0.8F,0.8F), new Color(0.8F,0.8F,0.8F));
		fingerHandLineRenderer.SetWidth(0.3F,0.3F);

		fingerHandTrail = new GameObject("Finger-Hand Trail"); //create a new gameobject. This gameobject will hold the mesh we’re creating.
		fingerHandTrail.AddComponent<MeshFilter>(); //this is what makes the mesh available to the other mesh components
		fingerHandTrail.AddComponent<MeshRenderer>(); //this is what makes the mesh visible
		fingerHandTrail.SetActive(false);
	}

	public void SetEnabled(bool enable)
	{
		isEnabled = enable;
		fingerHandTrail.SetActive(isEnabled);
		fingerLineRenderer.gameObject.SetActive(isEnabled);
		fingerHandLineRenderer.gameObject.SetActive(false);
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

		//Debug.Log(currentState);

		// calculate how much time has passed since last update
		float currentTime = Time.timeSinceLevelLoad;
		float timeSinceLastUpdate = currentTime - timeLastUpdate;
		timeLastUpdate = currentTime;

		if(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
		{
			useRubberBand = true;
		}
		else
		{
			useRubberBand = false;
		}
		
		// update finger trace
		if(frame.Fingers.Count > 0)
		{
			fingerPosition.Add(goFingerList[0].transform.position);
			handPosition.Add(goHandList[0].transform.position);
			fingerPositionTime.Add(currentTime);

			if(!rubberBandActive)
			{
				while(currentTime-fingerPositionTime[0] > traceTime)
				{
					fingerPosition.RemoveAt(0);
					handPosition.RemoveAt(0);
					fingerPositionTime.RemoveAt(0);
				}
			}
			else
			{
				// remove everything except the first and last finger position
				for(int i = 1; i < fingerPosition.Count; i++)
				{
					fingerPosition.RemoveAt(1);
					handPosition.RemoveAt(1);
					fingerPositionTime.RemoveAt(1);
				}
			}

			fingerLineRenderer.SetVertexCount(fingerPosition.Count);
			for(int i = 0; i < fingerPosition.Count; i++)
				fingerLineRenderer.SetPosition(i,fingerPosition[i]);
		
			if(!rubberBandActive)
			{
				handLineRenderer.SetVertexCount(handPosition.Count);
				for(int i = 0; i < handPosition.Count; i++)
					handLineRenderer.SetPosition(i,handPosition[i]);
			}
			else if(handPosition.Count > 1)
			{
				handLineRenderer.SetVertexCount(2);
				handLineRenderer.SetPosition(0,fingerPosition[0]);
				handLineRenderer.SetPosition(1,handPosition[1]);
			}
			
			fingerHandLineRenderer.SetVertexCount(2);
			fingerHandLineRenderer.SetPosition(0,handPosition[handPosition.Count-1]);
			fingerHandLineRenderer.SetPosition(1,fingerPosition[fingerPosition.Count-1]);

			// create trail
			fingerHandTrailMesh = new Mesh(); //this is the mesh we’re creating.

			if(!rubberBandActive)
			{
				Vector3[] vertices = new Vector3[fingerPosition.Count*2];
				Vector2[] uv = new Vector2[fingerPosition.Count*2];
				int[] triangles = new int[(fingerPosition.Count-1)*6];
				Color[] colors = new Color[fingerPosition.Count*2];

				for(int i = 0; i < fingerPosition.Count; i++)
				{
					vertices[i*2]   = new Vector3(handPosition[i].x, handPosition[i].y, handPosition[i].z);
					// extend knife
					Vector3 handFingerVec = fingerPosition[i]-handPosition[i];
					float magnitude = handFingerVec.magnitude;
					handFingerVec.Normalize();
					handFingerVec *= magnitude*3.0F;
					vertices[i*2+1] = handPosition[i]+handFingerVec;//new Vector3(fingerPosition[i].x, fingerPosition[i].y, fingerPosition[i].z);

					uv[i*2]   = new Vector2(vertices[i*2].x, vertices[i*2].z);
					uv[i*2+1] = new Vector2(vertices[i*2+1].x, vertices[i*2+1].z);

					if(i > 0)
					{
						triangles[(i-1)*6]   = i*2;
						triangles[(i-1)*6+1] = i*2-2;
						triangles[(i-1)*6+2] = i*2-1;
						
						triangles[(i-1)*6+3] = i*2-1;
						triangles[(i-1)*6+4] = i*2+1;
						triangles[(i-1)*6+5] = i*2;
					}

					colors[i*2]   = new Color(0.7F,0.7F,0.7F,(float)i/(float)fingerPosition.Count);
					colors[i*2+1] = new Color(0.7F,0.7F,0.7F,(float)i/(float)fingerPosition.Count);
				}

				fingerHandTrailMesh.vertices = vertices;
				fingerHandTrailMesh.uv = uv;
				fingerHandTrailMesh.triangles = triangles;
				fingerHandTrailMesh.colors = colors;
			}
			else if(fingerPosition.Count > 1)
			{
				Vector3[] vertices = new Vector3[4];
				Vector2[] uv = new Vector2[4];
				int[] triangles = new int[6];
				Color[] colors = new Color[4];
				
				// extend knife
				Vector3 handFingerVec = fingerPosition[1]-handPosition[1];

				//vertices[0] = new Vector3((handPosition[0].x+handPosition[1].x)/2.0F, (handPosition[0].y+handPosition[1].y)/2.0F, (handPosition[0].z+handPosition[1].z)/2.0F);
				vertices[0] = fingerPosition[0]-handFingerVec;//new Vector3(handPosition[0].x, handPosition[0].y, handPosition[0].z);
				vertices[1] = vertices[0]+handFingerVec*3.0F;//new Vector3(fingerPosition[0].x, fingerPosition[0].y, fingerPosition[0].z);
				vertices[2] = new Vector3(handPosition[1].x, handPosition[1].y, handPosition[1].z);
				vertices[3] = handPosition[1]+handFingerVec*3.0F;//new Vector3(fingerPosition[1].x, fingerPosition[1].y, fingerPosition[1].z);//
				
				uv[0] = new Vector2(vertices[0].x, vertices[0].z);
				uv[1] = new Vector2(vertices[1].x, vertices[1].z);
				uv[2] = new Vector2(vertices[2].x, vertices[2].z);
				uv[3] = new Vector2(vertices[3].x, vertices[3].z);
				
				triangles[0] = 2;
				triangles[1] = 0;
				triangles[2] = 1;
				
				triangles[3] = 1;
				triangles[4] = 3;
				triangles[5] = 2;
				
				colors[0] = new Color(0.7F,0.7F,0.7F,0.1F);
				colors[1] = new Color(0.7F,0.7F,0.7F,0.1F);
				colors[2] = new Color(0.7F,0.7F,0.7F,1.0F);
				colors[3] = new Color(0.7F,0.7F,0.7F,1.0F);
				
				fingerHandTrailMesh.vertices = vertices;
				fingerHandTrailMesh.uv = uv;
				fingerHandTrailMesh.triangles = triangles;
				fingerHandTrailMesh.colors = colors;
			}

			fingerHandTrailMesh.RecalculateNormals();

			//fingerHandTrail.GetComponent<MeshFilter>().mesh = fingerHandTrailMesh;
			//if(!fingerHandTrail.GetComponent<MeshCollider>())
			//	fingerHandTrail.AddComponent<MeshCollider>();
			fingerHandTrail.renderer.material = Resources.Load("Trail", typeof(Material)) as Material;
			fingerHandTrail.SetActive(true);
		}
		else
		{
			fingerHandTrail.SetActive(false);
			fingerLineRenderer.SetVertexCount(0);
			handLineRenderer.SetVertexCount(0);
			if(fingerPosition.Count > 0)
			{
				fingerPosition.RemoveAt(0);
				handPosition.RemoveAt(0);
				fingerPositionTime.RemoveAt(0);
			}
		}
		
		timeSinceLastClickCompleted += timeSinceLastUpdate;
		if(timeSinceLastClickCompleted < 0.5F) return locked; // avoid detecting two clicks in one

		timeSinceLastStateChange += timeSinceLastUpdate;
		
		if(currentState == state.MOVING_CUT || currentState == state.MOVING_SELECT)
			updateCountSinceMovingSlashStarted++;
		
		HandList hl = frame.Hands;
		FingerList fl = frame.Fingers;
		
		float scalarVelocity;// = fl[0].TipVelocity.Magnitude; 
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
			if(currentState != state.CUT_SLASH && currentState != state.MOVING_SELECT)
			{
				currentState = state.NONE;
			}
		}
		lastScalarVelocity = scalarVelocity;

		//Debug.Log("Velocity: "+scalarVelocity+"    filtered: "+filteredVelocity);
		
		// reset state machine
		if(currentState == state.NONE && hl.Count >= 1 && fl.Count >= 1) 
		{
			pointCloud.TriggerSeparation(false,0);
			currentState = state.MOVING_FINGER;
			timeSinceLastStateChange = 0.0F;
		}
		// if velocity above threshold, record motion in moving cut state
		else if(currentState == state.MOVING_FINGER && hl.Count >= 1 && fl.Count >= 1 && (filteredVelocity > velocityThreshold || useRubberBand)) 
		{
			pointCloud.TriggerSeparation(false,0);
			currentState = state.MOVING_CUT;
			timeSinceLastStateChange = 0.0F;
			updateCountSinceMovingSlashStarted = 0;
			if(useRubberBand)
			{
				useRubberBand = false;
				rubberBandActive = true;

				// remove everything except the last finger position
				for(int i = 0; i < fingerPosition.Count; i++)
				{
					fingerPosition.RemoveAt(0);
					handPosition.RemoveAt(0);
					fingerPositionTime.RemoveAt(0);
				}
			}
		}
		// if velocity goes below the threshold again, change state to cut slash, where the cut has been made
		else if(currentState == state.MOVING_CUT && hl.Count >= 1 && fl.Count >= 1 && ((!rubberBandActive && filteredVelocity < velocityThreshold) || (rubberBandActive && !useRubberBand))) 
		{
			//Debug.Log("SLICE");
			int initialPosition = fingerPosition.Count-1-updateCountSinceMovingSlashStarted;
			slashPlane.Set3Points(fingerPosition[initialPosition < 0 ? 0 : initialPosition],
			                      handPosition[1],
			                      fingerPosition[fingerPosition.Count-1]);
			if(Mathf.Abs(Vector3.Angle(slashPlane.normal,GameObject.Find("Camera").transform.right)) > 90.0F)
			{
				slashPlane.Set3Points(fingerPosition[fingerPosition.Count-1],
				                      handPosition[1],
				                      fingerPosition[initialPosition < 0 ? 0 : initialPosition]);
			}

			//Debug.Log("normal: "+slashPlane.normal+"  d: "+slashPlane.distance);
			// first check if slice is valid
			if(pointCloud.SetSelectionPlane(slashPlane))
			{
				currentState = state.CUT_SLASH;
			
				pointCloud.TriggerSeparation(true,0);
				if(rubberBandActive)
					updateCountSinceMovingSlashStarted = 1;
				rubberBandActive = false;

				timeSinceLastStateChange = 0.0F;
			}
			else
			{
				rubberBandActive = false;
				pointCloud.ResetSelected();
				currentState = state.NONE;
				timeSinceLastStateChange = 0.0F;
			}
		}
		// if a cut has been made, velocity is above threshold again and significant time has passed since cut slash was registered, we record motions again for selection of 
		else if(currentState == state.CUT_SLASH && hl.Count >= 1 && fl.Count >= 1 && filteredVelocity > velocityThreshold && timeSinceLastStateChange > stateChangeTimeThreshold)
		{
			pointCloud.TriggerSeparation(true,0);
			//Debug.Log("STARTING SWIPE");
			currentState = state.MOVING_SELECT;
			timeSinceLastStateChange = 0.0F;
			updateCountSinceMovingSlashStarted = 0;
		}
		else if(currentState == state.MOVING_SELECT && hl.Count >= 1 && fl.Count >= 1 && filteredVelocity < velocityThreshold && timeSinceLastStateChange > stateChangeTimeThreshold)
		{
			//Debug.Log("SWIPE");
			int initialPosition = (fingerPosition.Count-1-updateCountSinceMovingSlashStarted < 0) ? 0 : (fingerPosition.Count-1-updateCountSinceMovingSlashStarted);
			Vector3 direction = new Vector3();
			for(int i = initialPosition; i < fingerPosition.Count; i++)
				direction += fingerPosition[i];
			direction /= fingerPosition.Count-initialPosition;
			//Debug.Log(direction.normalized);
			//Debug.Log(Mathf.Abs(Vector3.Angle(slashPlane.normal,direction.normalized)));
			Plane tmp = new Plane();
			tmp.Set3Points(GameObject.Find("Camera").transform.position+GameObject.Find("Camera").transform.forward,
			               GameObject.Find("Camera").transform.position,
			               GameObject.Find("Camera").transform.position+GameObject.Find("Camera").transform.up);
			//Debug.Log(tmp.normal);
			//Debug.Log(Mathf.Abs(Vector3.Angle(tmp.normal,direction.normalized)));

			pointCloud.SelectSide(slashPlane,(Mathf.Abs(Vector3.Angle(tmp.normal,direction.normalized)) < 90.0F));
			pointCloud.TriggerSeparation(false,(Mathf.Abs(Vector3.Angle(tmp.normal,direction.normalized)) > 90.0F) ? 1 : 2);
			currentState = state.SELECT_SLASH;
			timeSinceLastStateChange = 0.0F;
		}

		ProcessKeys ();

		// if the user wants to try rubber band again
		if(currentState != state.MOVING_FINGER && currentState != state.MOVING_CUT && useRubberBand)
		{
			pointCloud.ResetSelected();
			currentState = state.MOVING_FINGER;
			timeSinceLastStateChange = 0.0F;
		}

		// updates rubber band rendering, if active
		if(rubberBandActive && fingerPosition.Count > 1)
		{
			slashPlane.Set3Points(fingerPosition[0], 
			                      handPosition[1], 
			                      fingerPosition[1]);
			pointCloud.SetSelectionPlane(slashPlane);
		}
				
		if(currentState == state.SELECT_SLASH)
		{
			currentState = state.NONE;
			timeSinceLastClickCompleted = 0.0F;
		}

		if(currentState == state.CUT_SLASH || currentState == state.MOVING_SELECT)
			locked = true;

		return locked;
	}

	public void Clear()
	{
		if(!needsClear)
			return;
		needsClear = false;

		//Debug.Log("SLICENSWIPE");

		// if two seconds have passed without a state change, reset state machine
		if(currentState != state.NONE && currentState != state.MOVING_FINGER)
		{
			pointCloud.ResetSelected();
			currentState = state.NONE;
			timeSinceLastStateChange = 0.0F;
		}
	}

	public void RenderTransparentObjects()
	{
		if(fingerHandTrailMesh != null && isEnabled && fingerHandTrail.activeSelf)
		{
			fingerHandTrail.SetActive(true);
			fingerHandTrail.renderer.material.SetPass(0);
			Graphics.DrawMeshNow(fingerHandTrailMesh,Matrix4x4.identity);
			fingerHandTrail.SetActive(false);
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
			Clear();
			pointCloud.ResetAll();
			resetTimer = 0;
		}
		else if(Input.GetKeyUp(KeyCode.Escape) && resetTimer > 0.0f && currentTime-resetTimer < 2.0f)
		{
			pointCloud.TriggerSeparation(false,0);
			if((currentState == state.NONE || currentState == state.MOVING_FINGER) && !rubberBandActive)//timeSinceLastStateChange > 4.0F)
				pointCloud.Undo();
			currentState = state.NONE;
			timeSinceLastStateChange = 0.0F;
			pointCloud.ResetSelected();
		}
	}
}
