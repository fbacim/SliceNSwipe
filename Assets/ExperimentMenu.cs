using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ExperimentMenu : MonoBehaviour {
	bool init = false;

	int selectedTechnique = 0;
	int selectedStrategy = 0;
	int selectedPointCloud = 0;
	
	string[] techniqueStrings;
	string[] strategyStrings;
	string[] pointCloudStrings;

	// Use this for initialization
	void Start () {
		
		techniqueStrings  = new string[] {"Slice'n'Swipe", 
			"Volume Sweeper", 
			"Lasso"};
		
		strategyStrings   = new string[] {"Fast", 
			"Precise",  
			"Both"};
		
		pointCloudStrings = new string[] {"/logo.pointcloud.csv", 
			"/LongHornBeetle_PointCloud.pointcloud.csv", 
			"/QCAT_N3_Zebedee_color.pointcloud.csv"};
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	void OnGUI () {
		if(!init)
		{
			selectedTechnique  = 0;//GUI.SelectionGrid(new Rect (1.0F*Screen.width/4.0F-100.0F, Screen.height/2.0F-35*techniqueStrings.Length/2, 200, 35*techniqueStrings.Length),selectedTechnique, techniqueStrings, 1);
			selectedStrategy   = 1;//GUI.SelectionGrid(new Rect (2.0F*Screen.width/4.0F-100.0F, Screen.height/2.0F-35*strategyStrings.Length/2, 200, 35*strategyStrings.Length), selectedStrategy, strategyStrings, 1);
			selectedPointCloud = 1;//GUI.SelectionGrid(new Rect (3.0F*Screen.width/4.0F-100.0F, Screen.height/2.0F-35*pointCloudStrings.Length/2, 200, 35*pointCloudStrings.Length), selectedPointCloud, pointCloudStrings, 1);

			init = true;//GUI.Button(new Rect(Screen.width/2.0F-100.0F, Screen.height/2.0F+35*techniqueStrings.Length/2 + 10, 200, 35*techniqueStrings.Length), "Start");

			if(init) {
				LeapController leapObject = GameObject.Find("Leap").GetComponent<LeapController>();
				leapObject.init(selectedTechnique, selectedStrategy);

				PointCloud pointCloud = GameObject.Find("Camera").GetComponent<PointCloud>();
				pointCloud.init(Application.dataPath+pointCloudStrings[selectedPointCloud]);
			}
		}
	}
}
