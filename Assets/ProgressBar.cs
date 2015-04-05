using UnityEngine;
using System.Collections;

public class ProgressBar : MonoBehaviour {
	public float scale = 0.0f;
	public RectTransform rectTransform;
	
	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		if(float.IsNaN(scale)) 
			scale = 0.0f;
		rectTransform.localScale = new Vector3(scale,rectTransform.localScale.y,rectTransform.localScale.z);
	}
}
