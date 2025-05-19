#define GPU

using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VertexPainter))]
public class VertexPainterEditor : Editor
{

    VertexPainter t;
    ComputeShader paintShader;

    bool modified = false;

    string meshPath = null;

    bool red = true;
    bool green = true;
    bool blue = true;
    bool alpha = true;

    private void OnEnable()
    {
        t = target as VertexPainter;
        if (t.meshFilter == null) t.meshFilter = t.GetComponent<MeshFilter>();//make sure we have a meshcomponent target
        if (t.meshFilter == null) //Destroy self if no mesh component is on gameobject.
        {
            if (Application.isPlaying) Destroy(t);
            else
            {
                Debug.LogError("Game Object does not contain a MeshFilter component, Vertex painter requires to already have a mesh component attached.", t.gameObject);
                DestroyImmediate(t);
            }
        }
        const string shaderPath = "Packages/com.doublegaugegames.vertexpainter/Assets/FastVertexPainting.compute";
        paintShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(shaderPath);
        if (meshPath == null) meshPath = GetMeshPath();
    }

    private string GetMeshPath()
    {
        string path = AssetDatabase.GetAssetPath(t.meshFilter.sharedMesh);
        path = path[0..(path.LastIndexOf('/') + 1)];
        return path + t.gameObject.name + ".asset";
    }

