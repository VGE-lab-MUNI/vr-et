//includes
import java.util.Arrays; //for array sorting

//set-up variables (directories, processing type)
public static String DIRECTORY_SOURCE = "source";
public static String DIRECTORY_OUTPUT = "output";
public static String DATASET_PREFIX = "Turkey_West"; //to be added to the output table
enum loadSetup {FILE, DIRECTORY};
public loadSetup LOAD_AS = loadSetup.DIRECTORY; //to load either on directory or file level
//set-up variables (scenes)
int SCENE_SEARCH_FROM = 3;      //scene 1-2 is blank and test scene
int SCENE_SEARCH_TO = 32;       //scene 3-32 includes the 30 exp. scenes
//set-up variables (dataloss)
boolean RECOMPUTE_DATALOSS = true;            //if the "isConsideredDataLoss" column is recalculated by different params
boolean REMOVE_SMALL_DATALOSS = true;         //if small dataloss segments are to be deleted
float THRESHOLD_RECOMPUTE_DATALOSS = 0.00002; //the difference below which is dataloss (angles)
float THRESHOLD_REMOVE_SMALL_DATALOSS = 100; //the small dataloss threshold (miliseconds)
//set-up variables (delete/join brief ET entries)
public static int THRESHOLD_DELETE = 45;    //in miliseconds
public static int THRESHOLD_JOIN = 45;      //in miliseconds

//operational variables
public StringList filesEventlogNames = new StringList();
public StringList filesRaycasterNames = new StringList();
public Table[] FilesEventlog;  //all "eventLog" files in a folder
public Table[] FilesRaycaster; //all "raycastTargetter" files in a folder
public int filesAmount;        //1 if loadSetup.FILE
public StringList filesPathNames = new StringList();  //to get file path name, in absolute path
public StringList filesIdType = new StringList();     //id = pathNames[i].split(_); id[id.length-2]
                                                      //(the file type ("eventlog" or "raycastTargetter"))
public StringList filesIdDate = new StringList();     //id = pathNames[i].split(_); id[id.length-1]
                                                      //(file generation date, e.g. "20201019")
public StringList filesIdTime = new StringList();     //id = pathNames[i].split(_); id[id.length-0]
                                                      //(file generation time and tail-end, e.g. "093727.txt")
//verification variables
public boolean filesLoadedCorrectly; //equal amount of eventlogs to raycast files
public float dwellTimeBaseline;      //first raycast file entry time
//algorithm-processing container variables
public Table CurrentRawTable;       //to be rewritten on a per-log basis
public Table CurrentRenamedTable;   //to be rewritten on a per-log basis
public Table CurrentDatalossTable;  //to be rewritten on a per-log basis
public Table CurrentScenetimeTable; //to be rewritten on a per-log basis
public Table CurrentGazepointTable; //to be rewritten on a per-log basis
public Table CurrentStatisticsTable;    //to be rewritten on a per-log basis
public Table AllStatisticsTable;        //to store all the OutputTable as one

void setup() {
  noLoop();
}

void draw() {
  //load either single log/rayCast log, or a whole directory
  if (LOAD_AS == loadSetup.FILE) {
    //selectInput("Select log Datasource:", "selectLogFile");
    //selectInput("Select ET Datasource:", "selectEtFile");  
  } else if (LOAD_AS == loadSetup.DIRECTORY) {
    selectDirectory(DIRECTORY_SOURCE);
  }
  
  //init needed variables
  AllStatisticsTable = initTableStatistics();
  
  //if initialized correctly, process
  if (filesLoadedCorrectly) {
    printLogBorder();   
    for(int i = 0; i < filesAmount; i++) {
      println("Processing Log " + (i+1) + "/" + filesAmount);
      //1 filter gazepoint data to FO1, FO2, background
      CurrentRawTable = FilesRaycaster[i];
      CurrentRenamedTable = filterGazePoints(CurrentRawTable);
      dwellTimeBaseline = getRowTime(CurrentRenamedTable, 0);
      //2 create timetable per all scenes in a table (1-30)
      CurrentScenetimeTable = generateTimesTable(FilesEventlog[i]);
      //3+4 compute dataloss and add it to the filtered table
      CurrentDatalossTable = recomputeDataloss(CurrentRenamedTable);
      CurrentDatalossTable = removeSmallDataloss(CurrentRenamedTable);
      //5+6 clear gazepoint intervals
      CurrentGazepointTable = generateGazepointTable(CurrentDatalossTable);
      CurrentGazepointTable = recomputeGazepointScenetime(CurrentGazepointTable, CurrentScenetimeTable);
      CurrentGazepointTable = clearGazepointDuration( CurrentGazepointTable);
      //7 generate output and metrics
      CurrentStatisticsTable = generateStatistics(CurrentGazepointTable);
      
      //append to the all-tables
      AllStatisticsTable = appendStatisticsTable(CurrentStatisticsTable, AllStatisticsTable);
      
      //save the single output table (also useful for debug (seeing the intermediate steps))
      println("Saving Log " + (i+1) + "/" + filesAmount);
      saveTable(CurrentRenamedTable, "/" + DIRECTORY_OUTPUT + "/" +
                DATASET_PREFIX + "_" + filesRaycasterNames.get(i) + "_1_renamed.csv");
      saveTable(CurrentScenetimeTable, "/" + DIRECTORY_OUTPUT + "/" +
                DATASET_PREFIX + "_" + filesRaycasterNames.get(i) + "_2_scenetime.csv");
      saveTable(CurrentDatalossTable, "/" + DIRECTORY_OUTPUT + "/" +
                DATASET_PREFIX + "_" + filesRaycasterNames.get(i) + "_3_dataloss.csv");
      saveTable(CurrentGazepointTable, "/" + DIRECTORY_OUTPUT + "/" +
                DATASET_PREFIX + "_" + filesRaycasterNames.get(i) + "_4_gazepoint.csv");
      saveTable(CurrentStatisticsTable, "/" + DIRECTORY_OUTPUT + "/" +
                DATASET_PREFIX + "_" + filesRaycasterNames.get(i) + "_5_stats.csv");             
      //clear the temp tables
      CurrentRawTable = null;
      CurrentRenamedTable = null;
      CurrentDatalossTable = null;
      CurrentScenetimeTable = null;
      CurrentGazepointTable = null;
      CurrentStatisticsTable = null;
      dwellTimeBaseline = 0;
    }
    //write the table including all files
    printLogBorder();
    println("Saving the overall output table.");
    saveTable(AllStatisticsTable, "/" + DIRECTORY_OUTPUT + "/" + DATASET_PREFIX + "_ALL.csv");
    printLogBorder();
    println("All data processing done.");
  }
}

