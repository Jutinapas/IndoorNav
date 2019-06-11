using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;
using UnityEngine.XR.iOS;
using System.Runtime.InteropServices;
using System.IO;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;

[RequireComponent(typeof(CustomShapeManager))]
public class CreateMap : MonoBehaviour, PlacenoteListener
{

    //Text
    [SerializeField] Text statusText;

    //InputField
    [SerializeField] InputField destNameField;
    [SerializeField] InputField mapNameField;

    //GameObject
    [SerializeField] GameObject processBar;
    [SerializeField] GameObject process1;
    [SerializeField] GameObject process2;
    [SerializeField] GameObject process3;
    [SerializeField] GameObject pathing;
    [SerializeField] GameObject destNaming;
    [SerializeField] GameObject mapNaming;
    [SerializeField] GameObject saving;

    //Button
    [SerializeField] GameObject resetButton;
    [SerializeField] GameObject toPathButton;
    [SerializeField] GameObject toSaveButton;
    [SerializeField] GameObject undoButton;
    [SerializeField] GameObject startNodeButton;
    [SerializeField] GameObject stopNodeButton;
    [SerializeField] GameObject homeButton;

    //Alert
    [SerializeField] GameObject resetAlert;
    [SerializeField] GameObject toPathAlert;
    [SerializeField] GameObject undoAlert;
    [SerializeField] GameObject saveAlert;

    private string mapName = "";
    private string destName = "";
    private enum Stage { START = 0, MAPPING = 1, PATHING = 2, SAVING = 3 };
    private Stage currentStage = Stage.START;

    private CustomShapeManager shapeManager;

    private bool shouldDropNode = false;
    private bool shouldSaveMap = true;
    private bool shouldHit = false;

    private UnityARSessionNativeInterface mSession;
    private bool mARKitInit = false;

    private LibPlacenote.MapMetadataSettable mCurrMapDetails;

    private GameObject selectedNode;
    public Material[] materials;

    private const int WAYPOINT_MATERIAL = 0;
    private const int DIAMOND_MATERIAL = 1;

    //Color
    private Color32 GRAY_COLOR = new Color32(158, 158, 158, 255);
    private Color32 ORANGE_COLOR = new Color32(255, 162, 47, 255);
    private Color32 WHITE_COLOR = new Color32(255, 255, 255, 255);

    private int fingerID = -1;

    private void Awake()
    {
#if !UNITY_EDITOR
        fingerID = 0; 
#endif
    }

    void Start()
    {
        shapeManager = GetComponent<CustomShapeManager>();

        Input.location.Start();

        mSession = UnityARSessionNativeInterface.GetARSessionNativeInterface();
        StartARKit();
        FeaturesVisualizer.clearPointcloud();
        FeaturesVisualizer.EnablePointcloud();
        LibPlacenote.Instance.RegisterListener(this);
    }

    private void StartARKit()
    {
        statusText.text = "กำลังเริ่มทำงาน..";
        Application.targetFrameRate = 60;
        ConfigureSession();
    }

    private void ConfigureSession()
    {
#if !UNITY_EDITOR
		ARKitWorldTrackingSessionConfiguration config = new ARKitWorldTrackingSessionConfiguration ();

		if (UnityARSessionNativeInterface.IsARKit_1_5_Supported ()) {
			config.planeDetection = UnityARPlaneDetection.HorizontalAndVertical;
		} else {
			config.planeDetection = UnityARPlaneDetection.Horizontal;
		}

		config.alignment = UnityARAlignment.UnityARAlignmentGravity;
		config.getPointCloudData = true;
		config.enableLightEstimation = true;
		mSession.RunWithConfig (config);
        currentStage = Stage.START;
#endif
    }

