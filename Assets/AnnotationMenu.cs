using UnityEngine;
using System.Collections;

public class AnnotationMenu : MonoBehaviour {
	// Array of menu item control names.
	string[] menuOptions ;
	
	int menuLength;
	
	PointCloud pointCloud;	
	
	public bool menuOn;
	float menuLocationSN;
	const float menuLocationThersholdSN = 1;
	// selected menu item
	int selectedIndex;
	
	int firstVisibleIndex = 0;

	float timeLastChange = 0.0F;
	
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
		menuLocationSN = 0;
		menuLength = 20;
		selectedIndex = 0;
	}
	
	// Update is called once per frame
	void Update () {
		float currentTime = Time.timeSinceLevelLoad;
		float timeSinceLastChange = currentTime - timeLastChange;
		
		if (Input.GetKeyDown (KeyCode.Tab)) {
			menuOn = !menuOn;
			if (menuOn){
				menuOptions = new string[pointCloud.annotations.Count + 1];
				
				menuOptions[0] = "Select All";		
				for (int i=1; i<= pointCloud.annotations.Count; i++) {
					menuOptions[i] = pointCloud.annotations[i-1].ToString();
				}
			}
		}
		
		if(!menuOn)
			return;
		
		menuLocationSN = SpaceNavigator.Translation.z + SpaceNavigator.Rotation.Pitch() * 10;

		Debug.Log(""+SpaceNavigator.Translation.y+"  "+Mathf.Abs( SpaceNavigator.Translation.z)+"  "+Mathf.Abs( SpaceNavigator.Rotation.Pitch()));

		if (SpaceNavigator.Translation.y < -1.0 && timeSinceLastChange > 0.3) {
			pointCloud.SelectAnnotation(selectedIndex-1);
			menuOn = false;
		}
		else if (menuLocationSN < -menuLocationThersholdSN && timeSinceLastChange > 0.3) {
			selectedIndex = menuSelection(menuOptions, selectedIndex, "down");
			menuLocationSN = 0;
			timeLastChange = currentTime;
		}
		else if (menuLocationSN > menuLocationThersholdSN && timeSinceLastChange > 0.3) {
			selectedIndex = menuSelection(menuOptions, selectedIndex, "up");
			menuLocationSN = 0;
			timeLastChange = currentTime;
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
				GUIStyle customstyle = new GUIStyle();
				customstyle.focused.textColor = Color.red;
				
				if (i == selectedIndex)
				{
					GUIStyle buttonStyle;
					buttonStyle = new GUIStyle(GUI.skin.button);
					
					buttonStyle.fontSize = 20;
					buttonStyle.fontStyle = FontStyle.Bold;
					buttonStyle.hover.textColor = Color.green;
					
					GUI.Button (new Rect (20, 5 + (i-firstVisibleIndex) * 30, 20 + menuLength * 9, 30), menuOptions [i], buttonStyle);
				}
				else
				{
					GUI.Button (new Rect (5, 5 + (i-firstVisibleIndex) * 30, 20 + menuLength * 9, 30), menuOptions [i]);
				}
			}

			GUI.FocusControl (menuOptions [selectedIndex]);
		}	
	}
}
