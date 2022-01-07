// --------------------------------------------------------------------------------------------------------------------
// Unity VR PathScript, version 2020-06-22
// This script collects data a virtual environment user can generate.
//
// For the current range of logging possibilities and settings, see the UI in Unity inspector
//     Movement logging: Internal. Attach the script to the desired (FPS)Controller. Logs position and rotation. 
//     Collision logging: External. For user controller bumping into Trigger colliders.
//     Controller logging: Internal. Use of physical interface. Currently keyboard only, GetKeyDown() on Update().
//     Eye tracking: External. SMI format. Deprecated.
//     Eye tracking 2: External. Pupil Labs format (old). Deprecated.
//     Eventlog: External. Free format, accepts any string at logEventInfo().
//     Moving Objects log: Internal. Logs a list of movingObjects<GameObject>.
//       
//  Movement and moving objects are logged countinuously per interval, on a coroutine (max per fps).
//      Set BufferSize for write buffer size (every Nth entry).
//      Set MovementLogInterval for logging periodicity (0 = per fps). Otherwise, delay in seconds.
//
//  Data format: prefedined, per CSV standard and en-Us locale.
//      Changeable per separatorDecimal and separatorItem. Consider format dependency of other apps.
//  Data naming/location: set per dataPrefix and saveLocation. Make sure the dataLocation exists & is writeable.
//  Data structure: see GenerateFileNames() for headers. Self-explanatory.
//
// Basic data structure example (path):
//      userId -- generated per timestamp to identify the user in batch/multiple file processing
//      logId  -- iterator on the current log file
//      timestamp -- time (seconds), Unix epoch format
//      hour | minute | second | milliseconds
//      xpos | ypos | zpos -- location in global Unity coordinates (1 Unity unit = 1 meter)
//      uMousePos | vMousePos | wMousePos -- camera position per mouse. Only u/v relevant (LCD task); only v (VR task)
//      uGazePos | vGazePos | wGazePos    -- VR HMD camera. Only relevant when wearing HMD. Otherwise junk data
//
// Usage: Attach this script to an intended FPSController (dragging & dropping within the Hierarchy browser will do).
//      Other dependent object have to be linked to it, too (e.g. movingObjects<>)
// PathScript methods are public. If other scripts are linked to the GameObject with PathScript, they can log.
//      E.g.: Logger.GetComponent<PathScript>().logEventData(this.name + " triggered " + subObject.name);
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

public class PathScript : MonoBehaviour
{
    //data format
    public string separatorDecimal = ".";
    public string separatorItem = ";";
    private NumberFormatInfo numberFormat;

    //log buffering
    [Range(1, 1000)]
    public int bufferSize = 1;
    [Space(10)]

    //set up which variables are to be logged (some of these are called from external scripts on associated objects)
    public bool logMovement;
    public bool logMovingObjects;
    public List<GameObject> movingObjects;
    public bool logCollisions;
    public bool logController;
    public bool logEyeTracking;
    public bool logEyeTracking2;
    public bool eventLog;
    public bool allowCustomLogs;
    [Space(10)]

    //delay among individual movement measurements
    //  going too low (~100ms) is not recommended, as there is some measurement delay, esp. with low-end systems
    [Range(0f, 5f)]
    public float movementLogInterval = 0.5f;
    //target camera (careful with some Unity plugins of HMDs: there may be multiple cameras in such scenes)
    public Camera headCamera;
    public bool directCameraAccess;
    [Space(10)]

    //log files naming convention and save location
    public string datasetPrefix = "";
    public string saveLocation = "C:\\";
    [Space(10)]

    //extra controller keys
    public bool allowSpecialKeys;
    public KeyCode[] specialKeys;
    public string[] specialKeyMeanings;
    private int specialKeysLength;
    private int specialKeyMeaningsLength;

    //data file names
    private string pathFileName;
    private string etFileName;
    private string et2FileName;
    private string collisionFileName;
    private string controllerFileName;
    private string eventLogFileName;
    private string movingObjectsFileName;
    [HideInInspector]
    public List<string> customLogFileNames;
    [HideInInspector]
    public List<string> customLogNames;

