using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.iOS;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;

public class ShapeInfo
{
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

public class ShapeList
{
    public Shape[] shapes;
}

public class CustomShapeManager : MonoBehaviour
{
    public NavController navController;

    public List<GameObject> shapePrefabs = new List<GameObject>();

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
    private const int NODE_WAY = 2;
    private const int NODE_DEST = 3;
    private const int TYPE_EDGE = 4;
    private const float MAX_DISTANCE = 1.1f;
    private int id = 0;

    public void CreateWay(Vector3 position)
    {
        ShapeInfo info = new ShapeInfo();
        info.px = position.x;
        info.py = position.y;
        info.pz = position.z;
        info.qx = 0;
        info.qy = 0;
        info.qz = 0;
        info.qw = 0;
        info.type = TYPE_WAY.GetHashCode();
        Waypoint waypoint = new Waypoint();
        waypoint.id = id;
        waypoint.info = info;
        shapeList.Add(waypoint);
        id++;

        Debug.Log("ShapeFromInfo");
        GameObject gameObject = ShapeFromInfo(waypoint.info);
        shapeObjList.Add(gameObject);
        Debug.Log("Add Shape");

        Collider[] hitColliders = Physics.OverlapSphere(position, MAX_DISTANCE);
        numEdgeList.Add(hitColliders.Length);
        Debug.Log(hitColliders.Length);
        int i = 0;
        while (i < hitColliders.Length)
        {
            if (hitColliders[i].CompareTag("Node"))
            {
                GameObject edge = Instantiate(shapePrefabs[TYPE_EDGE]);
                edge.GetComponent<LineRenderer>().SetPosition(0, gameObject.transform.position);
                edge.GetComponent<LineRenderer>().SetPosition(1, hitColliders[i].transform.position);
                edgeObjList.Add(edge);
                Debug.Log(gameObject.transform.position);
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
            ShapeInfo info = new ShapeInfo();
            info.px = shape.info.px;
            info.py = shape.info.py;
            info.pz = shape.info.pz;
            info.qx = 0;
            info.qy = 0;
            info.qz = 0;
            info.qw = 0;
            info.type = TYPE_DEST.GetHashCode();
            Destination dest = new Destination();
            dest.id = shape.id;
            dest.name = destName;
            dest.info = info;

            Destroy(selectedNode);
            shapeObjList.RemoveAt(index);
            GameObject gameObject = ShapeFromInfo(dest.info);
            shapeObjList.Insert(index, gameObject);

            shapeList.RemoveAt(index);
            shapeList.Insert(index, dest);
            Debug.Log(((Destination)shapeList[index]).name);

        }

    }

    public GameObject ShapeFromInfo(ShapeInfo info)
    {
        GameObject shape;
        int type = TYPE_WAY;
        Vector3 position = new Vector3(info.px, info.py, info.pz);

        if (SceneManager.GetActiveScene().name == "ReadMap")
        {
            if (info.type == TYPE_WAY.GetHashCode())
            {
                type = NODE_WAY;

            }
            else if (info.type == TYPE_DEST.GetHashCode())
            {
                type = NODE_DEST;
            }
        }
        else
        {
            type = info.type;
        }
        shape = Instantiate(shapePrefabs[type]);
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
                Destroy(edgeObjList[edgeObjList.Count - 1]);
                edgeObjList.RemoveAt(lastIndex);
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
            Debug.Log(this.shapeList[i].info.type);
            if (this.shapeList[i].info.type == TYPE_WAY.GetHashCode())
            {
                shapeList.shapes[i] = (Waypoint)this.shapeList[i];
            }
            else if (this.shapeList[i].info.type == TYPE_DEST.GetHashCode())
            {
                shapeList.shapes[i] = (Destination)this.shapeList[i];
            }
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
            if (mapMetadata is JObject && mapMetadata["shapes"] is JObject)
            {
                ShapeList shapeList = mapMetadata["shapes"].ToObject<ShapeList>();
                if (shapeList.shapes == null)
                {
                    Debug.Log("No shapes dropped");
                    return;
                }

                foreach (Shape shape in shapeList.shapes)
                {
                    Debug.Log(shape.info.type);
                    Debug.Log(new Vector3(shape.info.px, shape.info.py, shape.info.pz));
                    if (shape.info.type == TYPE_WAY.GetHashCode())
                    {
                        Debug.Log(((Destination)shape).name);
                        this.shapeList.Add((Destination)shape);
                    }
                    else if (shape.info.type == TYPE_DEST.GetHashCode())
                    {
                        this.shapeList.Add((Waypoint)shape);
                    }
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
