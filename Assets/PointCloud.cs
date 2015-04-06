﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;

public class PointCloud : MonoBehaviour 
{
	bool initialized = false;

	public Material material;
	public int vertexCount; // this should match the amount of points from file
	private int instanceCount = 2; // no need to adjust (otherwise you have instanceCount * vertexCount amount of objects..
	
	private ComputeBuffer bufferPoints;
	private ComputeBuffer bufferPos;
	private ComputeBuffer bufferColors;
	private ComputeBuffer bufferColorOffset;
	private ComputeBuffer bufferNormals;
	private ComputeBuffer bufferSizes;
	private ComputeBuffer bufferSelected;
	private ComputeBuffer bufferHighlighted;

	private Vector4[] pos;
	public Vector3[] verts;
	private Vector3[] originalVerts;
	private Vector4[] colors;
	private Vector4[] originalColors;
	private Vector3[] colorsOffset;
	private Vector3[] normals;
	private float[] sizes;
	private int[] selected;
	private List< List<int> > selectedHistory;
	private int[] highlighted;
	private int[] notHighlighted;
	public int highlightedCount;
	public float hitPercent;
	public float falseHitPercent;
	public float minHitPercent = 0.95F;
	public float maxFalseHitPercent = 0.05F;
	private List< List<string> > cloudAnnotations;
	public List<string> annotations;
	private List<GameObject> goAnnotations;
	public float selectedAlpha = 0.7F;
	public float deselectedAlpha = 0.3F;
	public float selectedSize = 7.0F;
	public float deselectedSize = 1.0F;
	public float currentSelectedSize;
	public float currentDeselectedSize;
	public bool useSeparation = true;
	public bool resetAfterAnnotation = true;

	// store the subset of the PC that corresponds to an annotation, given the index of the point in the file
	private Dictionary<string, List<int>> annotationsPerVertex;

	public bool separate  = false;  // are the point cloud instances separate?
	public bool animating = false;  // is it currently animating?

	private List<GUIText> goAnnotation;

	private Vector3 min, max, size, oMin, oMax, oSize;
	public float bsRadius; // bounding sphere radius, always with center on 0,0,0
	private float idealDistance;
	private float initialBSRadius = 0.0f;
	
	public float animationTotalTime = 0.25F;
	private float animationStartTime;
	private float separationDistance;
	private int separationMode = 0;
	
	private Vector3 originalCenter = new Vector3();
	private Vector3 currentCenterOffset = new Vector3();

	private bool hasNormals = false;
	private bool rgbUnitTransform = false;
	private bool colored = false;

	private string pointCloudFile;

	private string modelName;
	private string taskName;

	public Strategy currentStrategy;

	public int steps = 0;
	public int mistakes = 0;
	public int cancels = 0;
	public DateTime startTime;
	public float maxTime = 300.0f;
	public float timeLeft = 0;
	public bool training = true;
	public bool taskDone = false;

	private Thread lassoThread;
	private List<Vector2> tPoints2D;
	private Vector2[] tLasso;
	private int tLassoSize;
	private bool tValidLasso;
	private List<Vector3> tLassoVertices;
	private Camera tCamera;
	private Matrix4x4 tProjMatrix;
	private Matrix4x4 tViewMatrix;

	GameObject taskCompletedAlert;
	GameObject timeElapsedAlert;

	private void preProcessFile(string fileName, ref Vector3 centerOffset, ref float scale) 
	{
		int count = 0; 
		
		// initialize min/max
		min = new Vector3(float.MaxValue,float.MaxValue,float.MaxValue);
		max = new Vector3(float.MinValue,float.MinValue,float.MinValue);

		using (StreamReader reader = new StreamReader(fileName))
		{
			string line = reader.ReadLine(); // read header first
			hasNormals = line.Split(',').Length == 9 ? true : false;
			while (!reader.EndOfStream)
			{
				line = reader.ReadLine();
				string[] values = line.Split(',');
				Vector3 tmp = new Vector3(float.Parse(values[0]),float.Parse(values[1]),float.Parse(values[2]));

				// calculate min/max of all vertices
				if(tmp.x < min.x)
					min.x = tmp.x;
				if(tmp.y < min.y)
					min.y = tmp.y;
				if(tmp.z < min.z)
					min.z = tmp.z;
				
				if(tmp.x > max.x)
					max.x = tmp.x;
				if(tmp.y > max.y)
					max.y = tmp.y;
				if(tmp.z > max.z)
					max.z = tmp.z;

				if(float.Parse(values[3]) > 1.0 || float.Parse(values[4]) > 1.0 || float.Parse(values[5]) > 1.0)
					rgbUnitTransform = true;
				if(float.Parse(values[3]) != float.Parse(values[4]) || float.Parse(values[3]) != float.Parse(values[5]) || float.Parse(values[4]) != float.Parse(values[5]))
					colored = true;

				count++;
			}
		}
		size = max-min;
		scale = 100.0F/size.magnitude;
		centerOffset = min+(size/2.0F);
		vertexCount = count;
	}

	void Start () 
	{

	}

