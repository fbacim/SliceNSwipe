using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class PointCloud : MonoBehaviour {
	public Material material;
	public int vertexCount; // this should match the amount of points from file
	private int instanceCount = 2; // no need to adjust (otherwise you have instanceCount * vertexCount amount of objects..
	
	private ComputeBuffer bufferPoints;
	private ComputeBuffer bufferPos;
	private ComputeBuffer bufferColors;
	
	private Vector3[] pos;
	private Vector3[] originalVerts;
	public Vector3[] verts;
	private Vector3[] originalColors;
	private Vector3[] colors;
	private bool[] selected;
	private List< List<string> > annotations;
	
	private bool separate  = false;  // are the point cloud instances separate?
	private bool animating = false;  // is it currently animating?

	List<GUIText> goAnnotation;
	bool pointsUpdated = false;

	Vector3 min, max, size, oMin, oMax, oSize;
	
	public float animationTotalTime = 0.25f;
	private float animationStartTime;
	private float separationDistance;

	static int CountLinesInFile(string f)
	{
		int count = 0; 
		using (StreamReader r = new StreamReader(f))
		{
			string line;
			while ((line = r.ReadLine()) != null)
			{
				count++;
			}
		}
		return count-1;// remove header
	}

	// Use this for initialization
	void Start () 
	{
		goAnnotation = new List<GUIText>();

		// file options:
		//   logo
		//   LongHornBeetle_PointCloud
		//   QCAT_N3_Zebedee_color
		string fileName = Application.dataPath+@"/QCAT_N3_Zebedee_color.pointcloud.csv";
		vertexCount = CountLinesInFile(fileName);
		Debug.Log("Points: "+vertexCount);
		
		// vertex, color, selected, annotations arrays
		verts = new Vector3[vertexCount];
		originalVerts = new Vector3[vertexCount];
		colors = new Vector3[vertexCount];
		originalColors = new Vector3[vertexCount];
		selected = new bool[vertexCount];
		annotations = new List< List<string> >();

		// normalized offset of each instance of the point cloud
		pos = new Vector3[instanceCount];
		pos[0] = new Vector3(0.0f,0,0);
		pos[1] = new Vector3(0.0f,0,0);

		StreamReader reader = new StreamReader(File.OpenRead(fileName));

        reader.ReadLine(); // ignore first line
        Vector3 center = new Vector3(0,0,0); // calculate center of the object
		int lineCount = 0; // counter for the line number
		while (!reader.EndOfStream)
        {
            string line = reader.ReadLine();
            string[] values = line.Split(',');

			// populate arrays
			verts[lineCount] = new Vector3(float.Parse(values[0]),float.Parse(values[1]),float.Parse(values[2]));
			originalVerts[lineCount] = new Vector3(float.Parse(values[0]),float.Parse(values[1]),float.Parse(values[2]));
			colors[lineCount] = new Vector3(float.Parse(values[3]),float.Parse(values[4]),float.Parse(values[5]));
			originalColors[lineCount] = new Vector3(float.Parse(values[3]),float.Parse(values[4]),float.Parse(values[5]));
			selected[lineCount] = true;
			annotations.Add(new List<string>());

			// accumulate center
			center = center + new Vector3(float.Parse(values[0]),float.Parse(values[1]),float.Parse(values[2]));

			lineCount++;
        }
		center = center / vertexCount;
		Debug.Log("center: "+center.x+","+center.y+","+center.z);

		min = new Vector3(float.MaxValue,float.MaxValue,float.MaxValue);
		max = new Vector3(float.MinValue,float.MinValue,float.MinValue);

		for (int i = 0; i < vertexCount ; i++) 
		{
			// offset for the center of the point cloud to be at 0,0,0
			verts[i].x -= center.x;
			verts[i].y -= center.y;
			verts[i].z -= center.z;

			// calculate min/max
			if(verts[i].x < min.x)
				min.x = verts[i].x;
			if(verts[i].y < min.y)
				min.y = verts[i].y;
			if(verts[i].z < min.z)
				min.z = verts[i].z;
			
			if(verts[i].x > max.x)
				max.x = verts[i].x;
			if(verts[i].y > max.y)
				max.y = verts[i].y;
			if(verts[i].z > max.z)
				max.z = verts[i].z;
		}
		
		size = new Vector3(max.x-min.x,max.y-min.y,max.z-min.z);

		oMin = new Vector3(min.x, min.y, min.z);
		oMax = new Vector3(max.x, max.y, max.z);
		oSize = new Vector3(size.x, size.y, size.z);
		
		ReleaseBuffers ();
		
		bufferPoints = new ComputeBuffer (vertexCount, 12);
		bufferPoints.SetData (verts);
		material.SetBuffer ("buf_Points", bufferPoints);
		
		bufferColors = new ComputeBuffer (vertexCount, 12);
		bufferColors.SetData (colors);
		material.SetBuffer ("buf_Colors", bufferColors);
		
		bufferPos = new ComputeBuffer (instanceCount, 12);
		material.SetBuffer ("buf_Positions", bufferPos);
		
		bufferPos.SetData (pos);

		//pointsUpdated = true;
	}
	
	private void ReleaseBuffers () 
	{
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

	// Update is called once per frame
	void Update () 
	{
		if (pointsUpdated)
		{
			bufferPoints.SetData (verts);
			bufferColors.SetData (colors);
			pointsUpdated = false;
		}

		float currentTime = Time.timeSinceLevelLoad;
		// animation
		// cut event, for now using slash but should be something else
		if(Input.GetKeyDown(KeyCode.Period))
		{
			animating = true;
			animationStartTime = currentTime;
		}
		
		if(!animating && !separate)
			separationDistance = Mathf.Min(new float[] {oSize.x, oSize.y, oSize.z})/2.0f;
		
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
	
	//void OnPostRender() 
	void OnRenderObject () 
	{
		material.SetPass(0);
		Graphics.DrawProcedural(MeshTopology.Points, vertexCount, instanceCount);
	}

	public void setLasso(List<Vector3> vertices)
	{
		Debug.Log("setLasso");

		Vector2[] vertices2D = new Vector2[vertices.Count];
		for(int i = 0; i < vertices.Count; i++)
		{
			Vector3 screenPoint = GameObject.Find("Camera").camera.WorldToScreenPoint(vertices[i]);
			vertices2D[i] = new Vector2(screenPoint.x,screenPoint.y);
		}
		
		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i])
			{
				colors[i] = originalColors[i];
				Vector3 tmp = colors[i];
				Vector3 screenPoint = GameObject.Find("Camera").camera.WorldToScreenPoint(verts[i]);
				Vector2 point2D = new Vector2(screenPoint.x, screenPoint.y);
				if(wn_PnPoly(point2D,vertices2D,vertices.Count-1) == 0) // outside
				{
					tmp.x += 0.5F;
				}
				else
				{
					tmp.z += 0.5F;
				}
				colors[i] = tmp;
			}
		}

		pointsUpdated = true;
	}
	
	public void SetSphere(Vector3 center, float radius)
	{

		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i])
			{
				colors[i] = originalColors[i];
				Vector3 tmp = colors[i];
				if(Vector3.Distance(center,verts[i]) > radius)
				{
					tmp.x += 0.5F;
				}
				else
				{
					tmp.z += 0.5F;
				}
				colors[i] = tmp;
			}
		}
		
		pointsUpdated = true;
	}
	
	public void SetSphereTrail(List<Sphere> spheres)
	{
		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i])
			{
				colors[i] = originalColors[i];
				Vector3 tmp = colors[i];
				bool inside = false;
				for(int j = 0; j < spheres.Count; j++)
				{
					if(Vector3.Distance(spheres[j].center,verts[i]) < spheres[j].radius)
					{
						tmp.z += 0.5F;
						inside = true;
						break;
					}
				}
				if(!inside)
				{
					tmp.x += 0.5F;
				}
				colors[i] = tmp;
			}
		}
		
		pointsUpdated = true;
	}

	public void SetSelectionPlane(Plane plane)
	{
		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i])
			{
				colors[i] = originalColors[i];
				Vector3 tmp = colors[i];
				if(plane.GetSide(verts[i]))
				{
					tmp.x += 0.5F;
				}
				else
				{
					tmp.z += 0.5F;
				}
				colors[i] = tmp;
			}
		}

		pointsUpdated = true;
	}

	public void SelectSide(Plane plane, bool side) // side is true when we want points in the same side as normal
	{
		Vector3 selectedCenter = new Vector3();
		int selectedCount = 0;
		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i])
			{
				colors[i] = originalColors[i];
				Vector3 tmp = colors[i];
				if(plane.GetSide(verts[i]) == side)
				{
					tmp.x += 0.3F;
					tmp.y += 0.3F;
					tmp.z += 0.3F;
					selected[i] = false;
				}
				else
				{
					selectedCenter = selectedCenter + verts[i];
					selectedCount++;
				}
				colors[i] = tmp;
			}
		}

		if(selectedCount == 0)
		{
			ResetSelected();
			return;
		}

		selectedCenter = selectedCenter / selectedCount;
		
		min = new Vector3(float.MaxValue,float.MaxValue,float.MaxValue);
		max = new Vector3(float.MinValue,float.MinValue,float.MinValue);

		for (int i = 0; i < vertexCount; i++) 
		{
			Vector3 tmp = verts[i];

			// offset for the center of the selected points in the point cloud to be at 0,0,0
			tmp.x -= selectedCenter.x;
			tmp.y -= selectedCenter.y;
			tmp.z -= selectedCenter.z;
			
			verts[i] = tmp;

			// calculate min/max of selected points
			if(selected[i])
			{
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
		
		size = new Vector3(max.x-min.x,max.y-min.y,max.z-min.z);

		GameObject.Find("Camera").GetComponent<ViewPoint3DMouse>().distance = 30 - (1.0F-((float)selectedCount/(float)vertexCount))*15.0F;
		GameObject.Find("Camera").GetComponent<Orbit>().distance = 30 - (1.0F-((float)selectedCount/(float)vertexCount))*15.0F;
		
		pointsUpdated = true;
	}

	public void SelectSphere(Vector3 center, float radius, bool inside)
	{
		Vector3 selectedCenter = new Vector3();
		int selectedCount = 0;
		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i])
			{
				colors[i] = originalColors[i];
				Vector3 tmp = colors[i];
				if((Vector3.Distance(center,verts[i]) > radius && inside) || (Vector3.Distance(center,verts[i]) < radius && !inside))
				{
					tmp.x += 0.3F;
					tmp.y += 0.3F;
					tmp.z += 0.3F;
					selected[i] = false;
				}
				else
				{
					selectedCenter = selectedCenter + verts[i];
					selectedCount++;
				}
				colors[i] = tmp;
			}
		}
		
		if(selectedCount == 0)
		{
			ResetSelected();
			return;
		}
		
		selectedCenter = selectedCenter / selectedCount;
		
		min = new Vector3(float.MaxValue,float.MaxValue,float.MaxValue);
		max = new Vector3(float.MinValue,float.MinValue,float.MinValue);
		
		for (int i = 0; i < vertexCount ; i++) 
		{
			Vector3 tmp = verts[i];
			
			// offset for the center of the selected points in the point cloud to be at 0,0,0
			tmp.x -= selectedCenter.x;
			tmp.y -= selectedCenter.y;
			tmp.z -= selectedCenter.z;
			
			verts[i] = tmp;
			
			// calculate min/max of selected points
			if(selected[i])
			{
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
		
		size = new Vector3(max.x-min.x,max.y-min.y,max.z-min.z);
		
		GameObject.Find("Camera").GetComponent<ViewPoint3DMouse>().distance = 30 - (1.0F-((float)selectedCount/(float)vertexCount))*15.0F;
		GameObject.Find("Camera").GetComponent<Orbit>().distance = 30 - (1.0F-((float)selectedCount/(float)vertexCount))*15.0F;

		pointsUpdated = true;
	}

	public void SelectSphereTrail(List<Sphere> spheres, bool inside)
	{
		Vector3 selectedCenter = new Vector3();
		int selectedCount = 0;

		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i])
			{
				colors[i] = originalColors[i];
				Vector3 tmp = colors[i];
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
					tmp.x += 0.3F;
					tmp.y += 0.3F;
					tmp.z += 0.3F;
					selected[i] = false;
				}
				else
				{
					selectedCenter = selectedCenter + verts[i];
					selectedCount++;
				}
				colors[i] = tmp;
			}
		}
		
		if(selectedCount == 0)
		{
			ResetSelected();
			return;
		}
		
		selectedCenter = selectedCenter / selectedCount;
		
		min = new Vector3(float.MaxValue,float.MaxValue,float.MaxValue);
		max = new Vector3(float.MinValue,float.MinValue,float.MinValue);
		
		for (int i = 0; i < vertexCount ; i++) 
		{
			Vector3 tmp = verts[i];
			
			// offset for the center of the selected points in the point cloud to be at 0,0,0
			tmp.x -= selectedCenter.x;
			tmp.y -= selectedCenter.y;
			tmp.z -= selectedCenter.z;
			
			verts[i] = tmp;
			
			// calculate min/max of selected points
			if(selected[i])
			{
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
		
		size = new Vector3(max.x-min.x,max.y-min.y,max.z-min.z);
		
		GameObject.Find("Camera").GetComponent<ViewPoint3DMouse>().distance = 30 - (1.0F-((float)selectedCount/(float)vertexCount))*15.0F;
		GameObject.Find("Camera").GetComponent<Orbit>().distance = 30 - (1.0F-((float)selectedCount/(float)vertexCount))*15.0F;
		
		pointsUpdated = true;
	}

	public void ResetSelected()
	{
		Debug.Log("ResetSelected");

		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i])
				colors[i] = originalColors[i];
		}
		
		pointsUpdated = true;
	}
	
	public void ResetAll()
	{
		for (int i = 0; i < vertexCount; ++i)
		{
			verts[i]    = originalVerts[i];
			colors[i]   = originalColors[i];
			selected[i] = true;
		}
		
		min = new Vector3(oMin.x, oMin.y, oMin.z);
		max = new Vector3(oMax.x, oMax.y, oMax.z);
		size = new Vector3(oSize.x, oSize.y, oSize.z);
		
		pointsUpdated = true;
	}

	public void Annotate(string annotation)
	{
		Vector3 center = new Vector3();
		int selectedCount = 0;

		// add annotation to all points that are selected
		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i])
			{
				annotations[i].Add(annotation);
				center = center + verts[i];
				selectedCount++;
			}
		}
		center = center / selectedCount;

		GameObject tmpGo = new GameObject("Annotation");
		GUIText t = tmpGo.AddComponent<GUIText>();
		t.text = annotation;
		t.fontSize = (int)(15.0F+(selectedCount/vertexCount)*15.0F);
		t.anchor = TextAnchor.MiddleCenter;
		t.alignment = TextAlignment.Center;
		goAnnotation.Add(t);
		ObjectLabel o = tmpGo.AddComponent<ObjectLabel>();
		o.target = center;
		o.useMainCamera = false;
		o.cameraToUse = GameObject.Find("Camera").GetComponent<Camera>();
		GameObject.Find("Camera").GetComponent<ViewPoint3DMouse>().distance = 30;
		GameObject.Find("Camera").GetComponent<Orbit>().distance = 30;

		ResetAll();
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

	public void Lasso(List<Vector3> vertices, bool inside)
	{
		// pseudo-code for lasso
		// for vertex in lasso
		//    vscreen = camera.worldtoscreen(vertex)
		//    lassoScreen.add(vscreen)
		Vector2[] vertices2D = new Vector2[vertices.Count];
		for(int i = 0; i < vertices.Count; i++)
		{
			Vector3 screenPoint = GameObject.Find("Camera").camera.WorldToScreenPoint(vertices[i]);
			vertices2D[i] = new Vector2(screenPoint.x,screenPoint.y);
		}

		// for point in pointcloud
		//    pscreen = camera.worldtoscreen(point)
		//    if(wn_PnPoly(pscreen,lassoScreen,lassoScreen.count))
		//       point red
		//    else
		//       point blue
		Vector3 selectedCenter = new Vector3();
		int selectedCount = 0;

		for (int i = 0; i < vertexCount; ++i)
		{
			if(selected[i])
			{
				colors[i] = originalColors[i];
				Vector3 tmp = colors[i];
				Vector3 screenPoint = GameObject.Find("Camera").camera.WorldToScreenPoint(verts[i]);
				Vector2 point2D = new Vector2(screenPoint.x, screenPoint.y);
				int windings = wn_PnPoly(point2D,vertices2D,vertices.Count-1);
				if((windings == 0 && inside) || (windings > 0 && !inside)) 
				{
					tmp.x += 0.3F;
					tmp.y += 0.3F;
					tmp.z += 0.3F;
					selected[i] = false;
				}
				else
				{
					selectedCenter = selectedCenter + verts[i];
					selectedCount++;
				}
				colors[i] = tmp;
			}
		}

		if(selectedCount == 0)
		{
			ResetSelected();
			return;
		}
		
		selectedCenter = selectedCenter / selectedCount;
		
		min = new Vector3(float.MaxValue,float.MaxValue,float.MaxValue);
		max = new Vector3(float.MinValue,float.MinValue,float.MinValue);
		
		for (int i = 0; i < vertexCount ; i++) 
		{
			Vector3 tmp = verts[i];
			
			// offset for the center of the selected points in the point cloud to be at 0,0,0
			tmp.x -= selectedCenter.x;
			tmp.y -= selectedCenter.y;
			tmp.z -= selectedCenter.z;
			
			verts[i] = tmp;
			
			// calculate min/max of selected points
			if(selected[i])
			{
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
		
		size = new Vector3(max.x-min.x,max.y-min.y,max.z-min.z);
		
		GameObject.Find("Camera").GetComponent<ViewPoint3DMouse>().distance = 30 - (1.0F-((float)selectedCount/(float)vertexCount))*15.0F;
		GameObject.Find("Camera").GetComponent<Orbit>().distance = 30 - (1.0F-((float)selectedCount/(float)vertexCount))*15.0F;
		
		pointsUpdated = true;
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
