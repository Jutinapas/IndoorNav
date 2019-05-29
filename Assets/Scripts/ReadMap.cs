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

public class ReadMap : MonoBehaviour, PlacenoteListener
{

    private const string MAP_NAME = "GenericMap";
    public NavController navController;

    [SerializeField] GameObject mListElement;
    [SerializeField] RectTransform mListContentParent;
    [SerializeField] ToggleGroup mToggleGroup;

    [SerializeField] GameObject dListElement;
    [SerializeField] RectTransform dListContentParent;
    [SerializeField] ToggleGroup dToggleGroup;

    [SerializeField] Text statusText;
    [SerializeField] GameObject mapList;
    [SerializeField] GameObject destList;
    [SerializeField] GameObject navigationButton;

    private bool mapListUpdated = false;
    private bool destListUpdated = false;
    private bool isLocalize = false;

    private UnityARSessionNativeInterface mSession;
    private bool mARKitInit = false;

    private LibPlacenote.MapMetadataSettable mCurrMapDetails;

    private LibPlacenote.MapInfo mSelectedMapInfo;
    private string mSelectedMapId
    {
        get
        {
            return mSelectedMapInfo != null ? mSelectedMapInfo.placeId : null;
        }
    }

    void Start()
    {
        Input.location.Start();

        mSession = UnityARSessionNativeInterface.GetARSessionNativeInterface();
        StartARKit();
        FeaturesVisualizer.EnablePointcloud();
        LibPlacenote.Instance.RegisterListener(this);
    }

    private void StartARKit()
    {
        Debug.Log("Initializing ARKit");
        Application.targetFrameRate = 60;
        ConfigureSession(false);
    }

    private void ConfigureSession(bool clearPlanes)
    {
#if !UNITY_EDITOR
		ARKitWorldTrackingSessionConfiguration config = new ARKitWorldTrackingSessionConfiguration ();
		config.planeDetection = UnityARPlaneDetection.None;
		config.alignment = UnityARAlignment.UnityARAlignmentGravity;
		config.getPointCloudData = true;
		config.enableLightEstimation = true;
		mSession.RunWithConfig (config);
#endif
    }

    void Update()
    {

        if (!LibPlacenote.Instance.Initialized())
        {
            Debug.Log("SDK ยังไม่ถูกติดตั้ง");
            statusText.text = "SDK ยังไม่ถูกติดตั้ง";
            return;
        }

        if (!mapListUpdated && LibPlacenote.Instance.Initialized())
        {
            GetListMaps();
        }

        if (!mARKitInit && LibPlacenote.Instance.Initialized() && mSelectedMapId != null)
        {
            mARKitInit = true;
            Debug.Log("กำลังดาวน์โหลด");
            statusText.text = "กำลังดาวน์โหลด";

            LibPlacenote.Instance.LoadMap(mSelectedMapId,
                (completed, faulted, percentage) =>
                {
                    if (completed)
                    {
                        LibPlacenote.Instance.StartSession();
                        Debug.Log("Starting Session " + mSelectedMapId);
                        statusText.text = "เริ่มค้นหาตำแหน่ง";
                    }
                    else if (faulted)
                    {
                        Debug.Log("Failed to Load " + mSelectedMapId);
                        statusText.text = "ไม่สามารถดาวน์โหลดได้";
                    }
                    else
                    {
                        Debug.Log("Downloaded " + ((int)(percentage * 100)).ToString() + "%");
                        statusText.text = "กำลังดาวน์โหลด " + ((int)(percentage * 100)).ToString() + " %";
                    }
                }
            );

            mapList.SetActive(false);
        }

    }

    public void GetListMaps()
    {
        foreach (Transform t in mListContentParent.transform)
        {
            Destroy(t.gameObject);
        }

        LibPlacenote.Instance.ListMaps((mapList) =>
        {
            foreach (LibPlacenote.MapInfo mapInfoItem in mapList)
            {
                if (mapInfoItem.metadata.userdata != null)
                {
                    Debug.Log(mapInfoItem.metadata.userdata.ToString(Formatting.None));
                }
                AddMapToList(mapInfoItem);
            }
        });

        mapListUpdated = true;
        Debug.Log("Select Map in List");
        statusText.text = "เลือกแผนที่ที่ต้องการ";
    }

    void AddMapToList(LibPlacenote.MapInfo mapInfo)
    {
        GameObject newElement = Instantiate(mListElement) as GameObject;
        MapInfoElement listElement = newElement.GetComponent<MapInfoElement>();
        listElement.Initialize(mapInfo, mToggleGroup, mListContentParent, (value) =>
        {
            OnMapSelected(mapInfo);
        });
    }

    void OnMapSelected(LibPlacenote.MapInfo mapInfo)
    {
        mSelectedMapInfo = mapInfo;
    }

    public void GetListDests()
    {
        foreach (Transform t in dListContentParent.transform)
        {
            Destroy(t.gameObject);
        }

        foreach (NodeShape shape in GetComponent<CustomShapeManager>().shapeList)
        {
            if (shape.info.type == 1.GetHashCode())
            {
                Debug.Log("Add " + shape.name);
                AddDestToList(shape);
            }
        }

        Debug.Log("Select Dest in List");
        statusText.text = "เลือกสถานที่ที่ต้องการ";
    }

    void AddDestToList(NodeShape dest)
    {
        GameObject newElement = Instantiate(dListElement) as GameObject;
        DestInfoElement listElement = newElement.GetComponent<DestInfoElement>();
        listElement.Initialize(dest, dToggleGroup, dListContentParent, (value) =>
        {
            Debug.Log(dest.name);
            OnDestSelected(dest);
        });
    }

    void OnDestSelected(NodeShape dest)
    {
        navController.SetInitialized(false);
        navController.SetComplete(false);
        Debug.Log("Start Init");
        if (navController != null)
        {
            navController.InitializeNavigation();
        }
        navController.InitNav(dest.id);
        destList.SetActive(false);
    }

    public void OnNavButtonClick()
    {
        if (!destListUpdated)
        {
            destList.SetActive(true);
            GetListDests();
            destListUpdated = true;
        }
        else
        {
            if (destList.activeSelf)
            {
                destList.SetActive(false);
                navigationButton.GetComponentInChildren<Text>().text = "เลือกสถานที่";
            }
            else
            {
                destList.SetActive(true);
                navigationButton.GetComponentInChildren<Text>().text = "ย้อนกลับ";
            }
        }
    }

    public void OnPose(Matrix4x4 outputPose, Matrix4x4 arkitPose) { }

    public void OnStatusChange(LibPlacenote.MappingStatus prevStatus, LibPlacenote.MappingStatus currStatus)
    {
        if (currStatus == LibPlacenote.MappingStatus.RUNNING && prevStatus == LibPlacenote.MappingStatus.LOST)
        {
            if (!isLocalize)
            {
                isLocalize = true;
                statusText.text = "ระบุตำแหน่งสำเร็จ";
                FeaturesVisualizer.DisablePointcloud();
                GetComponent<CustomShapeManager>().LoadShapesJSON(mSelectedMapInfo.metadata.userdata);
                navigationButton.SetActive(true);
            }
        }
        else if (currStatus == LibPlacenote.MappingStatus.RUNNING && prevStatus == LibPlacenote.MappingStatus.WAITING)
        {
        }
        else if (currStatus == LibPlacenote.MappingStatus.LOST)
        {
        }
        else if (currStatus == LibPlacenote.MappingStatus.WAITING)
        {
        }
    }

}
