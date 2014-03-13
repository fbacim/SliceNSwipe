using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class Point {
	public Vector3 xyz;
	public Color rgb;
	public float size;
	public bool selected;
	List<string> annotation;

	public Point(float x, float y, float z, float r, float g, float b, float size) {
		this.xyz = new Vector3(x,y,z);
		this.rgb = new Color(r,g,b,1F);
		this.size = size;
		this.selected = true;
		this.annotation = new List<string>();
	}

	public void AddAnnotation(string a) {
		annotation.Add(a);
	}
}

public class PointCloud : MonoBehaviour {
	List<Point> pointCloud;
	List<GUIText> goAnnotation;
	ArrayList particleSystemList;
	Mesh mesh;
	public ParticleSystem.Particle[] cloud;
	bool pointsUpdated = false;

	Vector3 min, max, size, oMin, oMax, oSize;
		
	// Use this for initialization
	void Start () 
	{
		/*GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position = new Vector3(0, 0.5F, 0);
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = new Vector3(0, 1.5F, 0);
        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.transform.position = new Vector3(2, 1, 0);
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.transform.position = new Vector3(-2, 1, 0);*/

		pointCloud = new List<Point>();
		goAnnotation = new List<GUIText>();

		//StreamReader reader = new StreamReader(File.OpenRead(Application.dataPath+@"/logo.pointcloud.csv"));
		StreamReader reader = new StreamReader(File.OpenRead(Application.dataPath+@"/LongHornBeetle_PointCloud.pointcloud.csv"));
		//StreamReader reader = new StreamReader(File.OpenRead(Application.dataPath+@"/QCAT_N3_Zebedee_color.pointcloud.csv"));
        reader.ReadLine(); // ignore first line
        Vector3 center = new Vector3(0,0,0); // calculate center of the object
		while (!reader.EndOfStream)
        {
            string line = reader.ReadLine();
            string[] values = line.Split(',');
			pointCloud.Add(new Point(float.Parse(values[0]),float.Parse(values[1]),float.Parse(values[2]),float.Parse(values[3]),float.Parse(values[4]),float.Parse(values[5]),1.0F));
			center = center + new Vector3(float.Parse(values[0]),float.Parse(values[1]),float.Parse(values[2]));
        }
		center = center / pointCloud.Count;
		Debug.Log("center: "+center.x+","+center.y+","+center.z);

		min = new Vector3(float.MaxValue,float.MaxValue,float.MaxValue);
		max = new Vector3(float.MinValue,float.MinValue,float.MinValue);

		for (int i = 0; i < pointCloud.Count ; i++) 
		{
			// offset for the center of the point cloud to be at 0,0,0
			pointCloud[i].xyz.x -= center.x;
			pointCloud[i].xyz.y -= center.y;
			pointCloud[i].xyz.z -= center.z;

			// calculate min/max
			if(pointCloud[i].xyz.x < min.x)
				min.x = pointCloud[i].xyz.x;
			if(pointCloud[i].xyz.y < min.y)
				min.y = pointCloud[i].xyz.y;
			if(pointCloud[i].xyz.z < min.z)
				min.z = pointCloud[i].xyz.z;
			
			if(pointCloud[i].xyz.x > max.x)
				max.x = pointCloud[i].xyz.x;
			if(pointCloud[i].xyz.y > max.y)
				max.y = pointCloud[i].xyz.y;
			if(pointCloud[i].xyz.z > max.z)
				max.z = pointCloud[i].xyz.z;
		}
		
		size = new Vector3(max.x-min.x,max.y-min.y,max.z-min.z);

		oMin = new Vector3(min.x, min.y, min.z);
		oMax = new Vector3(max.x, max.y, max.z);
		oSize = new Vector3(size.x, size.y, size.z);
		
		cloud = new ParticleSystem.Particle[pointCloud.Count];
		
		for (int i = 0; i < pointCloud.Count; ++i)
		{
			cloud[i].position = pointCloud[i].xyz;
			cloud[i].color = pointCloud[i].rgb;
			cloud[i].size = 100.0F; // make them huge, use maximum particle size to control their size
		}

		pointsUpdated = true;
	}

	// Update is called once per frame
	void Update () 
	{
		if (pointsUpdated)
		{
			particleSystem.SetParticles(cloud, cloud.Length);
			pointsUpdated = false;
		}
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
		
		for (int i = 0; i < pointCloud.Count; ++i)
		{
			if(pointCloud[i].selected)
			{
				cloud[i].color = pointCloud[i].rgb;
				Color tmp = cloud[i].color;
				Vector3 screenPoint = GameObject.Find("Camera").camera.WorldToScreenPoint(cloud[i].position);
				Vector2 point2D = new Vector2(screenPoint.x, screenPoint.y);
				if(wn_PnPoly(point2D,vertices2D,vertices.Count-1) == 0) // outside
				{
					tmp.r += 0.5F;
				}
				else
				{
					tmp.b += 0.5F;
				}
				cloud[i].color = tmp;
			}
		}

		pointsUpdated = true;
	}
	
