using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.iOS;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;

public class ShapeInfo {
    public float px;
    public float py;
    public float pz;
    public float qx;
    public float qy;
    public float qz;
    public float qw;
    public int type;
}

public class Shape
{
    public ShapeInfo info;
}

public class Waypoint : Shape
{
    public int id;
}

public class Destination : Shape
{
    public int id;
    public string name;
}

public class ShapeList {
    public Shape[] shapes;
}

public class CustomShapeManager : MonoBehaviour
{
    public NavController navController;

    public List<GameObject> ShapePrefabs = new List<GameObject>();

    [HideInInspector]
    public List<GameObject> shapeObjList = new List<GameObject>();
    [HideInInspector]
    public List<GameObject> edgeObjList = new List<GameObject>();
    [HideInInspector]
    public List<int> numEdgeList = new List<int>();

    [HideInInspector]
    public List<Shape> shapeList = new List<Shape>();

    private bool shapesLoaded = false;

    private const int TYPE_WAY = 0;
    private const int TYPE_DEST = 1;
    private const int TYPE_ARROW = 2;
    private const int TYPE_EDGE = 4;
    private const float MAX_DISTANCE = 1.1f;
    private int id = 0;

    public void CreateWay(Vector3 position)
    {

        Waypoint waypoint = new Waypoint();
        waypoint.id = id;
        waypoint.info.px = position.x;
        waypoint.info.py = position.y;
        waypoint.info.pz = position.z;
        waypoint.info.qx = 0;
        waypoint.info.qy = 0;
        waypoint.info.qz = 0;
        waypoint.info.qw = 0;
        waypoint.info.type = TYPE_WAY.GetHashCode();
        shapeList.Add(waypoint);
        id++;

        GameObject gameObject = ShapeFromInfo(waypoint.info);
        shapeObjList.Add(gameObject);

        Collider[] hitColliders = Physics.OverlapSphere(gameObject.transform.position, MAX_DISTANCE);
        numEdgeList.Add(hitColliders.Length);
        int i = 0;
        while (i < hitColliders.Length)
        {
            if (hitColliders[i].CompareTag("Node"))
            {
                GameObject edge = Instantiate(ShapePrefabs[TYPE_EDGE]);
                edge.GetComponent<LineRenderer>().SetPosition(0, gameObject.transform.position);
                edge.GetComponent<LineRenderer>().SetPosition(1, hitColliders[i].transform.position);
                edgeObjList.Add(edge);
                Debug.Log(position);
                Debug.Log(hitColliders[i].transform.position);
            }
            i++;
        }

    }

    public void CreateDest(GameObject selectedNode, string destName)
    {

        int index = shapeObjList.FindIndex(shape => shape.transform.position == selectedNode.transform.position);
        if (index >= 0)
        {

            Waypoint shape = (Waypoint)shapeList[index];
            Destination dest = new Destination();
            dest.id = shape.id;
            dest.name = destName;
            dest.info.px = shape.info.px;
            dest.info.py = shape.info.py;
            dest.info.pz = shape.info.pz;
            dest.info.qx = 0;
            dest.info.qy = 0;
            dest.info.qz = 0;
            dest.info.qw = 0;
            dest.info.type = TYPE_DEST.GetHashCode();

            Destroy(selectedNode);
            shapeObjList.RemoveAt(index);
            GameObject gameObject = ShapeFromInfo(dest.info);
            gameObject.GetComponent<DiamondBehavior>().Activate(true);
            shapeObjList.Insert(index, gameObject);

            shapeList.RemoveAt(index);
            shapeList.Insert(index, dest);

        }

    }

    public GameObject ShapeFromInfo(ShapeInfo info)
    {
        GameObject shape;
        Vector3 position = new Vector3(info.px, info.py, info.pz);

        if (SceneManager.GetActiveScene().name == "ReadMap" && info.type == TYPE_WAY)
        {
            shape = Instantiate(ShapePrefabs[TYPE_ARROW]);
        }
        else
        {
            shape = Instantiate(ShapePrefabs[info.type]);
        }

        shape.tag = "Node";
        shape.transform.position = position;
        shape.transform.rotation = new Quaternion(info.qx, info.qy, info.qz, info.qw);
        shape.transform.localScale = new Vector3(.3f, .3f, .3f);

        return shape;
    }

    public void UndoShape()
    {
        if (shapeList.Count > 0)
        {
            int lastIndex = shapeList.Count - 1;
            shapeList.RemoveAt(lastIndex);

            Destroy(shapeObjList[lastIndex]);
            shapeObjList.RemoveAt(lastIndex);

            for (int i = numEdgeList[lastIndex]; i > 0; i--)
            {
                Destroy(edgeObjList[edgeObjList.Count]);
                shapeObjList.RemoveAt(lastIndex);
            }

            numEdgeList.RemoveAt(lastIndex);
        }
    }

    public JObject Shapes2JSON()
    {
        ShapeList shapeList = new ShapeList();
        shapeList.shapes = new Shape[this.shapeList.Count];
        for (int i = 0; i < this.shapeList.Count; i++)
        {
            shapeList.shapes[i] = this.shapeList[i];
        }

        return JObject.FromObject(shapeList);
    }

    public void ClearShapes()
    {
        Debug.Log("Clearing Shapes");
        foreach (GameObject obj in shapeObjList)
        {
            Destroy(obj);
        }
        shapeObjList.Clear();
        shapeList.Clear();
    }

    public void LoadShapesJSON(JToken mapMetadata)
    {
        if (!shapesLoaded)
        {
            shapesLoaded = true;
            Debug.Log("LOADING SHAPES...");
            if (mapMetadata is JObject && mapMetadata["shapeList"] is JObject)
            {
                ShapeList shapeList = mapMetadata["shapeList"].ToObject<ShapeList>();
                if (shapeList.shapes == null)
                {
                    Debug.Log("No shapes dropped");
                    return;
                }

                foreach (Shape shape in shapeList.shapes)
                {
                    this.shapeList.Add(shape);
                    GameObject shapeObj = ShapeFromInfo(shape.info);
                    this.shapeObjList.Add(shapeObj);
                }

                if (navController != null)
                {
                    navController.InitializeNavigation();
                }
            }
        }
    }

}
