using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.iOS;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;

public class NodeShapeInfo
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

public class NodeShape
{
    public int id;
    public NodeShapeInfo info;
    public string name;
}

public class NodeShapeList
{
    public NodeShape[] shapes;
}

public class CustomShapeManager : MonoBehaviour
{
    public List<GameObject> shapePrefabs = new List<GameObject>();

    [HideInInspector]
    public List<GameObject> shapeObjList = new List<GameObject>();
    [HideInInspector]
    public List<GameObject> edgeObjList = new List<GameObject>();
    [HideInInspector]
    public List<int> numEdgeList = new List<int>();
    [HideInInspector]
    public List<NodeShape> shapeList = new List<NodeShape>();

    private bool shapesLoaded = false;

    [HideInInspector] public int numDest = 0;

    private const int TYPE_WAY = 0;
    private const int TYPE_DEST = 1;
    private const int TYPE_EDGE = 2;
    private const float MAX_DISTANCE = 1.1f;
    private int id = 0;

    public void CreateWay(Vector3 position)
    {
        NodeShapeInfo info = new NodeShapeInfo();
        info.px = position.x;
        info.py = position.y;
        info.pz = position.z;
        info.qx = 0;
        info.qy = 0;
        info.qz = 0;
        info.qw = 0;
        info.type = TYPE_WAY.GetHashCode();
        NodeShape waypoint = new NodeShape();
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
            NodeShape shape = shapeList[index];
            NodeShapeInfo info = new NodeShapeInfo();
            info.px = shape.info.px;
            info.py = shape.info.py;
            info.pz = shape.info.pz;
            info.qx = 0;
            info.qy = 0;
            info.qz = 0;
            info.qw = 0;
            info.type = TYPE_DEST.GetHashCode();
            NodeShape dest = new NodeShape();
            dest.id = shape.id;
            dest.name = destName;
            dest.info = info;

            Destroy(selectedNode);
            shapeObjList.RemoveAt(index);
            GameObject gameObject = ShapeFromInfo(dest.info);
            gameObject.GetComponent<TextMesh>().text = dest.name;
            shapeObjList.Insert(index, gameObject);

            shapeList.RemoveAt(index);
            shapeList.Insert(index, dest);
            Debug.Log((shapeList[index]).name);

        }

    }

    public GameObject ShapeFromInfo(NodeShapeInfo info)
    {
        Vector3 position = new Vector3(info.px, info.py, info.pz);
        int type = info.type;
        GameObject shape = Instantiate(shapePrefabs[type]);
        shape.transform.position = position;
        shape.transform.rotation = new Quaternion(info.qx, info.qy, info.qz, info.qw);
        shape.transform.localScale = new Vector3(.3f, .3f, .3f);

        return shape;
    }

    public GameObject ShapeFromJSON(int id, NodeShapeInfo info)
    {
        Vector3 position = new Vector3(info.px, info.py, info.pz);
        int type = info.type;
        GameObject shape = Instantiate(shapePrefabs[type]);

        if (shape.GetComponent<Node> () != null) 
        {
            shape.GetComponent<Node> ().id = id;
			shape.GetComponent<Node> ().pos = position;
		}

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
        NodeShapeList shapeList = new NodeShapeList();
        shapeList.shapes = new NodeShape[this.shapeList.Count];
        for (int i = 0; i < this.shapeList.Count; i++)
        {
            Debug.Log(this.shapeList[i].info.type);
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
            Debug.Log("Loading shapes");
            Debug.Log(mapMetadata is JObject);
            Debug.Log(mapMetadata["data"] is JObject);
            if (mapMetadata is JObject && mapMetadata["data"] is JObject)
            {
                NodeShapeList shapeList = mapMetadata["data"].ToObject<NodeShapeList>();
                if (shapeList.shapes == null)
                {
                    Debug.Log("No shapes dropped");
                    return;
                }

                foreach (NodeShape shape in shapeList.shapes)
                {
                    Debug.Log(shape.info.type);
                    Debug.Log(new Vector3(shape.info.px, shape.info.py, shape.info.pz));
                    this.shapeList.Add(shape);
                    
                    GameObject shapeObj = ShapeFromJSON(shape.id, shape.info);
                    if (shape.info.type == TYPE_DEST.GetHashCode())
                    {
                        numDest += 1;
                        shapeObj.GetComponent<TextMesh>().text = shape.name;
                    }
                    this.shapeObjList.Add(shapeObj);
                }
            }
        }
    }

}