	public void SetSphere(Vector3 center, float radius)
	{

		for (int i = 0; i < pointCloud.Count; ++i)
		{
			if(pointCloud[i].selected)
			{
				cloud[i].color = pointCloud[i].rgb;
				Color tmp = cloud[i].color;
				if(Vector3.Distance(center,cloud[i].position) > radius)
				{
					tmp.r += 0.5F;
				}
				else
				{
					tmp.b += 0.5F;
				}
				cloud[i].color = tmp;
			}
		}
		
		pointsUpdated = true;
	}
	
	public void SetSphereTrail(List<Sphere> spheres)
	{
		for (int i = 0; i < pointCloud.Count; ++i)
		{
			if(pointCloud[i].selected)
			{
				cloud[i].color = pointCloud[i].rgb;
				Color tmp = cloud[i].color;
				bool inside = false;
				for(int j = 0; j < spheres.Count; j++)
				{
					if(Vector3.Distance(spheres[j].center,cloud[i].position) < spheres[j].radius)
					{
						tmp.b += 0.5F;
						inside = true;
						break;
					}
				}
				if(!inside)
				{
					tmp.r += 0.5F;
				}
				cloud[i].color = tmp;
			}
		}
		
		pointsUpdated = true;
	}

	public void SetSelectionPlane(Plane plane)
	{
		for (int i = 0; i < pointCloud.Count; ++i)
		{
			if(pointCloud[i].selected)
			{
				cloud[i].color = pointCloud[i].rgb;
				Color tmp = cloud[i].color;
				if(plane.GetSide(cloud[i].position))
				{
					tmp.r += 0.5F;
				}
				else
				{
					tmp.b += 0.5F;
				}
				cloud[i].color = tmp;
			}
		}

		pointsUpdated = true;
	}

