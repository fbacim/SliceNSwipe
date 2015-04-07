using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Leap;

public class LeapController : MonoBehaviour {
	bool initialized = false;

	SlicenSwipe slicenSwipe;
	VolumeSweep volumeSweep;
	Lasso lasso;
	
	Technique currentTechnique = Technique.VOLUMESWEEP;  

	Leap.Controller controller;
	List<GameObject> goFingerList;
	List<GameObject> goHandList;
	AnnotationMenu annotationMenu;
	Transform cameraTransform;
	PointCloud pointCloud;
	GUIText annotationTextInput;

	Transform handTransform;

	GameObject targetPointsAlert;

	// Use this for initialization
	void Start () {
		controller = new Leap.Controller();
		
		annotationTextInput = GameObject.Find("Annotation Input").GetComponent<GUIText>();
		annotationMenu = GameObject.Find("Camera").GetComponent<AnnotationMenu>();
		cameraTransform = GameObject.Find("Camera").GetComponent<Transform>();
		pointCloud = GameObject.Find("Camera").GetComponent<PointCloud>();
		//handTransform = GameObject.Find("HandController").GetComponent<Transform>();
		targetPointsAlert = GameObject.Find("TargetPointsAlert");

		goFingerList = new List<GameObject>();
		for(int i = 0; i < 10; i++)
		{
			goFingerList.Add(GameObject.CreatePrimitive(PrimitiveType.Sphere));
			goFingerList[i].name = "Finger Tip "+i;
			goFingerList[i].GetComponent<Renderer>().material = Resources.Load("PhongUserLight", typeof(Material)) as Material;
			goFingerList[i].GetComponent<Renderer>().material.color = new Color(0.7F, 0.7F, 0.7F, 1.0F);
		}

		goHandList = new List<GameObject>();
		for(int i = 0; i < 2; i++)
		{
			goFingerList[i].name = "Palm "+i;
			goHandList.Add(GameObject.CreatePrimitive(PrimitiveType.Cube));
			goHandList[i].GetComponent<Renderer>().material = Resources.Load("PhongUserLight", typeof(Material)) as Material;
			goHandList[i].GetComponent<Renderer>().material.color = new Color(0.7F, 0.7F, 0.7F, 1.0F);
		}
	}

