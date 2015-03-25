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
		
		pointCloudStrings = new string[] {
//			"../NoNormals/LongHornBeetle", 
//			"NoNormals/QCAT_N3_Zebedee_color", 
//			"NoNormals/logo", 
//			"NoNormals/210King_80k", 
//			"NoNormals/CSite1_80k", 
//			"NoNormals/CSite2_80k", 
//			"NoNormals/CSite3_80k", 
//			"NoNormals/CSite4_80k", 
//			"NoNormals/FSite5_80k", 
//			"NoNormals/FSite6_80k", 
//			"NoNormals/FSite7_80k", 
//			"NoNormals/FSite8_80k", 
//			"NoNormals/Site20_80k", 
//			"NoNormals/GB_80k", 
//			"NoNormals/Lobby_80k", 
			"LongHornBeetle", 
//			"210King", 
//			"CSite1", 
//			"CSite2", 
//			"CSite3", 
			"CSite4", 
//			"FSite5", 
			"FSite6", 
//			"FSite7", 
			"FSite8", 
			"Site20", 
			"Lobby",
			"Office2"
		};
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
				pointCloud.init(pointCloudStrings[selectedPointCloud]);
			}
		}
	}
}
