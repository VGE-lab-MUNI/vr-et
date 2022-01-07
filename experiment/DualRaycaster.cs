// --------------------------------------------------------------------------------------------------------------------
// Dual raycaster script, version 2021-11-11
// This script processes (and debugs/visualizes) currently captured eye-tracking data, to send them to a logger.
//
// The script is derived from SRanipal API (based on SRanipal_EyeFocusSample_v2).
// Functionality: To obtain the data from the Focus function which enables to find the focal point of eyes in VR.
// 
// Use: pair it with logger (PathScript).
// The external logger already user camera position and rotation by default, as well as timestamp.
// The eye-tracking data (gaze coordinates, raycast.hit object) as passed on to the logger through this script. 
//
// Script setup and options:
//   Max distance: maximum eye-tracking distance, in meters
//   Dual raycaster settings: if enabled; which layers to ignore
//   Gaze point visualization
//   Calibration settings (per SRanipal functionality)
//   Logger reference (PathScript logger)
//   Logging settings (gaze position, gazed object name, etc.)
// --------------------------------------------------------------------------------------------------------------------
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ViveSR.anipal.Eye;
using System.Globalization;

public class DualRaycaster : MonoBehaviour
{
    //SRanipal objects/controls
    private FocusInfo focusInfoFirst, focusInfoSecond;
    private readonly GazeIndex[] gazePriority = new GazeIndex[] { GazeIndex.COMBINE, GazeIndex.LEFT, GazeIndex.RIGHT };
    private static EyeData_v2 EyeData = new EyeData_v2();
    private bool eyeCallbackRegistered = false;
    private GameObject gazeSphere;
    private int ignoreLayerBitMaskFirst, ignoreLayerBitMaskSecond;

    [Space(10)]
    [SerializeField] private float maxDistance = 100;
    //dual raycaster settings
    [SerializeField] private bool dualRaycaster;
    [SerializeField] private List<int> dualRaycasterIgnoreLayers;

    //visuals
    [Space(10)]
    [SerializeField] private LineRenderer gazeRayRenderer;
    [SerializeField] private bool renderGazeRay;
    [SerializeField] private bool drawGazeSphere;
    //if no ET data or !drawGazeSphere, hide the ET gaze sphere
    private Vector3 gazeSphereHiddenPosition = new Vector3(0f, -10000f, 0f);
    [SerializeField] private KeyCode GazeVisualizationKey;

    //calibration
    [Space(10)]
    [SerializeField] private bool runCalibrationOnStart;
    [SerializeField] private bool runCalibrationOnKeypress;
    [SerializeField] private KeyCode CalibrationKey;
    
    //Logging setup
    [Space(10)]
    public GameObject Logger;
    public string logName = "HtcEtLog";
    private bool loggerInitialized;
    private string customLogVariables;
    private Dictionary<string, string> LoggerFormatDictionary = new Dictionary<string, string>();
    [SerializeField] private bool logEtPosition = true;
    [SerializeField] private bool logFixatedObject = true;
    [SerializeField] private bool logBothEyes;
    [SerializeField] private bool logAccuracy;
    private NumberFormatInfo numberFormat;
    private string logData;

    //GUI fixation feedback
    [Space(10)]
    [SerializeField] private bool logFixationToCanvas;
    [SerializeField] private Text FixationCanvas;
    private string fixationReport;