	public void init(Technique selectedTechnique, Strategy selectedStrategy) {
		if (selectedTechnique == Technique.SLICENSWIPE) {
			slicenSwipe = new SlicenSwipe (selectedStrategy);
			currentTechnique = Technique.SLICENSWIPE;
		}
		else if (selectedTechnique == Technique.VOLUMESWEEP) {
			volumeSweep = new VolumeSweep(selectedStrategy);
			currentTechnique = Technique.VOLUMESWEEP;
		}
		else if (selectedTechnique == Technique.LASSO) {
			lasso = new Lasso(selectedStrategy);
			currentTechnique = Technique.LASSO;
		}
		else {
			currentTechnique = Technique.NONE;
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
		if(!initialized || pointCloud.taskDone)
			return;

		Frame frame = controller.Frame();
		HandList hl = frame.Hands;
		FingerList fl = frame.Fingers;

		//Debug.Log("<"+frame.InteractionBox.Width+", "+frame.InteractionBox.Height+", "+frame.InteractionBox.Depth+">  "+frame.InteractionBox.Center); // +"   "+pointCloud.Size()
		float maxd = Mathf.Max(new float[]{frame.InteractionBox.Width, frame.InteractionBox.Height, frame.InteractionBox.Depth});

		// transform hand model
		Vector3 position = new Vector3();
		Quaternion cameraRotation = cameraTransform.rotation;
		/*position.x =   ((0.0F - frame.InteractionBox.Center.x) / frame.InteractionBox.Width ) * (pointCloud.Size().magnitude*(frame.InteractionBox.Width/maxd));
		position.y =   ((0.0F - frame.InteractionBox.Center.y) / frame.InteractionBox.Height) * (pointCloud.Size().magnitude*(frame.InteractionBox.Height/maxd));
		position.z = -(((0.0F - frame.InteractionBox.Center.z) / frame.InteractionBox.Depth ) * (pointCloud.Size().magnitude*(frame.InteractionBox.Depth/maxd)));
		// rotate position to match camera
		handTransform.position = cameraTransform.rotation * position;
		// change scale
		handTransform.localScale = new Vector3(maxd,maxd,maxd);*/

		// update hand renderer
		for(int i = 0; i < goHandList.Count; i++)
			goHandList[i].SetActive(false);

		for(int i = 0; (i < hl.Count && i < goHandList.Count); i++)
		{
			//if(hl[i].is)
			position = new Vector3();
			// TRANSFORMING FINGER POSITION TO MATCH POINT CLOUD SIZE
			// normalize position of fingers, then multiply by magnitude of point cloud size vector adjusted by ratio of interaction box sizes
			position.x =   ((hl[i].PalmPosition.x - frame.InteractionBox.Center.x) / frame.InteractionBox.Width ) * (pointCloud.Size().magnitude*(frame.InteractionBox.Width/maxd));
			position.y =   ((hl[i].PalmPosition.y - frame.InteractionBox.Center.y) / frame.InteractionBox.Height) * (pointCloud.Size().magnitude*(frame.InteractionBox.Height/maxd));
			position.z = -(((hl[i].PalmPosition.z - frame.InteractionBox.Center.z) / frame.InteractionBox.Depth ) * (pointCloud.Size().magnitude*(frame.InteractionBox.Depth/maxd)));
			// rotate position to match camera
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

			position = new Vector3();
			// TRANSFORMING FINGER POSITION TO MATCH POINT CLOUD SIZE
			// normalize position of fingers, then multiply by magnitude of point cloud size vector adjusted by ratio of interaction box sizes
			position.x =   ((f.TipPosition.x - frame.InteractionBox.Center.x) / frame.InteractionBox.Width ) * (pointCloud.Size().magnitude*(frame.InteractionBox.Width/maxd));
			position.y =   ((f.TipPosition.y - frame.InteractionBox.Center.y) / frame.InteractionBox.Height) * (pointCloud.Size().magnitude*(frame.InteractionBox.Height/maxd));
			position.z = -(((f.TipPosition.z - frame.InteractionBox.Center.z) / frame.InteractionBox.Depth ) * (pointCloud.Size().magnitude*(frame.InteractionBox.Depth/maxd)));
			// rotate position to match camera
			position = cameraRotation * position;
			//Debug.Log("["+Time.time+"] finger["+i+"]: "+position);
			goFingerList[(int)f.Type()+5*System.Convert.ToInt32(f.Hand.IsRight)].SetActive(true);
			goFingerList[(int)f.Type()+5*System.Convert.ToInt32(f.Hand.IsRight)].transform.position = position;
			goFingerList[(int)f.Type()+5*System.Convert.ToInt32(f.Hand.IsRight)].transform.localScale = new Vector3(pointCloud.bsRadius*0.05F,pointCloud.bsRadius*0.05F,pointCloud.bsRadius*0.05F);
			goFingerList[(int)f.Type()+5*System.Convert.ToInt32(f.Hand.IsRight)].GetComponent<Renderer>().enabled = false;
			//Debug.Log("finger["+i+";"+((int)f.Type()+5*System.Convert.ToInt32(f.Hand.IsRight))+"]: "+f.Type()+"  -> "+f.IsValid+"  -> "+f.IsExtended+"  -> "+f.IsFinger+"  -> "+f.IsTool+"  -> "+f.TimeVisible);
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
				pointCloud.Annotate(annotationTextInput.text,true);
				annotationTextInput.text = "";
			}
		}
	}
	