	// Use this for initialization
	public void init (string fileName, string annotationFileName=null) 
	{
		pointCloudFile = Application.dataPath+"/PointClouds/WithNormals/"+fileName+".csv";

		modelName = fileName;
		currentStrategy = GameObject.Find ("Experiment Menu").GetComponent<ExperimentMenu>().selectedStrategy;

		taskCompletedAlert = GameObject.Find("TaskCompletedAlert");
		taskCompletedAlert.SetActive(false);
		timeElapsedAlert = GameObject.Find("TimeElapsedAlert");
		timeElapsedAlert.SetActive(false);

		Debug.Log (pointCloudFile);

		goAnnotation = new List<GUIText>();
		
		// calculate center offset (to make sure it's at 0,0,0) and scale to make sure it's possible to interact with it
		Vector3 centerOffset = new Vector3();
		float scale = 1.0F;
		preProcessFile(pointCloudFile, ref centerOffset, ref scale);

		Debug.Log("vertexCount: "+vertexCount);
		Debug.Log("centerOffset: "+centerOffset);
		Debug.Log("scale: "+scale);
		Debug.Log("min: "+min);
		Debug.Log("max: "+max);
		Debug.Log("size: "+size);
	
		// vertex, color, selected, annotations arrays
		verts = new Vector3[vertexCount];
		originalVerts = new Vector3[vertexCount];
		colors = new Vector4[vertexCount];
		originalColors = new Vector4[vertexCount];
		normals = new Vector3[vertexCount];
		sizes = new float[vertexCount];
		selected = new int[vertexCount];
		selectedHistory = new List< List<int> >();
		highlighted = new int[vertexCount];
		notHighlighted = new int[vertexCount];
		colorsOffset = new Vector3[vertexCount];
		cloudAnnotations = new List< List<string> >();
		annotations = new List<string>();
		goAnnotations = new List<GameObject>();
		currentSelectedSize = selectedSize;
		currentDeselectedSize = deselectedSize;

		// List of indexes of the vertices in a subset of the PC that correspond to an annotation given its string identifier
		annotationsPerVertex = new Dictionary<string, List<int>> ();

		// normalized offset of each instance of the point cloud
		pos = new Vector4[instanceCount];
		pos[0] = new Vector4(0.0f,0,0,0);
		pos[1] = new Vector4(0.0f,0,0,0);

		FileStream fileStream = File.OpenRead(pointCloudFile);
		if (fileStream == null)
			return;

		StreamReader reader = new StreamReader(fileStream);

        reader.ReadLine(); // ignore first line
        Vector3 center = new Vector3(0,0,0); // calculate center of the object
		int lineCount = 0; // counter for the line number
		while (!reader.EndOfStream)
        {
            string line = reader.ReadLine();
            string[] values = line.Split(',');

			// populate arrays
			//points first
			verts[lineCount] = new Vector3((float.Parse(values[0])-centerOffset.x)*scale,
			                               (float.Parse(values[1])-centerOffset.y)*scale,
			                               (float.Parse(values[2])-centerOffset.z)*scale);
			originalVerts[lineCount] = new Vector3(verts[lineCount].x,
			                                       verts[lineCount].y,
			                                       verts[lineCount].z);

			//then colors
			if(colored)
			{
				colors[lineCount] = new Vector4(float.Parse(values[3]) * (rgbUnitTransform ? 0.003921568627451F : 1F),
				                                float.Parse(values[4]) * (rgbUnitTransform ? 0.003921568627451F : 1F),
				                                float.Parse(values[5]) * (rgbUnitTransform ? 0.003921568627451F : 1F),
				                                selectedAlpha);
			}
			else
			{
				float t = (float.Parse(values[2])-min.z)/size.z;
				HSBColor rainbowInterpolation = t <= 0.5 ? //HSBColor.Lerp(new HSBColor(new Color(1.0f, 1.0f, 1.0f, 1.0f)), new HSBColor(new Color(0.0f, 0.0f, 1.0f, 1.0f)), t);//
						HSBColor.Lerp(new HSBColor(new Color(1.0f, 0.0f, 0.0f, 1.0f)), new HSBColor(new Color(0.0f, 1.0f, 0.0f, 1.0f)), t * 2.0f) : 
						HSBColor.Lerp(new HSBColor(new Color(0.0f, 1.0f, 0.0f, 1.0f)), new HSBColor(new Color(0.0f, 0.0f, 1.0f, 1.0f)), (t-0.5f) * 2.0f);
				Color rainbowColor = rainbowInterpolation.ToColor();
				colors[lineCount] = new Vector4(rainbowColor.r,
				                                rainbowColor.b,
				                                rainbowColor.g,
				                                selectedAlpha);
			}
			originalColors[lineCount] = new Vector4(colors[lineCount].x,
			                                        colors[lineCount].y,
			                                        colors[lineCount].z,
			                                        colors[lineCount].w);

			//normals
			if(hasNormals)
			{
				normals[lineCount] = new Vector3(float.Parse(values[6]),
				                                 float.Parse(values[7]),
				                                 float.Parse(values[8]));
			}
			else
			{
				normals[lineCount] = new Vector3(0,0,0);
			}

			colorsOffset[lineCount] = new Vector3(0.0F,0.0F,0.0F);
			sizes[lineCount] = currentSelectedSize;
			selected[lineCount] = 1;
			notHighlighted[lineCount] = 0;
			highlighted[lineCount] = 2;
			cloudAnnotations.Add(new List<string>());

			center = center + verts[lineCount];

			lineCount++;
        }
		center = center / vertexCount;
		originalCenter = center;

	
		if (annotationFileName!=null) {
			taskName = annotationFileName.Split(new char[]{'_'})[1];
			annotationFileName = Application.dataPath+@"/PointClouds/WithNormals/Tasks/"+annotationFileName;
			

			System.IO.StreamReader loadAnnotationFile = new System.IO.StreamReader(annotationFileName);
			loadAnnotationFile.ReadLine();		// Rewrite the first argument as the first line in the Annotation file will have the tag
			string[] indexesInAnnotation = loadAnnotationFile.ReadLine().Split(',');
			loadAnnotationFile.Close();
			
			foreach( string index in indexesInAnnotation ){
				int num;
				if (int.TryParse(index, out num))
				{
					highlighted[num] = 1;
					highlightedCount++;
				}
			}
		}
		else if (GameObject.Find("StartUpOptions").GetComponent<StartUpOptions>().loadAnnotations)
		{
			int annotationCount = 0;
			foreach(string annotationFilename in GameObject.Find("StartUpOptions").GetComponent<StartUpOptions>().annotationNameList)
			{
				string annotationFullPath = Application.dataPath+@"/PointClouds/WithNormals/"+annotationFilename;

				if(annotationFullPath.Contains(fileName) && annotationCount++ == GameObject.Find("StartUpOptions").GetComponent<StartUpOptions>().selectedAnnotation)
				{
					System.IO.StreamReader loadAnnotationFile = new System.IO.StreamReader(annotationFullPath);
					string annotation = loadAnnotationFile.ReadLine();		// Rewrite the first argument as the first line in the Annotation file will have the tag
					string[] indexesInAnnotation = loadAnnotationFile.ReadLine().Split(',');
					loadAnnotationFile.Close();

					taskName = annotationFilename.Split(new char[]{'_'})[1];

					foreach( string index in indexesInAnnotation ){
						int num;
						if (int.TryParse(index, out num))
						{
							highlighted[num] = 1;
							highlightedCount++;
						}
					}

					break;
				}
			}
		}

		ReleaseBuffers ();
		
		bufferPoints = new ComputeBuffer (vertexCount, 12);
		bufferPoints.SetData (verts);
		material.SetBuffer ("buf_Points", bufferPoints);
		
		bufferColors = new ComputeBuffer (vertexCount, 16);
		bufferColors.SetData (colors);
		material.SetBuffer ("buf_Colors", bufferColors);
		
		bufferColorOffset = new ComputeBuffer (vertexCount, 12);
		bufferColorOffset.SetData (colorsOffset);
		material.SetBuffer ("buf_ColorsOffset", bufferColorOffset);
		
		bufferNormals = new ComputeBuffer (vertexCount, 12);
		bufferNormals.SetData (normals);
		material.SetBuffer ("buf_Normals", bufferNormals);
		
		bufferPos = new ComputeBuffer (instanceCount, 16);
		bufferPos.SetData (pos);
		material.SetBuffer ("buf_Positions", bufferPos);
		
		bufferSizes = new ComputeBuffer (vertexCount, 4);
		bufferSizes.SetData (sizes);
		material.SetBuffer ("buf_Sizes", bufferSizes);

		bufferSelected = new ComputeBuffer (vertexCount, 4);
		bufferSelected.SetData (selected);
		material.SetBuffer ("buf_Selected", bufferSelected);
		
		bufferHighlighted = new ComputeBuffer (vertexCount, 4);
		bufferHighlighted.SetData (notHighlighted);
		material.SetBuffer ("buf_Highlighted", bufferHighlighted);
		
		CenterPointCloud(center);

		currentCenterOffset = new Vector3();

		// save original min/max values
		oMin = new Vector3(min.x, min.y, min.z);
		oMax = new Vector3(max.x, max.y, max.z);
		oSize = new Vector3(size.x, size.y, size.z);

		Debug.Log("min: "+min);
		Debug.Log("max: "+max);
		Debug.Log("size: "+size);

		initialized = true;
		startTime = DateTime.Now ;
		/*if (GameObject.Find("StartUpOptions").GetComponent<StartUpOptions>().loadAnnotations){
			foreach(string annotationFilename in GameObject.Find("StartUpOptions").GetComponent<StartUpOptions>().annotationNameList){
				string annotationFullPath = Application.dataPath+@"/PointClouds/WithNormals/"+annotationFilename;
				if(annotationFullPath.Contains(fileName))
					Annotate(annotationFilename,annotationFullPath);
			}
		}*/
	}
	