    //data buffers
    private string pathBuffer;
    private string etBuffer;
    private string movingObjectsBuffer;
    [HideInInspector]
    public List<string> customLogBuffers;

    //keyPress states
    private bool isPressedUp;
    private bool isPressedDown;
    private bool isPressedLeft;
    private bool isPressedRight;

    //service variables - to prevent streams of same data
    private string etLastFocusType;
    private string collisionLastObject;

    //measurement iterator
    private int pathCounter;
    private int etCounter;
    private int collisionCounter;
    private int controllerCounter;
    private int eventLogCounter;
    private int movingObjectsCounter;
    [HideInInspector]
    public List<int> customLogCounters;
    //participant/data marker
    private string fileNameTime;

    //auxiliary structures
    [HideInInspector] public StreamWriter streamWriterMovement;
    [HideInInspector] public StreamWriter streamWriterCollisions;
    [HideInInspector] public StreamWriter streamWriterController;
    [HideInInspector] public StreamWriter streamWriterEyeTracking;
    [HideInInspector] public StreamWriter streamWriterEyeTracking2;
    [HideInInspector] public StreamWriter streamWriterEventLog;
    [HideInInspector] public StreamWriter streamWriterMovingObjects;
    [HideInInspector] public List<StreamWriter> customLogStreamWriters;

    // ----------------------------------------------------------------------------------------------------------------
    // Program initialization and run update
    // ----------------------------------------------------------------------------------------------------------------

    // Use this for initialization
    void Awake()
    {
        //init datastructures
        customLogFileNames = new List<string>();
        customLogNames = new List<string>();
        customLogBuffers = new List<string>();
        customLogCounters = new List<int>();
        customLogStreamWriters = new List<StreamWriter>();

        //to have a standardized decimal separator across different system locales
        //usage: someNumber.ToString(numberFormat)
        numberFormat = new NumberFormatInfo();
        numberFormat.NumberDecimalSeparator = separatorDecimal;

        specialKeysLength = specialKeys.Length;
        specialKeyMeaningsLength = specialKeyMeanings.Length;
        fileNameTime = System.DateTime.Now.ToString("_yyyyMMdd_HHmmss");
        GenerateFileNames(true);
        StartCoroutine(PathLogger());
        StartCoroutine(MovingObjectsLogger());
    }

    //for logController (keyPress)
    void Update()
    {
        //multiple keys can be (un)pressed in a single frame
        if (Input.GetKeyDown("up")) { logControllerData("up", true); }
        if (Input.GetKeyDown("down")) { logControllerData("down", true); }
        if (Input.GetKeyDown("left")) { logControllerData("left", true); }
        if (Input.GetKeyDown("right")) { logControllerData("right", true); }
        if (Input.GetKeyUp("up")) { logControllerData("up", false); }
        if (Input.GetKeyUp("down")) { logControllerData("down", false); }
        if (Input.GetKeyUp("left")) { logControllerData("left", false); }
        if (Input.GetKeyUp("right")) { logControllerData("right", false); }
        //other, special keys
        if (allowSpecialKeys)
        {
            //if (Input.GetKeyDown ("x")) { logControllerData("event_marker", true); }
            //if (Input.GetKeyDown("q")) { logControllerData("teleporter", true); }
            for (int i = 0; i < specialKeysLength; i++)
            {
                if (i <= specialKeyMeaningsLength)
                {
                    if (specialKeyMeanings[i] != "")
                    {
                        if (Input.GetKeyDown(specialKeys[i])) { logControllerData(specialKeyMeanings[i], true); }
                    }
                    else
                    {
                        if (Input.GetKeyDown(specialKeys[i])) { logControllerData("special-" + i, true); }
                    }
                }
            }
        }
    }

