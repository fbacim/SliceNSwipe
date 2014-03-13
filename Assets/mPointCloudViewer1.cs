// PointCloudViewer Unity3D - http://unitycoder.com/blog/

using UnityEngine;
//using System.Collections;
//using System;
//using System.IO;

public class mPointCloudViewer1 : MonoBehaviour {
	
	public Material material;
	private int vertexCount; // this should match the amount of points from file
	private int instanceCount = 2; // no need to adjust (otherwise you have instanceCount * vertexCount amount of objects..
	
	private ComputeBuffer bufferPoints;
	private ComputeBuffer bufferPos;
	private ComputeBuffer bufferColors;
	
	private Vector3[] pos;
	private Vector3[] verts;
	private Vector3[] colors;

	private bool separate  = false;  // are the point cloud instances separate?
	private bool animating = false;  // is it currently animating?
	
	public TextAsset fileName;
	public Vector3 offset;
	
	void Start() {
		// load from file
		string[] xyzline = fileName.text.Split('\n');
		Debug.Log("Points in file:" + xyzline.Length);

		// init arrays, using value from the file
		vertexCount = xyzline.Length;
		
		verts = new Vector3[vertexCount];
		colors = new Vector3[vertexCount];
		
		// get xyz values
		for(int i = 0; i < vertexCount; ++i)
		{
			string[] xyz = xyzline[i].Split(' ');
			float x = float.Parse(xyz[0]);
			float y = float.Parse(xyz[2]);
			float z = float.Parse(xyz[1]);
			
			verts[i] = new Vector3(x,y,z);
			colors[i] = new Vector3(1,0,0);
		}

		// OR for testing
		/*
		// generate random points (up to 40mil points)
		vertexCount = 100000;		
		verts = new Vector3[vertexCount];
		for (var i = 0; i < vertexCount; ++i)
		{
			//float x = Random.insideUnitSphere*50;
			//float y = Random.insideUnitSphere*50;
			//float z = Random.insideUnitSphere*50;
			verts[i] = Random.insideUnitSphere*50;
		}
		*/

		ReleaseBuffers ();
		
		bufferPoints = new ComputeBuffer (vertexCount, 12);
		bufferPoints.SetData (verts);
		material.SetBuffer ("buf_Points", bufferPoints);
				
		bufferColors = new ComputeBuffer (vertexCount, 12);
		bufferColors.SetData (colors);
		material.SetBuffer ("buf_Colors", bufferColors);

		// normalized offset of each instance of the point cloud
		pos = new Vector3[instanceCount];
		pos[0] = new Vector3(0.0f,0,0);
		pos[1] = new Vector3(0.0f,0,0);
		
		bufferPos = new ComputeBuffer (instanceCount, 12);
		material.SetBuffer ("buf_Positions", bufferPos);

		bufferPos.SetData (pos);
		
	}
	
	private void ReleaseBuffers() {
		if (bufferPoints != null) bufferPoints.Release();
		bufferPoints = null;
		if (bufferPos != null) bufferPos.Release();
		bufferPos = null;
		if (bufferColors != null) bufferColors.Release();
		bufferColors = null;
	}
	
	void OnDisable() {
		ReleaseBuffers();
	}

	public float animationTotalTime = 0.25f;
	private float animationStartTime;
	private float separationDistance;
	public float pcSize = 50;
	
	// each frame, update the positions buffer (one vector per instance)
	void Update() {
		float currentTime = Time.timeSinceLevelLoad;
		// animation
		// cut event, for now using slash but should be something else
		if(Input.GetKeyDown(KeyCode.Slash))
		{
			animating = true;
			animationStartTime = currentTime;
		}

		if(!animating && !separate)
			separationDistance = pcSize;

		if(animating)
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
			pos[0] = new Vector3(-((Mathf.Cos(t)+1.0f)*0.5f),0,separationDistance); // first instance goes to left
			pos[1] = new Vector3( ((Mathf.Cos(t)+1.0f)*0.5f),0,separationDistance); // second goes to right

			// if it's done animating, invert variable that tells if point cloud is separated already
			if(!animating)
				separate = !separate;
		}
		
		bufferPos.SetData(pos);
	}
	
	// called if script attached to the camera, after all regular rendering is done
	//void OnPostRender() {
	void OnRenderObject() {
		material.SetPass(0);
		Graphics.DrawProcedural(MeshTopology.Points, vertexCount, instanceCount);
	}
}