	private void ReleaseBuffers() 
	{
		if (bufferPoints != null) bufferPoints.Release();
		bufferPoints = null;
		if (bufferColors != null) bufferColors.Release();
		bufferColors = null;
		if (bufferColorOffset != null) bufferColorOffset.Release();
		bufferColorOffset = null;
		if (bufferNormals != null) bufferNormals.Release();
		bufferNormals = null;
		if (bufferPos != null) bufferPos.Release();
		bufferPos = null;
		if (bufferSizes != null) bufferSizes.Release();
		bufferSizes = null;
		if (bufferSelected != null) bufferSelected.Release();
		bufferSelected = null;
		if (bufferHighlighted != null) bufferHighlighted.Release();
		bufferHighlighted = null;
	}
	
	void OnDisable() 
	{
		ReleaseBuffers();
	}

	// mode 0 cancel 1 left 2 right
	public void TriggerSeparation(bool state, int mode) 
	{
		// animation
		// cut event, for now using slash but should be something else
		if(useSeparation && state != separate && !animating)
		{
			animating = true;
			animationStartTime = Time.timeSinceLevelLoad;
			separationMode = mode;

			GameObject.Find("Camera").GetComponent<ViewPoint3DMouse>().CenterView();

			if(!state && mode > 0)
			{
				steps++;
				Annotate(""+(steps+cancels+mistakes), false);
			}
			else if(!state && mode == 0)
			{
				cancels++;
				Annotate(""+(steps+cancels+mistakes), false);
			}
		}
	}