    // Generate new file name on every run (as per timestamp)
    void GenerateFileNames(bool includeVariableNames)
    {
        this.pathFileName = @saveLocation + datasetPrefix + "_" + "path" + fileNameTime + ".txt";
        this.etFileName = @saveLocation + datasetPrefix + "_" + "et" + fileNameTime + ".txt";
        this.et2FileName = @saveLocation + datasetPrefix + "_" + "et2" + fileNameTime + ".txt";
        this.collisionFileName = @saveLocation + datasetPrefix + "_" + "collision" + fileNameTime + ".txt";
        this.controllerFileName = @saveLocation + datasetPrefix + "_" + "controller" + fileNameTime + ".txt";
        this.eventLogFileName = @saveLocation + datasetPrefix + "_" + "eventlog" + fileNameTime + ".txt";
        this.movingObjectsFileName = @saveLocation + datasetPrefix + "_" + "movingObj" + fileNameTime + ".txt";

        //append the first row to files to indicate variable names, if specified
        if (includeVariableNames && verifyLoggingDirectory())
        {
            if (logMovement)
            {
                System.IO.File.Create(pathFileName).Dispose();
                System.IO.File.AppendAllText(pathFileName,
                    "userId" + separatorItem + "logId" + separatorItem + "timestamp" + separatorItem +
                    "hour" + separatorItem + "min" + separatorItem + "sec" + separatorItem + "ms" + separatorItem +
                    "xpos" + separatorItem + "ypos" + separatorItem + "zpos" + separatorItem +
                    "uMousePos" + separatorItem + "vMousePos" + separatorItem + "wMousePos" + separatorItem +
                    "uGazePos" + separatorItem + "vGazePos" + separatorItem + "wGazePos" +
                    "\r\n");
                streamWriterMovement = new StreamWriter(pathFileName, true);
            }
            if (logCollisions)
            {
                System.IO.File.Create(collisionFileName).Dispose();
                System.IO.File.AppendAllText(collisionFileName,
                    "userId" + separatorItem + "logId" + separatorItem + "timestamp" + separatorItem +
                    "hour" + separatorItem + "min" + separatorItem + "sec" + separatorItem + "ms" + separatorItem +
                    "xpos" + separatorItem + "ypos" + separatorItem + "zpos" + separatorItem +
                    "uMousePos" + separatorItem + "vMousePos" + separatorItem + "wMousePos" + separatorItem +
                    "uGazePos" + separatorItem + "vGazePos" + separatorItem + "wGazePos" + separatorItem +
                    "objectName" + separatorItem + "xobj" + separatorItem + "yobj" + separatorItem + "zobj" +
                    "\r\n");
                streamWriterCollisions = new StreamWriter(collisionFileName, true);
            }
            if (logController)
            {
                System.IO.File.Create(controllerFileName).Dispose();
                System.IO.File.AppendAllText(controllerFileName,
                    "userId" + separatorItem + "logId" + separatorItem + "timestamp" + separatorItem +
                    "hour" + separatorItem + "min" + separatorItem + "sec" + separatorItem + "ms" + separatorItem +
                    "xpos" + separatorItem + "ypos" + separatorItem + "zpos" + separatorItem +
                    "xangle" + separatorItem + "yangle" + separatorItem + "zangle" + separatorItem +
                    "xrot" + separatorItem + "yrot" + separatorItem + "zrot" + separatorItem +
                    "keyPressed" + separatorItem + "isDown" + separatorItem + "keyDirection" +
                    "\r\n");
                streamWriterController = new StreamWriter(controllerFileName, true);
            }
            if (logEyeTracking)
            {
                System.IO.File.Create(etFileName).Dispose();
                System.IO.File.AppendAllText(etFileName,
                    "userId" + separatorItem + "logId" + separatorItem + "timestamp" + separatorItem +
                    "hour" + separatorItem + "min" + separatorItem + "sec" + separatorItem + "ms" + separatorItem +
                    "xpos" + separatorItem + "ypos" + separatorItem + "zpos" + separatorItem +
                    "uMousePos" + separatorItem + "vMousePos" + separatorItem + "wMousePos" + separatorItem +
                    "uGazePos" + separatorItem + "vGazePos" + separatorItem + "wGazePos" + separatorItem +
                    "objName" + separatorItem + "objFocusType" + separatorItem +
                    "xobj" + separatorItem + "yobj" + separatorItem + "zobj" +
                    "\r\n");
                streamWriterEyeTracking = new StreamWriter(etFileName, true);
            }
            if (logEyeTracking2)
            {
                System.IO.File.Create(et2FileName).Dispose();
                System.IO.File.AppendAllText(etFileName,
                    "userId" + separatorItem + "logId" + separatorItem + "timestamp" + separatorItem +
                    "hour" + separatorItem + "min" + separatorItem + "sec" + separatorItem + "ms" + separatorItem +
                    "xpos" + separatorItem + "ypos" + separatorItem + "zpos" + separatorItem +
                    "uMousePos" + separatorItem + "vMousePos" + separatorItem + "wMousePos" + separatorItem +
                    "uGazePos" + separatorItem + "vGazePos" + separatorItem + "wGazePos" + separatorItem +
                    "xobj" + separatorItem + "yobj" + separatorItem + "zobj" +
                    "\r\n");
                streamWriterEyeTracking2 = new StreamWriter(et2FileName, true);
            }
            if (eventLog)
            {
                System.IO.File.Create(eventLogFileName).Dispose();
                System.IO.File.AppendAllText(eventLogFileName,
                    "userId" + separatorItem + "logId" + separatorItem + "timestamp" + separatorItem +
                    "hour" + separatorItem + "min" + separatorItem + "sec" + separatorItem + "ms" + separatorItem +
                    "eventInfo" +
                    "\r\n");
                streamWriterEventLog = new StreamWriter(eventLogFileName, true);
            }
            if (logMovingObjects)
            {
                System.IO.File.Create(movingObjectsFileName).Dispose();
                System.IO.File.AppendAllText(movingObjectsFileName,
                    "userId" + separatorItem + "logId" + separatorItem + "timestamp" + separatorItem +
                    "hour" + separatorItem + "min" + separatorItem + "sec" + separatorItem + "ms" + separatorItem +
                    "objName" + separatorItem +
                    "xobj" + separatorItem + "yobj" + separatorItem + "zobj" + separatorItem +
                    "xobjRot" + separatorItem + "yobjRot" + separatorItem + "zobjRot" +
                    //not really needed - just for object ET purposes (observer-objects coordinates in one place)
                    separatorItem + "xpos" + separatorItem + "ypos" + separatorItem + "zpos" +
                    //-------------------------------------------------------------------------------------------
                    "\r\n");
                streamWriterMovingObjects = new StreamWriter(movingObjectsFileName, true);
            }
        }
        else
        {
            Debug.LogWarning("Log file creation faled.");
        }
    }

