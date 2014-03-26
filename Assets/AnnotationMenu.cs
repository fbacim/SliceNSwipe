using UnityEngine;
using System.Collections;

public class AnnotationMenu : MonoBehaviour {
	
	// Array of menu item control names.
	string[] menuOptions ;
	
	int menuLength;
	
	PointCloud pointCloud;
	
	
	public bool menuOn;
	float menuLocationSN;
	const float menuLocationThersholdSN = 15;
	// selected menu item
	int selectedIndex;
	
	int firstVisibleIndex = 0;
	
	// Function to scroll through possible menu items array, looping back to start/end depending on direction of movement.
	
	int menuSelection (string[] menuItems, int selectedItem, string direction) {
		
		if (direction == "up") {
			
			if (selectedItem == 0) {
				selectedItem = menuItems.Length - 1;
			} else {
				selectedItem -= 1;
			}		
		}
		
		if (direction == "down") {
			
			if (selectedItem == menuItems.Length - 1) {
				selectedItem = 0;
			} 
			else {
				selectedItem += 1;
			}
		}
		return selectedItem;
	}
	
	
	void Start () {
		
		pointCloud = GameObject.Find("Camera").GetComponent<PointCloud>();
		
		menuOn = false;
		/*menuOptions = new string[pointCloud.annotations.Count + 1];

		menuOptions[0] = "All";		
		for (int i=1; i<= pointCloud.annotations.Count; i++) {
			menuOptions[i] = pointCloud.annotations[i-1].ToString();		
		}



		menuOptions = new string[4];
		menuOptions[0] = "annot 1";
		menuOptions[1] = "ann 2";
		menuOptions[2] = "annotation 1";
		menuOptions[3] = "verrrrrrryyyyyyyy llllllllooooooooonggggggggggg annnnnnnnotation 4";
*/
		
		menuLocationSN = 0;
		/*
		menuLength = 0;

		for (int i=0; i<menuOptions.Length; i++) {
			if (menuOptions[i].Length > menuLength)
				menuLength = menuOptions[i].Length;
		}
		*/
		//if (menuLength > 20)
		menuLength = 20;
		
		selectedIndex = 0;
	}
	
	// Update is called once per frame
	void Update () {
		
		//Debug.Log (pointCloud.annotations.Count);
		
		//if (menuOn)
		//OnGUI ();
		if (Input.GetKeyDown (KeyCode.Tab)) {
			
			menuOn = !menuOn;
			if (menuOn){
				menuOptions = new string[pointCloud.annotations.Count + 1];
				
				menuOptions[0] = "All";		
				for (int i=1; i<= pointCloud.annotations.Count; i++) {
					menuOptions[i] = pointCloud.annotations[i-1].ToString();
				}
				
				/*menuLength = 0;
				
				for (int i=0; i<menuOptions.Length; i++) {
					if (menuOptions[i].Length > menuLength)
						menuLength = menuOptions[i].Length;
				}
				
				//if (menuLength > 40)
					menuLength = 20;*/
			}
			
		}
		
		if(!menuOn)
			return;
		
		menuLocationSN += SpaceNavigator.Translation.z;
		menuLocationSN += SpaceNavigator.Rotation.Pitch() * 10;
		
		if (menuLocationSN < -menuLocationThersholdSN) {
			selectedIndex = menuSelection(menuOptions, selectedIndex, "down");
			menuLocationSN = 0;
		}
		else if (menuLocationSN > menuLocationThersholdSN) {
			selectedIndex = menuSelection(menuOptions, selectedIndex, "up");
			menuLocationSN = 0;
		}
		else if ((SpaceNavigator.Translation.y < -0.2)&& 
		         (Mathf.Abs( SpaceNavigator.Translation.z)<0.5 )&& 
		         (Mathf.Abs( SpaceNavigator.Rotation.Pitch())<0.2 )) {
			pointCloud.SelectAnnotation(selectedIndex-1);
		}
		
		if (Input.GetKeyDown (KeyCode.DownArrow)) {
			selectedIndex = menuSelection(menuOptions, selectedIndex, "down");
		}
		
		if (Input.GetKeyDown (KeyCode.UpArrow)) {
			selectedIndex = menuSelection(menuOptions, selectedIndex, "up");
		}	
	}
	
	void OnGUI ()
	{
		if (menuOn) {
			
			int screenHeight = Screen.height;
			int visibleMenuItems = (screenHeight-5)/30;
			
			if ((selectedIndex - firstVisibleIndex) >= visibleMenuItems){
				//firstVisibleIndex += (selectedIndex - firstVisibleIndex) - (visibleMenuItems-1);
				firstVisibleIndex += 1;
			}
			if (selectedIndex < firstVisibleIndex){
				//firstVisibleIndex = firstVisibleIndex - selectedIndex;
				firstVisibleIndex -=1;
			}
			
			
			for (int i=0+firstVisibleIndex; i<menuOptions.Length; i++) {
				GUI.SetNextControlName (menuOptions [i]);
				//GUI.skin.button.focused = Color.red;
				//GUI.skin.button.focused.textColor = Color.yellow;
				//GUIStyle style = new GUIStyle(
				GUIStyle customstyle = new GUIStyle();
				customstyle.focused.textColor = Color.red;
				
				if (i == selectedIndex)
				{
					
					//GUIStyle style = new GUIStyle(GUI.Button);
					//style.fontSize = 24;
					
					GUIStyle buttonStyle;
					buttonStyle = new GUIStyle(GUI.skin.button);
					
					buttonStyle.fontSize = 20;
					buttonStyle.fontStyle = FontStyle.Bold;
					//buttonStyle.normal.textColor = Color.red;
					//buttonStyle.focused.textColor = Color.yellow;
					buttonStyle.hover.textColor = Color.green;
					//buttonStyle.active.textColor = Color.blue;
					
					//buttonStyle.onNormal.textColor = Color.black;
					//buttonStyle.onFocused.textColor = Color.cyan;
					//buttonStyle.onHover.textColor = Color.magenta;
					//buttonStyle.onActive.textColor = Color.white;
					
					GUI.Button (new Rect (20, 5 + (i-firstVisibleIndex) * 30, 20 + menuLength * 9, 30), menuOptions [i], buttonStyle);
					
				}
				else
					GUI.Button (new Rect (5, 5 + (i-firstVisibleIndex) * 30, 20 + menuLength * 9, 30), menuOptions [i]);
			}
			
			
			GUI.FocusControl (menuOptions [selectedIndex]);
		}	
	}
}
