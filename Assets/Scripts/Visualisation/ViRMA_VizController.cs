﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

public class ViRMA_VizController : MonoBehaviour
{
    /* --- public --- */

    // cells and axes objects
    [HideInInspector] public List<Cell> cellData;
    [HideInInspector] public List<GameObject> cellObjs, axisXPointObjs, axisYPointObjs, axisZPointObjs;
    [HideInInspector] public LineRenderer axisXLine, axisYLine, axisZLine;
    public GameObject focusedCell;

    /*--- private --- */

    // general
    private ViRMA_GlobalsAndActions globals;
    public Rigidbody rigidBody;
    private float previousDistanceBetweenHands;
    private Bounds cellsAndAxesBounds;
    public Query activeQuery;
    public bool vizFullyLoaded;

    [HideInInspector] public bool activeBrowsingState;
    [HideInInspector] public Vector3 activeVizPosition;
    [HideInInspector] public Quaternion activeVizRotation;

    // cell properties
    public GameObject cellsandAxesWrapper;
    private float maxParentScale = 0.025f;
    private float minParentScale = 0.015f;
    private float defaultParentSize;
    private float defaultCellSpacingRatio = 1.5f;
    private string cellMaterial = "Materials/BasicTransparent";

    private void Awake()
    {
        // define ViRMA globals script
        globals = Player.instance.gameObject.GetComponent<ViRMA_GlobalsAndActions>();
        activeQuery = new Query();

        // setup cells and axes wrapper
        cellsandAxesWrapper = new GameObject("CellsAndAxesWrapper");

        // setup rigidbody
        gameObject.AddComponent<Rigidbody>();
        rigidBody = GetComponent<Rigidbody>();
        rigidBody.useGravity = false;
        rigidBody.drag = 0.1f;
        rigidBody.angularDrag = 0.5f;
    }
    private void Start()
    {
        // dummy queries for debugging
        Query dummyQuery = new Query();

        //// OLD
        //dummyQuery.SetAxis("X", 3, "Tagset");
        //dummyQuery.SetAxis("Y", 7, "Tagset");
        //dummyQuery.SetAxis("Z", 77, "Hierarchy");

        //dummyQuery.AddFilter(115, "Hierarchy");
        //dummyQuery.AddFilter(116, "Hierarchy");

        //// NEW
        dummyQuery.SetAxis("X", 1770, "Hierarchy");
        dummyQuery.SetAxis("Y", 3733, "Hierarchy");
        dummyQuery.SetAxis("Z", 5, "Tagset");
        //dummyQuery.SetAxis("Z", 34, "Hierarchy");

        //dummyQuery.SetAxis("X", 3733, "Hierarchy");
        //dummyQuery.SetAxis("X", 34, "Hierarchy");
        //dummyQuery.SetAxis("Y", 1749, "Hierarchy");
        //dummyQuery.SetAxis("Z", 691, "Hierarchy");

        StartCoroutine(SubmitVizQuery(dummyQuery));
    }
    private void Update()
    {
        if (vizFullyLoaded)
        {
            // enable viz movement with SteamVR controllers
            CellNavigationController();

            // prevent viz from moving too far away if moving
            CellNavigationLimiter();

            // draw axes line renderers 
            DrawAxesLines();
        }     
    }

