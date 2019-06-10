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

public class ReadMap : MonoBehaviour, PlacenoteListener
{

    [SerializeField] NavController navController;

    //GameObject
    [SerializeField] GameObject mListElement;
    [SerializeField] GameObject dListElement;
    [SerializeField] GameObject mapList;
    [SerializeField] GameObject destList;
    [SerializeField] GameObject navigationButton;
    [SerializeField] GameObject resetButton;
    [SerializeField] GameObject resetAlert;

    [SerializeField] RectTransform mListContentParent;
    [SerializeField] ToggleGroup mToggleGroup;
    [SerializeField] RectTransform dListContentParent;
    [SerializeField] ToggleGroup dToggleGroup;

    [SerializeField] Text statusText;

    private bool mapListUpdated = false;
    private bool destListUpdated = false;
    private bool isLocalize = false;

    private UnityARSessionNativeInterface mSession;
    private bool mARKitInit = false;

    private LibPlacenote.MapInfo mSelectedMapInfo;
    private string mSelectedMapId
    {
        get
        {
            return mSelectedMapInfo != null ? mSelectedMapInfo.placeId : null;
        }
    }

    private enum Stage { START = 0, NAVI = 1};
    private Stage currentStage = Stage.START;

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
        Application.targetFrameRate = 60;
        ConfigureSession();
    }

    private void ConfigureSession()
    {
#if !UNITY_EDITOR
		ARKitWorldTrackingSessionConfiguration config = new ARKitWorldTrackingSessionConfiguration ();
		config.planeDetection = UnityARPlaneDetection.None;
		config.alignment = UnityARAlignment.UnityARAlignmentGravity;
		config.getPointCloudData = true;
		config.enableLightEstimation = true;
		mSession.RunWithConfig (config);
        currentStage = Stage.START;
#endif
    }

    void Update()
    {

        if (!LibPlacenote.Instance.Initialized())
        {
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
            statusText.text = "กำลังดาวน์โหลด.." ;

            LibPlacenote.Instance.LoadMap(mSelectedMapId,
                (completed, faulted, percentage) =>
                {
                    if (completed)
                    {
                        LibPlacenote.Instance.StartSession();
                        Debug.Log("Starting Session " + mSelectedMapId);
                        statusText.text = "เริ่มค้นหาตำแหน่งใน " + mSelectedMapInfo.metadata.name;
                        currentStage = Stage.NAVI;
                    }
                    else if (faulted)
                    {
                        Debug.Log("Failed to Load " + mSelectedMapId);
                        statusText.text = "ไม่สามารถดาวน์โหลดได้ ลองใหม่อีกครั้ง";
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
        statusText.text = "เลือกแผนที่";
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
                AddDestToList(shape);
            }
        }
        statusText.text = "เลือกสถานที่เพื่อเริ่มนำทาง";
    }

    void AddDestToList(NodeShape dest)
    {
        GameObject newElement = Instantiate(dListElement) as GameObject;
        DestInfoElement listElement = newElement.GetComponent<DestInfoElement>();
        listElement.Initialize(dest, dListContentParent, () =>
        {
            Debug.Log(dest.name);
            OnDestSelected(dest);
        });
    }

    void OnDestSelected(NodeShape dest)
    {
        if (navController != null)
        {
            navController.InitializeNavigation();
        }
        navController.SetInitialized(false);
        navController.SetComplete(false);
        navController.InitNav(dest.id);
        destList.SetActive(false);
        resetButton.SetActive(true);
        navigationButton.GetComponentInChildren<Text>().text = "เลือกสถานที่";
        statusText.text = "นำทางไปยัง " + dest.name;
    }

    public void OnNavButtonClick()
    {
        if (!destListUpdated)
        {
            resetButton.SetActive(false);
            destList.SetActive(true);
            GetListDests();
            destListUpdated = true;
        }
        else
        {
            if (destList.activeSelf)
            {
                resetButton.SetActive(true);
                destList.SetActive(false);
                navigationButton.GetComponentInChildren<Text>().text = "เลือกสถานที่";
                statusText.text = "เลือกสถานที่เพื่อเริ่มนำทาง";
            }
            else
            {
                resetButton.SetActive(false);
                destList.SetActive(true);
                navController.DeactivatePath();
                navigationButton.GetComponentInChildren<Text>().text = "ย้อนกลับ";
                statusText.text = "เลือกสถานที่เพื่อเริ่มนำทาง";
            }
        }
    }

    public void OnResetButtonClick()
    {
        resetButton.SetActive(false);
        resetAlert.SetActive(true);
        if (currentStage == Stage.START)
        {
            mapList.SetActive(false);
        }
        else if (currentStage == Stage.NAVI)
        {
            navigationButton.SetActive(false);
        }
        statusText.text = "ต้องการเลือกแผนที่ใหม่ใช่ไหม ?";
    }

    public void OnResetConfirmClick()
    {
        resetButton.SetActive(true);
        resetAlert.SetActive(false);

        if (currentStage == Stage.NAVI)
        {
            navigationButton.SetActive(false);
        }
        statusText.text = "เลือกแผนที่";

        mapList.SetActive(true);
        LibPlacenote.Instance.StopSession();
        FeaturesVisualizer.clearPointcloud();
        GetComponent<CustomShapeManager>().ClearShapes();
        ConfigureSession();
        mARKitInit = false;
        mapListUpdated = false;
        destListUpdated = false;
        isLocalize = false;
        mSelectedMapInfo = null;
    }

    public void OnResetCancelClick()
    {
        resetButton.SetActive(true);
        resetAlert.SetActive(false);
        if (currentStage == Stage.START)
        {
            mapList.SetActive(true);
            statusText.text = "เลือกแผนที่";
        }
        else if (currentStage == Stage.NAVI)
        {
            navigationButton.SetActive(true);
            statusText.text = "เลือกสถานที่เพื่อเริ่มนำทาง";
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
                statusText.text = "เลือกสถานที่ใน " + mSelectedMapInfo.metadata.name;
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