//----------------------------------------------------------------------------------------------------------------------

public void selectLogFile() {
  
}

public void selectEtFile() {
  
}

public void selectDirectory(String directory) {
  //selectFolder("Select a folder to process:", "folderSelected");
  listFileNames(sketchPath() + "/" + DIRECTORY_SOURCE + "/");
}

public void folderSelected(File selection) {
  if (selection == null) {
    println("Window was closed or the user hit cancel.");
  } else {
    println("User selected " + selection.getAbsolutePath());
  }
}

public void listFileNames(String dir) {
  File file = new File(dir);
  if (file.isDirectory()) {
    //init variables, per existing files
    String[] split = null;
    String[] fileListTemp = file.list();
    for(int i = 0; i < fileListTemp.length; i ++) {
      filesPathNames.set(i,fileListTemp[i].toString());
    }
    //throw away irrelevant files
    int filePathSizeTemp = filesPathNames.size();
    for (int i = 0; i < filePathSizeTemp; i++) {
      if (!filesPathNames.get(i).contains("eventlog") && !filesPathNames.get(i).contains("raycastTargetter")) {
        filesPathNames.remove(i);
        i--;
        filePathSizeTemp = filesPathNames.size();        
      }
    }
    //init file info for it to be parseable into relate tuples
    for(int i = 0; i < filesPathNames.size(); i++) {
      //println(i + " -- " + filesPathNames.get(i));
      split = filesPathNames.get(i).split("_");
      filesIdType.append(split[split.length-3]);
      filesIdDate.append(split[split.length-2]);
      filesIdTime.append(split[split.length-1]);
    }
    //parse the tuples (eventlog:raycastTargetter, 1:1)
    for(int i = 0; i < filesIdType.size(); i++) {
      //compare all eventlog dates...
      if (filesIdType.get(i).equals("eventlog")) {
        String thisTupleDateMatch1 = filesIdDate.get(i);
        String thisTupleTimeMatch1 = filesIdTime.get(i);
        for(int j = 0; i < filesIdType.size(); j++) {
          //...against all raycastTargetter dates
          if (filesIdType.get(j).equals("raycastTargetter")) {
            String thisTupleDateMatch2 = filesIdDate.get(j);
            String thisTupleTimeMatch2 = filesIdTime.get(j);
            //and once found, add them
            if (thisTupleDateMatch1.equals(thisTupleDateMatch2) && thisTupleTimeMatch1.equals(thisTupleTimeMatch2)) {
              filesEventlogNames.append(filesPathNames.get(i));
              filesRaycasterNames.append(filesPathNames.get(j));
              i++;
              break;
            }
          }
        }
      }
    }  
    //load the parsed tuples into an array of Tables
    FilesEventlog = new Table[filesEventlogNames.size()];
    FilesRaycaster = new Table[filesRaycasterNames.size()];
    printLogBorder();
    println("Loading Et datafiles to memory. This may take up to several GB of RAM.");
    printLogBorder();
    for(int i = 0; i < filesEventlogNames.size(); i++) {      
      println(i + ": " + filesEventlogNames.get(i) + " -- " + filesRaycasterNames.get(i));
      FilesEventlog[i] = loadTable(sketchPath() + "/" + DIRECTORY_SOURCE + "/" +
                         filesEventlogNames.get(i), "header,csv");
      FilesRaycaster[i] = loadTable(sketchPath() + "/" + DIRECTORY_SOURCE + "/" +
                          filesRaycasterNames.get(i), "header,csv");
    }    
    //succesfull init - files loaded and parsed
    printLogBorder();
    println("Files loaded from directory " + DIRECTORY_SOURCE +
            ". Total " + filesEventlogNames.size() + " file tuples to process.");
    filesAmount = filesEventlogNames.size();
    filesLoadedCorrectly = true;    
  } else {
    //failed to init - not a directory
    println("Failed to load - not a directory");
    filesLoadedCorrectly = false;
  }
  printLogBorder();
}