    private void Start()
    {
        if (!SRanipal_Eye_Framework.Instance.EnableEye)
        {
            enabled = false;
            return;
        }

        if (runCalibrationOnStart)
        {
            SRanipal_Eye_API.LaunchEyeCalibration(System.IntPtr.Zero);
        }

        //GazeRayParameter param = new GazeRayParameter();
        //param.sensitive_factor = 1;
        //EyeParameter eyeParam = new EyeParameter();
        //eyeParam.gaze_ray_parameter = param;
        //SRanipal_Eye_API.SetEyeParameter(eyeParam);

        //instantiate the gaze sphere (either to be usede or not)
        gazeSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Collider gazeSphereCollider = gazeSphere.GetComponent<Collider>();
        gazeSphereCollider.enabled = false;
        gazeSphere.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        gazeSphere.transform.position = gazeSphereHiddenPosition;
        gazeSphere.SetActive(drawGazeSphere);

        //ignoreLayer is a bit mask that tells which layers should be subjected to Physics.Raycast function.
        //once bitmask is inverted, only the layers with id of ignoreLayer is really ignored.
        ignoreLayerBitMaskFirst = 0;
        ignoreLayerBitMaskFirst = ~ignoreLayerBitMaskFirst;
        //dual raycaster bitmask has multiple specificifiable layers to ignore
        if (dualRaycaster)
        {
            foreach (int i in dualRaycasterIgnoreLayers)
            {
                if (i > 0) { ignoreLayerBitMaskSecond = 1 << i - 1; }
            }
            ignoreLayerBitMaskSecond = ~ignoreLayerBitMaskSecond;
        }

        //init the logger (PathScript, in this case)
        if (Logger != null && Logger.GetComponent<PathScript>() != null)
        {
            LoggerFormatDictionary = Logger.GetComponent<PathScript>().getLogFormat();
            //set number format, per what is specified in logger
            numberFormat = new NumberFormatInfo();
            numberFormat.NumberDecimalSeparator = LoggerFormatDictionary["decimalFormat"];
            generateCustomLogVariables();
            Logger.GetComponent<PathScript>().generateCustomFileNames(customLogVariables, logName, this.name);
            loggerInitialized = true;
        }
        else
        {
            Debug.LogWarning("No eyetracking logger found on " + Logger.name +
                             ". Therefore, HTC ET script on " + this.name + " not logging.");
        }

        //init the Canvas GUI fixation logger
        if (FixationCanvas == null && logFixationToCanvas)
        {
        	Debug.LogWarning("No fixation canvas for ET logging. Disabling this functionality");
        	logFixationToCanvas = false;
        }
    }

