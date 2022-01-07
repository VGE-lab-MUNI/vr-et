// --------------------------------------------------------------------------------------------------------------------
// Heatmap Visualizer script, version 2022-01-06
// This script processes loaded CSV user eye-tracking data and visualizes them.
//
// Functionality: To visualize user eye-tracking data via a heatmap consisting of individual gaze points colored according to the density of neighboring gaze points.
//
// Script setup and options:
// Loading a CSV file: name of the file (must be located in the Assets/Resources folder of the Unity project)
// Eye-tracking variables: setting the names of the XYZ gaze positions of the eye-tracking dataset
// Max Point Distance: setting the max distance of nearby gaze points to be included in the density computation
// Min Cluster Size: determine the minimum number of gaze points to be considered a cluster (for further computation)
// Optimization for large data files: culling the data either by determining from-to range in the dataset itself or by an in-scene collider (box)
// Visualization: chose the color (from low to high density) and size of the displayed gaze points in the heatmap; optionally choose if you want to visualize the eye-tracking trail (individual gaze points connected by a line) and it's color or the max length of that trail
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class HeatmapVisualizer : MonoBehaviour
{
	//link to the file name -- has to be located in the Assets/Resources folder
	public string fileToLoad;
	//the datastructure to load the CSV file to
	private List<Dictionary<string, object>> data;
	[Space(10)]

	//format variables
	public String userGazeX = "EtPositionX";
	public String userGazeY = "EtPositionY";
	public String userGazeZ = "EtPositionZ";

	//optimizations for loading big chunks of data
	//specify a from-to range on the dataset
	public bool cullByRange;
	public int cullFrom;
	public int cullTo;
	//cull by an in-scene collider (must be a box!)
	//TODO: multiple boxes
	public bool cullByContainer;
	public GameObject cullContainer;
	[Space(10)]

	//algorithm precision
	public float maxPointDistance = 0.05f;
	public int minClusterSize = 5;
	[Space(10)]

	//coloring/visualization
	public Color defaultColorLow; //white
	public Color defaultColorHigh; //red
	public Color failedDistanceColor; //gray
	public float pointSize = 0.075f;
	public bool drawTrail;
	public bool drawCloseTrailOnly;
	public Color trailColor; //white
	public float trailMaxDistance = 1f; 

	//load the data
	void Awake()
    {
        data = CSVReader.Read(fileToLoad);
        data = CullData(data);
		VisualizeTrail(data);
        VisualizeHeatmapData(data);
    }

	//draw the ET trail, if enabled
	void VisualizeTrail(List<Dictionary<string, object>> thisData)
	{
		if (drawTrail)
		{
			LineRenderer lineRenderer = gameObject.AddComponent<LineRenderer>();
			lineRenderer.material = new Material(Shader.Find("Unlit/Color"));
			lineRenderer.material.color = trailColor;
			lineRenderer.startWidth = 0.02f;
			lineRenderer.endWidth = 0.02f;
			//generate a Vector3[] array to pass to the renderer
			Vector3[] lineVectors = new Vector3[thisData.Count];
			float trailDistance;
			Vector3 vectorToAdd;
			Vector3 vectorPrevious = new Vector3();
			for (int i = 0; i < thisData.Count; i++)
			{
				vectorToAdd = new Vector3(float.Parse(data[i][userGazeX].ToString()),
										  float.Parse(data[i][userGazeY].ToString()),
										  float.Parse(data[i][userGazeZ].ToString()));				
				//if "spider web" ET trails are not wanted, exclude them...
				//TODO: instantiate into multiple LineRenderers so that there is no long skipping line
				if (drawCloseTrailOnly && i > 0)
                {
					trailDistance = Vector3.Distance(vectorToAdd, vectorPrevious);
					if (trailDistance <= trailMaxDistance)
                    {
						lineVectors[i] = vectorToAdd;
					}
				}
				else
                {
					lineVectors[i] = vectorToAdd;
				}
				vectorPrevious = vectorToAdd;
			}
			//draw it...
			lineRenderer.positionCount = thisData.Count;
			lineRenderer.SetPositions(lineVectors);
		}
	}

	void VisualizeHeatmapData(List<Dictionary<string, object>> thisData)
	{
        //generate all viable ET objects
        List<GameObject> raycasterListAll = new List<GameObject>();
        for (int i = 0; i < thisData.Count; i++)
        {
            float xPos = float.Parse(data[i][userGazeX].ToString());
			float yPos = float.Parse(data[i][userGazeY].ToString());
            float zPos = float.Parse(data[i][userGazeZ].ToString());
            GameObject newSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			//TODO: attach a data component on each such instantiated eye-tracking coordinate
			//      so that spatial data processing can commence without array search loops
            newSphere.GetComponent<Collider>().enabled = false;
			//newSphere.tag = "EyeTracking"; //gotta be created from the editor first 
			newSphere.transform.localScale = new Vector3(pointSize, pointSize, pointSize);
            newSphere.transform.position = new Vector3(xPos, yPos, zPos);
            newSphere.name = "dwell_" + i;
            raycasterListAll.Add(newSphere);
        }

		//iterate on them
		float maxClusterSize = 0f;
		foreach(GameObject thisObject in raycasterListAll)
		{
			float minimumDistance = 1000f;
			float currentClusterSize = 0f;
			//this ET object compared against all other raycasted objects, except itself
			foreach (GameObject anotherObject in raycasterListAll)
			{
				if(thisObject != anotherObject)
				{
					//object distance to other objects
					float distance = Vector3.Distance(thisObject.transform.position,
					anotherObject.transform.position);
					if (distance < minimumDistance) { minimumDistance = distance; }
					if (distance < maxPointDistance) { currentClusterSize++; }
				}
			}

			//determine max absolute cluster size in the ET objects
			//so that cluster visualization density relatives are based on this
			if (currentClusterSize > maxClusterSize) { maxClusterSize = currentClusterSize; }
			if ((minimumDistance > maxPointDistance) || (currentClusterSize < minClusterSize))
			{
				thisObject.GetComponent<Renderer>().material.color = failedDistanceColor;
			}
			else
			{
				thisObject.GetComponent<Renderer>().material.color = Color.Lerp(defaultColorLow, defaultColorHigh,
																				currentClusterSize / maxClusterSize);
			}
		}
    }
    
    //cull the unnecesssary data to reduce rendering and computational load
    //as set up in the inspector
    //TODO: refactor visualizers
    List<Dictionary<string, object>> CullData(List<Dictionary<string, object>> passedList)
    {
		if (!cullByRange && !cullByContainer) { return passedList; } //nothing removed

		List<Dictionary<string, object>> tempList = new List<Dictionary<string, object>>();
		//accept interval subsection of the data, per data entry id
		if (cullByRange)
        {
            if ((data.Count > cullFrom) && (data.Count >= cullTo) && (cullFrom < cullTo))
            {
				for (var i = 0; i < passedList.Count; i++)
                {
                    if (i >= cullFrom && i <= cullTo) { tempList.Add(passedList[i]); }     
                }
				Debug.Log("ET data culled by range from " + data.Count + " to " + tempList.Count);
			}
        }
        //accept what is included inside a gameobject
        if (cullByContainer)
        {
			Bounds bounds = cullContainer.GetComponent<Collider>().bounds;
			float cullFromX = bounds.min.x;
			float cullFromY = bounds.min.y;
			float cullFromZ = bounds.min.z;
			float cullToX = bounds.max.x;
			float cullToY = bounds.max.y;
			float cullToZ = bounds.max.z;
			if (tempList.Count == 0) { tempList = passedList; }
			
			for (var i = 0; i < tempList.Count; i++)
            {
				float xPos = float.Parse(data[i][userGazeX].ToString());
			    float yPos = float.Parse(data[i][userGazeY].ToString());
                float zPos = float.Parse(data[i][userGazeZ].ToString());
				if (!((xPos >= cullFromX && xPos <= cullToX) &&
					  (yPos >= cullFromY && xPos <= cullToY) &&
					  (zPos >= cullFromZ && xPos <= cullToZ)))
				{
					//tempList.Add(passedList[i]);
					tempList.RemoveAt(i);
				}
            }
            passedList = tempList;
			Debug.Log("ET data culled by collider from " + data.Count + " to " + tempList.Count);
        }       
        
        return tempList; //something removed...
    }
}