	// Update is called once per frame
	void Update () 
	{
		if (!initialized)
			return;

		float currentTime = Time.timeSinceLevelLoad;
		// animation
		// cut event, for now using slash but should be something else
		if(!animating)
		{
			// use the bounding sphere radius as separation distance
			separationDistance = bsRadius;
			//separationDistance = Mathf.Max(new float[] {oSize.x, oSize.y, oSize.z})/3.0f;
			if(separate)
				pos[1].y = 1;
			else
				pos[1].y = 0;
		}
		else
		{
			// get current step/time in the animation
			float t = (currentTime-animationStartTime)/animationTotalTime; 
			
			// is it done?
			if(t >= 1.0f)
			{
				animating = false;
				t = 1.0f;
			}
			
			// if we're separating things, make sure cosine starts in 0
			if(!separate)
				t += 1.0f;
			
			// multiply it to make it go from 0 to 1
			t *= Mathf.PI;
			
			// x,y offset, z scale
			if(separationMode == 1 && t < Mathf.PI) // get rid of left
			{
				//print("left");
				pos[0] = new Vector4(-separationDistance*6.0F+((Mathf.Cos(t)+1.0f)*0.5f)*separationDistance*5.0F,0,0,-(Mathf.Cos(t)+1.0f)*0.5f); // first instance goes to left
				pos[1] = new Vector4( ((Mathf.Cos(t)+1.0f)*0.5f)*separationDistance,0,0,(Mathf.Cos(t)+1.0f)*0.5f); // second goes to right
			}
			else if(separationMode == 2 && t < Mathf.PI) // get rid of right
			{
				//print("right");
				pos[0] = new Vector4(-((Mathf.Cos(t)+1.0f)*0.5f)*separationDistance,0,0,-(Mathf.Cos(t)+1.0f)*0.5f); // first instance goes to left
				pos[1] = new Vector4( separationDistance*6.0F-((Mathf.Cos(t)+1.0f)*0.5f)*separationDistance*5.0F,0,0,(Mathf.Cos(t)+1.0f)*0.5f); // second goes to right
			}
			else // put them back together
			{
				pos[0] = new Vector4(-((Mathf.Cos(t)+1.0f)*0.5f)*separationDistance,0,0,-(Mathf.Cos(t)+1.0f)*0.5f); // first instance goes to left
				pos[1] = new Vector4( ((Mathf.Cos(t)+1.0f)*0.5f)*separationDistance,0,0,(Mathf.Cos(t)+1.0f)*0.5f); // second goes to right
			}
			
			// if it's done animating, invert variable that tells if point cloud is separated already
			if(!animating)
				separate = !separate;
		}
		
		bufferPos.SetData (pos);

		if (Input.GetKeyDown (KeyCode.Tab)) 
		{
			bufferHighlighted.SetData (highlighted);
		}
		else if (Input.GetKeyUp (KeyCode.Tab)) 
		{
			bufferHighlighted.SetData (notHighlighted);
		}

		UpdateAnnotations();

		checkTaskCompletion();
		
		DateTime endTime = DateTime.Now;
		TimeSpan ts = endTime - startTime;
		timeLeft = (maxTime-(int)ts.TotalSeconds);
		
		if(timeLeft < 0.0f && !taskDone && !training)
		{
			taskDone = true;
			Annotate("time_elapsed", true);
			startTime = DateTime.Now;
			timeElapsedAlert.SetActive(true);
		}
		else if(hitPercent >= minHitPercent && falseHitPercent <= maxFalseHitPercent && !taskDone)
		{
			taskDone = true;
			Annotate("task_completed", true);
			startTime = DateTime.Now;
			taskCompletedAlert.SetActive(true);
		}

		// wait 5 seconds before quitting after task is finished
		if(taskDone && !training && (DateTime.Now - startTime).TotalSeconds > 5.0f)
		{
			if (Application.isEditor)
			{
				Debug.Log("Cannot quit the application (Application is editor).");
			}
			else
			{
				System.Diagnostics.Process.GetCurrentProcess().Kill();
			}
		}
	}


	void OnRenderObject() 
	{
		if (!initialized)
			return;

		material.SetFloat("_SelectedSize",currentSelectedSize);
		material.SetFloat("_DeselectedSize",currentDeselectedSize);

		material.SetPass(0);
		Graphics.DrawProcedural(MeshTopology.Points, vertexCount, instanceCount);

		GameObject.Find("Leap").GetComponent<LeapController>().RenderTransparentObjects();
	}

	void UpdateAnnotations()
	{
		for(int i = 0; i < goAnnotations.Count; i++)
		{
			goAnnotations[i].SetActive(!(animating || separate));
			goAnnotations[i].GetComponent<ObjectLabel>().offset = currentCenterOffset;
		}
	}

	private void SaveToHistory()
	{
		List<int> entry = new List<int>();
		for (int i = 0; i < vertexCount; i++)
			entry.Add(selected[i]);
		selectedHistory.Add(entry);
		if(selectedHistory.Count > 50)
			selectedHistory.RemoveAt(0);
	}

	private void CenterPointCloud(Vector3 selectedCenter)
	{
		Debug.Log("currentCenterOffset: "+currentCenterOffset);
		Debug.Log("selectedCenter: "+selectedCenter);
		currentCenterOffset -= selectedCenter;
		min = new Vector3(float.MaxValue,float.MaxValue,float.MaxValue);
		max = new Vector3(float.MinValue,float.MinValue,float.MinValue);
		bsRadius = 0;
		
		for (int i = 0; i < vertexCount; i++) 
		{
			Vector3 tmp = verts[i];
			
			// offset for the center of the selected points in the point cloud to be at 0,0,0
			tmp.x -= selectedCenter.x;
			tmp.y -= selectedCenter.y;
			tmp.z -= selectedCenter.z;
			
			verts[i] = tmp;

			if(selected[i] == 1)
			{
				// calculate bounding sphere radius
				float r = Mathf.Sqrt(Mathf.Pow(verts[i].x, 2) + Mathf.Pow(verts[i].y, 2) + Mathf.Pow(verts[i].z, 2));
				if(r > bsRadius)
					bsRadius = r;

				// calculate min/max of selected points (bounding box)
				if(tmp.x < min.x)
					min.x = tmp.x;
				if(tmp.y < min.y)
					min.y = tmp.y;
				if(tmp.z < min.z)
					min.z = tmp.z;
				
				if(tmp.x > max.x)
					max.x = tmp.x;
				if(tmp.y > max.y)
					max.y = tmp.y;
				if(tmp.z > max.z)
					max.z = tmp.z;
			}
		}
		
		size = max-min;

		// calculate size of points in the point cloud
		if(initialBSRadius == 0.0f)
			initialBSRadius = bsRadius;
		currentSelectedSize = selectedSize+initialBSRadius/bsRadius;

		// apply sizes
		for (int i = 0; i < vertexCount; ++i)
			if(selected[i] == 1)
				sizes[i] = currentSelectedSize;

		// calculate ideal distance from the camera
		float fieldOfViewX = 2.0F * Mathf.Atan( Mathf.Tan( (GetComponent<Camera>().fieldOfView/57.2957795F) / 2.0F ) * GetComponent<Camera>().aspect ) * 57.2957795F;
		float distX = bsRadius/Mathf.Tan(GetComponent<Camera>().fieldOfView * 0.0174532925F * 0.5F);
		float distY = bsRadius/Mathf.Tan(fieldOfViewX * 0.0174532925F * 0.5F);
		idealDistance = Mathf.Max(distX,distY);
		//Debug.Log("1Sphere of radius "+bsRadius+" should be at "+idealDistance);
		//idealDistance = bsRadius/Mathf.Sin(Mathf.Max(camera.fieldOfView, fieldOfViewX) * 0.0174532925F * 0.5F);
		//Debug.Log("2Sphere of radius "+bsRadius+" should be at "+idealDistance);
		GameObject.Find("Camera").GetComponent<ViewPoint3DMouse>().CenterView(idealDistance);//distance = 30 - (1.0F-((float)selectedCount/(float)vertexCount))*15.0F;
		GameObject.Find("Camera").GetComponent<Orbit>().CenterView(idealDistance);//distance = 30 - (1.0F-((float)selectedCount/(float)vertexCount))*15.0F;


		bufferPoints.SetData (verts);
		bufferColors.SetData (colors);
		bufferSizes.SetData (sizes);
		bufferColorOffset.SetData (colorsOffset);
		bufferSelected.SetData (selected);
		//bufferHighlighted.SetData (notHighlighted);
	}