    //This is an internal unity method for raycasting which isnt naturally exposed.
    //This is also the pramary reason why other tools have been depreciated.
    private static readonly MethodInfo intersectRayMeshMethod = typeof(HandleUtility).GetMethod("IntersectRayMesh", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

    public override void OnInspectorGUI()
    {
        if (!CheckIsMeshEditable())
        {
            EditorGUILayout.HelpBox("Mesh assets are not editable, if you want to keep the changes make a copy", MessageType.Warning);
        }
        t.paint = GUILayout.Toggle(t.paint, EditorGUIUtility.TrTextContent("Paint", null), "Button");
        GUILayout.BeginHorizontal();
        red = GUILayout.Toggle(red, "R");
        green = GUILayout.Toggle(green, "G");
        blue = GUILayout.Toggle(blue, "B");
        alpha = GUILayout.Toggle(alpha, "A");
        GUILayout.EndHorizontal();
        t.paintColor = EditorGUILayout.ColorField("Color", t.paintColor);
        t.paintRadius = EditorGUILayout.FloatField("Paint Radius", t.paintRadius);

        if (GUILayout.Button("Create Mesh copy"))
        {
            t.meshFilter.sharedMesh = Instantiate(t.meshFilter.sharedMesh);
        }
        EditorGUILayout.HelpBox("this path expects a path following the following: \nAssets/folders/to/your/destination/nameOfAsset.asset", MessageType.Info);
        meshPath = GUILayout.TextField(meshPath);

        if (GUILayout.Button(new GUIContent("Save Mesh at path", "Saves a new asset with your mesh at the indicated path")))
        {
            AssetDatabase.CreateAsset(t.meshFilter.sharedMesh, meshPath);
        }
    }
        
    bool CheckIsMeshEditable()
    {
        string path = AssetDatabase.GetAssetPath(t.meshFilter.sharedMesh);
        var len = path.Length;
        if (len == 0) return true;
        const string assetExtension = ".asset";
        if (len <= assetExtension.Length) return false;
        if (path[(len - assetExtension.Length)..(len)] != assetExtension) return false;
        return true;
    }

    private void OnSceneGUI()
    {
        //only accept valid events
        switch (Event.current.type)
        {
            case EventType.Layout:
            case EventType.Repaint:
            case EventType.MouseDown:
            case EventType.MouseUp:
            case EventType.MouseDrag:
                break;
            default: return;
        }


        if (!t.paint) return; //return if we have not painting enabled
        if (Event.current.alt) return; //return if alt is pressedto allow camera rotation

        if (Event.current.shift)// if shift is pressed resize brush
        {
            if ((Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag) && Event.current.button == 0)//Duplicated since we can resize without beeing on themesh
            {
                var delta = Event.current.delta;
                t.paintRadius += (delta.x - delta.y)*t.paintRadius*0.03f;
                t.paintRadius = Mathf.Max(t.paintRadius, 0.001f);
                Event.current.Use(); // if we resize we do not paint
            }
        }

        //Get the hit point
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition); //Get the ray from mouse
        object[] rayMeshParameters = new object[] { ray, t.meshFilter.sharedMesh, t.transform.localToWorldMatrix, null }; //get the raycast parameters
        if ((bool)intersectRayMeshMethod.Invoke(null, rayMeshParameters)) // check if mouse is on top of the mesh
        {
            RaycastHit hit = (RaycastHit)rayMeshParameters[3];

            //Draw the gizmo
            Handles.DrawWireDisc(hit.point, hit.normal, t.paintRadius);


            if (!((Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag) && Event.current.button == 0)) return;//return if we are not actively painting


            //paint the vertex

            Color[] vertColors = t.meshFilter.sharedMesh.colors;//clone the current vertex colors
            //all of this might not work if mesh does not have previous vetex colors so we fix it
            if (vertColors.Length != t.meshFilter.sharedMesh.vertexCount)
            {
                var newColors = new Color[t.meshFilter.sharedMesh.vertexCount];
                if (vertColors != null)
                {
                    Debug.Log("Vertex Colors size was unfit. Resizing.");
                    for (int i = 0; i < vertColors.Length && i < newColors.Length; i++)
                    {
                        newColors[i] = vertColors[i];
                    }
                }
                vertColors = newColors;
            }
            //for each vertex we check distance to see if it is between paint distance
#if !GPU
            //Slow CPU version
            float sqrPaintRadius = t.paintRadius * t.paintRadius;
            for (int i = 0; i < t.meshFilter.sharedMesh.vertexCount; i++)
            {
                Vector3 worldVertex = t.transform.localToWorldMatrix.MultiplyPoint3x4(t.meshFilter.sharedMesh.vertices[i]);//convert vertex to world space
                var dist = Vector3.SqrMagnitude(worldVertex - hit.point);
                if (dist < sqrPaintRadius)//Check if vertex is between paint distance (sqrt=faster)
                {
                    if(red) vertColors[i].r = t.paintColor.r;
                    if(green) vertColors[i].g = t.paintColor.g;
                    if(blue) vertColors[i].b = t.paintColor.b;
                    if(alpha) vertColors[i].a = t.paintColor.a;
                    //vertColors[i] = t.paintColor;
                }
            }
#else
            //Fast GPU version
            {
                var mesh = t.meshFilter.sharedMesh;
                var vertices = mesh.vertices;
                var matrix = t.transform.localToWorldMatrix;

                ComputeBuffer vertexPosBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
                ComputeBuffer vertColorBuffer = new ComputeBuffer(vertColors.Length, sizeof(float) * 4);

                vertexPosBuffer.SetData(vertices);
                vertColorBuffer.SetData(vertColors);

                int kernel = paintShader.FindKernel("PaintVertices");

                paintShader.SetBool("_red", red);
                paintShader.SetBool("_green", green);
                paintShader.SetBool("_blue", blue);
                paintShader.SetBool("_alpha", alpha);

                paintShader.SetBuffer(kernel, "_VertexColors", vertColorBuffer);
                paintShader.SetBuffer(kernel, "_Vertices", vertexPosBuffer);

                paintShader.SetVector("_HitPoint", hit.point);
                paintShader.SetFloat("_PaintRadius", t.paintRadius);
                paintShader.SetVector("_PaintColor", t.paintColor);
                paintShader.SetMatrix("_LocalToWorldVertex", matrix);

                int threadGroups = Mathf.CeilToInt(vertices.Length / 64f);
                paintShader.Dispatch(kernel, threadGroups, 1, 1);

                vertColorBuffer.GetData(vertColors);

                vertexPosBuffer.Dispose();
                vertColorBuffer.Dispose();
            }
            Debug.Log("Painted");
#endif
            t.meshFilter.sharedMesh.colors = (vertColors);//set the colors back

            Event.current.Use(); //use the event to not misuse
        }
    }

}