    void Update()
    {
        if (!mARKitInit)
        {
            mARKitInit = true;
            StartSavingMap();
        }

        if (shouldDropNode && mARKitInit)
        {
            Transform player = Camera.main.transform;

            Collider[] hitColliders = Physics.OverlapSphere(player.position, .75f);
            int i = 0;
            while (i < hitColliders.Length)
            {
                if (hitColliders[i].CompareTag("Node"))
                {
                    return;
                }
                i++;
            }
            Vector3 position = player.position;
            position.y = -.5f;
            Debug.Log(position);
            shapeManager.CreateWay(position);
        }

        if (shouldHit && selectedNode == null && mARKitInit)
        {
            if (!EventSystem.current.IsPointerOverGameObject(fingerID) && Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.GetTouch(0).position);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit) && hit.transform.tag == "Node")
                {
                    shouldHit = false;
                    resetButton.SetActive(false);
                    toSaveButton.SetActive(false);
                    undoButton.SetActive(false);
                    startNodeButton.SetActive(false);
                    destNaming.SetActive(true);
                    selectedNode = hit.transform.gameObject;

                    statusText.text = "ใส่ชื่อสถานที่";

                    if (selectedNode.name == "Waypoint(Clone)")
                    {
                        selectedNode.transform.GetChild(0).gameObject.GetComponent<Renderer>().sharedMaterial = materials[DIAMOND_MATERIAL];
                    }
                    else if (selectedNode.name == "Destination(Clone)")
                    {
                        destNameField.text = selectedNode.transform.GetChild(0).GetComponent<TextMeshPro>().text;
                    }
                }
            }
        }
    }

    void StartSavingMap()
    {
        if (!LibPlacenote.Instance.Initialized())
        {
            statusText.text = "เกิดข้อผิดพลาด โปรดลองใหม่อีกครั้ง";
            resetButton.SetActive(true);
            return;
        }

        statusText.text = "กวาดกล้องไปรอบ ๆ เพื่อสร้างแผนที่";
        LibPlacenote.Instance.StartSession();

        currentStage = Stage.MAPPING;
        processBar.SetActive(true);
        resetButton.SetActive(true);
        toPathButton.SetActive(true);
    }

    //Reset Handle
    public void OnResetClick()
    {
        //
        if (currentStage == Stage.MAPPING)
        {
            toPathButton.SetActive(false);
        }
        else if (currentStage == Stage.PATHING)
        {
            toSaveButton.SetActive(false);
            startNodeButton.SetActive(false);
            undoButton.SetActive(false);
            shouldHit = false;
        }

        statusText.text = "สร้างแผนที่ใหม่ ?";
        resetAlert.SetActive(true);
        resetButton.SetActive(false);
    }

    public void OnResetConfirmClick()
    {
        //
        if (currentStage == Stage.START)
        {
            resetAlert.SetActive(false);
        }
        else if (currentStage == Stage.MAPPING)
        {
            resetAlert.SetActive(false);
        }
        else if (currentStage == Stage.PATHING)
        {
            resetAlert.SetActive(false);
            startNodeButton.SetActive(true);
            undoButton.SetActive(true);
            pathing.SetActive(false);
            shouldHit = false;
            process1.GetComponent<Image>().color = ORANGE_COLOR;
            process1.transform.localScale = new Vector3(1.175f, 1.175f, 1.175f);
            process2.GetComponent<Image>().color = WHITE_COLOR;
            process2.transform.localScale = new Vector3(1, 1, 1);
            process1.transform.SetAsLastSibling();
        }
        else if (currentStage == Stage.SAVING)
        {
            homeButton.SetActive(false);
            saving.SetActive(false);
            process1.GetComponent<Image>().color = ORANGE_COLOR;
            process1.transform.localScale = new Vector3(1.175f, 1.175f, 1.175f);
            process2.GetComponent<Image>().color = WHITE_COLOR;
            process2.transform.localScale = new Vector3(1, 1, 1);
            process3.GetComponent<Image>().color = WHITE_COLOR;
            process3.transform.localScale = new Vector3(1, 1, 1);
            process1.transform.SetAsLastSibling();
        }

        destName = "";
        mapName = "";
        destNameField.text = "";
        mapNameField.text = "";

        LibPlacenote.Instance.StopSession();
        FeaturesVisualizer.clearPointcloud();
        GetComponent<CustomShapeManager>().ClearShapes();
        ConfigureSession();
        mARKitInit = false;
    }

    public void OnResetCancelClick()
    {
        //
        if (currentStage == Stage.MAPPING)
        {
            toPathButton.SetActive(true);
            statusText.text = "กวาดกล้องไปรอบ ๆ เพื่อสร้างแผนที่";
        }
        else if (currentStage == Stage.PATHING)
        {
            toSaveButton.SetActive(true);
            startNodeButton.SetActive(true);
            undoButton.SetActive(true);
            shouldHit = true;
            statusText.text = "วางโหนดและสร้างเส้นทาง แตะที่โหนดเพื่อสร้างสถานที่";
        }

        resetAlert.SetActive(false);
        resetButton.SetActive(true);
    }

    //ToPath Handle
    public void OnToPathClick()
    {
        statusText.text = "เริ่มสร้างเส้นทาง ?";
        resetButton.SetActive(false);
        toPathButton.SetActive(false);
        toPathAlert.SetActive(true);
    }

    public void OnToPathConfirmClick()
    {
        statusText.text = "เริ่มสร้างเส้นทาง ?";
        pathing.SetActive(true);
        toSaveButton.SetActive(false);
        toPathButton.SetActive(false);
        toPathAlert.SetActive(false);
        resetButton.SetActive(true);
        shouldHit = true;
        currentStage = Stage.PATHING;
        process1.GetComponent<Image>().color = GRAY_COLOR;
        process1.transform.localScale = new Vector3(1, 1, 1);
        process2.GetComponent<Image>().color = ORANGE_COLOR;
        process2.transform.localScale = new Vector3(1.175f, 1.175f, 1.175f);
        process2.transform.SetAsLastSibling();
        statusText.text = "วางโหนดเพื่อสร้างเส้นทาง";
    }

    public void OnToPathCancelClick()
    {
        statusText.text = "กวาดกล้องไปรอบ ๆ เพื่อสร้างแผนที่";
        resetButton.SetActive(true);
        toPathButton.SetActive(true);
        toPathAlert.SetActive(false);
    }

    //StartNode Handle
    public void OnStartNodeClick()
    {
        resetButton.SetActive(false);
        toSaveButton.SetActive(false);
        undoButton.SetActive(false);
        startNodeButton.SetActive(false);
        stopNodeButton.SetActive(true);

        shouldDropNode = true;
        shouldHit = false;
        statusText.text = "เดินไปรอบ ๆ เพื่อวางโหนด และสร้างเส้นทาง";
    }

    //StopNode Handle
    public void OnStopCreateNodeClick()
    {
        resetButton.SetActive(true);
        toSaveButton.SetActive(true);
        undoButton.SetActive(true);
        startNodeButton.SetActive(true);
        stopNodeButton.SetActive(false);

        shouldDropNode = false;
        shouldHit = true;
        statusText.text = "แตะที่โหนดเพื่อสร้างสถานที่";
    }

    //Undo Handle
    public void OnUndoClick()
    {
        undoAlert.SetActive(true);
        resetButton.SetActive(false);
        toSaveButton.SetActive(false);
        startNodeButton.SetActive(false);
        undoButton.SetActive(false);

        shouldHit = false;
        statusText.text = "ลบโหนดล่าสุด ?";
    }

    public void OnUndoConfirmClick()
    {
        undoAlert.SetActive(false);
        resetButton.SetActive(true);
        toSaveButton.SetActive(true);
        startNodeButton.SetActive(true);
        undoButton.SetActive(true);
        shapeManager.UndoShape();

        shouldHit = true;
        statusText.text = "วางโหนดและสร้างเส้นทาง แตะที่โหนดเพื่อสร้างสถานที่";
    }

    public void OnUndoCancelClick()
    {
        undoAlert.SetActive(false);
        resetButton.SetActive(true);
        toSaveButton.SetActive(true);
        startNodeButton.SetActive(true);
        undoButton.SetActive(true);

        shouldHit = true;
        statusText.text = "วางโหนดและสร้างเส้นทาง แตะที่โหนดเพื่อสร้างสถานที่";
    }

    public void OnSaveDestClick()
    {
        if (destNameField.text != "" && selectedNode != null)
        {
            destName = destNameField.text;
            shapeManager.CreateDest(selectedNode, destName);
            backFromDestNaming();
        }
    }

    public void OnCancelDestClick()
    {
        if (selectedNode != null)
        {
            if (selectedNode.name == "Waypoint(Clone)")
            {
                selectedNode.transform.GetChild(0).gameObject.GetComponent<Renderer>().sharedMaterial = materials[WAYPOINT_MATERIAL];
            }
            backFromDestNaming();
        }
    }

    private void backFromDestNaming()
    {
        destNameField.text = "";
        Debug.Log(destNameField.text);
        selectedNode = null;
        destNaming.SetActive(false);
        resetButton.SetActive(true);
        toSaveButton.SetActive(true);
        undoButton.SetActive(true);
        startNodeButton.SetActive(true);
        shouldHit = true;
        statusText.text = "วางโหนดและสร้างเส้นทาง แตะที่โหนดเพื่อสร้างสถานที่";
    }

    public void OnToSaveClick()
    {
        resetButton.SetActive(false);
        toSaveButton.SetActive(false);
        undoButton.SetActive(false);
        startNodeButton.SetActive(false);

        shouldHit = false;

        mapNaming.SetActive(true);
        statusText.text = "ใส่ชื่อสถานที่";
    }

    public void OnToSaveSaveClick()
    {
        mapName = mapNameField.text;
        mapNaming.SetActive(false);
        saveAlert.SetActive(true);
        saveAlert.transform.GetChild(2).GetComponent<Text>().text = "ชื่อแผนที่ : " + mapName;
        saveAlert.transform.GetChild(3).GetComponent<Text>().text = "จำนวนสถานที่ : " + shapeManager.numDest;
        statusText.text = "บันทึกแผนที่ " + mapName;
    }

    public void OnToSaveCancelClick()
    {
        mapName = "";
        mapNameField.text = "";
        mapNaming.SetActive(false);
        resetButton.SetActive(true);
        toSaveButton.SetActive(true);
        undoButton.SetActive(true);
        startNodeButton.SetActive(true);

        shouldHit = true;
        statusText.text = "วางโหนดและสร้างเส้นทาง แตะที่โหนดเพื่อสร้างสถานที่";
    }

    public void OnSaveConfirmClick()
    {
        if (mapNameField.text != "")
        {
            mapName = mapNameField.text;

            if (!LibPlacenote.Instance.Initialized())
            {
                statusText.text = "SDK ยังไม่ถูกติดตั้ง";
                return;
            }

            resetButton.SetActive(false);
            toSaveButton.SetActive(false);
            saveAlert.SetActive(false);
            undoButton.SetActive(true);
            startNodeButton.SetActive(true);
            pathing.SetActive(false);

            saving.SetActive(true);

            LibPlacenote.Instance.SearchMaps(mapName, (LibPlacenote.MapInfo[] obj) =>
            {
                bool foundMap = false;
                foreach (LibPlacenote.MapInfo map in obj)
                {
                    if (map.metadata.name == mapName)
                    {
                        foundMap = true;
                        LibPlacenote.Instance.DeleteMap(map.placeId, (deleted, errMsg) =>
                        {
                            if (deleted)
                            {
                                Debug.Log("ลบแผนที่ ID: " + map.placeId);
                                SaveCurrentMap();
                            }
                            else
                            {
                                Debug.Log("ไม่สามารถลบแผนที่ ID: " + map.placeId);
                            }
                        });
                    }
                }

                if (!foundMap)
                    SaveCurrentMap();
            });

        }
    }

    public void OnSaveCancelClick()
    {
        mapName = "";
        mapNameField.text = "";
        resetButton.SetActive(true);
        toSaveButton.SetActive(true);
        undoButton.SetActive(true);
        startNodeButton.SetActive(true);
        saveAlert.SetActive(false);

        shouldHit = true;
        statusText.text = "วางโหนดและสร้างเส้นทาง แตะที่โหนดเพื่อสร้างสถานที่";
    }

    void SaveCurrentMap()
    {
        if (shouldSaveMap)
        {
            shouldSaveMap = false;

            if (!LibPlacenote.Instance.Initialized())
            {
                statusText.text = "SDK ยังไม่ถูกติดตั้ง";
                return;
            }

            currentStage = Stage.SAVING;
            process2.GetComponent<Image>().color = GRAY_COLOR;
            process2.transform.localScale = new Vector3(1, 1, 1);
            process3.GetComponent<Image>().color = ORANGE_COLOR;
            process3.transform.localScale = new Vector3(1.175f, 1.175f, 1.175f);
            process3.transform.SetAsLastSibling();

            bool useLocation = Input.location.status == LocationServiceStatus.Running;
            LocationInfo locationInfo = Input.location.lastData;

            statusText.text = "กำลังอัพโหลด..";
            LibPlacenote.Instance.SaveMap(
                (mapId) =>
                {
                    LibPlacenote.Instance.StopSession();
                    FeaturesVisualizer.clearPointcloud();

                    LibPlacenote.MapMetadataSettable metadata = new LibPlacenote.MapMetadataSettable();
                    metadata.name = mapName;
                    statusText.text = metadata.name;
                    Debug.Log(metadata.name);

                    JObject userdata = new JObject();
                    metadata.userdata = userdata;

                    JObject data = GetComponent<CustomShapeManager>().Shapes2JSON();
                    userdata["data"] = data;

                    if (useLocation)
                    {
                        metadata.location = new LibPlacenote.MapLocation();
                        metadata.location.latitude = locationInfo.latitude;
                        metadata.location.longitude = locationInfo.longitude;
                        metadata.location.altitude = locationInfo.altitude;
                    }

                    LibPlacenote.Instance.SetMetadata(mapId, metadata);
                    mCurrMapDetails = metadata;
                },
                (completed, faulted, percentage) =>
                {
                    if (completed)
                    {
                        statusText.text = "อัพโหลดเสร็จสิ้น";
                        homeButton.SetActive(true);
                    }
                    else if (faulted)
                    {
                        statusText.text = "เกิดข้อผิดพลาด โปรดลองใหม่อีกครั้ง";
                        homeButton.SetActive(true);
                    }
                    else
                    {
                        statusText.text = "กำลังอัพโหลด " + ((int)(percentage * 100)).ToString() + "%";
                    }
                }
            );
        }
    }

    public void OnPose(Matrix4x4 outputPose, Matrix4x4 arkitPose) { }

    public void OnStatusChange(LibPlacenote.MappingStatus prevStatus, LibPlacenote.MappingStatus currStatus)
    {
        Debug.Log("prevStatus: " + prevStatus.ToString() + ", currStatus: " + currStatus.ToString());
        if (currStatus == LibPlacenote.MappingStatus.RUNNING && prevStatus == LibPlacenote.MappingStatus.LOST)
        {
            Debug.Log("Localized");
        }
        else if (currStatus == LibPlacenote.MappingStatus.RUNNING && prevStatus == LibPlacenote.MappingStatus.WAITING)
        {
            Debug.Log("Mapping");
        }
        else if (currStatus == LibPlacenote.MappingStatus.LOST)
        {
            Debug.Log("Searching for position lock");
        }
        else if (currStatus == LibPlacenote.MappingStatus.WAITING)
        {
        }
    }

}