//----------------------------------------------------------------------------------------------------------------------

public Table filterGazePoints(Table SourceRawTable) {
  Table OutputTable = SourceRawTable;
  int tableRows = OutputTable.getRowCount();
  for (int i = 0; i < tableRows; i++) {
    if(!OutputTable.getRow(i).getString("objectHitName").contains("_scene_")) { //exclude objects FO1/2_scene_0XY
      if(OutputTable.getRow(i).getString("objectHitName").equals("FixationPlane")) { //and delete fixation plane gaze
        OutputTable.removeRow(i);
        tableRows--;
        i--;
      } else {
        OutputTable.setString(i, "objectHitName", "background"); //and override everything else to background
      }
    //fix for crappy object naming in Unity scene
    //  e.g. FO1_scene_10_(3), FO1_scene_10_(4), F01_scene_... all are the same thing...
    } else {
      if (OutputTable.getRow(i).getString("objectHitName").contains("1_scene_")) {
        OutputTable.getRow(i).setString("objectHitName", "FO1");
      } else if (OutputTable.getRow(i).getString("objectHitName").contains("2_scene_")) {
        OutputTable.getRow(i).setString("objectHitName", "FO2");
      }
    }
  }
  return OutputTable;
}

public Table generateTimesTable(Table SourceEventlogTable) {
  Table OutputTable = initTableTimes();
  //auxilary variables
  int sourceTableRows = SourceEventlogTable.getRowCount();
  int sceneSearchFrom = 3; //because 1 and 2 are empty scenes
  int sceneSearchTo = 32;  //... and the 30 exp. scenes are 3-32
  float timeFrom;
  float timeTo;
  //sweep through the source table for dataloss rows, to log
  for (int j = sceneSearchFrom; j <= sceneSearchTo; j++) {
    for(int i=1; i < sourceTableRows; i++) {
      if (SourceEventlogTable.getRow(i).getString("eventInfo").equals(
          "TriggerTimer ScenesController triggered with state " + j + " on object FixationPlane")) {
        timeFrom = getRowTime(SourceEventlogTable,i); //this row is scene start time
        timeTo = getRowTime(SourceEventlogTable,i+1); //next row is scene end time
        TableRow newRow2 = OutputTable.addRow();
        newRow2.setString("participantId", SourceEventlogTable.getRow(0).getString("userId"));
        newRow2.setFloat("sceneId", j-2);
        newRow2.setFloat("sceneStart", (timeFrom - dwellTimeBaseline));
        newRow2.setFloat("sceneEnd", (timeTo - dwellTimeBaseline));
        newRow2.setFloat("sceneTime", (timeTo - timeFrom));
        newRow2.setFloat("hour", SourceEventlogTable.getRow(i).getFloat("hour"));
        newRow2.setFloat("min", SourceEventlogTable.getRow(i).getFloat("min"));
        newRow2.setFloat("sec", SourceEventlogTable.getRow(i).getFloat("sec"));
        newRow2.setFloat("ms", SourceEventlogTable.getRow(i).getFloat("ms"));
      }
    }
  }
  return OutputTable;
}

public Table recomputeDataloss (Table SourceRawTable) {
  int sourceTableRows = SourceRawTable.getRowCount();
  boolean flaggedForDataloss = false;
  float flaggedForDatalossAtAngle = 0;
  int flaggedForDatalossAtMeasurement = 0;
  for(int i=1; i < sourceTableRows; i++) {
    if ((abs(SourceRawTable.getRow(i).getFloat("cameraEyetrackingAngleDifference") - 
             SourceRawTable.getRow(i-1).getFloat("cameraEyetrackingAngleDifference"))
        < THRESHOLD_RECOMPUTE_DATALOSS) && !flaggedForDataloss)
    {
      flaggedForDatalossAtAngle = SourceRawTable.getRow(i).getFloat("cameraEyetrackingAngleDifference");
      flaggedForDataloss = true;
      SourceRawTable.getRow(i).setString("isConsideredDataLoss", "False");
    } else if (flaggedForDataloss) {
      if (abs(SourceRawTable.getRow(i).getFloat("cameraEyetrackingAngleDifference") - flaggedForDatalossAtAngle)
          < THRESHOLD_RECOMPUTE_DATALOSS)
      {
        SourceRawTable.getRow(i).setString("isConsideredDataLoss", "True");
      } else {
        SourceRawTable.getRow(i).setString("isConsideredDataLoss", "False");
        flaggedForDataloss = false;
        flaggedForDatalossAtAngle = 0;
      }
    } else {
      SourceRawTable.getRow(i).setString("isConsideredDataLoss", "False");
    }
  }
  return SourceRawTable;
}

