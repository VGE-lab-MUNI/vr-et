// --------------------------------------------------------------------------------------------------------------------
// Path Visualizer script, version 2022-01-06
// This script processes loaded CSV user trajectory data a visualizes them. 
//
// Functionality: To visualize user movement trajectory through 3D virtual environment as a 2D line using Unity's LineRenderer class (or discrete points).
//
// Script setup and options:
// Loading a CSV file: name of the file (must be located in the Assets/Resources folder of the Unity project)
// Optimization for large data files: culling the data either by determining from-to range in the dataset itself or by an in-scene collider (box)
// Visualization: choose the color and size of points (when visualizing discrete coordinates), or the color of the line
// --------------------------------------------------------------------------------------------------------------------

public class PathVisualizer : MonoBehaviour
{
    //link to the file name -- has to be located in the Assets/Resources folder
    public string fileToLoad;
    //the datastructure to load the CSV file to
    private List<Dictionary<string, object>> data;
    [Space(10)]
    
    //optimizations for loading big chunks of data
    //specify a from-to range on the dataset
    public bool cullByRange;
    public int cullFrom;
    public bool cullTo;
    //cull by an in-scene collider (must be a box!)
    //TODO: multiple boxes
    public bool cullByContainer;
    public GameObject cullContainer;
    [Space(10)]

    //visualization parameters (can be set up from Unity inspector)
    public Color pointColor;
    public float pointSize = 0.15f;
    public Color lineColor;
    //to visualize a continuous line
    private Vector3 previousCubePosition;

    void Awake()
    {
        data = CSVReader.Read(fileToLoad);
        data = CullData(data);
  
        for (var i = 0; i < data.Count; i++)
        {
            //display the point
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cube.name = ”cube” + data[i][”id”];
            cube.GetComponent<Renderer>().material.color = pointColor;
            cube.GetComponent<Renderer>().transform.localScale =
            new Vector3(pointSize, pointSize, pointSize);
            cube.GetComponent<Collider>().enabled = false; //no collisions
            float xPos = float.Parse(data[i][”xpos”].ToString());
            float yPos = float.Parse(data[i][”ypos”].ToString());
            float zPos = float.Parse(data[i][”zpos”].ToString());
            cube.transform.position = new Vector3(xPos, yPos, zPos); //placement
 
            //connect the current point to the previous one
            LineRenderer lineRenderer;
            if ((i > 0) && (i != (data.Count-1)))
            {
                lineRenderer = cube.AddComponent<LineRenderer>();
                lineRenderer.SetVertexCount(2);
                lineRenderer.material.color = lineColor;
                lineRenderer.SetPosition(0, cube.transform.position);
                lineRenderer.SetPosition(1, previousCubePosition);
                lineRenderer.SetWidth(pointSize/2, pointSize/2);
            }
            previousCubePosition = cube.transform.position;
        }
    }
    
    //cull the unnecesssary data to reduce rendering and computational load
    //as set up in the inspector
    //TODO: refactor visualizers
    List<Dictionary<string, object>> CullData(List<Dictionary<string, object>> passedList)
    {
		List<Dictionary<string, object>> tempList = new List<Dictionary<string, object>>;
		//accept interval subsection of the data, per data entry id
        if (cullByRange)
        {
            if ((data.Count > cullFrom) && (data.Count > cullTo) && (cullFrom < cullTo))
            {
                for (var i = 0; i < passedList.Count; i++)
                {
                    if (i >= cullFrom && i <= cullTo) { tempList.Add(passedList[i]); }     
                }
                passedList = tempList;
                tempList = new List<Dictionary<string, object>>;
            }
        }
        //accept what is included inside a gameobject
        if (cullByContainer)
        {
			Bounds bounds = cullContainer.GetComponent<Collider>().bounds;
			cullFromX = bounds.min.x;
			cullFromY = bounds.min.y;
			cullFromZ = bounds.min.z;
			cullToX = bounds.max.x;
			cullToY = bounds.max.y;
			cullToZ = bounds.max.z;
			
			for (var i = 0; i < passedList.Count; i++)
            {
				float xPos = float.Parse(data[i][”xpos”].ToString());
			    float yPos = float.Parse(data[i][”ypos”].ToString());
                float zPos = float.Parse(data[i][”zpos”].ToString());
                if ((xPos >= cullFromX && xPos =< cullToX) &&
                    (yPos >= cullFromY && xPos =< cullToY) &&
                    (zPos >= cullFromZ && xPos =< cullToZ))
                {
                   tempList.Add(passedList[i]);
                }     
            }
            passedList = tempList;
        }
        
        if (!cullByRange && !cullByContainer) { return passedList; } //nothing removed
        else { return tempList; } //something removed...
    }
}