    //generate file names for custom loggers (if allowed)
    //if not allowed, return false so that the external logger doesn't send junk data
    public bool generateCustomFileNames(string customLogVariables, string logName, string callerName)
    {
        //input verification
        if (!allowCustomLogs)
        {
            Debug.LogWarning("Object " + callerName + " tried to create custom log " + logName + ". " +
                             "Custom logs are disabled");
            return false;
        }
        else if (logName == "")
        {
            Debug.LogWarning("Object " + callerName + " tried to create custom log with no name.");
            return false;
        }
        else if (!verifyLoggingDirectory())
        {
            Debug.LogWarning("Log file creation faled.");
            return false;
        }
        //file creation
        else
        {
            //custom log names/fileNames/buffers have to be created simultaneously, so that they retain the same [i]
            //this is loose coupling of two Lists, as oppposed to List<Hashtable<string, string>>
            customLogNames.Add(logName);
            customLogFileNames.Add(@saveLocation + datasetPrefix + "_" + logName + fileNameTime + ".txt");
            customLogBuffers.Add("");
            customLogCounters.Add(1);
            System.IO.File.Create(customLogFileNames[customLogFileNames.Count - 1]).Dispose();
            if (customLogVariables != "")
            {
                customLogVariables = "userId" + separatorItem + "logId" + separatorItem + "timestamp" + separatorItem +
                                     "hour" + separatorItem + "min" + separatorItem + "sec" + separatorItem +
                                     "ms" + separatorItem +
                                     //////////////////////////////////////////////////////////////////////////////////
                                     //TODO: implement a unified log requesting feature
                                     //////////////////////////////////////////////////////////////////////////////////                                    
                                     "xpos" + separatorItem + "ypos" + separatorItem + "zpos" + separatorItem +
                                     "uMousePos" + separatorItem + "vMousePos" + separatorItem +
                                                                   "wMousePos" + separatorItem +
                                     "uGazePos" + separatorItem + "vGazePos" + separatorItem +
                                                                  "wGazePos" + separatorItem +
                                     //////////////////////////////////////////////////////////////////////////////////
                                     //TODO: implement a unified log requesting feature
                                     //////////////////////////////////////////////////////////////////////////////////  
                                     customLogVariables + "\r\n";
                System.IO.File.AppendAllText(customLogFileNames[customLogFileNames.Count - 1], customLogVariables);
            }
            customLogStreamWriters.Add(new StreamWriter(customLogFileNames[customLogNames.IndexOf(logName)]));
            return true;
        }
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Main logger functions
    // ----------------------------------------------------------------------------------------------------------------

    //movement & path is logged continuously, from within this script (gotta be attached to (a part of) the controller)
    IEnumerator PathLogger()
    {
        while (logMovement)
        {
            string currentData = fileNameTime + separatorItem + pathCounter + separatorItem +
                                 GetCurrentTimestamp() + separatorItem + GetCurrentTime() + separatorItem;
            if (directCameraAccess)
            {
                currentData += GetCurrentPositionDirectly();
            }
            else
            {
                currentData += GetCurrentPosition();
            }
            currentData += "\r\n";

            //log, or buffer to log
            pathBuffer += currentData;
            if (pathCounter % bufferSize == 0)
            {
                //System.IO.File.AppendAllText(pathFileName, pathBuffer);
                streamWriterMovement.Write(currentData);
                pathBuffer = "";
                //Debug.Log("PathScript emptied a buffer of " + bufferSize + " items @" + Time.time);
            }

            pathCounter++;
            yield return new WaitForSeconds(movementLogInterval);
        }
    }

    //logging of other moving objects is also a coroutine; these objects are accessed from the movingObjects<> list
    IEnumerator MovingObjectsLogger()
    {
        while (logMovingObjects)
        {
            string dataPerCycle = "";
            string dataPerEachItem = fileNameTime + separatorItem + movingObjectsCounter + separatorItem +
                                     GetCurrentTimestamp() + separatorItem + GetCurrentTime();
            foreach (GameObject item in movingObjects)
            {
                dataPerCycle += dataPerEachItem + separatorItem + item.name + separatorItem +
                                item.transform.position.x.ToString(numberFormat) + separatorItem +
                                item.transform.position.y.ToString(numberFormat) + separatorItem +
                                item.transform.position.z.ToString(numberFormat) + separatorItem +
                                item.transform.rotation.eulerAngles.x.ToString(numberFormat) + separatorItem +
                                item.transform.rotation.eulerAngles.y.ToString(numberFormat) + separatorItem +
                                item.transform.rotation.eulerAngles.z.ToString(numberFormat) +
                                //not really needed - just for object ET purposes (observer-objects coords in one place)
                                separatorItem + transform.position.x.ToString(numberFormat) + separatorItem +
                                transform.position.y.ToString(numberFormat) + separatorItem +
                                transform.position.z.ToString(numberFormat) +
                                //--------------------------------------------------------------------------------------
                                "\r\n";
            }

            //log, or buffer to log
            movingObjectsBuffer += dataPerCycle;
            if (movingObjectsCounter % bufferSize == 0)
            {
                //System.IO.File.AppendAllText(movingObjectsFileName, movingObjectsBuffer);
                streamWriterMovingObjects.Write(movingObjectsBuffer);
                movingObjectsBuffer = "";
                Debug.Log("PathScript emptied a buffer of " + bufferSize + " moving object items @" + Time.time);
            }

            movingObjectsCounter++;
            yield return new WaitForSeconds(movementLogInterval);
        }
    }

    //object-based ET logger (SMI, deprecated)
    public void logEtData(string objName, string objFocusType, string objCoordinates)
    {
        if (logEyeTracking)
        {
            //necessary precondition, as continuous logging of gaze being kept on an object is not wanted here
            //if (etLastFocusType != objFocusType) {
            //get rid of extra brackets, if present
            objCoordinates = cleanNumericData(objCoordinates);
            string currentData = fileNameTime + separatorItem + etCounter + separatorItem +
                                 GetCurrentTimestamp() + separatorItem + GetCurrentTime() + separatorItem +
                                 GetCurrentPosition() + separatorItem +
                                 objName + separatorItem +
                                 objFocusType + separatorItem +
                                 objCoordinates +
                                 "\r\n";
            //System.IO.File.AppendAllText(etFileName, currentData);
            streamWriterEyeTracking.Write(currentData);
            etCounter++;
            etLastFocusType = objFocusType;
            //}
        }
    }

    //coordinate-based ET logger (Pupil Labs 1.13, deprecated)
    public void logEtData2(Vector3 fixationPosition)
    {
        //to implement this, try the following:
        //public GameObject Player = null;
        //void Start()
        //{
        //  Player = GameObject.FindGameObjectWithTag("Player");
        //}
        //Player.GetComponent<PathScript>().logEt2Data(Vector3 fixationPosition);
        if (logEyeTracking2)
        {
            string currentData = fileNameTime + separatorItem + etCounter + separatorItem +
                                 GetCurrentTimestamp() + separatorItem + GetCurrentTime() + separatorItem +
                                 GetCurrentPosition() + separatorItem +
                                 fixationPosition.x.ToString(numberFormat) + separatorItem +
                                 fixationPosition.y.ToString(numberFormat) + separatorItem +
                                 fixationPosition.z.ToString(numberFormat) +
                                 "\r\n";

            //log, or buffer to log
            etBuffer += currentData;
            if (etCounter % bufferSize == 0)
            {
                //System.IO.File.AppendAllText(etFileName, etBuffer);
                streamWriterEyeTracking2.Write(etBuffer);
                etBuffer = "";
            }
            etCounter++;
        }
    }

    //external collision logger, as reported from a script attached to a GameObject with Collider.isTrigger = true;
    public void logCollisionData(string objName, string objCoordinates)
    {
        if (logCollisions)
        {
            //necessary precondition, as continuous logging of object collision is not wanted here
            if (collisionLastObject != objName)
            {
                objCoordinates = cleanNumericData(objCoordinates);
                string currentData = fileNameTime + separatorItem + collisionCounter + separatorItem +
                                     GetCurrentTimestamp() + separatorItem + GetCurrentTime() + separatorItem +
                                     GetCurrentPosition() + separatorItem +
                                     objName + separatorItem +
                                     objCoordinates +
                                     "\r\n";
                //System.IO.File.AppendAllText(collisionFileName, currentData);
                streamWriterCollisions.Write(currentData);
                collisionCounter++;
                collisionLastObject = objName;
            }
        }
    }

    //physical controller logger; currently keyboard-only;
    //basic arrow keys & the ability to define own speacial keys in specialKeys[] and specialKeyMeanings[]
    public void logControllerData(string keyPress, bool isDown)
    {
        if (logController)
        {
            //keyPress direction logic
            if (keyPress == "up" && !isPressedDown) { isPressedUp = !isPressedUp; }
            if (keyPress == "down" && !isPressedUp) { isPressedDown = !isPressedDown; }
            if (keyPress == "left" && !isPressedRight) { isPressedLeft = !isPressedLeft; }
            if (keyPress == "right" && !isPressedLeft) { isPressedRight = !isPressedRight; }

            //---------------------------------------------------------------------------------------------------------
            //ADD FIX FOR THE LIKES OF PRESS.LEFT, PRESS.RIGHT, KEYUP.LEFT (+ UP/DOWN). DIRECTION CHANGES IN SUCH CASES

            //current movement direction (pressing an opposite (e.g. left while already moving right) does nothing)
            string keyDirection;
            if (!isPressedUp && !isPressedDown && !isPressedLeft && !isPressedRight)
            {
                keyDirection = "still";
            }
            else if (isPressedUp && !isPressedDown && !isPressedLeft && !isPressedRight)
            {
                keyDirection = "up";
            }
            else if (!isPressedUp && isPressedDown && !isPressedLeft && !isPressedRight)
            {
                keyDirection = "down";
            }
            else if (!isPressedUp && !isPressedDown && isPressedLeft && !isPressedRight)
            {
                keyDirection = "left";
            }
            else if (!isPressedUp && !isPressedDown && !isPressedLeft && isPressedRight)
            {
                keyDirection = "right";
            }
            else if (isPressedUp && !isPressedDown && isPressedLeft && !isPressedRight)
            {
                keyDirection = "up-left";
            }
            else if (isPressedUp && !isPressedDown && !isPressedLeft && isPressedRight)
            {
                keyDirection = "up-right";
            }
            else if (!isPressedUp && isPressedDown && isPressedLeft && !isPressedRight)
            {
                keyDirection = "down-left";
            }
            else if (!isPressedUp && isPressedDown && !isPressedLeft && isPressedRight)
            {
                keyDirection = "down-right";
            }
            else
            { //just in case...
                keyDirection = "still";
            }

            //logging
            //this is certainly not buffered, as there can be just a few keys pressed thru runtime
            string currentData = fileNameTime + separatorItem + controllerCounter + separatorItem +
                                 GetCurrentTimestamp() + separatorItem + GetCurrentTime() + separatorItem +
                                 GetCurrentPosition() + separatorItem +
                                 keyPress + separatorItem +
                                 isDown + separatorItem +
                                 keyDirection +
                                 "\r\n";
            //System.IO.File.AppendAllText(controllerFileName, currentData);
            streamWriterController.Write(currentData);
            controllerCounter++;
        }
    }

    //external freeform data logger; takes an event string as an input (meaning/format up to the external script)
    public void logEventData(string eventInfo)
    {
        if (!eventLog)
        {
            Debug.LogWarning("Event logging called despite this being disabled. More info: " + eventInfo);
        }
        else
        {
            string currentData = fileNameTime + separatorItem + eventLogCounter + separatorItem +
                                 GetCurrentTimestamp() + separatorItem + GetCurrentTime() + separatorItem +
                                 eventInfo +
                                 "\r\n";
            //System.IO.File.AppendAllText(eventLogFileName, currentData);
            streamWriterEventLog.Write(currentData);
            eventLogCounter++;
        }
    }

    //external pre-specified data logger with dedicated log file to it; can be multiple files, has to specify which one
    //format validity is up to the external script
    public bool logCustomData(string logName, string customData, GameObject loggedObject = null)
    {
        //verify if log file exists
        int targettedCustomLogId = 0;
        if (!customLogNames.Contains(logName))
        {
            Debug.LogWarning("Custom logging called into a non-existent log file: " + logName + ". Aborting.");
            return false;
        }
        else
        {
            targettedCustomLogId = customLogNames.IndexOf(logName);
            string currentData = fileNameTime + separatorItem +
                                 customLogCounters[targettedCustomLogId] + separatorItem +
                                 GetCurrentTimestamp() + separatorItem + GetCurrentTime() + separatorItem +
                                 //////////////////////////////////////////////////////////////////////////////////
                                 //TODO: implement a unified log requesting feature
                                 //////////////////////////////////////////////////////////////////////////////////
                                 GetCurrentPosition(loggedObject) + separatorItem +
                                 //////////////////////////////////////////////////////////////////////////////////
                                 //TODO: implement a unified log requesting feature
                                 //////////////////////////////////////////////////////////////////////////////////
                                 customData +
                                 "\r\n";

            //log, or buffer to log
            customLogBuffers[targettedCustomLogId] += currentData;
            if (customLogCounters[targettedCustomLogId] % bufferSize == 0)
            {
                //System.IO.File.AppendAllText(customLogFileNames[targettedCustomLogId],
                //                             customLogBuffers[targettedCustomLogId]);

                //file writes are handled by streamwriter now -- faster than File.AppenAllText
                //      File.AppenAllText open a file, writes to it, and closes
                //      StreamWriter just writes...
                customLogStreamWriters[targettedCustomLogId].Write(customLogBuffers[targettedCustomLogId]);
                customLogBuffers[targettedCustomLogId] = "";
            }
            customLogCounters[targettedCustomLogId]++;
            return true;
        }
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Auxilary functions
    // ----------------------------------------------------------------------------------------------------------------

    // Get current time
    string GetCurrentTime()
    {
        long milliseconds = (System.DateTime.Now.Ticks / System.TimeSpan.TicksPerMillisecond) % 1000;
        return System.DateTime.Now.ToString("HH,mm,ss").Replace(",", separatorItem) + separatorItem + milliseconds;
    }

    //Get current time in Unix epoch format
    string GetCurrentTimestamp()
    {
        var currentTime = new System.DateTimeOffset(System.DateTime.Now).ToUniversalTime().ToUnixTimeSeconds();
        return currentTime.ToString();
    }

    // Get current player position and look direction
    string GetCurrentPosition(GameObject loggedObject = null)
    {
        //if no object to track specified, assume itself
        if (loggedObject == null) loggedObject = this.gameObject;

        //string coordinates = transform.position.ToString().Trim('(', ')');
        string coordinates = loggedObject.transform.position.x.ToString(numberFormat) + separatorItem +
                             loggedObject.transform.position.y.ToString(numberFormat) + separatorItem +
                             loggedObject.transform.position.z.ToString(numberFormat);
        //player mouse center, player gaze center (VR only)
        //transform.rotation -- in radians; transform.rotation.eulerAngles -- in degrees
        //string rotationMouse = transform.rotation.eulerAngles.ToString().Trim('(', ')');
        string rotationMouse = loggedObject.transform.rotation.eulerAngles.x.ToString(numberFormat) + separatorItem +
                               loggedObject.transform.rotation.eulerAngles.y.ToString(numberFormat) + separatorItem +
                               loggedObject.transform.rotation.eulerAngles.z.ToString(numberFormat);
        //string rotationGaze = headCamera.transform.rotation.eulerAngles.ToString().Trim('(', ')');
        string rotationGaze = headCamera.transform.rotation.eulerAngles.x.ToString(numberFormat) + separatorItem +
                              headCamera.transform.rotation.eulerAngles.y.ToString(numberFormat) + separatorItem +
                              ((Mathf.Round(headCamera.transform.rotation.eulerAngles.z * 100)) / 100.0)
                              .ToString(numberFormat); //dirty fix
        //Debug.Log("camera gloabl Y rotation: " + headCamera.transform.rotation.eulerAngles.y);
        //Debug.Log("camera local  Y rotation: " + headCamera.transform.localRotation.eulerAngles.y);
        return coordinates + separatorItem + rotationMouse + separatorItem + rotationGaze;
    }

    // Get current player position and look direction -- directly acessing the main camera (works better)
    //TODO: probably delete, uselesss...
    string GetCurrentPositionDirectly()
    {
        string coordinates = cleanNumericData(transform.position.ToString());
        string rotationMouse = Camera.main.transform.eulerAngles.x.ToString(numberFormat) + separatorItem +
                               Camera.main.transform.eulerAngles.y.ToString(numberFormat) + separatorItem + "0";
        string rotationGaze = rotationMouse; //not relevant, as this solution logs them the same
        return coordinates + separatorItem + rotationMouse + separatorItem + rotationGaze;
    }

    //do some cleaning on nasty numeric data converted to string, e.g. Vector3
    public string cleanNumericData(string inputString)
    {
        string outputString = inputString.Replace("(", "").Replace(")", "").Replace(",", separatorItem);
        //do more cleaning, if applicable
        return outputString;
    }

    //get separator/decimal format (useful for external scripts that send sth to the logger)
    public Dictionary<string, string> getLogFormat() {
        Dictionary<string, string> formatDictionary = new Dictionary<string, string>();
        formatDictionary.Add("decimalFormat", separatorDecimal);
        formatDictionary.Add("separatorFormat", separatorItem);
        return formatDictionary;
    }

    //verify if logging directory exists
    public bool verifyLoggingDirectory()
    {
        if (Directory.Exists(saveLocation))
        {
            return true;
        }
        else 
        {
            try
            {
                Directory.CreateDirectory(saveLocation);
            }
            catch (IOException e)
            {
                return false;
            }
            return true;
        }
    }
}
