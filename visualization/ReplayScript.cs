// --------------------------------------------------------------------------------------------------------------------
// Replay script, version 2022-01-06
// This script processes plays loaded CSV behavioral data (movement, eye-tracking)
//
// Functionality: The script takes control of mainCamera. Keyboard shortcuts for play control.
//
// Script setup and options:
// targetCamera: mainCamera to control
// file format parameters (user movement/rotation, ET data if any)
// keyboard shortcuts
// --------------------------------------------------------------------------------------------------------------------

public class ReplayScript : MonoBehaviour
{
    //link to the file name -- has to be located in the Assets/Resources folder
    public string fileToLoad;
    //the datastructure to load the CSV file to
    private List<Dictionary<string, object>> data;
    [Space(10)]
    
    //mainCamera to use
    public GameObject targetCamera;
    [Space(10)]
    
    //format variables
	public String userMoveX = "xpos";
	public String userMoveY = "ypos";
	public String userMoveZ = "zpos";
	public String userRotateX = "upos";
	public String userRotateY = "vpos";
	public String userRotateZ = "wpos";
	public String userGazeX = "EtPositionX";
	public String userGazeY = "EtPositionY";
	public String userGazeZ = "EtPositionZ";
    [Space(10)]
    
    //control keys
    public KeyCode startKey;
    public KeyCode moveFwdKey;
    public KeyCode moveBwdKey;
    public KeyCode speedUpKey;
    public KeyCode speedDownKey;
    public KeyCode moveToStartKey;
    public KeyCode moveToEndEky; 
    [Space(10)]
    
    //settings, optimizations
    public bool displayEtData;
    public bool displayEtTrail;
    [Range(2,100)]
    public int displayedEtTrail;
    [Space(10)]

    //visualization parameters (can be set up from Unity inspector)
    public Color etPointColor;
    public float etPointSize = 0.15f;
    public Color lineColor;
    
    //auxiliaries
    private bool initialized;
    private List<Vector3> previousEtPositions;
    private float timestampInitial;
    private float timestampFinal;
    private float timestampCurrent;
    private int timestampPointer;
    private bool isPlaying;
    private float stepSpeedup = 0.1; //10%
    private float stepMove = 5;      //5s

    void Awake()
    {
		if (targetCamera != null || targetCamera.GetComponent<Camera>() != null)
		{
		    initialized = true;
		    try
		    {
		        data = CSVReader.Read(fileToLoad);
		        data = GetTimestamps(data);
		    }
		    catch (Exception e) { initialized = false; }
		}
    }
    
    List<Dictionary<string, object>> GetTimestamps(List<Dictionary<string, object>> thisData)
    {
        for (var i = 0; i < data.Count; i++)
        { 
            //add timestamp column
            //timestampInitial + timestampFinal as well
        }
        return thisData;
    }
    
    void FixedUpdate()
    {
        if(Key.IsDown("startKey") && initialized)
        {
            isPlaying != isPlaying;
        }
        else if (Key.IsDown("moveToStartKey") && initialized)
        {
            isPlaying = false;
            timestampCurrent = 0;
            timestampPointer = 0;
        }
        //TODO: other keys
        
        if(initialized && isPlaying)
        {
            timestampCurrent += Time.DeltaTime();
            
            //TODO: seach through data till time on pointer > timestampCurrent
            targetCamera.transform.position = Vector3(data[timestampPointer][userMoveX],
                                                      data[timestampPointer][userMoveY]
                                                      data[timestampPointer][userMoveZ]);
            targetCamera.transform.rotation = Vector3(data[timestampPointer][userRotateX],
                                                      data[timestampPointer][userRotateY]
                                                      data[timestampPointer][userRotateZ]);
            if (displaEtData)
            {
                //TODO: display ET point (sphere)
                if (displayEtTrail)
                {
                    //TODO: the same, for trail (pre-instantiate the trail length for optimization)
                    //  ... or do angle-based fixations on trail (varying no. spheres, for demo purposes)
                }
            }
        }
    }
}