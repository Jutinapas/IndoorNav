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

[RequireComponent(typeof(CustomShapeManager))]
public class CreateMap : MonoBehaviour, PlacenoteListener
{

    public Text statusText;
    public Text destNameText;
    public Text mapNameText;

    private string mapName;
    private string destName;

    private CustomShapeManager shapeManager;

    private bool dropNode = false;
    private bool shouldSaveMap = true;
    private bool shouldHit = true;

    private UnityARSessionNativeInterface mSession;
    private bool mARKitInit = false;

    private LibPlacenote.MapMetadataSettable mCurrMapDetails;

    public GameObject mapping;
    public GameObject pathing;
    public GameObject destNaming;
    public GameObject mapNaming;
    public GameObject saving;
    public GameObject homeButton;

    private GameObject selectedNode;
    public Material[] materials;

    private const int WAYPOINT_MATERIAL = 0;
    private const int DIAMOND_MATERIAL = 1;

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
        statusText.text = "กำลังเตรียมพร้อม";
        Debug.Log("กำลังเตรียมพร้อม");
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
#endif
    }

    void Update()
    {

        if (!mARKitInit && LibPlacenote.Instance.Initialized())
        {
            mARKitInit = true;
            statusText.text = "พร้อมสร้างแผนที่";
            Debug.Log("พร้อมสร้างแผนที่");
            StartSavingMap();
        }

        if (dropNode && mARKitInit)
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

        if (shouldHit && !dropNode && selectedNode == null && mARKitInit)
        {
            if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.GetTouch(0).position);
                RaycastHit hit;

                Debug.Log("Tapped!");

                if (Physics.Raycast(ray, out hit) && hit.transform.tag == "Node" && hit.transform.name == "Waypoint(Clone)")
                {
                    shouldHit = false;
                    mapping.SetActive(false);
                    destNaming.SetActive(true);
                    selectedNode = hit.transform.gameObject;

                    Debug.Log("Tapped!");

                    Renderer[] renderers = selectedNode.GetComponentsInChildren<Renderer>();
                    foreach (Renderer renderer in renderers)
                    {
                        renderer.sharedMaterial = materials[DIAMOND_MATERIAL];
                        Debug.Log("Tapped!");
                    }

                }
            }
        }
    }

    void StartSavingMap()
    {
        if (!LibPlacenote.Instance.Initialized())
        {
            statusText.text = "เกิดข้อผิดพลาด";
            Debug.Log("เกิดข้อผิดพลาด โปรดลองใหม่อีกครั้ง");
            return;
        }

        statusText.text = "เริ่มสร้างแผนที่";
        Debug.Log("เริ่มสร้างแผนที่");
        LibPlacenote.Instance.StartSession();
    }

    public void OnToPathClick()
    {
        dropNode = true;
        statusText.text = "เริ่มสร้างเส้นทาง";
        Debug.Log("เริ่มสร้างเส้นทาง");
    }

    public void OnToMapClick()
    {
        dropNode = false;
        statusText.text = "หยุดสร้างเส้นทาง";
        Debug.Log("หยุดสร้างเส้นทาง");
    }

    public void OnUndoClick()
    {
        shapeManager.UndoShape();
        statusText.text = "ลบเส้นทาง";
        Debug.Log("ลบเส้นทาง");
    }

    public void OnSaveDestClick()
    {
        if (destNameText.text != "" && selectedNode != null)
        {
            destName = destNameText.text;
            shapeManager.CreateDest(selectedNode, destName);
            backFromDestNaming();
        }
    }

    public void OnCancelDestClick()
    {
        if (selectedNode != null)
        {
            Renderer[] renderers = selectedNode.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                renderer.sharedMaterial = materials[WAYPOINT_MATERIAL];
                Debug.Log("Tapped!");
            }
            backFromDestNaming();
        }
    }

    private void backFromDestNaming()
    {
        destNameText.text = "";
        selectedNode = null;
        destNaming.SetActive(false);
        mapping.SetActive(true);
        shouldHit = true;
    }

    public void OnSaveMapClick()
    {
        if (mapNameText.text != "")
        {

            mapName = mapNameText.text;

            if (!LibPlacenote.Instance.Initialized())
            {
                statusText.text = "SDK ยังไม่ถูกติดตั้ง";
                Debug.Log("SDK ยังไม่ถูกติดตั้ง");
                ToastManager.ShowToast("SDK ยังไม่ถูกติดตั้ง", 2f);
                return;
            }

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
                                statusText.text = "ลบแผนที่ ID: " + map.placeId;
                                Debug.Log("ลบแผนที่ ID: " + map.placeId);
                                SaveCurrentMap();
                            }
                            else
                            {
                                statusText.text = "ไม่สามารถลบแผนที่ ID: " + map.placeId;
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

    public void OnCancelMapClick()
    {
        mapNameText.text = "";
    }

    void SaveCurrentMap()
    {
        if (shouldSaveMap)
        {
            shouldSaveMap = false;

            if (!LibPlacenote.Instance.Initialized())
            {
                statusText.text = "SDK ยังไม่ถูกติดตั้ง";
                Debug.Log("SDK ยังไม่ถูกติดตั้ง");
                ToastManager.ShowToast("SDK ยังไม่ถูกติดตั้ง", 2f);
                return;
            }

            mapNameText.text = "";
            mapNaming.SetActive(false);
            saving.SetActive(true);

            bool useLocation = Input.location.status == LocationServiceStatus.Running;
            LocationInfo locationInfo = Input.location.lastData;

            statusText.text = "กำลังอัพโหลด";
            Debug.Log("กำลังอัพโหลด");
            LibPlacenote.Instance.SaveMap(
                (mapId) =>
                {
                    LibPlacenote.Instance.StopSession();

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
                        Debug.Log("อัพโหลดเสร็จสิ้น: " + mCurrMapDetails.name);
                        statusText.text = "อัพโหลดเสร็จสิ้น";
                        homeButton.SetActive(true);
                    }
                    else if (faulted)
                    {
                        Debug.Log("เกิดข้อผิดพลาด: " + mCurrMapDetails.name);
                        statusText.text = "เกิดข้อผิดพลาด";
                        homeButton.SetActive(true);
                    }
                    else
                    {
                        Debug.Log("กำลังอัพโหลด: " + mCurrMapDetails.name + " " + ((int)(percentage * 100)).ToString() + "%");
                        statusText.text = "กำลังอัพโหลด " + ((int)(percentage * 100)).ToString() + "%";
                    }
                }
            );
        }
    }

    public void OnExitButtonClick()
    {
        mapping.SetActive(true);
        pathing.SetActive(false);
        mapNaming.SetActive(false);
        destNaming.SetActive(false);
        saving.SetActive(false);
        homeButton.SetActive(false);
        destName = "";
        mapName = "";
        destNameText.text = "";
        mapNameText.text = "";
        LibPlacenote.Instance.StopSession ();
        FeaturesVisualizer.clearPointcloud();
        GetComponent<ShapeManager>().ClearShapes();
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
            if (GetComponent<CustomShapeManager>().shapeObjList.Count != 0)
            {
                GetComponent<CustomShapeManager>().ClearShapes();
            }
        }
    }

}
