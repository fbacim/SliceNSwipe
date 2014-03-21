using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Leap;

public class LeapController : MonoBehaviour {

	public SlicenSwipe slicenSwipe;
	public VolumeSweep volumeSweep;
	public Lasso lasso;
	
	enum technique { SLICENSWIPE=0, VOLUMESWEEP=1, LASSO=2, SIZE=3 };
	technique currentTechnique = technique.SLICENSWIPE;  

	Leap.Controller controller;
	List<GameObject> goFingerList;
	List<GameObject> goHandList;
	Transform cameraTransform;
	PointCloud pointCloud;
	GUIText annotationTextInput;

	float fingerAvg = 0.0F;

	bool techniqueMenuActive = false;

	// Use this for initialization
	void Start () {
		controller = new Leap.Controller();
		
		annotationTextInput = GameObject.Find("Annotation Input").GetComponent<GUIText>();
		cameraTransform = GameObject.Find("Camera").GetComponent<Transform>();
		pointCloud = GameObject.Find("Camera").GetComponent<PointCloud>();

		slicenSwipe = new SlicenSwipe();
		volumeSweep = new VolumeSweep();
		lasso = new Lasso();

		goFingerList = new List<GameObject>();
		for(int i = 0; i < 10; i++)
			goFingerList.Add(GameObject.CreatePrimitive(PrimitiveType.Sphere));

		goHandList = new List<GameObject>();
		for(int i = 0; i < 2; i++)
			goHandList.Add(GameObject.CreatePrimitive(PrimitiveType.Cube));
	}

	bool UpdateAnnotation() {
		foreach (char c in Input.inputString) 
		{
			//Debug.Log((int)c);
			// if backspace, erase text character
			if (c == "\b"[0])
			{
				if (annotationTextInput.text.Length != 0)
					annotationTextInput.text = annotationTextInput.text.Substring(0, annotationTextInput.text.Length - 1);
			}
			// if enter is pressed, need to process annotation
			else if (c == "\n"[0] || c == "\r"[0])
			{
				return true; 
			}
			// if enter is pressed, need to process annotation
			else if (c == 47)
			{
			}
			// else just add to the string
			else
				annotationTextInput.text += c;
		}
		return false;
	}
	
	// Update is called once per frame
	void Update () {
		Frame frame = controller.Frame();
		HandList hl = frame.Hands;
		FingerList fl = frame.Fingers;
		GestureList gl = frame.Gestures(controller.Frame(1));

		for(int i = 0; i < gl.Count; i++)
		{
			//Debug.Log("["+gl[i].DurationSeconds+"] "+gl[i].Type+" -> "+gl[i].State);
		}

		//Debug.Log("<"+frame.InteractionBox.Width+", "+frame.InteractionBox.Height+", "+frame.InteractionBox.Depth+">  "+frame.InteractionBox.Center); // +"   "+pointCloud.Size()
		float maxd = Mathf.Max(new float[]{frame.InteractionBox.Width, frame.InteractionBox.Height, frame.InteractionBox.Depth});

		// update hand renderer
		for(int i = 0; i < goHandList.Count; i++)
			goHandList[i].SetActive(false);

		for(int i = 0; (i < hl.Count && i < goHandList.Count); i++)
		{
			Vector3 position = new Vector3();
			// TRANSFORMING FINGER POSITION TO MATCH POINT CLOUD SIZE
			// normalize position of fingers, then multiply by magnitude of point cloud size vector adjusted by ratio of interaction box sizes
			position.x =   ((hl[0].PalmPosition.x - frame.InteractionBox.Center.x) / frame.InteractionBox.Width ) * (pointCloud.Size().magnitude*(frame.InteractionBox.Width/maxd));
			position.y =   ((hl[0].PalmPosition.y - frame.InteractionBox.Center.y) / frame.InteractionBox.Height) * (pointCloud.Size().magnitude*(frame.InteractionBox.Height/maxd));
			position.z = -(((hl[0].PalmPosition.z - frame.InteractionBox.Center.z) / frame.InteractionBox.Depth ) * (pointCloud.Size().magnitude*(frame.InteractionBox.Depth/maxd)));
			// rotate position to match camera
			position = cameraTransform.rotation * position;
			//Debug.Log("["+Time.time+"] hand["+i+"]: "+position);
			goHandList[i].SetActive(true);
			goHandList[i].transform.position = position;
		}

		// update finger renderer
		for(int i = 0; i < goFingerList.Count; i++)
			goFingerList[i].SetActive(false);

		for(int i = 0; (i < fl.Count && i < goFingerList.Count); i++)
		{
			Vector3 position = new Vector3();
			// TRANSFORMING FINGER POSITION TO MATCH POINT CLOUD SIZE
			// normalize position of fingers, then multiply by magnitude of point cloud size vector adjusted by ratio of interaction box sizes
			position.x =   ((fl[i].TipPosition.x - frame.InteractionBox.Center.x) / frame.InteractionBox.Width ) * (pointCloud.Size().magnitude*(frame.InteractionBox.Width/maxd));
			position.y =   ((fl[i].TipPosition.y - frame.InteractionBox.Center.y) / frame.InteractionBox.Height) * (pointCloud.Size().magnitude*(frame.InteractionBox.Height/maxd));
			position.z = -(((fl[i].TipPosition.z - frame.InteractionBox.Center.z) / frame.InteractionBox.Depth ) * (pointCloud.Size().magnitude*(frame.InteractionBox.Depth/maxd)));
			// rotate position to match camera
			position = cameraTransform.rotation * position;
			//Debug.Log("["+Time.time+"] finger["+i+"]: "+position);
			goFingerList[i].SetActive(true);
			goFingerList[i].transform.position = position;
		}

		fingerAvg = fingerAvg * 0.9F + fl.Count * 0.1F;
		//Debug.Log(fingerAvg);

		slicenSwipe.SetEnabled(false);
		volumeSweep.SetEnabled(false);
		lasso.SetEnabled(false);
		
		if(Input.GetKeyDown(KeyCode.PageUp))
		{
			currentTechnique++;
			if(currentTechnique == technique.SIZE)
				currentTechnique = 0;
		}
		else if(Input.GetKeyDown(KeyCode.PageDown))
		{
			currentTechnique--;
			if(currentTechnique < 0)
				currentTechnique = technique.SIZE-1;
		}

		if(fingerAvg > 4 || techniqueMenuActive)
		{
			techniqueMenuActive = true;
			if(Input.GetKeyUp(KeyCode.Escape) || fingerAvg == 0)
				techniqueMenuActive = false;
		}
		else
		{
			if(currentTechnique == technique.SLICENSWIPE)
				slicenSwipe.SetEnabled(true);
			else if(currentTechnique == technique.VOLUMESWEEP)
				volumeSweep.SetEnabled(true);
			else if(currentTechnique == technique.LASSO)
				lasso.SetEnabled(true);
			//Debug.Log(currentTechnique);
		}

		bool techniqueLock;
		techniqueLock = slicenSwipe.ProcessFrame(frame, goHandList, goFingerList);
		techniqueLock = volumeSweep.ProcessFrame(frame, goHandList, goFingerList);
		techniqueLock = lasso.ProcessFrame(frame, goHandList, goFingerList);
		//Debug.Log(techniqueLock);
		
		if(UpdateAnnotation())
		{
			pointCloud.Annotate(annotationTextInput.text);
			annotationTextInput.text = "";
		}
	}
}