public Table removeSmallDataloss (Table SourceRawTable) {
  int sourceTableRows = SourceRawTable.getRowCount();
  boolean isCountingDatalossInterval = false;
  float countFromTime = 0;
  float countToTime = 0;
  int countFromId = 0;
  int countToId = 0;
  //sweep through the dataloss columnm, throw away small dataloss entries
  for(int i=0; i < sourceTableRows; i++) {
    //start the count here
    if (!isCountingDatalossInterval && SourceRawTable.getRow(i).getString("isConsideredDataLoss").equals("True")) {
      isCountingDatalossInterval = true;
      countFromTime = getRowTime(SourceRawTable,i); 
      countFromId = i;
    //end the count here
    } else if (isCountingDatalossInterval) {
      if (SourceRawTable.getRow(i).getString("isConsideredDataLoss").equals("False")) {
        countToTime = getRowTime(SourceRawTable,i-1);
        countToId = i-1;
        //and determine what to do with it (delete small ones, keep big ones)
        if ((countToTime - countFromTime) < THRESHOLD_REMOVE_SMALL_DATALOSS) {
          for (int j = countFromId; j < countToId; j++) {
            SourceRawTable.getRow(j).setString("isConsideredDataLoss", "False");
          }
        } else {
          //do nothing on the dataloss column -- just keep it as is
          //also rewrite the fixated object name to "dataloss"
          for (int j = countFromId; j < countToId; j++) {
            SourceRawTable.getRow(j).setString("objectHitName", "dataloss");
          }
        }
        //clear local vars for next iteration
        isCountingDatalossInterval = false;
        countFromTime = 0;
        countToTime = 0;
        countFromId = 0;
        countToId = 0;
      }
    }
  }
  return SourceRawTable;
}

public Table generateGazepointTable (Table SourceRawTable) {
  //init the table and the constants
  Table OutputTable = initTableGazePoint();
  int thisTableRows = SourceRawTable.getRowCount();
  String thisParticipantId = SourceRawTable.getRow(0).getString("userId");
  //init the auxiliary variables
  boolean isPointingAtObject = false;
  int isPointingFromId = 0;
  float isPointingFromTime = 0;
  float isPointingToTime = 0;
  String pointingobjectHitName = "";
  //sweep through the table; count up duration, instead of the extra rows
  for(int i = 0; i < thisTableRows; i++) {
    //get the current object
    if (!isPointingAtObject) {
      isPointingAtObject = true;
      isPointingFromId = i;
      isPointingFromTime = getRowTime(SourceRawTable,i);
      pointingobjectHitName = SourceRawTable.getRow(i).getString("objectHitName");
    //until it is not on pointer anymore...
    } else {
      if (!pointingobjectHitName.equals(SourceRawTable.getRow(i).getString("objectHitName")) || i == (thisTableRows-1)) {
        //process the object that ended being pointed on...
        isPointingToTime = getRowTime(SourceRawTable,i);
        TableRow newRowGp = OutputTable.addRow();
        newRowGp.setString("participantId", thisParticipantId);
        newRowGp.setFloat("logId", isPointingFromId);
        newRowGp.setFloat("hour", SourceRawTable.getRow(isPointingFromId).getInt("hour"));
        newRowGp.setFloat("min", SourceRawTable.getRow(isPointingFromId).getInt("min"));
        newRowGp.setFloat("sec", SourceRawTable.getRow(isPointingFromId).getInt("sec"));
        newRowGp.setFloat("ms", SourceRawTable.getRow(isPointingFromId).getInt("ms"));
        newRowGp.setString("objectHitName", pointingobjectHitName);
        newRowGp.setFloat("duration", isPointingToTime - isPointingFromTime);
        //clean the variables for the next object
        isPointingFromId = i;
        isPointingFromTime = isPointingToTime;
        isPointingToTime = 0;
        pointingobjectHitName = SourceRawTable.getRow(i).getString("objectHitName");        
      }
    }
  }  
  return OutputTable;
}

