using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.iOS;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;

/*========================================
 * Main Class for Managing Markers
======================================== */

public class CustomShapeManager : MonoBehaviour {

	public NavController navController;

	public List<GameObject> ShapePrefabs = new List<GameObject> ();

	[HideInInspector]
	public List<Place> places = new List<Place>();
	[HideInInspector]
	public List<Pathway> pathways = new List<Pathway>();

	[HideInInspector]
	public List<ShapeInfo> placeInfoList = new List<ShapeInfo>();
	[HideInInspector]
    public List<ShapeInfo> pathInfoList = new List<ShapeInfo>();

	[HideInInspector]
	public List<GameObject> placeObjList = new List<GameObject>();
	[HideInInspector]
    public List<GameObject> pathObjList = new List<GameObject>();

	private bool shapesLoaded = false;

    //-------------------------------------------------
    // All shape management functions (add shapes, save shapes to metadata etc.
    //-------------------------------------------------

    public void AddShape(Vector3 shapePosition, Quaternion shapeRotation, int shapeType)
    {
        ShapeInfo shapeInfo = new ShapeInfo();
        shapeInfo.px = shapePosition.x;
        shapeInfo.py = shapePosition.y;
        shapeInfo.pz = shapePosition.z;
        shapeInfo.qx = shapeRotation.x;
        shapeInfo.qy = shapeRotation.y;
        shapeInfo.qz = shapeRotation.z;
        shapeInfo.qw = shapeRotation.w;
		shapeInfo.shapeType = shapeType.GetHashCode();

		GameObject shape = ShapeFromInfo(shapeInfo);

		if (shapeType == 0 || shapeType == 1) {
			pathInfoList.Add(shapeInfo);
			pathObjList.Add(shape);
		} else if (shapeType == 3) {
			placeInfoList.Add(shapeInfo);
			placeObjList.Add(shape);

			Place place = new Place();
			place.name = "Default Place";
			place.shape = shapeInfo;
			places.Add(place);
		}

    }

	public void AddDestinationShape (string destName) {
		//change last waypoint to diamond
		ShapeInfo lastInfo = pathInfoList [pathInfoList.Count - 1];
		lastInfo.shapeType = 1.GetHashCode ();
		GameObject shape = ShapeFromInfo(lastInfo);
		shape.GetComponent<DiamondBehavior> ().Activate (true);
		//destroy last shape
		Destroy (pathObjList [pathObjList.Count - 1]);
		//add new shape
		pathObjList.Add (shape);
		
		Pathway pathway = new Pathway();
		pathway.name = destName;
		pathway.shapes = pathInfoList;
		pathways.Add(pathway);

		//ClearPaths();
	}

    public GameObject ShapeFromInfo(ShapeInfo info)
    {
		GameObject shape;
		Vector3 position = new Vector3 (info.px, info.py, info.pz);
		//if loading map, change waypoint to arrow
		if (SceneManager.GetActiveScene ().name == "ReadMap" && info.shapeType == 0) {
			shape = Instantiate (ShapePrefabs [2]);
		} else {
			shape = Instantiate (ShapePrefabs [info.shapeType]);
			if (info.shapeType == 0)
				shape.tag = "Waypoint";
			else if (info.shapeType == 3) {
				shape.tag = "Place";
				//shape.GetComponent<TextMesh>()
			}
		}
		if (shape.GetComponent<Node> () != null) {
			shape.GetComponent<Node> ().pos = position;
            Debug.Log(position);
		}
		shape.tag = "Waypoint";
		shape.transform.position = position;
		shape.transform.rotation = new Quaternion(info.qx, info.qy, info.qz, info.qw);
		shape.transform.localScale = new Vector3(.3f, .3f, .3f);

		return shape;
    }

    public void ClearShapes()
    {
        Debug.Log("CLEARING SHAPES!!!!!!!");
		ClearPlaces();
        ClearPaths();
    }

	public void ClearPlaces() {
		foreach (var obj in placeObjList)
        {
            Destroy(obj);
        }
        placeObjList.Clear();
        placeInfoList.Clear();
	}

	public void ClearPaths() {
		foreach (var obj in pathObjList)
        {
            Destroy(obj);
        }
        pathObjList.Clear();
        pathInfoList.Clear();
	}

	public JObject Places2JSON()
    {
        PlaceList placeList = new PlaceList();
        placeList.places = new Place[places.Count];
        for (int i = 0; i < places.Count; i++)
        {
            placeList.places[i] = places[i];
        }

        return JObject.FromObject(placeList);
    }

    public JObject Pathways2JSON()
    {
        PathwayList pathList = new PathwayList();
        pathList.pathways = new Pathway[pathways.Count];
        for (int i = 0; i < pathways.Count; i++)
        {
            pathList.pathways[i] = pathways[i];
        }

        return JObject.FromObject(pathList);
    }

    public void LoadShapesJSON(JToken mapMetadata)
    {
		if (!shapesLoaded) {
			shapesLoaded = true;
            Debug.Log("LOADING SHAPES>>>");
			if (mapMetadata is JObject && mapMetadata ["shapeList"] is JObject) {
				ShapeList shapeList = mapMetadata ["shapeList"].ToObject<ShapeList> ();
				if (shapeList.shapes == null) {
					Debug.Log ("no shapes dropped");
					return;
				}

				foreach (var shapeInfo in shapeList.shapes) {
					pathInfoList.Add (shapeInfo);
					GameObject shape = ShapeFromInfo (shapeInfo);
					pathObjList.Add (shape);
				}

				if (navController != null) {
					navController.InitializeNavigation ();
				}
			}
		}
    }
}

public class PathwayList {
    public Pathway[] pathways;
}

public class Pathway {
	public string name; 
    public List<ShapeInfo> shapes;
}

public class PlaceList {
	public Place[] places;
}

public class Place {
	public string name;
	public ShapeInfo shape;
}