	void OnGUI ()
	{
		// update gauges based on task completion
		if(pointCloud.hitPercent >= pointCloud.minHitPercent)
		{
			float value = pointCloud.hitPercent;
			float offset = pointCloud.minHitPercent;
			GameObject.Find("GoodRedProgressBarFill").GetComponent<ProgressBar>().scale = 1.0f;
			//GameObject.Find("GoodYellowProgressBarFill").GetComponent<ProgressBar>().scale = 1.0f;
			GameObject.Find("GoodGreenProgressBarFill").GetComponent<ProgressBar>().scale = Mathf.Clamp((value-offset)/(1.0f-offset),0.0f,1.0f);
			targetPointsAlert.SetActive(false);
		}
//		else if(pointCloud.hitPercent >= (pointCloud.minHitPercent-(1.0f-pointCloud.minHitPercent)))
//		{
//			float value = pointCloud.hitPercent;
//			float offset = (pointCloud.minHitPercent-(1.0f-pointCloud.minHitPercent));
//			GameObject.Find("GoodRedProgressBarFill").GetComponent<ProgressBar>().scale = 1.0f;
//			GameObject.Find("GoodYellowProgressBarFill").GetComponent<ProgressBar>().scale = Mathf.Clamp((value-offset)/(1.0f-offset),0.0f,1.0f);
//			GameObject.Find("GoodGreenProgressBarFill").GetComponent<ProgressBar>().scale = 0.0f;
//		}
		else
		{
			float value = pointCloud.hitPercent;
			float offset = pointCloud.minHitPercent;//(pointCloud.minHitPercent-(1.0f-pointCloud.minHitPercent));
			GameObject.Find("GoodRedProgressBarFill").GetComponent<ProgressBar>().scale = Mathf.Clamp(value/offset,0.0f,1.0f);
			//GameObject.Find("GoodYellowProgressBarFill").GetComponent<ProgressBar>().scale = 0.0f;
			GameObject.Find("GoodGreenProgressBarFill").GetComponent<ProgressBar>().scale = 0.0f;
			targetPointsAlert.SetActive(true);
		}

		float yellowBarThresholdScale = 10.0f;
		if(pointCloud.falseHitPercent <= pointCloud.maxFalseHitPercent)
		{
			float value = pointCloud.falseHitPercent;
			float offset = pointCloud.maxFalseHitPercent;
			GameObject.Find("BadRedProgressBarFill").GetComponent<ProgressBar>().scale = 0.0f;
			GameObject.Find("BadYellowProgressBarFill").GetComponent<ProgressBar>().scale = 0.0f;
			GameObject.Find("BadGreenProgressBarFill").GetComponent<ProgressBar>().scale = 1.0f - Mathf.Clamp((offset-value)/offset,0.0f,1.0f);
		}
		else if(pointCloud.falseHitPercent <= pointCloud.maxFalseHitPercent*yellowBarThresholdScale)
		{
			float value = pointCloud.falseHitPercent;
			float offset = pointCloud.maxFalseHitPercent*yellowBarThresholdScale;
			GameObject.Find("BadRedProgressBarFill").GetComponent<ProgressBar>().scale = 0.0f;
			GameObject.Find("BadYellowProgressBarFill").GetComponent<ProgressBar>().scale = 1.0f-Mathf.Clamp((offset-value)/offset,0.0f,1.0f);
			GameObject.Find("BadGreenProgressBarFill").GetComponent<ProgressBar>().scale = 1.0f;
		}
		else
		{
			float value = pointCloud.falseHitPercent*(float)pointCloud.highlightedCount;
			float offset = pointCloud.maxFalseHitPercent;
			float total = (float)pointCloud.vertexCount-(float)pointCloud.highlightedCount;
			GameObject.Find("BadRedProgressBarFill").GetComponent<ProgressBar>().scale = 1.0f-Mathf.Clamp(1.0f-((value-offset*yellowBarThresholdScale)/(total-offset*yellowBarThresholdScale)),0.0f,1.0f);
			GameObject.Find("BadYellowProgressBarFill").GetComponent<ProgressBar>().scale = 1.0f;
			GameObject.Find("BadGreenProgressBarFill").GetComponent<ProgressBar>().scale = 1.0f;
		}


		
		// display task timer
		GUIStyle style = new GUIStyle(GUI.skin.box);
		style.border.left = style.border.right = style.border.top = style.border.bottom = 3;
		style.fontSize = 20;
		style.fontStyle = FontStyle.Bold;
		if(pointCloud.timeLeft > 60)
			style.normal.textColor = Color.green;
		else if(pointCloud.timeLeft > 20)
			style.normal.textColor = Color.yellow;
		else
			style.normal.textColor = Color.red;
		GUI.Label (new Rect (UnityEngine.Screen.width-75, UnityEngine.Screen.height-35, 70, 30), ""+pointCloud.timeLeft+"s", style);
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

		if(currentTechnique == Technique.SLICENSWIPE && slicenSwipe != null)
			slicenSwipe.RenderTransparentObjects();
		else if(currentTechnique == Technique.VOLUMESWEEP && volumeSweep != null)
			volumeSweep.RenderTransparentObjects();
		//else if(currentTechnique == technique.LASSO && lasso != null)
		//	lasso.RenderTransparentObjects();
	}
}