public Table recomputeGazepointScenetime(Table SourceGazepointTable, Table SourceTimesTable) {
  //has to init a new table, because some entries in the old one can create multiple rows/entries
  Table OutputTable = initTableGazePoint();
  float newHour = 0;
  float newMin = 0;
  float newSec = 0;
  float newMs = 0;
  float newDuration = 0;
  //auxiliabry variables
  float fitFrom, fitTo;
  float rangeFrom, rangeTo;
  float currentFitTime;
  boolean foundFit;
  int rangeGazepoint = SourceGazepointTable.getRowCount();
  int rangeTimes = SourceTimesTable.getRowCount();
  //sweep through all gazepoint logs and try to fit them somewhere
  for (int i=0; i < rangeGazepoint; i++) {
    fitFrom = (getRowTime(SourceGazepointTable, i) - dwellTimeBaseline);
    fitTo = fitFrom + (SourceGazepointTable.getRow(i).getFloat("duration"));   
    for (int j=0; j < rangeTimes; j++) {
      foundFit = false;
      rangeFrom = SourceTimesTable.getRow(j).getFloat("sceneStart");
      rangeTo = SourceTimesTable.getRow(j).getFloat("sceneEnd");
      //if the gaze time fits fully, add it fully
      if (rangeFrom < fitFrom && rangeTo > fitTo) {
        newHour = SourceGazepointTable.getRow(i).getFloat("hour");
        newMin = SourceGazepointTable.getRow(i).getFloat("min");
        newSec = SourceGazepointTable.getRow(i).getFloat("sec");
        newMs = SourceGazepointTable.getRow(i).getFloat("ms");
        newDuration = SourceGazepointTable.getRow(i).getFloat("duration");
        foundFit = true;
        //println("Full fit ("+ (j+1) + "). " +
        //        "Range: " + rangeFrom + " -- " + rangeTo + ". Fit: " + fitFrom + " -- " + fitTo); 
      }
      //if gaze overlaps fully (staring at a single object more than scene time), crop it to range
      else if (rangeFrom > fitFrom && rangeTo < fitTo) {
        newHour = SourceTimesTable.getRow(j).getFloat("hour");
        newMin = SourceTimesTable.getRow(j).getFloat("min");
        newSec = SourceTimesTable.getRow(j).getFloat("sec");
        newMs = SourceTimesTable.getRow(j).getFloat("ms");
        newDuration = SourceTimesTable.getRow(j).getFloat("sceneTime");
        foundFit = true;
        //println("Overlap fit ("+ (j+1) + "). " +
        //        "Range: " + rangeFrom + " -- " + rangeTo + ". Fit: " + fitFrom + " -- " + fitTo);
      }      
      //if the gaze overlaps on start, add it partially
      else if (rangeFrom > fitFrom && rangeTo > fitTo && fitTo > rangeFrom) {
        newHour = SourceTimesTable.getRow(j).getFloat("hour");
        newMin = SourceTimesTable.getRow(j).getFloat("min");
        newSec = SourceTimesTable.getRow(j).getFloat("sec");
        newMs = SourceTimesTable.getRow(j).getFloat("ms");
        newDuration = fitTo - rangeFrom;
        foundFit = true;
        //println("Partial left fit ("+ (j+1) + "). " +
        //        "Range: " + rangeFrom + " -- " + rangeTo + ". Fit: " + fitFrom + " -- " + fitTo); 
      }
      //if the dataloss overlaps on end, add it partially
      else if (rangeFrom < fitFrom && rangeTo < fitTo && fitFrom < rangeTo) {
        newHour = SourceGazepointTable.getRow(i).getFloat("hour");
        newMin = SourceGazepointTable.getRow(i).getFloat("min");
        newSec = SourceGazepointTable.getRow(i).getFloat("sec");
        newMs = SourceGazepointTable.getRow(i).getFloat("ms");
        newDuration = rangeTo - fitFrom;
        foundFit = true;
        //println("Partial right fit ("+ (j+1) + "). " +
        //        "Range: " + rangeFrom + " -- " + rangeTo + ". Fit: " + fitFrom + " -- " + fitTo); 
      }
      //otherwise do nothing (no overlap)
      else {
        foundFit = false;
      }
      //finally, add the dataloss entry, if any
      if (foundFit) {
        SourceGazepointTable.getRow(i).setFloat("sceneId", SourceTimesTable.getRow(j).getFloat("sceneId"));
        TableRow newRowGp2 = OutputTable.addRow();
        newRowGp2.setString("participantId", SourceGazepointTable.getRow(i).getString("participantId"));
        newRowGp2.setFloat("logId", SourceGazepointTable.getRow(i).getFloat("logId"));
        newRowGp2.setFloat("sceneId", SourceGazepointTable.getRow(i).getFloat("sceneId"));
        newRowGp2.setFloat("hour", newHour);
        newRowGp2.setFloat("min", newMin);
        newRowGp2.setFloat("sec", newSec);
        newRowGp2.setFloat("ms", newMs);
        newRowGp2.setString("objectHitName", SourceGazepointTable.getRow(i).getString("objectHitName"));
        newRowGp2.setFloat("duration", newDuration);
      }
    }
  }
  return OutputTable;
}

