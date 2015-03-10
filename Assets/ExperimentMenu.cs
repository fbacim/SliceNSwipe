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
		
		pointCloudStrings = new string[] {"logo", 
			"LongHornBeetle", 
			"QCAT_N3_Zebedee_color", 
			"CSite1_80k", 
			"CSite2_80k", 
			"CSite3_80k", 
			"CSite4_80k", 
			"FSite5_80k", 
			"FSite6_80k", 
			"FSite7_80k", 
			"FSite8_80k", 
			"GB_80k", 
			"Lobby_80k"};
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	void OnGUI () {
		if(!init)
		{
			selectedTechnique  = GUI.SelectionGrid(new Rect (1.0F*Screen.width/4.0F-100.0F, Screen.height/2.0F-35*techniqueStrings.Length/2, 200, 35*techniqueStrings.Length),selectedTechnique, techniqueStrings, 1);
			selectedStrategy   = GUI.SelectionGrid(new Rect (2.0F*Screen.width/4.0F-100.0F, Screen.height/2.0F-35*strategyStrings.Length/2, 200, 35*strategyStrings.Length), selectedStrategy, strategyStrings, 1);
			selectedPointCloud = GUI.SelectionGrid(new Rect (3.0F*Screen.width/4.0F-100.0F, Screen.height/2.0F-35*pointCloudStrings.Length/2, 200, 35*pointCloudStrings.Length), selectedPointCloud, pointCloudStrings, 1);

			init = GUI.Button(new Rect(Screen.width/2.0F-100.0F, Screen.height/2.0F+35*techniqueStrings.Length/2 + 10, 200, 35*techniqueStrings.Length), "Start");

			if(init) {
				LeapController leapObject = GameObject.Find("Leap").GetComponent<LeapController>();
				leapObject.init(selectedTechnique, selectedStrategy);

				PointCloud pointCloud = GameObject.Find("Camera").GetComponent<PointCloud>();
				pointCloud.init(Application.dataPath+"/PointClouds/NoNormals/"+pointCloudStrings[selectedPointCloud]+".ply.withoutnormals.csv");
			}
		}
	}
}