	public void Undo()
	{
		if(selectedHistory.Count == 0)
			return;

		List<int> entry = selectedHistory[selectedHistory.Count-1];
		selectedHistory.RemoveAt(selectedHistory.Count-1);
	
		Vector3 selectedCenter = new Vector3();
		int selectedCount = 0;
		for (int i = 0; i < vertexCount; ++i)
		{
			selected[i] = entry[i];

			if(selected[i] == 1)
			{
				colorsOffset[i].x = 0.0F;
				colorsOffset[i].y = 0.0F;
				colorsOffset[i].z = 0.0F;
				colors[i].w = selectedAlpha;
				sizes[i] = currentSelectedSize;
				selectedCenter = selectedCenter + verts[i];
				selectedCount++;
			}
			else 
			{
				colorsOffset[i].x = 0.3F;
				colorsOffset[i].y = 0.3F;
				colorsOffset[i].z = 0.3F;
				colors[i].w = deselectedAlpha;
				sizes[i] = currentDeselectedSize;
				selected[i] = 0;
			}
		}
		
		if(selectedCount == 0)
			return;

		ResetSelected();
		
		selectedCenter = selectedCenter / selectedCount;
		
		CenterPointCloud(selectedCenter);

		mistakes++;
		steps--;
		
		Annotate(""+(steps+cancels+mistakes), false);
	}

	public void SelectAnnotation(int index)
	{
		string annotation = "";
		if(index >= 0)
			annotation = annotations[index];
		
		Vector3 selectedCenter = new Vector3();
		int selectedCount = 0;
		for (int i = 0; i < vertexCount; ++i)
		{
			bool hasAnnotation = false;
			for (int j = 0; j < cloudAnnotations[i].Count; j++)
			{
				if(cloudAnnotations[i][j] == annotation)
				{
					hasAnnotation = true;
					break;
				}
			}

			if(index == -1 || hasAnnotation) // -1 -> select all
			{
				colorsOffset[i].x = 0.0F;
				colorsOffset[i].y = 0.0F;
				colorsOffset[i].z = 0.0F;
				colors[i].w = selectedAlpha;
				sizes[i] = currentSelectedSize;
				selected[i] = 1;
				selectedCenter = selectedCenter + verts[i];
				selectedCount++;
			}
			else 
			{
				colorsOffset[i].x = 0.3F;
				colorsOffset[i].y = 0.3F;
				colorsOffset[i].z = 0.3F;
				colors[i].w = deselectedAlpha;
				sizes[i] = currentDeselectedSize;
				selected[i] = 0;
			}
		}
		
		if(selectedCount == 0)
			return;

		selectedCenter = selectedCenter / selectedCount;
		
		CenterPointCloud(selectedCenter);
	}

	public bool SetLasso(List<Vector3> vertices)
	{
		Debug.Log("setLasso");

		// calculate lasso input

		if(lassoThread == null || !lassoThread.IsAlive)
		{
			if(lassoThread != null)
			{
				// process the last lasso
				DateTime s = DateTime.Now;
				ProcessLasso();
				print("processlasso: "+(DateTime.Now - s).TotalSeconds);
			}
			// start a new processLassoInput thread to calculate lasso vertices and points
			tLassoVertices = vertices;
			tProjMatrix = GameObject.Find("Camera").GetComponent<Camera>().projectionMatrix;
			tViewMatrix = GameObject.Find("Camera").GetComponent<Camera>().worldToCameraMatrix;
			tCamera = GameObject.Find("Camera").GetComponent<Camera>();
			ProcessLassoInput();
			//lassoThread = new Thread(new ThreadStart(ProcessLassoInput));
			//lassoThread.Start();
		}

		if(!tValidLasso)
		{
			ResetSelected();
		}
		else
		{
			bufferPoints.SetData (verts);
			bufferColors.SetData (colors);
			bufferSizes.SetData (sizes);
			bufferColorOffset.SetData (colorsOffset);
			bufferSelected.SetData (selected);
			//bufferHighlighted.SetData (notHighlighted);
		}
		
		return tValidLasso;
	}