public Table clearGazepointDuration(Table SourceGazepointTable) {  
  int thisTableRows = SourceGazepointTable.getRowCount();
  float tablePointerRow = 0;
  float tablePointerScene = SourceGazepointTable.getRow(0).getFloat("sceneId");
  String tablePointerName = SourceGazepointTable.getRow(0).getString("objectHitName");
  float tablePointerGazeStart = getRowTime(SourceGazepointTable,0);
  float tablePointerGazeStop = tablePointerGazeStart + SourceGazepointTable.getRow(0).getFloat("duration");
  boolean lookAheadIsolated = false;
  boolean lookBackIsolated = false;
  //first pass - delete small isolated entries
  for (int i = 0; i < thisTableRows; i++) {
    //determine if to be considered to be deleted    
    if (SourceGazepointTable.getRow(i).getFloat("duration") <= THRESHOLD_DELETE) {
      //look ahead to determine if isolated
      for (int j = i+1; j < thisTableRows; j++) {
        if (((getRowTime(SourceGazepointTable,j) - tablePointerGazeStop) <= THRESHOLD_DELETE) &&
             SourceGazepointTable.getRow(j).getString("sceneId").equals(tablePointerScene)) {
          if (SourceGazepointTable.getRow(j).getString("objectHitName").equals(tablePointerName)) {
            lookAheadIsolated = false;
            continue;
          }               
        } else {
          lookAheadIsolated = true;
          continue;
        }
      }
      //look back to determine if isolated
      for (int k = i-1; k > 0; k--) {
        if ((tablePointerGazeStart -
               (getRowTime(SourceGazepointTable,k) + SourceGazepointTable.getRow(k).getFloat("duration"))
             <= THRESHOLD_DELETE) && SourceGazepointTable.getRow(k).getString("sceneId").equals(tablePointerScene)) {
          if (SourceGazepointTable.getRow(k).getString("objectHitName").equals(tablePointerName)) {
            lookBackIsolated = false;
            continue;
          }               
        } else {
          lookBackIsolated = true;
          continue;
        }
      }
      //onto the next pointer
      tablePointerRow = i;
      tablePointerScene = SourceGazepointTable.getRow(i).getFloat("sceneId");
      tablePointerName = SourceGazepointTable.getRow(i).getString("objectHitName");
      tablePointerGazeStart = getRowTime(SourceGazepointTable,i);
      tablePointerGazeStart = tablePointerGazeStart + SourceGazepointTable.getRow(i).getFloat("duration");
    }
    //delete the small entry
    if (lookAheadIsolated && lookBackIsolated) {
      //println("Deleting isolated entry at id " + i);
      SourceGazepointTable.removeRow(i);
      thisTableRows--;
      i--;
    }
    //continue the sweep
    lookAheadIsolated = false;
    lookBackIsolated = false;
  }
  
  //second pass - join equal entries with small gaps inbetween
  tablePointerRow = 0;
  tablePointerScene = SourceGazepointTable.getRow(0).getFloat("sceneId");
  tablePointerName = SourceGazepointTable.getRow(0).getString("objectHitName");
  tablePointerGazeStart = getRowTime(SourceGazepointTable,0);
  tablePointerGazeStop = tablePointerGazeStart + SourceGazepointTable.getRow(0).getFloat("duration");
  for (int i = 0; i < thisTableRows-1; i++) {
    //got to be same name, same scene Id, within the time of join distance
    if((tablePointerName.equals(SourceGazepointTable.getRow(i+1).getString("objectHitName"))) &&
       tablePointerScene == SourceGazepointTable.getRow(i+1).getFloat("sceneId"))
    {
      if((getRowTime(SourceGazepointTable,i+1) - tablePointerGazeStop) <= THRESHOLD_JOIN) {
        //println("Joining isolated entry at id " + i);
        SourceGazepointTable.getRow(i).setFloat("duration",
          SourceGazepointTable.getRow(i).getFloat("duration") +
          SourceGazepointTable.getRow(i+1).getFloat("duration") +
          (getRowTime(SourceGazepointTable,i+1) - tablePointerGazeStop));
        SourceGazepointTable.removeRow(i+1);
        thisTableRows--;    
      }
    }
    //continue the sweep unless last entry
    if ((i+1) < thisTableRows) {
      tablePointerRow = i+1;
      tablePointerScene = SourceGazepointTable.getRow(i+1).getFloat("sceneId");
      tablePointerName = SourceGazepointTable.getRow(i+1).getString("objectHitName");
      tablePointerGazeStart = getRowTime(SourceGazepointTable,i+1);
      tablePointerGazeStop = tablePointerGazeStart + SourceGazepointTable.getRow(i+1).getFloat("duration");
    }
  }
  return SourceGazepointTable; 
}

