using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Leap;

public class LeapController : MonoBehaviour {
	bool initialized = false;

	SlicenSwipe slicenSwipe;
	VolumeSweep volumeSweep;
	Lasso lasso;
	
	enum technique { SLICENSWIPE=0, VOLUMESWEEP=1, LASSO=2, NONE=3, SIZE=4 };
	technique currentTechnique = technique.VOLUMESWEEP;  

	Leap.Controller controller;
	List<GameObject> goFingerList;
	List<GameObject> goHandList;
	AnnotationMenu annotationMenu;
	Transform cameraTransform;
	PointCloud pointCloud;
	GUIText annotationTextInput;

	float fingerAvg = 0.0F;

	bool techniqueMenuActive = false;
	technique techniqueQuadrant = technique.VOLUMESWEEP;

	// Use this for initialization
	void Start () {
		controller = new Leap.Controller();
		
		annotationTextInput = GameObject.Find("Annotation Input").GetComponent<GUIText>();
		annotationMenu = GameObject.Find("Camera").GetComponent<AnnotationMenu>();
		cameraTransform = GameObject.Find("Camera").GetComponent<Transform>();
		pointCloud = GameObject.Find("Camera").GetComponent<PointCloud>();

		goFingerList = new List<GameObject>();
		for(int i = 0; i < 10; i++)
		{
			goFingerList.Add(GameObject.CreatePrimitive(PrimitiveType.Sphere));
			goFingerList[i].renderer.material = Resources.Load("PhongUserLight", typeof(Material)) as Material;
			goFingerList[i].renderer.material.color = new Color(0.7F, 0.7F, 0.7F, 1.0F);
		}

		goHandList = new List<GameObject>();
		for(int i = 0; i < 2; i++)
		{
			goHandList.Add(GameObject.CreatePrimitive(PrimitiveType.Cube));
			goHandList[i].renderer.material = Resources.Load("PhongUserLight", typeof(Material)) as Material;
			goHandList[i].renderer.material.color = new Color(0.7F, 0.7F, 0.7F, 1.0F);
		}
	}

	public void init(int selectedTechnique, int selectedStrategy) {
		if (selectedTechnique == (int)technique.SLICENSWIPE) {
			slicenSwipe = new SlicenSwipe (selectedStrategy);
			currentTechnique = technique.SLICENSWIPE;
			techniqueQuadrant = technique.SLICENSWIPE;
		}
		else if (selectedTechnique == (int)technique.VOLUMESWEEP) {
			volumeSweep = new VolumeSweep(selectedStrategy);
			currentTechnique = technique.VOLUMESWEEP;
			techniqueQuadrant = technique.VOLUMESWEEP;
		}
		else if (selectedTechnique == (int)technique.LASSO) {
			lasso = new Lasso(selectedStrategy);
			currentTechnique = technique.LASSO;
			techniqueQuadrant = technique.LASSO;
		}
		else {
			return;
		}

		initialized = true;
	}

