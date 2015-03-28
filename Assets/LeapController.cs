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
			goFingerList[i].GetComponent<Renderer>().material = Resources.Load("PhongUserLight", typeof(Material)) as Material;
			goFingerList[i].GetComponent<Renderer>().material.color = new Color(0.7F, 0.7F, 0.7F, 1.0F);
		}

		goHandList = new List<GameObject>();
		for(int i = 0; i < 2; i++)
		{
			goHandList.Add(GameObject.CreatePrimitive(PrimitiveType.Cube));
			goHandList[i].GetComponent<Renderer>().material = Resources.Load("PhongUserLight", typeof(Material)) as Material;
			goHandList[i].GetComponent<Renderer>().material.color = new Color(0.7F, 0.7F, 0.7F, 1.0F);
		}
	}

	public void init(int selectedTechnique, int selectedStrategy) {
		if (selectedTechnique == (int)technique.SLICENSWIPE) {
			slicenSwipe = new SlicenSwipe (selectedStrategy);
			currentTechnique = technique.SLICENSWIPE;
		}
		else if (selectedTechnique == (int)technique.VOLUMESWEEP) {
			volumeSweep = new VolumeSweep(selectedStrategy);
			currentTechnique = technique.VOLUMESWEEP;
		}
		else if (selectedTechnique == (int)technique.LASSO) {
			lasso = new Lasso(selectedStrategy);
			currentTechnique = technique.LASSO;
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

		//Debug.Log("<"+frame.InteractionBox.Width+", "+frame.InteractionBox.Height+", "+frame.InteractionBox.Depth+">  "+frame.InteractionBox.Center); // +"   "+pointCloud.Size()
		float maxd = Mathf.Max(new float[]{frame.InteractionBox.Width, frame.InteractionBox.Height, frame.InteractionBox.Depth});

		// update hand renderer
		for(int i = 0; i < goHandList.Count; i++)
			goHandList[i].SetActive(false);

		for(int i = 0; (i < hl.Count && i < goHandList.Count); i++)
		{
			//if(hl[i].is)
			Vector3 position = new Vector3();
			// TRANSFORMING FINGER POSITION TO MATCH POINT CLOUD SIZE
			// normalize position of fingers, then multiply by magnitude of point cloud size vector adjusted by ratio of interaction box sizes
			position.x =   ((hl[i].PalmPosition.x - frame.InteractionBox.Center.x) / frame.InteractionBox.Width ) * (pointCloud.Size().magnitude*(frame.InteractionBox.Width/maxd));
			position.y =   ((hl[i].PalmPosition.y - frame.InteractionBox.Center.y) / frame.InteractionBox.Height) * (pointCloud.Size().magnitude*(frame.InteractionBox.Height/maxd));
			position.z = -(((hl[i].PalmPosition.z - frame.InteractionBox.Center.z) / frame.InteractionBox.Depth ) * (pointCloud.Size().magnitude*(frame.InteractionBox.Depth/maxd)));
			// rotate position to match camera
			Quaternion cameraRotation = cameraTransform.rotation;
			position = cameraRotation * position;
			//Debug.Log("["+Time.time+"] hand["+i+"]: "+position);
			goHandList[System.Convert.ToInt32(hl[i].IsRight)].SetActive(true);
			goHandList[System.Convert.ToInt32(hl[i].IsRight)].transform.position = position;
			goHandList[System.Convert.ToInt32(hl[i].IsRight)].transform.localScale = new Vector3(pointCloud.bsRadius*0.05F,pointCloud.bsRadius*0.05F,pointCloud.bsRadius*0.05F);
			goHandList[System.Convert.ToInt32(hl[i].IsRight)].GetComponent<Renderer>().enabled = false;
		}

		// update finger renderer
		for(int i = 0; i < goFingerList.Count; i++)
			goFingerList[i].SetActive(false);

		for(int i = 0; (i < fl.Count && i < goFingerList.Count); i++)
		{
			Finger f = fl[i];

			Vector3 position = new Vector3();
			// TRANSFORMING FINGER POSITION TO MATCH POINT CLOUD SIZE
			// normalize position of fingers, then multiply by magnitude of point cloud size vector adjusted by ratio of interaction box sizes
			position.x =   ((f.TipPosition.x - frame.InteractionBox.Center.x) / frame.InteractionBox.Width ) * (pointCloud.Size().magnitude*(frame.InteractionBox.Width/maxd));
			position.y =   ((f.TipPosition.y - frame.InteractionBox.Center.y) / frame.InteractionBox.Height) * (pointCloud.Size().magnitude*(frame.InteractionBox.Height/maxd));
			position.z = -(((f.TipPosition.z - frame.InteractionBox.Center.z) / frame.InteractionBox.Depth ) * (pointCloud.Size().magnitude*(frame.InteractionBox.Depth/maxd)));
			// rotate position to match camera
			Quaternion cameraRotation = cameraTransform.rotation;
			position = cameraRotation * position;
			//Debug.Log("["+Time.time+"] finger["+i+"]: "+position);
			goFingerList[(int)f.Type()+5*System.Convert.ToInt32(f.Hand.IsRight)].SetActive(true);
			goFingerList[(int)f.Type()+5*System.Convert.ToInt32(f.Hand.IsRight)].transform.position = position;
			goFingerList[(int)f.Type()+5*System.Convert.ToInt32(f.Hand.IsRight)].transform.localScale = new Vector3(pointCloud.bsRadius*0.05F,pointCloud.bsRadius*0.05F,pointCloud.bsRadius*0.05F);
			goFingerList[(int)f.Type()+5*System.Convert.ToInt32(f.Hand.IsRight)].GetComponent<Renderer>().enabled = false;

			Debug.Log("finger["+i+";"+((int)f.Type()+5*System.Convert.ToInt32(f.Hand.IsRight))+"]: "+f.Type()+"  -> "+f.IsValid+"  -> "+f.IsExtended+"  -> "+f.IsFinger+"  -> "+f.IsTool+"  -> "+f.TimeVisible);
		}
		
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
		GUIStyle style = new GUIStyle(GUI.skin.box);
		style.fontSize = 20;
		style.fontStyle = FontStyle.Bold;
		if(pointCloud.hitPercent >= 1.0F)
			style.normal.textColor = Color.green;
		else if(pointCloud.hitPercent >= pointCloud.minHitPercent)
			style.normal.textColor = Color.yellow;
		else
			style.normal.textColor = Color.red;
		GUI.Label (new Rect (UnityEngine.Screen.width-205, 5, 200, 30), "Selection hits", style);

		style = new GUIStyle(GUI.skin.box);
		style.fontSize = 20;
		style.fontStyle = FontStyle.Bold;
		if(pointCloud.falseHitPercent <= pointCloud.maxFalseHitPercent/2.0F)
			style.normal.textColor = Color.green;
		else if(pointCloud.falseHitPercent <= pointCloud.maxFalseHitPercent)
			style.normal.textColor = Color.yellow;
		else
			style.normal.textColor = Color.red;
		GUI.Label (new Rect (UnityEngine.Screen.width-205, 35, 200, 30), "Extra points", style);
	}

	public void RenderTransparentObjects() {
		for(int i = 0; i < goHandList.Count; i++)
		{
			if(goHandList[i].activeSelf)
			{
				goHandList[i].GetComponent<Renderer>().enabled = true;
				goHandList[i].GetComponent<Renderer>().material.SetVector("_LightPosition",new Vector4(cameraTransform.position.x,cameraTransform.position.y,cameraTransform.position.z,1.0F));
				for (int pass = 0; pass < goHandList[i].GetComponent<Renderer>().material.passCount; pass++)
				{
					if(goHandList[i].GetComponent<Renderer>().material.SetPass(pass))
						Graphics.DrawMeshNow(goHandList[i].GetComponent<MeshFilter>().mesh,goHandList[i].transform.localToWorldMatrix);
				}
				goHandList[i].GetComponent<Renderer>().enabled = false;
			}
		}
		
		// update finger renderer
		for(int i = 0; i < goFingerList.Count; i++)
		{
			if(goFingerList[i].activeSelf)
			{
				goFingerList[i].GetComponent<Renderer>().enabled = true;
				goFingerList[i].GetComponent<Renderer>().material.SetVector("_LightPosition",new Vector4(cameraTransform.position.x,cameraTransform.position.y,cameraTransform.position.z,1.0F));
				for (int pass = 0; pass < goFingerList[i].GetComponent<Renderer>().material.passCount; pass++)
				{
					if(goFingerList[i].GetComponent<Renderer>().material.SetPass(pass))
						Graphics.DrawMeshNow(goFingerList[i].GetComponent<MeshFilter>().mesh,goFingerList[i].transform.localToWorldMatrix);
				}
				goFingerList[i].GetComponent<Renderer>().enabled = false;
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