	private void ProcessLassoInput()
	{
		tLasso = new Vector2[tLassoVertices.Count];
		for(int i = 0; i < tLassoVertices.Count; i++)
		{
			Vector3 screenPoint = tCamera.WorldToScreenPoint(tLassoVertices[i]);
			tLasso[i] = new Vector2(screenPoint.x,screenPoint.y);
		}
		
		tPoints2D = new List<Vector2>();
		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i] == 1)
			{
				Vector3 screenPoint = tCamera.WorldToScreenPoint(verts[i]);
				Vector2 point2D = new Vector2(screenPoint.x, screenPoint.y);
				tPoints2D.Add(point2D);
			}
		}
	}

	private void ProcessLasso()
	{
		int countp1 = 0;
		int countp2 = 0;

		for(int i = 0; i < tPoints2D.Count; i++)
		{
			if(wn_PnPoly(tPoints2D[i], tLasso, tLassoSize-1) == 0) // outside
			{
				countp1++;
				colorsOffset[i].x = 0.5F;
				colorsOffset[i].y = -0.5F;
				colorsOffset[i].z = -0.5F;
			}
			else
			{
				countp2++;
				colorsOffset[i].x = -0.5F;
				colorsOffset[i].y = -0.5F;
				colorsOffset[i].z = 0.5F;
			}
		}
		
		if(countp1 == 0 || countp2 == 0)
			tValidLasso = false;
		else
			tValidLasso = true;
	}
	
	public bool SetSphere(Vector3 center, float radius, bool resetColor)
	{
		int countp1 = 0;
		int countp2 = 0;
		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i] == 1)
			{
				if(Vector3.Distance(center,verts[i]) > radius)
				{
					if(resetColor)
					{
						colorsOffset[i].x = 0.5F;
						colorsOffset[i].y = -0.5F;
						colorsOffset[i].z = -0.5F;
					}
				}
				else
				{
					colorsOffset[i].x = -0.5F;
					colorsOffset[i].y = -0.5F;
					colorsOffset[i].z = 0.5F;
				}
				if(colorsOffset[i].x == 0.5F)
					countp2++;
				else if(colorsOffset[i].z == 0.5F)
					countp1++;
			}
		}
		
		if(resetColor && (countp1 == 0 || countp2 == 0))
		{
			ResetSelected();
			return false;
		}
		
		bufferPoints.SetData (verts);
		bufferColors.SetData (colors);
		bufferSizes.SetData (sizes);
		bufferColorOffset.SetData (colorsOffset);
		bufferSelected.SetData (selected);
		//bufferHighlighted.SetData (notHighlighted);
		
		return true;
	}
	
	public bool SetSphereTrail(List<Sphere> spheres)
	{
		int countp1 = 0;
		int countp2 = 0;
		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i] == 1)
			{
				bool inside = false;
				for(int j = 0; j < spheres.Count; j++)
				{
					if(Vector3.Distance(spheres[j].center,verts[i]) < spheres[j].radius)
					{
						countp2++;
						colorsOffset[i].x = -0.5F;
						colorsOffset[i].y = -0.5F;
						colorsOffset[i].z = 0.5F;
						inside = true;
						break;
					}
				}
				if(!inside)
				{
					countp1++;
					colorsOffset[i].x = 0.5F;
					colorsOffset[i].y = -0.5F;
					colorsOffset[i].z = -0.5F;
				}
			}
		}
		
		if(countp1 == 0 || countp2 == 0)
		{
			ResetSelected();
			return false;
		}
		
		bufferPoints.SetData (verts);
		bufferColors.SetData (colors);
		bufferSizes.SetData (sizes);
		bufferColorOffset.SetData (colorsOffset);
		bufferSelected.SetData (selected);
		//bufferHighlighted.SetData (notHighlighted);
		
		return true;
	}

	public bool SetSelectionPlane(Plane plane)
	{
		int countp1 = 0;
		int countp2 = 0;
		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i] == 1)
			{
				if(plane.GetSide(verts[i]))
				{
					countp1++;
					colorsOffset[i].x = 0.5F;
					colorsOffset[i].y = -0.5F;
					colorsOffset[i].z = -0.5F;
				}
				else
				{
					countp2++;
					colorsOffset[i].x = -0.5F;
					colorsOffset[i].y = -0.5F;
					colorsOffset[i].z = 0.5F;
				}
			}
		}

		if(countp1 == 0 || countp2 == 0)
		{
			ResetSelected();
			return false;
		}
		
		bufferPoints.SetData (verts);
		bufferColors.SetData (colors);
		bufferSizes.SetData (sizes);
		bufferColorOffset.SetData (colorsOffset);
		bufferSelected.SetData (selected);
		//bufferHighlighted.SetData (notHighlighted);
		
		return true;
	}

	public bool ValidateSets()
	{
		int countp1 = 0;
		int countp2 = 0;
		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i] == 1)
			{
				if(colorsOffset[i].x == 0.5F)
					countp2++;
				else if(colorsOffset[i].z == 0.5F)
					countp1++;
			}
		}
		
		if(countp1 == 0 || countp2 == 0)
			return false;

		return true;
	}

	public void SelectSide(Plane plane, bool side) // side is true when we want points in the same side as normal
	{
		SaveToHistory();

		Vector3 selectedCenter = new Vector3();
		int selectedCount = 0;
		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i] == 1)
			{
				if(plane.GetSide(verts[i]) == side)
				{
					colorsOffset[i].x = 0.3F;
					colorsOffset[i].y = 0.3F;
					colorsOffset[i].z = 0.3F;
					colors[i].w = deselectedAlpha;
					sizes[i] = currentDeselectedSize;
					selected[i] = 0;
				}
				else
				{
					selectedCenter = selectedCenter + verts[i];
					selectedCount++;
				}
			}
		}

		if(selectedCount == 0)
			return;

		ResetSelected();

		selectedCenter = selectedCenter / selectedCount;
		
		CenterPointCloud(selectedCenter);
	}

	public void SelectSphere(Vector3 center, float radius, bool inside)
	{
		SaveToHistory();

		Vector3 selectedCenter = new Vector3();
		int selectedCount = 0;
		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i] == 1)
			{
				if((Vector3.Distance(center,verts[i]) > radius && inside) || (Vector3.Distance(center,verts[i]) < radius && !inside))
				{
					colorsOffset[i].x = 0.3F;
					colorsOffset[i].y = 0.3F;
					colorsOffset[i].z = 0.3F;
					colors[i].w = deselectedAlpha;
					sizes[i] = currentDeselectedSize;
					selected[i] = 0;
				}
				else
				{
					selectedCenter = selectedCenter + verts[i];
					selectedCount++;
				}
			}
		}
		
		if(selectedCount == 0)
			return;

		ResetSelected();

		selectedCenter = selectedCenter / selectedCount;
		
		CenterPointCloud(selectedCenter);
	}

	public void SelectSphereTrail(List<Sphere> spheres, bool inside)
	{
		SaveToHistory();

		Vector3 selectedCenter = new Vector3();
		int selectedCount = 0;

		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i] == 1)
			{
				bool isInside = false;
				for(int j = 0; j < spheres.Count; j++)
				{
					if(Vector3.Distance(spheres[j].center,verts[i]) < spheres[j].radius)
					{
						isInside = true;
						break;
					}
				}
				if((!isInside && inside) || (isInside && !inside))
				{
					colorsOffset[i].x = 0.3F;
					colorsOffset[i].y = 0.3F;
					colorsOffset[i].z = 0.3F;
					colors[i].w = deselectedAlpha;
					sizes[i] = currentDeselectedSize;
					selected[i] = 0;
				}
				else
				{
					selectedCenter = selectedCenter + verts[i];
					selectedCount++;
				}
			}
		}
		
		if(selectedCount == 0)
			return;

		ResetSelected();

		selectedCenter = selectedCenter / selectedCount;

		CenterPointCloud(selectedCenter);
	}

	public void SelectLasso(List<Vector3> vertices, bool inside)
	{
		SaveToHistory();

		Vector3 selectedCenter = new Vector3();
		int selectedCount = 0;

		Vector3 leftColorOffset = new Vector3(-0.5F,-0.5F,0.5F);
		Vector3 rightColorOffset = new Vector3(0.5F,-0.5F,-0.5F);
		Vector3 deselectedColorOffset = new Vector3(0.3F,0.3F,0.3F);

		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i] == 1)
			{
				if((colorsOffset[i].x == leftColorOffset.x && colorsOffset[i].y == leftColorOffset.y && colorsOffset[i].z == leftColorOffset.z && inside) || 
				   (colorsOffset[i].x == rightColorOffset.x && colorsOffset[i].y == rightColorOffset.y && colorsOffset[i].z == rightColorOffset.z && !inside)) 
				{
					colorsOffset[i] = deselectedColorOffset;
					colors[i].w = deselectedAlpha;
					sizes[i] = currentDeselectedSize;
					selected[i] = 0;
				}
				else
				{
					colorsOffset[i].x = 0.3F;
					colorsOffset[i].y = 0.3F;
					colorsOffset[i].z = 0.3F;
					colors[i].w = selectedAlpha;
					sizes[i] = currentSelectedSize;
					selectedCenter = selectedCenter + verts[i];
					selectedCount++;
				}
			}
		}
		
		if(selectedCount == 0)
			return;

		ResetSelected();

		selectedCenter = selectedCenter / selectedCount;

		CenterPointCloud(selectedCenter);
	}

	public void ResetSelected()
	{
		//Debug.Log("ResetSelected");

		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i] == 1)
			{
				colorsOffset[i].x = 0.0F;
				colorsOffset[i].y = 0.0F;
				colorsOffset[i].z = 0.0F;
				colors[i].w = selectedAlpha;
			}
		}
		
		bufferPoints.SetData (verts);
		bufferColors.SetData (colors);
		bufferSizes.SetData (sizes);
		bufferColorOffset.SetData (colorsOffset);
		bufferSelected.SetData (selected);
		//bufferHighlighted.SetData (notHighlighted);
	}
	
	public void ResetAll()
	{
		SaveToHistory();

		Vector3 selectedCenter = new Vector3();

		for (int i = 0; i < vertexCount; ++i)
		{
			verts[i]    = originalVerts[i];
			colors[i]   = originalColors[i];
			selected[i] = 1;
			sizes[i]    = currentSelectedSize;
			colorsOffset[i].x = 0.0F;
			colorsOffset[i].y = 0.0F;
			colorsOffset[i].z = 0.0F;

			selectedCenter = selectedCenter + verts[i];
		}
		
		selectedCenter = selectedCenter / vertexCount;

		min = new Vector3(oMin.x, oMin.y, oMin.z);
		max = new Vector3(oMax.x, oMax.y, oMax.z);
		size = new Vector3(oSize.x, oSize.y, oSize.z);

		CenterPointCloud(selectedCenter);
		
		currentCenterOffset = new Vector3();
	}

	// The parameter is the name of the annotation that the user writes on screen
	public void Annotate(string annotation, bool reset)
	{

//		if(separate || animating)
//			return;
		
		Vector3 center = new Vector3();
		int selectedCount = 0;
		
		if ( annotationsPerVertex.ContainsKey (annotation) ) {
			annotationsPerVertex[annotation].Clear();
		} else {
			annotationsPerVertex[annotation] = new List<int>();
		}
		
		int[] selectedByIndex;
		selectedByIndex = selected;

		// add annotation to all points that are selected
		for (int i = 0; i < vertexCount; ++i)
		{
			if(selectedByIndex[i] == 1)
			{
				cloudAnnotations[i].Add(annotation);
				center = center + originalVerts[i];
				selectedCount++;
				
				annotationsPerVertex[annotation].Add(i);
			}
		}
		// and to the list of annotations
		if(!annotations.Contains(annotation))
			annotations.Add(annotation);
		
		center = center / selectedCount;
		
		GameObject tmpGo = new GameObject("Annotation");
		GUIText t = tmpGo.AddComponent<GUIText>();
		t.text = reset ? annotation : "";
		t.fontSize = (int)(15.0F+(selectedCount/vertexCount)*15.0F);
		t.anchor = TextAnchor.MiddleCenter;
		t.alignment = TextAlignment.Center;
		goAnnotation.Add(t);
		ObjectLabel o = tmpGo.AddComponent<ObjectLabel>();
		o.target = center-originalCenter;
		o.useMainCamera = false;
		o.cameraToUse = GameObject.Find("Camera").GetComponent<Camera>();
		goAnnotations.Add(tmpGo);
		
		//string annotationFileName = pointCloudFile.Remove(pointCloudFile.Length-4)+@"_"+annotation+@"_"+Path.GetRandomFileName().Substring(0,2)+@".annotation.csv";

		string participantID = GameObject.Find ("Experiment Menu").GetComponent<ExperimentMenu>().participantID;

		
		DateTime endTime = DateTime.Now;
		TimeSpan ts = endTime - startTime;

		try {
			string path = Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)).FullName;
			if ( Environment.OSVersion.Version.Major >= 6 ) {
				path = Directory.GetParent(path).ToString();
			}
			string annotationFileName = path+@"\Dropbox\TaskResults\"
				+ participantID
				+ @"_"+ annotation 
				+ @"_t" +(int) GameObject.Find ("Experiment Menu").GetComponent<ExperimentMenu>().selectedTechnique
				+ @"s" +(int) GameObject.Find ("Experiment Menu").GetComponent<ExperimentMenu>().selectedStrategy
				+ @"_" + modelName + @"_" + taskName
				+ @"_" + endTime.ToShortDateString ().Replace ('/', '_')
				+ @"_" + endTime.ToLongTimeString ().Replace (':', '_').Replace(" ","")
				+ @".csv";

			System.IO.StreamWriter annotationFile = new System.IO.StreamWriter (annotationFileName);
			annotationFile.WriteLine(participantID
			                         +@","+annotation
			                         +@","+GameObject.Find("Experiment Menu").GetComponent<ExperimentMenu>().selectedTechnique
			                         +@","+GameObject.Find("Experiment Menu").GetComponent<ExperimentMenu>().selectedStrategy
			                         +@","+currentStrategy
			                         +@","+modelName
			                         +@","+taskName
			                         +@","+steps
			                         +@","+mistakes
			                         +@","+cancels
			                         +@","+ts.TotalSeconds);
			foreach (int index in annotationsPerVertex[annotation]) {
				annotationFile.Write (index + ",");
			}
			annotationFile.Close ();
		}
		catch(DirectoryNotFoundException e) {
			print("Caught exception:\n"+e.StackTrace);
		}
		
		if (reset) {
			ResetAll ();
			steps = 0;
			mistakes = 0;
		}
	}


	private void checkTaskCompletion()
	{
		int hits = 0;
		int falseHits = 0;
		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i] == 1 && highlighted[i] == 1)
			{
				hits++;
			}
			else if(selected[i] == 1)
			{
				falseHits++;
			}
		}
		hitPercent = hits / (float)highlightedCount;
		falseHitPercent = falseHits / (float)highlightedCount;
	}

	public Vector3 Size()
	{
		return size;
	}
	
	public Vector3 Min()
	{
		return min;
	}
	
	public Vector3 Max()
	{
		return max;
	}


	// Copyright 2000 softSurfer, 2012 Dan Sunday
	// This code may be freely used and modified for any purpose
	// providing that this copyright notice is included with it.
	// SoftSurfer makes no warranty for this code, and cannot be held
	// liable for any real or imagined damage resulting from its use.
	// Users of this code must verify correctness for their application.

	// http://geomalgorithms.com/a03-_inclusion.html
	
	// isLeft(): tests if a point is Left|On|Right of an infinite line.
	//    Input:  three points P0, P1, and P2
	//    Return: >0 for P2 left of the line through P0 and P1
	//            =0 for P2  on the line
	//            <0 for P2  right of the line
	//    See: Algorithm 1 "Area of Triangles and Polygons"
	public int isLeft( Vector3 P0, Vector3 P1, Vector3 P2 )
	{
		return (int)( (P1.x - P0.x) * (P2.y - P0.y) - (P2.x -  P0.x) * (P1.y - P0.y) );
	}
	//===================================================================
		
	// cn_PnPoly(): crossing number test for a point in a polygon
	//      Input:   P = a point,
	//               V[] = vertex points of a polygon V[n+1] with V[n]=V[0]
	//      Return:  0 = outside, 1 = inside
	// This code is patterned after [Franklin, 2000]
	public int cn_PnPoly( Vector2 P, Vector2[] V, int n )
	{
		int    cn = 0;    // the  crossing number counter
		
		// loop through all edges of the polygon
		for (int i=0; i<n; i++) {    // edge from V[i]  to V[i+1]
			if (((V[i].y <= P.y) && (V[i+1].y > P.y))     // an upward crossing
			    || ((V[i].y > P.y) && (V[i+1].y <=  P.y))) { // a downward crossing
				// compute  the actual edge-ray intersect x-coordinate
				float vt = (float)(P.y  - V[i].y) / (V[i+1].y - V[i].y);
				if (P.x <  V[i].x + vt * (V[i+1].x - V[i].x)) // P.x < intersect
					++cn;   // a valid crossing of y=P.y right of P.x
			}
		}
		return (cn&1);  // 0 if even (out), and 1 if  odd (in)
		
	}
	//===================================================================
	
	// wn_PnPoly(): winding number test for a point in a polygon
	//      Input:   P = a point,
	//               V[] = vertex points of a polygon V[n+1] with V[n]=V[0]
	//      Return:  wn = the winding number (=0 only when P is outside)
	public int wn_PnPoly( Vector2 P, Vector2[] V, int n )
	{
		int    wn = 0;    // the  winding number counter
		
		// loop through all edges of the polygon
		for (int i=0; i<n; i++) {   // edge from V[i] to  V[i+1]
			if (V[i].y <= P.y) {          // start y <= P.y
				if (V[i+1].y  > P.y)      // an upward crossing
					if (isLeft( V[i], V[i+1], P) > 0)  // P left of  edge
						++wn;            // have  a valid up intersect
			}
			else {                        // start y > P.y (no test needed)
				if (V[i+1].y  <= P.y)     // a downward crossing
					if (isLeft( V[i], V[i+1], P) < 0)  // P right of  edge
						--wn;            // have  a valid down intersect
			}
		}
		return wn;
	}
	//===================================================================
}
