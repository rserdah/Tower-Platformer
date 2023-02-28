using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Level : MonoBehaviour
{
    [Serializable]
    public struct SubLevel
    {
        public string name;
        public Level level;
        public int index;
        public Transform transform;
        public BoxCollider ground;
        public bool active;

        public static SubLevel Empty = new SubLevel();


        //public SubLevel(string _name, Level _level, int _index, Transform _transform)
        //{
        //    name = _name;
        //    level = _level;
        //    index = _index;
        //    transform = _transform;
        //    active = false;
        //}
    }

    public Color color = Color.white;
    //public Transform[] subLevels;
    public SubLevel[] allSubLevels;
    private int activeSubLevelIndex;
    //public BoxCollider[] subLevelGrounds;
    public Level lastLevel;
    public Renderer[] levelRenderers;
    public List<Enemy> enemies = new List<Enemy>();

    public bool active;
    public bool waitingActivation;

    Vector3 extents;

    //Safehouses are always the last Sublevel in a Level; safehouse.groundCol is the BoxCollider attached to the SafeHouse ground (Sublevel grounds are always the first child of the Sublevel)
    private (Transform transform, BoxCollider groundCol, Animator anim) safeHouse;


    private void Start()
    {
        //subLevels = new Transform[transform.childCount];
        allSubLevels = new SubLevel[transform.childCount];
        //subLevelGrounds = new BoxCollider[transform.childCount];

        for(int i = 0; i < transform.childCount; i++)
        {
            //subLevels[i] = transform.GetChild(i);

            allSubLevels[i].level = this;
            allSubLevels[i].index = i;
            allSubLevels[i].transform = transform.GetChild(i);
            allSubLevels[i].ground = allSubLevels[i].transform.GetChild(0).GetComponent<BoxCollider>();
            allSubLevels[i].name = allSubLevels[i].transform.name;

            foreach(Enemy e in allSubLevels[i].transform.GetComponentsInChildren<Enemy>())
            {
                e.level = this;
                e.subLevel = allSubLevels[i];

                enemies.Add(e);
            }

            //subLevelGrounds[i] = transform.GetChild(i).GetChild(0).GetComponent<BoxCollider>();
        }

        //If this is not the first child (b/c Level0's lastLevel will be Level-1, which is the last child) AND not the last child (b/c Level-1 is the last child and does not have a lastLevel)
        if(transform.GetSiblingIndex() > 0 && transform.GetSiblingIndex() < transform.parent.childCount - 1)
        {
            lastLevel = transform.parent.GetChild(transform.GetSiblingIndex() - 1).GetComponent<Level>();
        }
        else if(transform.GetSiblingIndex() == 0)
        {
            lastLevel = transform.parent.GetChild(transform.parent.childCount - 1).GetComponent<Level>();
        }

        //safeHouse.transform = subLevels[transform.childCount - 1];
        safeHouse.transform = allSubLevels[transform.childCount - 1].transform;
        safeHouse.groundCol = safeHouse.transform.GetChild(0).GetComponent<BoxCollider>();
        safeHouse.anim = safeHouse.transform.GetComponent<Animator>();

        if(transform.GetSiblingIndex() == transform.parent.childCount - 1) //If this Level is Level-1, partially close the SafeHouse
            CloseSafeHouse(true);
        else
            CloseSafeHouse(); //Else close all other Levels' Safehouses

        //foreach(Enemy e in GetComponentsInChildren<Enemy>())
        //{
        //    e.level = this;
        //    enemies.Add(e);
        //}
    }

    private void FixedUpdate()
    {
        if(active || waitingActivation)
        {
            Collider hitCol;
            //Transform hitPlatform;
            SubLevel hitSubLevel;
            if(CheckPlatforms("Player", out hitCol,/* out hitPlatform*/ out hitSubLevel))
            {
                //if(/*hitPlatform.*/hitSubLevel.ground.transform.Equals(safeHouse.transform.GetChild(0))) //The hitPlatform would have been the ground of the SafeHouse (which is the first child of the SafeHouse Transform)
                //{
                //    CloseSafeHouse(true); //Close Player into the SafeHouse b/c they reached the end of the Level
                //}
                //else if(waitingActivation) //Else if Player hit any non-SafeHouse SubLevel platform in this Level for the first time
                //{
                //    if(lastLevel)
                //    {
                //        lastLevel.CloseSafeHouse();
                //        lastLevel.active = lastLevel.waitingActivation = false;
                //    }

                //    waitingActivation = false;
                //    active = true;
                //}
                //else //Else Player hit any non-SafeHouse platform for the non-first time
                //{
                //    allSubLevels[activeSubLevelIndex].active = false;
                //    allSubLevels[activeSubLevelIndex].name = allSubLevels[activeSubLevelIndex].transform.name;

                //    activeSubLevelIndex = hitSubLevel.index;

                //    allSubLevels[activeSubLevelIndex].active = true;
                //    allSubLevels[activeSubLevelIndex].name = allSubLevels[activeSubLevelIndex].transform.name + " (active)";
                //}

                if(waitingActivation) //Close the last Level b/c they started the next Level
                {
                    if(lastLevel)
                    {
                        lastLevel.CloseSafeHouse();
                        lastLevel.active = lastLevel.waitingActivation = false;
                        lastLevel.DeactivateCurrentSubLevel();

                        try { hitCol.gameObject.GetComponent<TPSPlayer>().stats.isInSafeHouse = false; } catch(Exception) { }
                    }

                    waitingActivation = false;
                    active = true;
                }
                else if(hitSubLevel.ground.transform.Equals(safeHouse.transform.GetChild(0))) //The hitPlatform would have been the ground of the SafeHouse (which is the first child of the SafeHouse Transform)
                {
                    CloseSafeHouse(true); //Close Player into the SafeHouse b/c they reached the end of the Level

                    try { hitCol.gameObject.GetComponent<TPSPlayer>().stats.isInSafeHouse = true; } catch(Exception) { }
                }

                ChangeSubLevels(hitSubLevel.index);
            }
            else
            {
                Debug.LogError("No collisions with tag");
            }
            //Collider[] hitCols;
            //Vector3 localScale = Vector3.zero;

            //for(int i = 0; i < subLevelGrounds.Length - 1; i++)
            //{
            //    int j = 0;
            //    localScale = subLevelGrounds[i].transform.localScale;
            //    extents = new Vector3(localScale.x - 0.5f, localScale.y + 1f, localScale.z - 0.5f);

            //    hitCols = Physics.OverlapBox(subLevelGrounds[i].transform.position, extents / 2f);

            //    foreach(Collider c in hitCols)
            //    {
            //        if(c.transform.tag.Equals("Player"))
            //        {
            //            Debug.LogError(hitCols[j].name + " hit " + subLevelGrounds[i].transform.name, subLevelGrounds[i].gameObject);

            //            if(subLevelGrounds[i].transform.Equals(safeHouse.transform))
            //            {
            //                CloseSafeHouse(true);
            //            }
            //            else if(lastLevel)
            //            {
            //                lastLevel.CloseSafeHouse();
            //            }
            //        }
            //        else
            //            j++;
            //    }
            //}
        }
    }

    public bool CheckPlatforms(string targetTag, out Collider hitCollider, /*out Transform hitPlatform,*/ out SubLevel hitSubLevel)
    {
        Collider[] hitCols;
        Vector3 localScale = Vector3.zero, extents = Vector3.zero;

        for(int i = 0; i < /*subLevelGrounds*/allSubLevels.Length; i++)
        {
            int j = 0;
            localScale = /*subLevelGrounds*/allSubLevels[i].ground.transform.localScale;
            extents = new Vector3(localScale.x - 0.5f, localScale.y + 1f, localScale.z - 0.5f);

            hitCols = Physics.OverlapBox(/*subLevelGrounds*/allSubLevels[i].ground.transform.position, extents / 2f);

            foreach(Collider c in hitCols)
            {
                if(c.transform.tag.Equals(targetTag))
                {
                    //Debug.LogError($"{hitCols[j].name} hit {subLevelGrounds[i].transform.name} ({subLevelGrounds[i].transform.parent.name})", subLevelGrounds[i].gameObject);
                    Debug.LogError($"{hitCols[j].name} hit {allSubLevels[i].name} ({allSubLevels[i].transform.parent.name})", allSubLevels[i].transform.gameObject);


                    hitCollider = hitCols[j];
                    //hitPlatform = /*subLevelGrounds*/allSubLevels[i].ground.transform;
                    hitSubLevel = allSubLevels[i];


                    return true;
                }
                else
                {
                    j++;
                }
            }
        }


        hitCollider = null;
        //hitPlatform = null;
        hitSubLevel = SubLevel.Empty;


        return false;
    }

    //private void OpenLevel()
    //{

    //}

    //private void CloseLevel()
    //{

    //}

    private void DeactivateCurrentSubLevel()
    {
        allSubLevels[activeSubLevelIndex].active = false;
        allSubLevels[activeSubLevelIndex].name = allSubLevels[activeSubLevelIndex].transform.name; //Temp.
    }

    private void ChangeSubLevels(int newSubLevelIndex)
    {
        DeactivateCurrentSubLevel();

        activeSubLevelIndex = newSubLevelIndex;

        allSubLevels[activeSubLevelIndex].active = true;
        allSubLevels[activeSubLevelIndex].name = allSubLevels[activeSubLevelIndex].transform.name + " (active)"; //Temp.
    }

    private void OpenSafeHouse()
    {
        safeHouse.anim.Play("Open");
    }

    private void CloseSafeHouse(bool partialClose = false)
    {
        if(!partialClose)
        {
            safeHouse.anim.Play("Close");
        }
        else
        {
            safeHouse.anim.Play("PartialClose");
        }
    }

    public void OnEnemyDie(Enemy enemy)
    {
        enemies.Remove(enemy);

        if(enemies.Count <= 0)
        {
            OpenSafeHouse();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        //for(int i = 0; i < subLevelGrounds.Length - 1; i++)
        for(int i = 0; i < allSubLevels.Length - 1; i++)
        {
        //    Gizmos.DrawCube(subLevelGrounds[i].transform.position, extents);
            Gizmos.DrawCube(allSubLevels[i].ground.transform.position, extents);
        }
    }
}