public Table generateStatistics(Table SourceGazepointTable) {
  Table OutputStatisticsTable = initTableStatistics();
  int thisTableRows = SourceGazepointTable.getRowCount();
  float tablePointerScene = SourceGazepointTable.getRow(0).getFloat("sceneId");
  boolean lastRowPerScene = false;
  boolean thisRowCommonInitialized = false;
  //temp vars for per-row writes
  String thisRowParticipantId = "";
  float thisRowSceneId = 0;
  float thisRowDwellFO1 = 0;
  float thisRowDwellFO2 = 0;
  float thisRowDwellBackground = 0;
  float thisRowDwellDataloss = 0;
  float thisRowVisitFO1 = 0;
  float thisRowVisitFO2 = 0;
  float thisRowVisitBackground = 0;
  float thisRowVisitDataloss = 0;
  float thisRowTransitionFO1_FO2 = 0;
  float thisRowTransitionFO1_BG = 0;
  float thisRowTransitionFO2_FO1 = 0;
  float thisRowTransitionFO2_BG = 0;
  float thisRowTransitionBG_FO1 = 0;
  float thisRowTransitionBG_FO2 = 0;
  float thisRowDatalossPercentage = 0;
  
  //sweep through the statistics table
  for(int i = 0; i < thisTableRows; i++) {
    String thisGazeRowObject = SourceGazepointTable.getRow(i).getString("objectHitName");
    float thisGazeRowDuration = SourceGazepointTable.getRow(i).getFloat("duration");
    //see if i+1 checks are to be done at all
    if ((i == (thisTableRows-1)) || tablePointerScene != SourceGazepointTable.getRow(i+1).getFloat("sceneId")) {
      lastRowPerScene = true;
    }
    //see if this row common values initialized
    if (!thisRowCommonInitialized) {
      thisRowParticipantId = SourceGazepointTable.getRow(i).getString("participantId");
      thisRowSceneId = SourceGazepointTable.getRow(i).getFloat("sceneId");
      thisRowCommonInitialized = true;
    }
    //see what is being dwelled at
    if (thisGazeRowObject.equals("FO1")) {
      thisRowDwellFO1 += thisGazeRowDuration;
      thisRowVisitFO1++;
    } else if (thisGazeRowObject.equals("FO2")) {
      thisRowDwellFO2 += thisGazeRowDuration;
      thisRowVisitFO2++;
    } else if (thisGazeRowObject.equals("background")) {
      thisRowDwellBackground += thisGazeRowDuration;
      thisRowVisitBackground++;
    } else if (thisGazeRowObject.equals("dataloss")) {
      thisRowDwellDataloss += thisGazeRowDuration;
      thisRowVisitDataloss++;
    }
    //see what is the next transition - if any
    if (!lastRowPerScene) {
      String nextGazeRowObject = SourceGazepointTable.getRow(i+1).getString("objectHitName");
      if (thisGazeRowObject.equals("FO1") && nextGazeRowObject.equals("FO2")) {
        thisRowTransitionFO1_FO2++;
      } else if (thisGazeRowObject.equals("FO1") && nextGazeRowObject.equals("background")) {
        thisRowTransitionFO1_BG++;
      } else if (thisGazeRowObject.equals("FO2") && nextGazeRowObject.equals("FO1")) {
        thisRowTransitionFO2_FO1++;
      } else if (thisGazeRowObject.equals("FO2") && nextGazeRowObject.equals("background")) {
        thisRowTransitionFO2_BG++;
      } else if (thisGazeRowObject.equals("background") && nextGazeRowObject.equals("FO1")) {
        thisRowTransitionBG_FO1++;
      } else if (thisGazeRowObject.equals("background") && nextGazeRowObject.equals("FO2")) {
        thisRowTransitionBG_FO2++;
      }
    }
    //write the row now (one whole scene), and proceed on ot the next scene
    if (lastRowPerScene) {
      if (thisRowDwellDataloss == 0) {
        thisRowDatalossPercentage = 0;
      } else {
        thisRowDatalossPercentage =
          thisRowDwellDataloss / (thisRowDwellFO1 + thisRowDwellFO2 + thisRowDwellBackground + thisRowDwellDataloss);
        thisRowDatalossPercentage = thisRowDatalossPercentage*100;  
      }      
      TableRow newRowStat = OutputStatisticsTable.addRow();
      newRowStat.setString("participantId", thisRowParticipantId);
      newRowStat.setFloat("sceneId", thisRowSceneId);
      newRowStat.setFloat("dwellFO1", thisRowDwellFO1);
      newRowStat.setFloat("dwellFO2", thisRowDwellFO2);
      newRowStat.setFloat("dwellBG", thisRowDwellBackground);
      newRowStat.setFloat("dwellDataloss", thisRowDwellDataloss);
      newRowStat.setFloat("datalossPercentage", thisRowDatalossPercentage);
      newRowStat.setFloat("visitFO1", thisRowVisitFO1);
      newRowStat.setFloat("visitFO2", thisRowVisitFO2);
      newRowStat.setFloat("visitBG", thisRowVisitBackground);
      newRowStat.setFloat("visitDataloss", thisRowVisitDataloss);
      newRowStat.setFloat("transitionFO1_FO2", thisRowTransitionFO1_FO2);
      newRowStat.setFloat("transitionFO1_BG", thisRowTransitionFO1_BG);
      newRowStat.setFloat("transitionFO2_FO1", thisRowTransitionFO2_FO1);
      newRowStat.setFloat("transitionFO2_BG", thisRowTransitionFO2_BG);
      newRowStat.setFloat("transitionBG_FO1", thisRowTransitionBG_FO1);
      newRowStat.setFloat("transitionBG_FO2", thisRowTransitionBG_FO2);
      //... and clear it up for the next row/scene
      lastRowPerScene = false;
      thisRowCommonInitialized = false;
      thisRowParticipantId = "";
      thisRowSceneId = 0;
      thisRowDwellFO1 = 0;
      thisRowDwellFO2 = 0;
      thisRowDwellBackground = 0;
      thisRowDwellDataloss = 0;
      thisRowVisitFO1 = 0;
      thisRowVisitFO2 = 0;
      thisRowVisitBackground = 0;
      thisRowVisitDataloss = 0;
      thisRowTransitionFO1_FO2 = 0;
      thisRowTransitionFO1_BG = 0;
      thisRowTransitionFO2_FO1 = 0;
      thisRowTransitionFO2_BG = 0;
      thisRowTransitionBG_FO1 = 0;
      thisRowTransitionBG_FO2 = 0;
      thisRowDatalossPercentage = 0;
      if (i < (thisTableRows-1)) {
        tablePointerScene = SourceGazepointTable.getRow(i+1).getFloat("sceneId");
      }
    }
  }
  return OutputStatisticsTable;  
}

//----------------------------------------------------------------------------------------------------------------------

public float getRowTime(Table table, int row) {
  return table.getRow(row).getFloat("hour")*3600*1000 + table.getRow(row).getFloat("min")*60*1000 +
         table.getRow(row).getFloat("sec")*1000 + table.getRow(row).getFloat("ms");
}

public void printLogBorder() {
  println("=========================================================================================================="); 
}