	bool UpdateAnnotation() {
		foreach (char c in Input.inputString) {
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
				if(annotationTextInput.text.Replace(" ","").Length > 0)
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
		if(!initialized)
			return;

		Frame frame = controller.Frame();
		HandList hl = frame.Hands;
		FingerList fl = frame.Fingers;
		GestureList gl = frame.Gestures(controller.Frame(1));

		for(int i = 0; i < gl.Count; i++) {
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
			Quaternion cameraRotation = cameraTransform.rotation;
			position = cameraRotation * position;
			//Debug.Log("["+Time.time+"] hand["+i+"]: "+position);
			goHandList[i].SetActive(true);
			goHandList[i].transform.position = position;
			goHandList[i].transform.localScale = new Vector3(pointCloud.bsRadius*0.05F,pointCloud.bsRadius*0.05F,pointCloud.bsRadius*0.05F);
			goHandList[i].renderer.enabled = false;
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
			Quaternion cameraRotation = cameraTransform.rotation;
			position = cameraRotation * position;
			//Debug.Log("["+Time.time+"] finger["+i+"]: "+position);
			goFingerList[i].SetActive(true);
			goFingerList[i].transform.position = position;
			goFingerList[i].transform.localScale = new Vector3(pointCloud.bsRadius*0.05F,pointCloud.bsRadius*0.05F,pointCloud.bsRadius*0.05F);
			goFingerList[i].renderer.enabled = false;
		}

		fingerAvg = fingerAvg * 0.9F + fl.Count * 0.1F;
		//Debug.Log(fingerAvg);
		
		// TRANSFORMING HAND POSITION TO MATCH POINT CLOUD SIZE
		Vector3 p = new Vector3();
		p.x =   ((hl[0].PalmPosition.x - frame.InteractionBox.Center.x) / frame.InteractionBox.Width ) * (pointCloud.Size().magnitude*(frame.InteractionBox.Width/maxd));
		p.y =   ((hl[0].PalmPosition.y - frame.InteractionBox.Center.y) / frame.InteractionBox.Height) * (pointCloud.Size().magnitude*(frame.InteractionBox.Height/maxd));
		
		bool[] techniqueLock = new bool[4]; // 3 techniques + none
		if(slicenSwipe != null) 
		{
			techniqueLock[0] = slicenSwipe.ProcessFrame(frame, goHandList, goFingerList);
		}
		else if(volumeSweep != null) 
		{
			techniqueLock[1] = volumeSweep.ProcessFrame(frame, goHandList, goFingerList);
		}
		else if(lasso != null) 
		{
			techniqueLock[2] = lasso.ProcessFrame(frame, goHandList, goFingerList);
		}
		//Debug.Log("current technique: "+currentTechnique);
		
		if(!techniqueLock[(int)currentTechnique])
		{
			if(UpdateAnnotation())
			{
				pointCloud.Annotate(annotationTextInput.text);
				annotationTextInput.text = "";
			}
		}
	}
	
	void OnGUI ()
	{
		if(!techniqueMenuActive)
		{
			string name;
			if(currentTechnique == technique.SLICENSWIPE)
				name = "Slice\'n\'Swipe";
			else if(currentTechnique == technique.VOLUMESWEEP)
				name = "Bubble";
			else if(currentTechnique == technique.LASSO)
				name = "Lasso";
	        else
		        name = "Free Mode";
			GUIStyle style = new GUIStyle(GUI.skin.box);
			style.fontSize = 20;
			style.fontStyle = FontStyle.Bold;

			GUI.Label (new Rect (UnityEngine.Screen.width-205, 5, 200, 30), name, style);
		}
		else
		{
			GUIStyle style = new GUIStyle(GUI.skin.box);
			style.fontSize = 40;
			style.fontStyle = FontStyle.Bold;
			style.alignment = TextAnchor.MiddleCenter;

			GUIStyle selectedStyle = new GUIStyle(GUI.skin.box);
			selectedStyle.fontSize = 40;
			selectedStyle.fontStyle = FontStyle.Bold;
			selectedStyle.alignment = TextAnchor.MiddleCenter;
			selectedStyle.normal.textColor = Color.black;
			selectedStyle.normal.background = new Texture2D(1, 1);
			selectedStyle.normal.background.SetPixel(1,1,Color.yellow);
			selectedStyle.normal.background.Apply();

			Color c = GUI.backgroundColor;

			GUI.backgroundColor = techniqueQuadrant == technique.SLICENSWIPE ? Color.yellow : c;
			GUI.Label (new Rect (0, 0, UnityEngine.Screen.width/2, UnityEngine.Screen.height/2), "Slice'n'Swipe", techniqueQuadrant == technique.SLICENSWIPE ? selectedStyle : style);
			GUI.backgroundColor = techniqueQuadrant == technique.VOLUMESWEEP ? Color.yellow : c;
			GUI.Label (new Rect (UnityEngine.Screen.width/2, 0, UnityEngine.Screen.width/2, UnityEngine.Screen.height/2), "Bubble", techniqueQuadrant == technique.VOLUMESWEEP ? selectedStyle : style);
			GUI.backgroundColor = techniqueQuadrant == technique.LASSO ? Color.yellow : c;
			GUI.Label (new Rect (0, UnityEngine.Screen.height/2, UnityEngine.Screen.width/2, UnityEngine.Screen.height/2), "Lasso", techniqueQuadrant == technique.LASSO ? selectedStyle : style);
			GUI.backgroundColor = techniqueQuadrant == technique.NONE ? Color.yellow : c;
			GUI.Label (new Rect (UnityEngine.Screen.width/2, UnityEngine.Screen.height/2, UnityEngine.Screen.width/2, UnityEngine.Screen.height/2), "Free Mode", techniqueQuadrant == technique.NONE ? selectedStyle : style);
			//GUI.backgroundColor = c;
		}
	}

	public void RenderTransparentObjects() {
		for(int i = 0; i < goHandList.Count; i++)
		{
			if(goHandList[i].activeSelf)
			{
				goHandList[i].renderer.enabled = true;
				goHandList[i].renderer.material.SetVector("_LightPosition",new Vector4(cameraTransform.position.x,cameraTransform.position.y,cameraTransform.position.z,1.0F));
				for (int pass = 0; pass < goHandList[i].renderer.material.passCount; pass++)
				{
					if(goHandList[i].renderer.material.SetPass(pass))
						Graphics.DrawMeshNow(goHandList[i].GetComponent<MeshFilter>().mesh,goHandList[i].transform.localToWorldMatrix);
				}
				goHandList[i].renderer.enabled = false;
			}
		}
		
		// update finger renderer
		for(int i = 0; i < goFingerList.Count; i++)
		{
			if(goFingerList[i].activeSelf)
			{
				goFingerList[i].renderer.enabled = true;
				goFingerList[i].renderer.material.SetVector("_LightPosition",new Vector4(cameraTransform.position.x,cameraTransform.position.y,cameraTransform.position.z,1.0F));
				for (int pass = 0; pass < goFingerList[i].renderer.material.passCount; pass++)
				{
					if(goFingerList[i].renderer.material.SetPass(pass))
						Graphics.DrawMeshNow(goFingerList[i].GetComponent<MeshFilter>().mesh,goFingerList[i].transform.localToWorldMatrix);
				}
				goFingerList[i].renderer.enabled = false;
			}
		}

		if(currentTechnique == technique.SLICENSWIPE && slicenSwipe != null)
			slicenSwipe.RenderTransparentObjects();
		else if(currentTechnique == technique.VOLUMESWEEP && volumeSweep != null)
			volumeSweep.RenderTransparentObjects();
		//else if(currentTechnique == technique.LASSO && lasso != null)
		//	lasso.RenderTransparentObjects();
	}
}
