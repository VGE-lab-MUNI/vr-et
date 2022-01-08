// --------------------------------------------------------------------------------------------------------------------
// Recaster script, version 2022-01-06
// This script recasts already collected CSV eye-tracking data and writes the results into a new CSV file.
//
// Functionality: To recast user eye-tracking data (individual raycasts) in a given virtual environment to possibly collect new data with the use of altered environment (colliders).
//
// Script setup and options:
// Loading a CSV file: name of the file (must be located in the Assets/Resources folder of the Unity project)
// Writing to a new CSV file: name of the output file (must be located in the Assets/Resources folder of the Unity project)
// Rows to recast: set up the maximum number of rows to be recast
// Input CSV variables: write the names of the columns of the input CSV for user XYZ positions, eye-tracking XYZ positions, and names of the fixated objects
// --------------------------------------------------------------------------------------------------------------------

public class Recaster : MonoBehaviour
{
    //service variables
    [System.Serialize] private String fileToLoad;
    private String fileToWrite;
    private List<Dictionary<string,object>> data;
    private bool fileLoaded;
    private int rowToProcess, rowMaximum;
    [Space(10)]
    //format variables
    [System.Serialize] private String userPositionX = "xpos";
    [System.Serialize] private String userPositionY = "ypos";
    [System.Serialize] private String userPositionZ = "zpos";
    [System.Serialize] private String userGazeX = "EtPositionX";
    [System.Serialize] private String userGazeY = "EtPositionY";
    [System.Serialize] private String userGazeZ = "EtPositionZ";
    [System.Serialize] private String fixatedObjectName = "FixatedObjectName";
    //TODO: et direction normalized vector (if available), enu to specify Vector conversion type, if needed be...
    
    void Awake ()
    {
		if (!File.Exists(fileToLoad + ".txt"))
		{
            Debug.LogWarning("File " + filetoload + ".csv does not exist in Assets/Resources/. " +
                             "Nothing to process.");
        }
        else
        {
            List<Dictionary<string,object>> data = CSVReader.Read ("example");
            rowMaximum = data.Count - 1;
            fileLoaded = true;
            Debug.Log("Loaded " + filetoload + ".csv, containing " + (rowMaximum + 1) + " entries.");            
        }
    }
    
    void FixedUpdate ()
    {
        if (fileLoaded)
        {
            RaycastHit hit;
            //bit mask is for raycast to hit things
            //TODO: pass a distinction (dual raycaster, etc.) here if needed            
            int layerMask = 0;
            layerMask = ~layerMask;
            //position and direction vectors, as loaded in the CSV datafile
            //TODO: different formats, different ways of obtaining this
            Vector3 userPosition;
            Vector3 userGaze;
            Vector3 userDirection;
            
            //recast the data
            for (int i = 0; i <= rowMaximum; i++) {
                //get the coordinate data from the loaded CSV
                userPosition = new Vector3(data[i][userPositionX], data[i][userPositionY], data[i][userPositionZ]);
                userGaze = new Vector3(data[i][userGazeX], data[i][userGazeY], data[i][userGazeZ]);
                userDirection = Vector3(userPosition - userGaze).normalized;
                //get gazed upon collider position and name
                if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward),
                    out hit, Mathf.Infinity, layerMask))
                {
                    data[i][userGazeX] = hit.point.x;
                    data[i][userGazeY] = hit.point.y;
                    data[i][userGazeZ] = hit.point.z;
                    data[i][fixatedObjectName] = hit.collider.GameObject.name;
                }
                else 
                {
                    data[i][userGazeX] = 0;
                    data[i][userGazeY] = 0;
                    data[i][userGazeZ] = 0;
                    data[i][fixatedObjectName] = "no data";
                }
            }
            
            //TODO: write back the altered data List<Dictionary<>> to a file
        }
    }
}