public Table initTableTimes() {
  Table InitTable = new Table();
  //create the table structure  
  InitTable.addColumn("participantId", Table.STRING);
  InitTable.addColumn("sceneId", Table.FLOAT); //scene 1-30; use 99 for total
  InitTable.addColumn("sceneStart", Table.FLOAT);
  InitTable.addColumn("sceneEnd", Table.FLOAT);
  InitTable.addColumn("sceneTime", Table.FLOAT);
  //create other table structures (will be filled in later, as recomputed)
  InitTable.addColumn("hour", Table.FLOAT);
  InitTable.addColumn("min", Table.FLOAT);
  InitTable.addColumn("sec", Table.FLOAT);
  InitTable.addColumn("ms", Table.FLOAT);
  return InitTable;
}

public Table initTableGazePoint() {
  Table InitTable = new Table();
  //init the variables
  InitTable.addColumn("participantId", Table.STRING);
  InitTable.addColumn("logId", Table.FLOAT);
  InitTable.addColumn("sceneId", Table.FLOAT);
  InitTable.addColumn("hour", Table.FLOAT);
  InitTable.addColumn("min", Table.FLOAT);
  InitTable.addColumn("sec", Table.FLOAT);
  InitTable.addColumn("ms", Table.FLOAT);
  InitTable.addColumn("objectHitName", Table.STRING);
  InitTable.addColumn("duration", Table.FLOAT);  
  return InitTable;
}

public Table initTableStatistics() {
  Table InitTable = new Table();
  //init the variables
  InitTable.addColumn("participantId", Table.STRING);
  InitTable.addColumn("sceneId", Table.FLOAT);
  InitTable.addColumn("dwellFO1", Table.FLOAT);
  InitTable.addColumn("dwellFO2", Table.FLOAT);
  InitTable.addColumn("dwellBG", Table.FLOAT);
  InitTable.addColumn("dwellDataloss", Table.FLOAT);
  InitTable.addColumn("datalossPercentage", Table.FLOAT);
  InitTable.addColumn("visitFO1", Table.FLOAT);
  InitTable.addColumn("visitFO2", Table.FLOAT);
  InitTable.addColumn("visitBG", Table.FLOAT);
  InitTable.addColumn("visitDataloss", Table.FLOAT);
  InitTable.addColumn("transitionFO1_FO2", Table.FLOAT);
  InitTable.addColumn("transitionFO1_BG", Table.FLOAT);
  InitTable.addColumn("transitionFO2_FO1", Table.FLOAT);
  InitTable.addColumn("transitionFO2_BG", Table.FLOAT);
  InitTable.addColumn("transitionBG_FO1", Table.FLOAT);
  InitTable.addColumn("transitionBG_FO2", Table.FLOAT);  
  return InitTable;
}

public Table appendStatisticsTable(Table SourceStatisticsTable, Table OutputStatisticsTable) {
  for(int i = 0; i < CurrentStatisticsTable.getRowCount(); i++) {
    TableRow newRowStat = OutputStatisticsTable.addRow();
    newRowStat.setString("participantId", SourceStatisticsTable.getRow(i).getString("participantId"));
    newRowStat.setFloat("sceneId", SourceStatisticsTable.getRow(i).getFloat("sceneId"));
    newRowStat.setFloat("dwellFO1", SourceStatisticsTable.getRow(i).getFloat("dwellFO1"));
    newRowStat.setFloat("dwellFO2", SourceStatisticsTable.getRow(i).getFloat("dwellFO2"));
    newRowStat.setFloat("dwellBG", SourceStatisticsTable.getRow(i).getFloat("dwellBG"));
    newRowStat.setFloat("dwellDataloss", SourceStatisticsTable.getRow(i).getFloat("dwellDataloss"));
    newRowStat.setFloat("datalossPercentage", SourceStatisticsTable.getRow(i).getFloat("datalossPercentage"));
    newRowStat.setFloat("visitFO1", SourceStatisticsTable.getRow(i).getFloat("visitFO1"));
    newRowStat.setFloat("visitFO2", SourceStatisticsTable.getRow(i).getFloat("visitFO2"));
    newRowStat.setFloat("visitBG", SourceStatisticsTable.getRow(i).getFloat("visitBG"));
    newRowStat.setFloat("visitDataloss", SourceStatisticsTable.getRow(i).getFloat("visitDataloss"));
    newRowStat.setFloat("transitionFO1_FO2", SourceStatisticsTable.getRow(i).getFloat("transitionFO1_FO2"));
    newRowStat.setFloat("transitionFO1_BG", SourceStatisticsTable.getRow(i).getFloat("transitionFO1_BG"));
    newRowStat.setFloat("transitionFO2_FO1", SourceStatisticsTable.getRow(i).getFloat("transitionFO2_FO1"));
    newRowStat.setFloat("transitionFO2_BG", SourceStatisticsTable.getRow(i).getFloat("transitionFO2_BG"));
    newRowStat.setFloat("transitionBG_FO1", SourceStatisticsTable.getRow(i).getFloat("transitionBG_FO1"));
    newRowStat.setFloat("transitionBG_FO2", CurrentStatisticsTable.getRow(i).getFloat("transitionBG_FO2")); 
   }
   return OutputStatisticsTable;
}