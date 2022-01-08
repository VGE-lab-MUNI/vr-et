// --------------------------------------------------------------------------------------------------------------------
// Multi-level collider segmentation script (client side), version 2022-01-06
// This script 
//
// Functionality:
//
// Script setup and options:
// --------------------------------------------------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//the collider object datastructure
public class ColliderLevel
{
    public int number;
    public float maxDistance;
    public GameObject colliderObject;

    public void setLevel (int i, float distance, GameObject obj)
    {
        number = i;
        maxDistance = distance;
        colliderObject = obj;
    }
}

public class MultilevelColliderClient : MonoBehaviour
{
	//to subscribe to the multilevel collider server
	private GameObject multilevelServer;
    private List<ColliderLevel> colliderList;
    //validation
	private bool initPassed, listPassed;

    //the following structure is necessary, so as to have a 2D array (objects per distance) visible in the inspector
    [System.Serializable]
    public struct TargetDistanceLists
    {
        //controlNumber is sent on a per key basis
        public float maxDistance;
        public GameObject colliderObject;
    }
    public TargetDistanceLists[] targetDistances;

    //just init and send the data to the server to be computed from there
    void Awake()
    {
        buildList();
		
        //multilevelServer = GameObject.Find("MultilevelColliderServer");
        //if (multilevelServer.GetComponent<MultilevelColliderServer>() != null)
        //{
        //    multilevelServer.GetComponent<MultilevelColliderServer>().subscribe(this, colliderList);
        //    initPassed = true;
        //}
    }

    //for the server to call the collider switch
    public void switchCollider (int i)
    {
        if (i > 0 && i <= colliderList.Count)
        {
            foreach (ColliderLevel thisLevel in colliderList)
            {
                if (thisLevel.number == i) { thisLevel.colliderObject.SetActive(true); }
                else { thisLevel.colliderObject.SetActive(false); }
            }
        }
    }
    
    //to build the valid collider id/distance/gameObject list
    private void buildList()
    {
		int validObjectIterator = 1;
		for (int i = 0; i < targetDistances.Length; i++)
		{
			if (targetDistances[i].maxDistance > 0 && targetDistances[i].colliderObject != null)
			{
                ColliderLevel thisCollider = new ColliderLevel();
                thisCollider.setLevel(validObjectIterator,
                                      targetDistances[i].maxDistance,
                                      targetDistances[i].colliderObject);
                colliderList.Add(thisCollider);

                validObjectIterator++;
			}
		}
		if (colliderList.Count > 0) { listPassed = true; }
    }
}