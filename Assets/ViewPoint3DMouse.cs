﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using System.Diagnostics;
using System.Threading;

public class ViewPoint3DMouse : MonoBehaviour {
	const float xRotationCoef = 0.008f;
	const float yRotationCoef = 0.008f;
	const float zRotationCoef = 0.008f;
	
	const float xTranslationCoef = 1.0f;
	const float yTranslationCoef = 1.0f;
	const float zTranslationCoef = 1.0f;
	
	//boundaries
	const float xMax = 22; const float xMin = -22;
	const float yMax = 10; const float yMin = -8;
	const float zMax = 0; const float zMin = -50;
	
	//Translation and rotation thresholds
	const float translationThreshold = 0.001f;
	const float rotationThreshold = 0.001f;
	Vector3 rayCenter;
	
	//Coordinate system
	Vector3 _center;
	
	// Crosshair
	///////////////////////////////////////////////////////////
	///////////////////////////////////////////////////////////
	public Texture2D centerTexture, topTexture, bottomTexture, leftTexture, rightTexture;
	public float width = 25.0f;
	public float height = 25.0f;
	public float spacing = 4.0f;
	Color color;
	
	//Raycasting sphere
	/////////////////////////////////////////////////////////////
	GameObject sphereC;
	GameObject sphereR;
	Vector3 closestPoint;
	
	bool realtimeCasting;
	bool showSphere;
	
	/////////////////////////////////////////////////////////// 
	/// Stopwatch
	Stopwatch stopWatch;
	const double timeDelayThershould = 2000;

	public float distance = 35;
	PointCloud pointCloud;

	bool needRecenter = false;

	// camera animation variables
	public float totalAnimationTime = 0.25F;
	float animationStartTime;
	Vector3 initialPosition;
	Vector3 initialCenter;
	Vector3 positionOffset;
	Vector3 centerOffset;

	// Use this for initialization
	void Start () {
		_center = new Vector3 (0, 0, 0);

		positionOffset = new Vector3();
		
		rayCenter = _center;
		sphereC = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		sphereC.transform.renderer.material.color = new Color(256, 256, 1);
		
		sphereR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		sphereR.transform.renderer.material.color = new Color(256, 1, 256);

		initialPosition = new Vector3();
		initialCenter = new Vector3();
		
		realtimeCasting = false;
		showSphere = false;
		
		closestPoint = new Vector3(0,0,0);
		
		stopWatch = new Stopwatch();
		stopWatch.Start();
		
		pointCloud = GameObject.Find("Camera").GetComponent<PointCloud>();
	}

	public void CenterView(float d, Vector3 cameraOffset) {
		distance = d;
		
		initialPosition = camera.transform.position-cameraOffset;
		initialCenter = camera.transform.position+camera.transform.forward*distance-cameraOffset;

		// set distance
		Quaternion rotation = camera.transform.rotation;
		Vector3 position = rotation * (new Vector3(0.0F, 0.0F, -distance)) + _center;
		positionOffset = position;

		animationStartTime = 0.0F;
		needRecenter = true;
	}
	
	// Update is called once per frame
	void Update () {
		if(needRecenter)
		{
			float currentTime = Time.timeSinceLevelLoad;

			// if starting new animation, save transform
			if(animationStartTime == 0.0F)
			{
				camera.transform.LookAt(new Vector3(0,0,0));
				animationStartTime = currentTime;
				
				//print ("position:"+initialPosition+positionOffset);
				//print ("center:"+initialCenter+_center);
			}


			float t = (currentTime-animationStartTime)/totalAnimationTime;

			// end of animation
			if(t >= 1.0F)
			{
				t = 1.0F;
				closestPoint = new Vector3(0,0,0);
				needRecenter = false;
			}


			camera.transform.position = Vector3.Lerp(initialPosition,positionOffset,t);
			Vector3 lookat = Vector3.Lerp(initialCenter,_center,t);
			camera.transform.LookAt(lookat);
			//print("("+t+") "+camera.transform.position);
		}
		else if(!GameObject.Find("Camera").GetComponent<AnnotationMenu>().menuOn)
		{
			//print(camera.transform.position);
			camera.transform.RotateAround (rayCenter, -1*camera.transform.up, SpaceNavigator.Rotation.Yaw () * Mathf.Rad2Deg * yRotationCoef*100);
			camera.transform.RotateAround (rayCenter, -1*camera.transform.right, SpaceNavigator.Rotation.Pitch () * Mathf.Rad2Deg * xRotationCoef*100);
			camera.transform.RotateAround (rayCenter, -1*camera.transform.forward, SpaceNavigator.Rotation.Roll () * Mathf.Rad2Deg * zRotationCoef*100);

			// get the offset from 3d mouse + whatever was added to it 
			camera.transform.Translate(-SpaceNavigator.Translation.x * xTranslationCoef,
									   -SpaceNavigator.Translation.y * yTranslationCoef,
			                           -SpaceNavigator.Translation.z * zTranslationCoef);
		}

		sphereR.transform.position = camera.transform.position + camera.transform.forward*50;
		
		
		TimeSpan ts = stopWatch.Elapsed;
		//UnityEngine.Debug.Log (ts);
		//UnityEngine.Debug.Log (ts.TotalMilliseconds);
		
		if (realtimeCasting || 
			((Mathf.Abs(SpaceNavigator.Translation.x)<= translationThreshold) &&  
			 (Mathf.Abs(SpaceNavigator.Translation.y)<= translationThreshold) &&  
			 (Mathf.Abs(SpaceNavigator.Translation.z)<= translationThreshold) &&  
			 (Mathf.Abs(SpaceNavigator.Rotation.Pitch())<= rotationThreshold) &&  
			 (Mathf.Abs(SpaceNavigator.Rotation.Yaw())<= rotationThreshold) &&  
			 (Mathf.Abs(SpaceNavigator.Rotation.Roll())<= rotationThreshold)))
		{
			if (ts.TotalMilliseconds > timeDelayThershould)
			{
				Transform cam  = camera.transform;
				Ray ray = new Ray(cam.position, cam.forward);
				//Debug.DrawRay (ray.origin, ray.direction *  50, Color.yellow);
				
				RaycastHit hit ;
				if( Physics.Raycast (ray, out hit, 50)	)
				{
					//UnityEngine.Debug.Log (hit.collider.name);
					//UnityEngine.Debug.Log (hit.collider.transform.position.ToString());
					//UnityEngine.Debug.Log (hit.distance);
				}
				
				float minDistance = Mathf.Infinity;
				float distance = 0;
				
				for (int i = 0; i < pointCloud.vertexCount; ++i) //ii+=10)
				{
					distance = Vector3.Cross(ray.direction, pointCloud.verts[i] - ray.origin).magnitude;
					if (distance < minDistance)
					{
						minDistance = distance;
						closestPoint = pointCloud.verts[i];
					}
				}
				rayCenter = closestPoint;
				stopWatch.Reset();
				stopWatch.Start();
			}
			
			if (showSphere){
				sphereC.renderer.enabled = true;
				sphereR.renderer.enabled = true;
				sphereC.transform.position = closestPoint;
			}
			else 
			{
				sphereC.renderer.enabled = false;
				sphereR.renderer.enabled = false;
			}
			
		}
	}
}
