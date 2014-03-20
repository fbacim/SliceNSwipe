using UnityEngine;
using System.Collections;

public class AnnotationMenu : MonoBehaviour {
	
	// Array of menu item control names.
	string[] menuOptions ;

	int menuLength;

	PointCloud pointCloud;
	

	bool menuOn;
	float menuLocationSN;
	const float menuLocationThersholdSN = 10;
	// selected menu item
	public int selectedIndex;
	
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

		menuLength = 0;

		for (int i=0; i<menuOptions.Length; i++) {
			if (menuOptions[i].Length > menuLength)
				menuLength = menuOptions[i].Length;
		}

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
				
				menuLength = 0;
				
				for (int i=0; i<menuOptions.Length; i++) {
					if (menuOptions[i].Length > menuLength)
						menuLength = menuOptions[i].Length;
				}
				
				//if (menuLength > 40)
					menuLength = 20;
			}

		}

		menuLocationSN += SpaceNavigator.Translation.z;
		menuLocationSN += SpaceNavigator.Rotation.Pitch() * 10;

		if (menuLocationSN < -menuLocationThersholdSN) {
			selectedIndex = menuSelection(menuOptions, selectedIndex, "down");
			menuLocationSN = 0;
		}
		if (menuLocationSN > menuLocationThersholdSN) {
			selectedIndex = menuSelection(menuOptions, selectedIndex, "up");
			menuLocationSN = 0;
		}

		if (SpaceNavigator.Translation.y < -0.2) {
			selectedAnnotation(selectedIndex);	
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
				for (int i=0; i<menuOptions.Length; i++) {
		
					GUI.SetNextControlName (menuOptions [i]);
					//GUI.skin.button.focused = Color.red;
					GUI.skin.button.focused.textColor = Color.yellow;
					GUIStyle style = new GUIStyle();

					if (GUI.Button (new Rect (5, 5 + i * 30, 20 + menuLength * 9, 30), menuOptions [i])) {
						selectedAnnotation(i);
				}		
			}
		

			GUI.FocusControl (menuOptions [selectedIndex]);
		}
		
	}

	void selectedAnnotation(int selectedItem){
		for (int j=0; j<100; j++)
			print (selectedItem);
	}

}