    private void Update()
    {
        if (SRanipal_Eye_Framework.Status != SRanipal_Eye_Framework.FrameworkStatus.WORKING &&
            SRanipal_Eye_Framework.Status != SRanipal_Eye_Framework.FrameworkStatus.NOT_SUPPORT) return;

        //listen to key control inputs
        if (runCalibrationOnKeypress && Input.GetKeyDown(CalibrationKey))
        {
            SRanipal_Eye_API.LaunchEyeCalibration(System.IntPtr.Zero);
        }
        if (Input.GetKeyDown(GazeVisualizationKey))
        {
            drawGazeSphere = !drawGazeSphere;
            gazeSphere.SetActive(drawGazeSphere);
        }

        if (SRanipal_Eye_Framework.Instance.EnableEyeDataCallback == true && eyeCallbackRegistered == false)
        {
            SRanipal_Eye_v2.WrapperRegisterEyeDataCallback(
                Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback));
            eyeCallbackRegistered = true;
        }
        else if (SRanipal_Eye_Framework.Instance.EnableEyeDataCallback == false && eyeCallbackRegistered == true)
        {
            SRanipal_Eye_v2.WrapperUnRegisterEyeDataCallback(
                Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback));
            eyeCallbackRegistered = false;
        }

        foreach (GazeIndex index in gazePriority)
        {
            Ray GazeRay;
            bool eyeFocusFirst, eyeFocusSecond;

            //eye call back should be enabled so we can get extra EyeData and save them in a logger 
            if (eyeCallbackRegistered)
            {
                eyeFocusFirst = SRanipal_Eye_v2.Focus(index, out GazeRay, out focusInfoFirst, 0,
                                                  maxDistance, ignoreLayerBitMaskFirst, EyeData);
                if (dualRaycaster)
                {
                    eyeFocusSecond = SRanipal_Eye_v2.Focus(index, out GazeRay, out focusInfoSecond, 0,
                                                  maxDistance, ignoreLayerBitMaskSecond, EyeData);
                }
                //Can be used if we don't want to specify layer to be ignored
                //eye_focus = SRanipal_Eye_v2.Focus(index, out GazeRay, out FocusInfo, 0, MaxDistance, eyeData);
            }
            else
            {
                eyeFocusFirst = SRanipal_Eye_v2.Focus(index, out GazeRay, out focusInfoFirst, 0,
                                                  maxDistance, ignoreLayerBitMaskFirst);
                if (dualRaycaster)
                {
                    eyeFocusSecond = SRanipal_Eye_v2.Focus(index, out GazeRay, out focusInfoSecond, 0,
                                                  maxDistance, ignoreLayerBitMaskSecond);
                }
            }
            //Debug.Log("Time_delta_unity: " + Time.deltaTime*1000 + "Time_unity: " + Time.time*1000 +
            //          " Frame_unity: " + Time.frameCount + " Time_ET: " + EyeData.timestamp +
            //          " Frame_ET: " + EyeData.frame_sequence);

            //ET Logging
            //if(focusInfo.collider != null) Debug.Log("Looking at: " + focusInfo.collider.gameObject.name);
            if (loggerInitialized)
            {
                logData = "";
                //for 3D ET projection (collider hit) coordinate
                if (logEtPosition)
                {
                    logData += focusInfoFirst.point.x + LoggerFormatDictionary["separatorFormat"] +
                               focusInfoFirst.point.y + LoggerFormatDictionary["separatorFormat"] +
                               focusInfoFirst.point.z;
                    if (dualRaycaster)
                    {
                        logData += LoggerFormatDictionary["separatorFormat"] + 
                                   focusInfoSecond.point.x + LoggerFormatDictionary["separatorFormat"] +
                                   focusInfoSecond.point.y + LoggerFormatDictionary["separatorFormat"] +
                                   focusInfoSecond.point.z;
                    }
                }
                //for collider hit name
                if (logFixatedObject)
                {
                    if (!string.IsNullOrEmpty(logData))
                    {
                        logData += LoggerFormatDictionary["separatorFormat"];
                    }

                    if (focusInfoFirst.collider == null)
                    {
                        if (eyeCallbackRegistered && EyeData.no_user)
                        {
                            fixationReport = "no user";
                            if (dualRaycaster) {
                                fixationReport += LoggerFormatDictionary["separatorFormat"] + "no user";
                            }
                        }
                        else
                        {
                            fixationReport = "no data";
                            if (dualRaycaster || focusInfoSecond.collider == null)
                            {
                                fixationReport += LoggerFormatDictionary["separatorFormat"] + "no data";
                            }
                        }
                    }
                    else
                    {                        
                        fixationReport = focusInfoFirst.collider.gameObject.name;
                        if (dualRaycaster)
                        {
                            fixationReport += LoggerFormatDictionary["separatorFormat"] +
                                              focusInfoSecond.collider.gameObject.name;
                        }
                    }
                    logData += fixationReport;
                }
                //extra data per left/right eye
                if (logBothEyes)
                {
                    //TODO...
                }
                //extra data per HTC eyetracker service variables
                if (logAccuracy)
                {
                    //TODO...
                }

                //finally, sent to logger...
                Logger.GetComponent<PathScript>().logCustomData(logName, logData, this.gameObject);
            }

            //canvas logging
            if (logFixationToCanvas)
            {
               	FixationCanvas.text = fixationReport;
            }

            if (eyeFocusFirst)
            {
                //renders user-point
                // the line has coordinates of gaze ray previosly calulated by Focus function 
                if (renderGazeRay && gazeRayRenderer != null)
                {
                    Vector3 GazeDirectionCombined_FromFocus =
                        Camera.main.transform.TransformDirection(GazeRay.direction);
                    gazeRayRenderer.SetPosition(0, Camera.main.transform.position);
                    gazeRayRenderer.SetPosition(1, Camera.main.transform.position +
                                                GazeDirectionCombined_FromFocus * maxDistance);
                }

                if (drawGazeSphere)
                {
                    gazeSphere.transform.position = focusInfoFirst.point;
                }
                break;
            }
        }
    }

    private void Release()
    {
        if (eyeCallbackRegistered == true)
        {
            SRanipal_Eye_v2.WrapperUnRegisterEyeDataCallback(
                Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback));
            eyeCallbackRegistered = false;
        }
    }

    private static void EyeCallback(ref EyeData_v2 eye_data)
    {
        EyeData = eye_data;
    }

    public EyeData_v2 GetEyeData()
    {
        return EyeData;
    }

    //returns just the first row for the log file to know what columns is what variable
    public string generateCustomLogVariables()
    {
        //for 3D ET projection (collider hit) coordinate
        if (logEtPosition)
        {
            customLogVariables += "EtPositionX" + LoggerFormatDictionary["separatorFormat"] +
                                  "EtPositionY" + LoggerFormatDictionary["separatorFormat"] +
                                  "EtPositionZ";
            if (dualRaycaster)
            {
                customLogVariables += LoggerFormatDictionary["separatorFormat"] +
                                      "EtPositionDualX" + LoggerFormatDictionary["separatorFormat"] +
                                      "EtPositionDualY" + LoggerFormatDictionary["separatorFormat"] +
                                      "EtPositionDualZ";
            }
        }
        //for collider hit name
        if (logFixatedObject)
        {
            if (!string.IsNullOrEmpty(customLogVariables))
            {
                customLogVariables += LoggerFormatDictionary["separatorFormat"];
            }
            customLogVariables += "FixatedObjectName";

            if (dualRaycaster)
            {
                customLogVariables += LoggerFormatDictionary["separatorFormat"] + "FixatedObjectDualName";
            }
        }
        //extra data per left/right eye
        if (logBothEyes)
        {
            //TODO...
        }
        //extra data per HTC eyetracker service variables
        if (logAccuracy)
        {
            //TODO...
        }
        //this is to be added to the log that is to be created
        return customLogVariables;
    }
}