	public void SelectSide(Plane plane, bool side) // side is true when we want points in the same side as normal
	{
		Vector3 selectedCenter = new Vector3();
		int selectedCount = 0;
		for (int i = 0; i < pointCloud.Count; ++i)
		{
			if(pointCloud[i].selected)
			{
				cloud[i].color = pointCloud[i].rgb;
				Color tmp = cloud[i].color;
				if(plane.GetSide(cloud[i].position) == side)
				{
					tmp.r += 0.3F;
					tmp.g += 0.3F;
					tmp.b += 0.3F;
					tmp.a = 0.1F;
					pointCloud[i].selected = false;
				}
				else
				{
					selectedCenter = selectedCenter + cloud[i].position;
					selectedCount++;
				}
				cloud[i].color = tmp;
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

		for (int i = 0; i < pointCloud.Count ; i++) 
		{
			Vector3 tmp = cloud[i].position;

			// offset for the center of the selected points in the point cloud to be at 0,0,0
			tmp.x -= selectedCenter.x;
			tmp.y -= selectedCenter.y;
			tmp.z -= selectedCenter.z;
			
			cloud[i].position = tmp;

			// calculate min/max of selected points
			if(pointCloud[i].selected)
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

		GameObject.Find("Camera").GetComponent<ViewPoint3DMouse>().distance = 30 - (1.0F-((float)selectedCount/(float)pointCloud.Count))*15.0F;
		GameObject.Find("Camera").GetComponent<Orbit>().distance = 30 - (1.0F-((float)selectedCount/(float)pointCloud.Count))*15.0F;
		
		pointsUpdated = true;
	}

	public void SelectSphere(Vector3 center, float radius, bool inside)
	{
		Vector3 selectedCenter = new Vector3();
		int selectedCount = 0;
		for (int i = 0; i < pointCloud.Count; ++i)
		{
			if(pointCloud[i].selected)
			{
				cloud[i].color = pointCloud[i].rgb;
				Color tmp = cloud[i].color;
				if((Vector3.Distance(center,cloud[i].position) > radius && inside) || (Vector3.Distance(center,cloud[i].position) < radius && !inside))
				{
					tmp.r += 0.3F;
					tmp.g += 0.3F;
					tmp.b += 0.3F;
					tmp.a = 0.1F;
					pointCloud[i].selected = false;
				}
				else
				{
					selectedCenter = selectedCenter + cloud[i].position;
					selectedCount++;
				}
				cloud[i].color = tmp;
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
		
		for (int i = 0; i < pointCloud.Count ; i++) 
		{
			Vector3 tmp = cloud[i].position;
			
			// offset for the center of the selected points in the point cloud to be at 0,0,0
			tmp.x -= selectedCenter.x;
			tmp.y -= selectedCenter.y;
			tmp.z -= selectedCenter.z;
			
			cloud[i].position = tmp;
			
			// calculate min/max of selected points
			if(pointCloud[i].selected)
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
		
		GameObject.Find("Camera").GetComponent<ViewPoint3DMouse>().distance = 30 - (1.0F-((float)selectedCount/(float)pointCloud.Count))*15.0F;
		GameObject.Find("Camera").GetComponent<Orbit>().distance = 30 - (1.0F-((float)selectedCount/(float)pointCloud.Count))*15.0F;

		pointsUpdated = true;
	}

	public void SelectSphereTrail(List<Sphere> spheres, bool inside)
	{
		Vector3 selectedCenter = new Vector3();
		int selectedCount = 0;

		for (int i = 0; i < pointCloud.Count; ++i)
		{
			if(pointCloud[i].selected)
			{
				cloud[i].color = pointCloud[i].rgb;
				Color tmp = cloud[i].color;
				bool isInside = false;
				for(int j = 0; j < spheres.Count; j++)
				{
					if(Vector3.Distance(spheres[j].center,cloud[i].position) < spheres[j].radius)
					{
						isInside = true;
						break;
					}
				}
				if((!isInside && inside) || (isInside && !inside))
				{
					tmp.r += 0.3F;
					tmp.g += 0.3F;
					tmp.b += 0.3F;
					tmp.a = 0.1F;
					pointCloud[i].selected = false;
				}
				else
				{
					selectedCenter = selectedCenter + cloud[i].position;
					selectedCount++;
				}
				cloud[i].color = tmp;
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
		
		for (int i = 0; i < pointCloud.Count ; i++) 
		{
			Vector3 tmp = cloud[i].position;
			
			// offset for the center of the selected points in the point cloud to be at 0,0,0
			tmp.x -= selectedCenter.x;
			tmp.y -= selectedCenter.y;
			tmp.z -= selectedCenter.z;
			
			cloud[i].position = tmp;
			
			// calculate min/max of selected points
			if(pointCloud[i].selected)
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
		
		GameObject.Find("Camera").GetComponent<ViewPoint3DMouse>().distance = 30 - (1.0F-((float)selectedCount/(float)pointCloud.Count))*15.0F;
		GameObject.Find("Camera").GetComponent<Orbit>().distance = 30 - (1.0F-((float)selectedCount/(float)pointCloud.Count))*15.0F;
		
		pointsUpdated = true;
	}

	public void ResetSelected()
	{
		Debug.Log("ResetSelected");

		for (int ii = 0; ii < pointCloud.Count; ++ii)
		{
			if(pointCloud[ii].selected)
				cloud[ii].color = pointCloud[ii].rgb;
		}
		
		pointsUpdated = true;
	}
	
	public void ResetAll()
	{
		for (int ii = 0; ii < pointCloud.Count; ++ii)
		{
			cloud[ii].position = pointCloud[ii].xyz;
			cloud[ii].color = pointCloud[ii].rgb;
			pointCloud[ii].selected = true;
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
		for (int i = 0; i < pointCloud.Count; ++i)
		{
			if(pointCloud[i].selected)
			{
				pointCloud[i].AddAnnotation(annotation);
				center = center + pointCloud[i].xyz;
				selectedCount++;
			}
		}
		center = center / selectedCount;

		GameObject tmpGo = new GameObject("Annotation");
		GUIText t = tmpGo.AddComponent<GUIText>();
		t.text = annotation;
		t.fontSize = (int)(15.0F+(selectedCount/pointCloud.Count)*15.0F);
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

		for (int i = 0; i < pointCloud.Count; ++i)
		{
			if(pointCloud[i].selected)
			{
				cloud[i].color = pointCloud[i].rgb;
				Color tmp = cloud[i].color;
				Vector3 screenPoint = GameObject.Find("Camera").camera.WorldToScreenPoint(cloud[i].position);
				Vector2 point2D = new Vector2(screenPoint.x, screenPoint.y);
				int windings = wn_PnPoly(point2D,vertices2D,vertices.Count-1);
				if((windings == 0 && inside) || (windings > 0 && !inside)) 
				{
					tmp.r += 0.3F;
					tmp.g += 0.3F;
					tmp.b += 0.3F;
					tmp.a = 0.1F;
					pointCloud[i].selected = false;
				}
				else
				{
					selectedCenter = selectedCenter + cloud[i].position;
					selectedCount++;
				}
				cloud[i].color = tmp;
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
		
		for (int i = 0; i < pointCloud.Count ; i++) 
		{
			Vector3 tmp = cloud[i].position;
			
			// offset for the center of the selected points in the point cloud to be at 0,0,0
			tmp.x -= selectedCenter.x;
			tmp.y -= selectedCenter.y;
			tmp.z -= selectedCenter.z;
			
			cloud[i].position = tmp;
			
			// calculate min/max of selected points
			if(pointCloud[i].selected)
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
		
		GameObject.Find("Camera").GetComponent<ViewPoint3DMouse>().distance = 30 - (1.0F-((float)selectedCount/(float)pointCloud.Count))*15.0F;
		GameObject.Find("Camera").GetComponent<Orbit>().distance = 30 - (1.0F-((float)selectedCount/(float)pointCloud.Count))*15.0F;
		
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