    // cell and axes generation
    public IEnumerator SubmitVizQuery(Query submittedQuery)
    {
        vizFullyLoaded = false;

        // get cell data from server (WAIT FOR)
        yield return StartCoroutine(ViRMA_APIController.GetCells(submittedQuery, (cells) => {
            cellData = cells;
        }));

        // generate axes with axis labels (WAIT FOR)
        yield return StartCoroutine(GenerateAxesFromLabels(submittedQuery));

        // generate textures and texture arrays from local image storage
        GenerateTexturesAndTextureArrays(cellData);

        // generate cells and their posiitons, centered on a parent
        GenerateCells(cellData);

        // set center point of wrapper around cells and axes
        CenterParentOnCellsAndAxes();

        // calculate bounding box to set cells positional limits
        CalculateCellsAndAxesBounds();

        // DEBUG: show cells/axes bounds and bounds center
        //ToggleDebuggingBounds(); 

        // add cells and axes to final parent to set default starting scale and position
        SetupDefaultScaleAndPosition();

        // set loading flags to true
        vizFullyLoaded = true;
        globals.queryController.vizQueryLoading = false; 
    }
    private void GenerateTexturesAndTextureArrays(List<Cell> cellData)
    {
        if (cellData.Count > 0)
        {
            //float before = Time.realtimeSinceStartup; // testing

            // make a list of all the unique image textures present in the current query
            List<KeyValuePair<string, Texture2D>> uniqueTextures = new List<KeyValuePair<string, Texture2D>>();
            foreach (var newCell in cellData)
            {
                if (!newCell.Filtered)
                {
                    int index = uniqueTextures.FindIndex(a => a.Key == newCell.ImageName);
                    if (index == -1)
                    {
                        byte[] imageBytes = File.ReadAllBytes(ViRMA_APIController.imagesDirectory + newCell.ImageName);
                        newCell.ImageTexture = ConvertImageToDDS(imageBytes); // dds stuff
                                                                              //newCell.ImageTexture = ConvertImageToTex(imageBytes); // jpg stuff
                        KeyValuePair<string, Texture2D> uniqueTexture = new KeyValuePair<string, Texture2D>(newCell.ImageName, newCell.ImageTexture);
                        uniqueTextures.Add(uniqueTexture);
                    }
                    else
                    {
                        newCell.ImageTexture = uniqueTextures[index].Value;
                    }
                }
            }

            // calculate the number of texture arrays needed based on the size of the first texture in the list
            int textureWidth = uniqueTextures[0].Value.width; // e.g. 1024 or 684
            int textureHeight = uniqueTextures[0].Value.height; // e.g. 765 or 485
            int maxTextureArraySize = SystemInfo.maxTextureSize; // e.g. 16384 (most GPUs)
            int maxTexturesPerArray = maxTextureArraySize / textureHeight; // e.g. 22 or 33
            int totalTextureArrays = uniqueTextures.Count / maxTexturesPerArray + 1;

            for (int i = 0; i < totalTextureArrays; i++)
            {
                //Debug.Log("----------------- " + i + " -----------------"); // debugging

                if (i != totalTextureArrays - 1)
                {
                    Material newtextureArrayMaterial = new Material(Resources.Load(cellMaterial) as Material);
                    Texture2D newTextureArray = new Texture2D(textureWidth, textureHeight * maxTexturesPerArray, TextureFormat.DXT1, false); // dds stuff
                                                                                                                                             //Texture2D newTextureArray = new Texture2D(textureWidth, textureHeight * maxTexturesPerArray, TextureFormat.RGB24, false); // jpg stuff
                    for (int j = 0; j < maxTexturesPerArray; j++)
                    {
                        int uniqueTextureIndex = j + maxTexturesPerArray * i;
                        Texture2D targetTexture = uniqueTextures[uniqueTextureIndex].Value;
                        if (targetTexture.width != textureWidth || targetTexture.height != textureHeight)
                        {
                            Debug.LogError("Texture " + uniqueTextures[uniqueTextureIndex].Key + " is not " + textureWidth + " x " + textureHeight + " and so will not fit properly in the texture array!");
                        }
                        Graphics.CopyTexture(targetTexture, 0, 0, 0, 0, targetTexture.width, targetTexture.height, newTextureArray, 0, 0, 0, targetTexture.height * j);

                        // Debug.Log(j + " | " + uniqueTextureIndex); // debugging

                        foreach (var cellDataObj in this.cellData)
                        {
                            if (cellDataObj.ImageName == uniqueTextures[uniqueTextureIndex].Key)
                            {
                                cellDataObj.TextureArrayId = j;
                                cellDataObj.TextureArrayMaterial = newtextureArrayMaterial;
                                cellDataObj.TextureArraySize = maxTexturesPerArray;
                            }
                        }
                    }
                    // newTextureArray.Compress(false);
                    newtextureArrayMaterial.mainTexture = newTextureArray;
                }
                else
                {
                    Material newtextureArrayMaterial = new Material(Resources.Load(cellMaterial) as Material);
                    int lastTextureArraySize = uniqueTextures.Count - (maxTexturesPerArray * (totalTextureArrays - 1));
                    Texture2D newTextureArray = new Texture2D(textureWidth, textureHeight * lastTextureArraySize, TextureFormat.DXT1, false); // dds stuff
                                                                                                                                              //Texture2D newTextureArray = new Texture2D(textureWidth, textureHeight * lastTextureArraySize, TextureFormat.RGB24, false); // jpg stuff
                    for (int j = 0; j < lastTextureArraySize; j++)
                    {
                        int uniqueTextureIndex = j + maxTexturesPerArray * i;
                        Texture2D targetTexture = uniqueTextures[uniqueTextureIndex].Value;
                        if (targetTexture.width != textureWidth || targetTexture.height != textureHeight)
                        {
                            Debug.LogError("Texture " + uniqueTextures[uniqueTextureIndex].Key + " is not " + textureWidth + " x " + textureHeight + " and so will not fit properly in the texture array!");
                        }
                        Graphics.CopyTexture(targetTexture, 0, 0, 0, 0, targetTexture.width, targetTexture.height, newTextureArray, 0, 0, 0, targetTexture.height * j);

                        // Debug.Log(j + " | " + uniqueTextureIndex); // debugging

                        foreach (var cellDataObj in this.cellData)
                        {
                            if (cellDataObj.ImageName == uniqueTextures[uniqueTextureIndex].Key)
                            {
                                cellDataObj.TextureArrayId = j;
                                cellDataObj.TextureArrayMaterial = newtextureArrayMaterial;
                                cellDataObj.TextureArraySize = lastTextureArraySize;
                            }
                        }
                    }
                    // newTextureArray.Compress(false);
                    newtextureArrayMaterial.mainTexture = newTextureArray;
                }
            }

            //float after = Time.realtimeSinceStartup; // testing
            // Debug.Log("TEXTURE PARSE TIME ~ ~ ~ ~ ~ " + (after - before).ToString("n3") + " seconds"); // testing
        }
    }
    private static Texture2D ConvertImageToDDS(byte[] ddsBytes)
    {
        byte ddsSizeCheck = ddsBytes[4];
        if (ddsSizeCheck != 124)
        {
            throw new Exception("Invalid DDS DXTn texture size! (not 124)");
        }
        int height = ddsBytes[13] * 256 + ddsBytes[12];
        int width = ddsBytes[17] * 256 + ddsBytes[16];

        int ddsHeaderSize = 128;
        byte[] dxtBytes = new byte[ddsBytes.Length - ddsHeaderSize];
        Buffer.BlockCopy(ddsBytes, ddsHeaderSize, dxtBytes, 0, ddsBytes.Length - ddsHeaderSize);
        Texture2D texture = new Texture2D(width, height, TextureFormat.DXT1, false);

        texture.LoadRawTextureData(dxtBytes);
        texture.Apply();
        return (texture);
    }
    private static Texture2D ConvertImageToTex(byte[] texBytes)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.LoadImage(texBytes);
        TextureScale.Bilinear(tex, 1024, 768);
        return (tex);
    }
    private void GenerateCells(List<Cell> cellData)
    {
        if (cellData.Count > 0)
        {
            // grab cell prefab from resoucres
            GameObject cellPrefab = Resources.Load("Prefabs/CellPrefab") as GameObject;

            // loop through all cells data from server
            foreach (var newCellData in cellData)
            {
                // create a primitive cube and set its scale to match image aspect ratio
                GameObject cell = Instantiate(cellPrefab);
                cell.AddComponent<ViRMA_Cell>().thisCellData = newCellData;

                // adjust aspect ratio
                float aspectRatio = 1.5f;
                cell.transform.localScale = new Vector3(aspectRatio, 1, aspectRatio);

                // assign coordinates to cell from server using a pre-defined space multiplier
                Vector3 nodePosition = new Vector3(newCellData.Coordinates.x, newCellData.Coordinates.y, newCellData.Coordinates.z) * (defaultCellSpacingRatio + 1);
                cell.transform.position = nodePosition;
                cell.transform.parent = cellsandAxesWrapper.transform;

                // name cell object and add it to a list of objects for reference
                cell.name = "Cell(" + newCellData.Coordinates.x + "," + newCellData.Coordinates.y + "," + newCellData.Coordinates.z + ")";
                cellObjs.Add(cell);
            }
        }   
    }
    private IEnumerator GenerateAxesFromLabels(Query submittedQuery)
    {
        // get label data from server
        yield return StartCoroutine(ViRMA_APIController.GetAxesLabels(submittedQuery, (axesLabels) => {

            // // global style for propety blocks
            Material transparentMaterial = Resources.Load("Materials/BasicTransparent") as Material;
            MaterialPropertyBlock materialProperties = new MaterialPropertyBlock();
            Color32 transparentRed = new Color32(255, 0, 0, 130);
            Color32 transparentGreen = new Color32(0, 255, 0, 130);
            Color32 transparentBlue = new Color32(0, 0, 255, 130);
            float axisLineWidth = 0.005f;

            // origin point
            GameObject AxisOriginPoint = GameObject.CreatePrimitive(PrimitiveType.Cube);
            AxisOriginPoint.GetComponent<Renderer>().material = transparentMaterial;
            materialProperties.SetColor("_Color", new Color32(0, 0, 0, 255));
            AxisOriginPoint.GetComponent<Renderer>().SetPropertyBlock(materialProperties);
            AxisOriginPoint.name = "AxisOriginPoint";
            AxisOriginPoint.transform.position = Vector3.zero;
            AxisOriginPoint.transform.localScale = Vector3.one * 0.5f;
            AxisOriginPoint.transform.parent = cellsandAxesWrapper.transform;

            // add origin block to all axis object lists
            axisXPointObjs.Add(AxisOriginPoint);
            axisYPointObjs.Add(AxisOriginPoint);
            axisZPointObjs.Add(AxisOriginPoint);

            // x axis points
            if (axesLabels.X != null)
            {
                materialProperties.SetColor("_Color", transparentRed);
                for (int i = 0; i < axesLabels.X.Labels.Count; i++)
                {
                    // create gameobject to represent axis point
                    GameObject axisXPoint = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    axisXPoint.GetComponent<Renderer>().material = transparentMaterial;
                    axisXPoint.GetComponent<Renderer>().SetPropertyBlock(materialProperties);
                    axisXPoint.name = "axisXPoint_" + i;
                    axisXPoint.transform.position = new Vector3(i + 1, 0, 0) * (defaultCellSpacingRatio + 1);
                    axisXPoint.transform.localScale = Vector3.one * 0.5f;
                    axisXPoint.transform.parent = cellsandAxesWrapper.transform;
                    axisXPoint.AddComponent<ViRMA_AxisPoint>().x = true;

                    // apply metadata to axis point
                    ViRMA_AxisPoint axisPoint = axisXPoint.GetComponent<ViRMA_AxisPoint>();
                    axisPoint.axisId = axesLabels.X.Id;
                    axisPoint.axisName = axesLabels.X.Name;
                    axisPoint.axisType = axesLabels.X.Type;
                    axisPoint.axisPointLabel = axesLabels.X.Labels[i].Key;
                    axisPoint.axisPointLabelId = axesLabels.X.Labels[i].Value;

                    // add gameobject to list
                    axisXPointObjs.Add(axisXPoint);
                }

                // x axis line
                if (axisXPointObjs.Count > 2)
                {
                    GameObject AxisXLineObj = new GameObject("AxisXLine");
                    axisXLine = AxisXLineObj.AddComponent<LineRenderer>();
                    axisXLine.GetComponent<Renderer>().material = transparentMaterial;
                    axisXLine.GetComponent<Renderer>().SetPropertyBlock(materialProperties);
                    axisXLine.positionCount = 2;
                    axisXLine.startWidth = axisLineWidth;
                    axisXLine.endWidth = axisLineWidth;
                }

            }

            // y axis points
            if (axesLabels.Y != null)
            {
                materialProperties.SetColor("_Color", transparentGreen);
                for (int i = 0; i < axesLabels.Y.Labels.Count; i++)
                {
                    // create gameobject to represent axis point
                    GameObject axisYPoint = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    axisYPoint.GetComponent<Renderer>().material = transparentMaterial;
                    axisYPoint.GetComponent<Renderer>().SetPropertyBlock(materialProperties);
                    axisYPoint.name = "axisYPoint_" + i;
                    axisYPoint.transform.position = new Vector3(0, i + 1, 0) * (defaultCellSpacingRatio + 1);
                    axisYPoint.transform.localScale = Vector3.one * 0.5f;
                    axisYPoint.transform.parent = cellsandAxesWrapper.transform;
                    axisYPoint.AddComponent<ViRMA_AxisPoint>().y = true;

                    // apply metadata to axis point
                    ViRMA_AxisPoint axisPoint = axisYPoint.GetComponent<ViRMA_AxisPoint>();
                    axisPoint.axisId = axesLabels.Y.Id;
                    axisPoint.axisName = axesLabels.Y.Name;
                    axisPoint.axisType = axesLabels.Y.Type;
                    axisPoint.axisPointLabel = axesLabels.Y.Labels[i].Key;
                    axisPoint.axisPointLabelId = axesLabels.Y.Labels[i].Value;

                    // add gameobject to list
                    axisYPointObjs.Add(axisYPoint);
                }

                // y axis line
                if (axisYPointObjs.Count > 2)
                {
                    GameObject AxisYLineObj = new GameObject("AxisYLine");
                    axisYLine = AxisYLineObj.AddComponent<LineRenderer>();
                    axisYLine.GetComponent<Renderer>().material = transparentMaterial;
                    axisYLine.GetComponent<Renderer>().SetPropertyBlock(materialProperties);
                    axisYLine.positionCount = 2;
                    axisYLine.startWidth = axisLineWidth;
                    axisYLine.endWidth = axisLineWidth;
                }
            }

            // z axis points
            if (axesLabels.Z != null)
            {
                materialProperties.SetColor("_Color", transparentBlue);
                for (int i = 0; i < axesLabels.Z.Labels.Count; i++)
                {
                    // create gameobject to represent axis point
                    GameObject axisZPoint = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    axisZPoint.GetComponent<Renderer>().material = transparentMaterial;
                    axisZPoint.GetComponent<Renderer>().SetPropertyBlock(materialProperties);
                    axisZPoint.name = "axisZPoint_" + i;
                    axisZPoint.transform.position = new Vector3(0, 0, i + 1) * (defaultCellSpacingRatio + 1);
                    axisZPoint.transform.localScale = Vector3.one * 0.5f;
                    axisZPoint.transform.parent = cellsandAxesWrapper.transform;
                    axisZPoint.AddComponent<ViRMA_AxisPoint>().z = true;

                    // apply metadata to axis point
                    ViRMA_AxisPoint axisPoint = axisZPoint.GetComponent<ViRMA_AxisPoint>();
                    axisPoint.axisId = axesLabels.Z.Id;
                    axisPoint.axisName = axesLabels.Z.Name;
                    axisPoint.axisType = axesLabels.Z.Type;
                    axisPoint.axisPointLabel = axesLabels.Z.Labels[i].Key;
                    axisPoint.axisPointLabelId = axesLabels.Z.Labels[i].Value;

                    // add gameobject to list
                    axisZPointObjs.Add(axisZPoint);
                }

                // z axis line
                if (axisZPointObjs.Count > 2)
                {
                    GameObject AxisZLineObj = new GameObject("AxisZLine");
                    axisZLine = AxisZLineObj.AddComponent<LineRenderer>();
                    axisZLine.GetComponent<Renderer>().material = transparentMaterial;
                    axisZLine.GetComponent<Renderer>().SetPropertyBlock(materialProperties);
                    axisZLine.positionCount = 2;
                    axisZLine.startWidth = axisLineWidth;
                    axisZLine.endWidth = axisLineWidth;
                }
            }    
        }));
    }
    public void HighlightAxisPoint(GameObject axisPointObj)
    {
        ViRMA_AxisPoint axisPoint = axisPointObj.GetComponent<ViRMA_AxisPoint>();
        List<GameObject> fadedPoints = new List<GameObject>();
        if (axisPoint.x)
        {
            fadedPoints = axisXPointObjs;
        }
        else if (axisPoint.y)
        {
            fadedPoints = axisYPointObjs;
        }
        else if (axisPoint.z)
        {
            fadedPoints = axisZPointObjs;
        }

        foreach (var fadedPoint in fadedPoints)
        {
            if (fadedPoint != axisPointObj)
            {
                if (fadedPoint.GetComponent<ViRMA_AxisPoint>())
                {
                    TextMeshPro axisLabelText = fadedPoint.GetComponent<ViRMA_AxisPoint>().axisLabelText;
                    Color fadeText = axisLabelText.color;
                    axisLabelText.color = new Color(axisLabelText.color.r, axisLabelText.color.g, axisLabelText.color.b, 0.2f);
                }
            }       
        }
    }
    private void DrawAxesLines()
    {
        // x axis
        if (axisXLine)
        {
            if (axisXPointObjs.Count > 1)
            {
                axisXLine.SetPosition(0, axisXPointObjs[1].transform.position);
                axisXLine.SetPosition(1, axisXPointObjs[axisXPointObjs.Count - 1].transform.position);
            }
            if (axisXLine.transform.parent == null)
            {
                axisXLine.transform.parent = cellsandAxesWrapper.transform;
            }
        }

        // y axis
        if (axisYLine)
        {
            if (axisYPointObjs.Count > 1)
            {
                axisYLine.SetPosition(0, axisYPointObjs[1].transform.position);
                axisYLine.SetPosition(1, axisYPointObjs[axisYPointObjs.Count - 1].transform.position);
            }
            if (axisYLine.transform.parent == null)
            {
                axisYLine.transform.parent = cellsandAxesWrapper.transform;
            }
        }

        // z axis
        if (axisZLine)
        {
            if (axisZPointObjs.Count > 1)
            {
                axisZLine.SetPosition(0, axisZPointObjs[1].transform.position);
                axisZLine.SetPosition(1, axisZPointObjs[axisZPointObjs.Count - 1].transform.position);
            }
            if (axisZLine.transform.parent == null)
            {
                axisZLine.transform.parent = cellsandAxesWrapper.transform;
            }
        }
    }
    public void ClearViz()
    {
        cellObjs.Clear();
        axisXPointObjs.Clear();
        axisYPointObjs.Clear();
        axisZPointObjs.Clear();

        if (vizFullyLoaded)
        {
            // maintain active position after viz is loaded for the first time
            activeVizPosition = transform.position;
            activeVizRotation = transform.rotation;
            activeBrowsingState = true;
        }
        
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;

        foreach (Transform child in cellsandAxesWrapper.transform)
        {
            Destroy(child.gameObject);
        }
    }


    // node navigation (position, rotation, scale)
    private void CellNavigationController()
    {
        if (globals.vizNavActions.IsActive())
        {
            if (rigidBody.isKinematic)
            {
                rigidBody.isKinematic = false;
            }

            if (globals.vizNav_Position.GetState(SteamVR_Input_Sources.Any) && globals.vizNav_Rotation.GetState(SteamVR_Input_Sources.Any))
            {
                // ToggleCellScaling();
            }
            else if (globals.vizNav_Position.GetState(SteamVR_Input_Sources.Any) || globals.vizNav_Rotation.GetState(SteamVR_Input_Sources.Any))
            {
                if (globals.vizNav_Position.GetState(SteamVR_Input_Sources.Any))
                {
                    ToggleCellPositioning();
                }
                if (globals.vizNav_Rotation.GetState(SteamVR_Input_Sources.Any))
                {
                    ToggleCellRotation();
                }
            }
            else
            {
                if (previousDistanceBetweenHands != 0)
                {
                    previousDistanceBetweenHands = 0;
                }
            }
        }
        else
        {
            if (!rigidBody.isKinematic)
            {
                rigidBody.isKinematic = true;
            }
        }
        
    }
    private void ToggleCellPositioning()
    {
        rigidBody.velocity = Vector3.zero;
        rigidBody.angularVelocity = Vector3.zero;

        /*
        if (Player.instance.rightHand.GetTrackedObjectVelocity().magnitude > 0.5f)
        { 
            //Vector3 localVelocity = transform.InverseTransformDirection(Player.instance.rightHand.GetTrackedObjectVelocity());
            //localVelocity.x = 0;
            //localVelocity.y = 0;
            //localVelocity.z = 0;
            //rigidBody.velocity = transform.TransformDirection(localVelocity) * 2f;

            // scale throwing velocity with the size of the parent
            float parentMagnitude = transform.lossyScale.magnitude;
            float thrustAdjuster = parentMagnitude * 5f;
            Vector3 controllerVelocity = Player.instance.rightHand.GetTrackedObjectVelocity();
            rigidBody.velocity = controllerVelocity * thrustAdjuster;
        }
        */

        // adjust throwing velocity and drag
        Vector3 controllerVelocity = Player.instance.rightHand.GetTrackedObjectVelocity();
        if (!float.IsNaN(controllerVelocity.x) && !float.IsNaN(controllerVelocity.y) && !float.IsNaN(controllerVelocity.z))
        {
            rigidBody.velocity = controllerVelocity * 0.75f;
        }
        rigidBody.drag = 2.5f;
    }
    private void ToggleCellRotation()
    {
        rigidBody.velocity = Vector3.zero;
        rigidBody.angularVelocity = Vector3.zero;

        Vector3 localAngularVelocity = transform.InverseTransformDirection(Player.instance.leftHand.GetTrackedObjectAngularVelocity());
        localAngularVelocity.x = 0;
        //localAngularVelocity.y = 0;
        localAngularVelocity.z = 0;
        rigidBody.angularVelocity = transform.TransformDirection(localAngularVelocity) * 0.1f;

        rigidBody.angularDrag = 1f;
    }
    private void ToggleCellScaling()
    {
        rigidBody.velocity = Vector3.zero;
        rigidBody.angularVelocity = Vector3.zero;

        Vector3 leftHandPosition = Player.instance.leftHand.transform.position;
        Vector3 rightHandPosition = Player.instance.rightHand.transform.position;
        float thisFrameDistance = Mathf.Round(Vector3.Distance(leftHandPosition, rightHandPosition) * 100.0f) * 0.01f;

        if (previousDistanceBetweenHands == 0)
        {
            previousDistanceBetweenHands = thisFrameDistance;
        }
        else
        {
            if (thisFrameDistance > previousDistanceBetweenHands)
            {
                Vector3 targetScale = Vector3.one * maxParentScale;            
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, 2f * Time.deltaTime);
                defaultParentSize = transform.localScale.x;
            }
            if (thisFrameDistance < previousDistanceBetweenHands)
            {
                Vector3 targetScale = Vector3.one * minParentScale;
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, 2f * Time.deltaTime);
                defaultParentSize = transform.localScale.x;
            }
            previousDistanceBetweenHands = thisFrameDistance;
        }

        // calculate bounding box again
        CalculateCellsAndAxesBounds();
    }
    private void CellNavigationLimiter()
    {
        if (Player.instance && !rigidBody.isKinematic)
        {
            Vector3 currentVelocity = rigidBody.velocity;

            // x and z
            float boundary = Mathf.Max(Mathf.Max(cellsAndAxesBounds.size.x, cellsAndAxesBounds.size.y), cellsAndAxesBounds.size.z);
            boundary = boundary < 1 ? 1.0f : boundary;
            if (Vector3.Distance(transform.position, Player.instance.hmdTransform.transform.position) > boundary)
            {
                Vector3 normalisedDirection = (transform.position - Player.instance.hmdTransform.transform.position).normalized;
                Vector3 v = rigidBody.velocity;
                float d = Vector3.Dot(v, normalisedDirection);
                if (d > 0f) v -= normalisedDirection * d;
                rigidBody.velocity = v;
            }

            // y max
            float verticalBoundary = cellsAndAxesBounds.extents.y + 0.50f;
            float maxDistanceY = Player.instance.eyeHeight + verticalBoundary;
            if (transform.position.y >= maxDistanceY && currentVelocity.y > 0)
            {
                currentVelocity.y = 0;
                rigidBody.velocity = currentVelocity;
            }

            // y min
            float minDistanceY = Player.instance.eyeHeight - verticalBoundary;
            if (transform.position.y <= minDistanceY && currentVelocity.y < 0)
            {
                currentVelocity.y = 0;
                rigidBody.velocity = currentVelocity;
            }

        }
    }


    // general  
    private void CalculateCellsAndAxesBounds()
    {
        Vector3 currentPosition = transform.position;

        transform.position = Vector3.zero;

        // calculate bounding box
        Renderer[] meshes = cellsandAxesWrapper.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(cellsandAxesWrapper.transform.position, Vector3.zero);
        foreach (Renderer mesh in meshes)
        {
            bounds.Encapsulate(mesh.bounds);
        }
        cellsAndAxesBounds = bounds;

        transform.position = currentPosition;
    }
    private void SetupDefaultScaleAndPosition()
    {
        // set wrapper position and parent cells/axes to wrapper and set default starting scale
        transform.position = cellsAndAxesBounds.center;
        cellsandAxesWrapper.transform.parent = transform;
        defaultParentSize = (maxParentScale + minParentScale) / 2f;
        transform.localScale = Vector3.one * defaultParentSize;

        // get the bounds of the newly resized cells/axes
        Renderer[] meshes = GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        foreach (Renderer mesh in meshes)
        {
            bounds.Encapsulate(mesh.bounds);
        }

        // if there is an active browsing state, maintain location of viz
        if (activeBrowsingState)
        {
            transform.position = activeVizPosition;
            transform.rotation = activeVizRotation;
        }
        else
        {
            // calculate distance to place cells/axes in front of player based on longest axis
            float distance = Mathf.Max(Mathf.Max(bounds.size.x, bounds.size.y), bounds.size.z);
            distance = distance < 1 ? 1.0f : distance;
            Vector3 flattenedVector = Player.instance.bodyDirectionGuess;
            flattenedVector.y = 0;
            flattenedVector.Normalize();
            Vector3 spawnPos = Player.instance.hmdTransform.position + flattenedVector * distance;
            transform.position = spawnPos;
            //transform.LookAt(Player.instance.hmdTransform.position);
            transform.LookAt(2 * transform.position - Player.instance.hmdTransform.position); // flip viz 180 degrees
        }

        // set new layer to prevent physical interactions with other objects on that layer and set flag that query is finished loading
        foreach (Transform child in cellsandAxesWrapper.transform)
        {
            child.gameObject.layer = 9;
        }

        // recalculate bounds to dertmine positional limits 
        CalculateCellsAndAxesBounds();
    }
    private void CenterParentOnCellsAndAxes()
    {
        Transform[] children = cellsandAxesWrapper.transform.GetComponentsInChildren<Transform>();
        Vector3 newPosition = Vector3.one;
        foreach (var child in children)
        {
            newPosition += child.position;
            child.parent = null;
        }
        newPosition /= children.Length;
        cellsandAxesWrapper.transform.position = newPosition;
        foreach (var child in children)
        {
            child.parent = cellsandAxesWrapper.transform;
        }
    }


    // testing and debugging
    private void ToggleDebuggingBounds()
    {
        // show bounds in-game for debugging
        GameObject debugBounds = GameObject.CreatePrimitive(PrimitiveType.Cube);
        debugBounds.name = "DebugBounds"; 
        Destroy(debugBounds.GetComponent<Collider>());
        debugBounds.GetComponent<Renderer>().material = Resources.Load("Materials/BasicTransparent") as Material;
        debugBounds.GetComponent<Renderer>().material.color = new Color32(100, 100, 100, 130);
        debugBounds.transform.position = cellsAndAxesBounds.center;
        debugBounds.transform.localScale = cellsAndAxesBounds.size;
        debugBounds.transform.SetParent(cellsandAxesWrapper.transform);
        debugBounds.transform.SetAsFirstSibling();

        // show center of bounds in-game for debugging
        GameObject debugBoundsCenter = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        debugBoundsCenter.name = "DebugBoundsCenter";       
        Destroy(debugBoundsCenter.GetComponent<Collider>());
        debugBoundsCenter.GetComponent<Renderer>().material = Resources.Load("Materials/BasicTransparent") as Material;
        debugBoundsCenter.GetComponent<Renderer>().material.color = new Color32(0, 0, 0, 255);
        debugBoundsCenter.transform.position = cellsAndAxesBounds.center;
        debugBoundsCenter.transform.rotation = cellsandAxesWrapper.transform.rotation;
        debugBoundsCenter.transform.parent = cellsandAxesWrapper.transform;
        debugBoundsCenter.transform.SetAsFirstSibling();
    }
    private void OrganiseHierarchy()
    {
        // add cells to hierarchy parent
        GameObject cellsParent = new GameObject("Cells");
        cellsParent.transform.parent = cellsandAxesWrapper.transform;
        cellsParent.transform.localScale = Vector3.one;
        foreach (var cell in cellObjs)
        {
            cell.transform.parent = cellsParent.transform;
        }

        // add axes to hierarchy parent
        GameObject axesParent = new GameObject("Axes");
        axesParent.transform.parent = cellsandAxesWrapper.transform;
        axesParent.transform.localScale = Vector3.one;
        foreach (GameObject point in axisXPointObjs)
        {
            point.transform.parent = axesParent.transform;
        }
        axisXLine.gameObject.transform.parent = axesParent.transform;
        foreach (GameObject point in axisYPointObjs)
        {
            point.transform.parent = axesParent.transform;
        }
        axisYLine.gameObject.transform.parent = axesParent.transform;
        foreach (GameObject point in axisZPointObjs)
        {
            point.transform.parent = axesParent.transform;
        }
        axisZLine.gameObject.transform.parent = axesParent.transform;
    }
}
