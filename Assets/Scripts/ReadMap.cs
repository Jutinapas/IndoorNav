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

    [SerializeField] GameObject mListElement;
    [SerializeField] RectTransform mListContentParent;
    [SerializeField] ToggleGroup mToggleGroup;

    [SerializeField] GameObject dListElement;
    [SerializeField] RectTransform dListContentParent;
    [SerializeField] ToggleGroup dToggleGroup;

    [SerializeField] Text statusText;
    [SerializeField] GameObject mapList;
    [SerializeField] GameObject placeList;
    [SerializeField] GameObject navigationButton;

    private bool mapListUpdated = false;

    private UnityARSessionNativeInterface mSession;
    private bool mFrameUpdated = false;
    private UnityARImageFrameData mImage = null;
    private UnityARCamera mARCamera;
    private bool mARKitInit = false;

    private LibPlacenote.MapMetadataSettable mCurrMapDetails;

    private bool mReportDebug = false;

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
        UnityARSessionNativeInterface.ARFrameUpdatedEvent += ARFrameUpdated;
        StartARKit();
        FeaturesVisualizer.EnablePointcloud();
        LibPlacenote.Instance.RegisterListener(this);
    }

    void OnDisable()
    {
        UnityARSessionNativeInterface.ARFrameUpdatedEvent -= ARFrameUpdated;
    }

    private void ARFrameUpdated(UnityARCamera camera)
    {
        mFrameUpdated = true;
        mARCamera = camera;
    }

    private void InitARFrameBuffer()
    {
        mImage = new UnityARImageFrameData();

        int yBufSize = mARCamera.videoParams.yWidth * mARCamera.videoParams.yHeight;
        mImage.y.data = Marshal.AllocHGlobal(yBufSize);
        mImage.y.width = (ulong)mARCamera.videoParams.yWidth;
        mImage.y.height = (ulong)mARCamera.videoParams.yHeight;
        mImage.y.stride = (ulong)mARCamera.videoParams.yWidth;

        int vuBufSize = mARCamera.videoParams.yWidth * mARCamera.videoParams.yWidth / 2;
        mImage.vu.data = Marshal.AllocHGlobal(vuBufSize);
        mImage.vu.width = (ulong)mARCamera.videoParams.yWidth / 2;
        mImage.vu.height = (ulong)mARCamera.videoParams.yHeight / 2;
        mImage.vu.stride = (ulong)mARCamera.videoParams.yWidth;

        mSession.SetCapturePixelData(true, mImage.y.data, mImage.vu.data);
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
        if (mFrameUpdated)
        {
            mFrameUpdated = false;
            if (mImage == null)
            {
                InitARFrameBuffer();
            }

            if (mARCamera.trackingState == ARTrackingState.ARTrackingStateNotAvailable)
            {
                return;
            }

            if (!LibPlacenote.Instance.Initialized())
            {
                Debug.Log("SDK not yet initialized");
                statusText.text = "SDK not yet initialized";
                return;
            }

            if (!mapListUpdated && LibPlacenote.Instance.Initialized())
            {
                GetListMaps();
            }

            if (!mARKitInit && LibPlacenote.Instance.Initialized() && mSelectedMapId != null)
            {
                mARKitInit = true;
                Debug.Log("LOADING MAP!!!!!");
                statusText.text = "LOADING MAP!!!!!";

                LibPlacenote.Instance.LoadMap(mSelectedMapId,
                    (completed, faulted, percentage) =>
                    {
                        if (completed)
                        {
                            if (mReportDebug)
                            {
                                LibPlacenote.Instance.StartRecordDataset(
                                    (datasetCompleted, datasetFaulted, datasetPercentage) =>
                                    {
                                        if (datasetCompleted)
                                        {
                                            Debug.Log("Dataset Upload Complete");
                                        }
                                        else if (datasetFaulted)
                                        {
                                            Debug.Log("Dataset Upload Faulted");
                                        }
                                        else
                                        {
                                            Debug.Log("Dataset Upload: " + datasetPercentage.ToString("F2") + "/1.0");
                                        }
                                    });
                                Debug.Log("Started Debug Report");
                            }

                            LibPlacenote.Instance.StartSession();
                            Debug.Log("Starting session " + mSelectedMapId);
                            statusText.text = "Starting session " + mSelectedMapId;
                        }
                        else if (faulted)
                        {
                            Debug.Log("Failed to load " + mSelectedMapId);
                            statusText.text = "Failed to load " + mSelectedMapId;
                        }
                        else
                        {
                            Debug.Log("Map Downloaded: " + ((int)(percentage * 100)).ToString() + "%");
                            statusText.text = "Map Downloaded: " + ((int)(percentage * 100)).ToString() + " %";
                        }
                    }
                );

                mSelectedMapInfo = null;
                mapList.SetActive(false);
                navigationButton.SetActive(true);
                GetListDests();
            }

            Matrix4x4 matrix = mSession.GetCameraPose();

            Vector3 arkitPosition = PNUtility.MatrixOps.GetPosition(matrix);
            Quaternion arkitQuat = PNUtility.MatrixOps.GetRotation(matrix);

            LibPlacenote.Instance.SendARFrame(mImage, arkitPosition, arkitQuat, mARCamera.videoParams.screenOrientation);
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
        statusText.text = "Select Map in List";
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

        foreach (Shape shape in GetComponent<CustomShapeManager>().shapeList)
        {
            if (shape.info.type == 1.GetHashCode())
            {
                Debug.Log("Add " + ((Destination)shape).name);
                AddDestToList((Destination)shape);
            }
        }

        Debug.Log("Select Dest in List");
        statusText.text = "Select Dest in List";
    }

    void AddDestToList(Destination dest)
    {
        GameObject newElement = Instantiate(dListElement) as GameObject;
        DestInfoElement listElement = newElement.GetComponent<DestInfoElement>();
        listElement.Initialize(dest, dToggleGroup, dListContentParent, (value) =>
        {
            OnDestSelected(dest);
        });
    }

    void OnDestSelected(Destination dest)
    {


    }

    public void OnPose(Matrix4x4 outputPose, Matrix4x4 arkitPose) { }

    public void OnStatusChange(LibPlacenote.MappingStatus prevStatus, LibPlacenote.MappingStatus currStatus)
    {
        Debug.Log("prevStatus: " + prevStatus.ToString() + " currStatus: " + currStatus.ToString());
        if (currStatus == LibPlacenote.MappingStatus.RUNNING && prevStatus == LibPlacenote.MappingStatus.LOST)
        {
            Debug.Log("Localized: " + mSelectedMapInfo.metadata.name);
            GetComponent<CustomShapeManager>().LoadShapesJSON(mSelectedMapInfo.metadata.userdata);
            FeaturesVisualizer.DisablePointcloud();
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
            // if (GetComponent<CustomShapeManager>().placeObjList.Count != 0) {
            //     //GetComponent<CustomShapeManager>().ClearShapes();
            // }
        }
    }

    public void OnApplicationQuit()
    {
        LibPlacenote.Instance.Shutdown();
        GetComponent<CustomShapeManager>().ClearShapes();
    }

}
