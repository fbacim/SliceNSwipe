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
	public enum CoordinateSystem { CameraMode, ObjectMode, GrabMode, CameraInCenter, BoundaryMode, RayCasting }
	private static CoordinateSystem _coordSystem;
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
	public PointCloud pointCloud;

	// Use this for initialization
	void Start () {
		_center = new Vector3 (0, 0, 0);
		//_coordSystem = CoordinateSystem.ObjectMode;
		_coordSystem = CoordinateSystem.RayCasting;
		
		rayCenter = _center;
		sphereC = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		sphereC.transform.renderer.material.color = new Color(256, 256, 1);
		
		sphereR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		sphereR.transform.renderer.material.color = new Color(256, 1, 256);
		
		realtimeCasting = false;
		showSphere = true;
		
		closestPoint = new Vector3(0,0,0);
		
		stopWatch = new Stopwatch();
		stopWatch.Start();
	}
	
	// Update is called once per frame
	void Update () {
		if (Input.GetKeyDown (KeyCode.C)){
			_coordSystem = CoordinateSystem.CameraMode;
		}
		if (Input.GetKeyDown (KeyCode.O)){
			_coordSystem = CoordinateSystem.ObjectMode;
		}
		if (Input.GetKeyDown (KeyCode.V)){
			_coordSystem = CoordinateSystem.CameraInCenter;
		}
		if (Input.GetKeyDown (KeyCode.B)){
			_coordSystem = CoordinateSystem.BoundaryMode;
		}
		if (Input.GetKeyDown (KeyCode.R)){
			_coordSystem = CoordinateSystem.RayCasting;
		}
		if (Input.GetKeyDown (KeyCode.T)){
			realtimeCasting = !realtimeCasting;
		}
		if ((Input.GetKeyDown (KeyCode.S)) || (Input.GetKeyDown (KeyCode.LeftControl))|| (Input.GetKeyDown (KeyCode.RightControl))){
			showSphere = !showSphere;
		}
		
		
		if (_coordSystem == CoordinateSystem.RayCasting) {
			Camera.current.transform.RotateAround (rayCenter, -1*Camera.current.transform.up, SpaceNavigator.Rotation.Yaw () 
			                                       * Mathf.Rad2Deg * yRotationCoef*100);
			Camera.current.transform.RotateAround (rayCenter, -1*Camera.current.transform.right, SpaceNavigator.Rotation.Pitch () 
			                                       * Mathf.Rad2Deg * xRotationCoef*100);
			Camera.current.transform.RotateAround (rayCenter, -1*Camera.current.transform.forward, SpaceNavigator.Rotation.Roll () 
			                                       * Mathf.Rad2Deg * zRotationCoef*100);
			
			Camera.current.transform.Translate (new Vector3 (-SpaceNavigator.Translation.x * xTranslationCoef ,
			                                                 -SpaceNavigator.Translation.y * yTranslationCoef ,
			                                                 -SpaceNavigator.Translation.z * zTranslationCoef ));
			
			sphereR.transform.position = Camera.current.transform.position+ Camera.current.transform.forward*50;
			
			
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
					Transform cam  = Camera.current.transform;
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
					
					for (int ii = 0; ii < pointCloud.cloud.Length; ++ii) //ii+=10)
					{
						distance = Vector3.Cross(ray.direction, pointCloud.cloud[ii].position - ray.origin).magnitude;
						if (distance < minDistance)
						{
							minDistance = distance;
							closestPoint = pointCloud.cloud[ii].position;
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
		else if (_coordSystem == CoordinateSystem.CameraMode) {
			Camera.current.transform.Translate (new Vector3 (SpaceNavigator.Translation.x,
			                                                 SpaceNavigator.Translation.y,
			                                                 SpaceNavigator.Translation.z));
			
			Camera.current.transform.RotateAround (Camera.current.transform.up, SpaceNavigator.Rotation.Yaw () 
			                                       * Mathf.Rad2Deg * yRotationCoef);
			Camera.current.transform.RotateAround (Camera.current.transform.right, SpaceNavigator.Rotation.Pitch () 
			                                       * Mathf.Rad2Deg * xRotationCoef);
			Camera.current.transform.RotateAround (Camera.current.transform.forward, SpaceNavigator.Rotation.Roll () 
			                                       * Mathf.Rad2Deg * zRotationCoef);
			
		} 
		else if (_coordSystem == CoordinateSystem.CameraInCenter) {
			
			Camera.current.transform.Translate (new Vector3 (-SpaceNavigator.Translation.x * xTranslationCoef ,
			                                                 -SpaceNavigator.Translation.y * yTranslationCoef ,
			                                                 -SpaceNavigator.Translation.z * zTranslationCoef ));
			
			Camera.current.transform.RotateAround (Camera.current.transform.position, -1*Camera.current.transform.up, SpaceNavigator.Rotation.Yaw () 
			                                       * Mathf.Rad2Deg * yRotationCoef*100);
			Camera.current.transform.RotateAround (Camera.current.transform.position, -1*Camera.current.transform.right, SpaceNavigator.Rotation.Pitch () 
			                                       * Mathf.Rad2Deg * xRotationCoef*100);
			Camera.current.transform.RotateAround (Camera.current.transform.position, -1*Camera.current.transform.forward, SpaceNavigator.Rotation.Roll () 
			                                       * Mathf.Rad2Deg * zRotationCoef*100);
		}
		
		else if (_coordSystem == CoordinateSystem.ObjectMode) {
			Camera.current.transform.RotateAround (_center, -1*Camera.current.transform.up, SpaceNavigator.Rotation.Yaw () 
			                                       * Mathf.Rad2Deg * yRotationCoef*100);
			Camera.current.transform.RotateAround (_center, -1*Camera.current.transform.right, SpaceNavigator.Rotation.Pitch () 
			                                       * Mathf.Rad2Deg * xRotationCoef*100);
			Camera.current.transform.RotateAround (_center, -1*Camera.current.transform.forward, SpaceNavigator.Rotation.Roll () 
			                                       * Mathf.Rad2Deg * zRotationCoef*100);
			
			Camera.current.transform.Translate (new Vector3 (-SpaceNavigator.Translation.x * xTranslationCoef ,
			                                                 -SpaceNavigator.Translation.y * yTranslationCoef ,
			                                                 -SpaceNavigator.Translation.z * zTranslationCoef ));
			
		}
	}
}
