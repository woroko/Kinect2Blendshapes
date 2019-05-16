using UnityEngine;

using System.Collections;

using System.Collections.Generic;

using Windows.Kinect;

using Microsoft.Kinect.Face;

public class HDFaceSourceManager : MonoBehaviour
{

    private KinectSensor sensor = null;

    private BodyFrameSource bodySource = null;

    private BodyFrameReader bodyReader = null;

    private HighDefinitionFaceFrameSource highDefinitionFaceFrameSource = null;

    private HighDefinitionFaceFrameReader highDefinitionFaceFrameReader = null;

    private FaceAlignment currentFaceAlignment = null;

    private FaceModel currentFaceModel = null;

    private Body currentTrackedBody = null;

    private ulong currentTrackingId = 0;

    public bool debugEnabled;
    public SkinnedMeshRenderer actorMesh;
    public List<BlendshapeMapping> blendshapeMapping;
    private Dictionary<string, float> tempAus;
    private string[] actorBlendshapeNames;


    private ulong CurrentTrackingId

    {

        get

        {

            return currentTrackingId;

        }

        set

        {

            currentTrackingId = value;

        }

    }

    private FaceModel CurrentFaceModel

    {

        get

        {

            return currentFaceModel;

        }

        set

        {

            if (currentFaceModel != null)

            {

                currentFaceModel.Dispose();

                currentFaceModel = null;

            }

            currentFaceModel = value;

        }

    }

    private static double VectorLength(CameraSpacePoint point)

    {

        var result = Mathf.Pow(point.X, 2) + Mathf.Pow(point.Y, 2) + Mathf.Pow(point.Z, 2);

        result = Mathf.Sqrt(result);

        return result;

    }

    private static Body FindClosestBody(BodyFrame bodyFrame)

    {

        Body result = null;

        double closestBodyDistance = double.MaxValue;

        Body[] bodies = new Body[bodyFrame.BodyCount];

        bodyFrame.GetAndRefreshBodyData(bodies);

        foreach (var body in bodies)

        {

            if (body.IsTracked)

            {

                var currentLocation = body.Joints[JointType.SpineBase].Position;

                var currentDistance = VectorLength(currentLocation);

                if (result == null || currentDistance < closestBodyDistance)

                {

                    result = body;

                    closestBodyDistance = currentDistance;

                }

            }

        }

        return result;

    }

    private static Body FindBodyWithTrackingId(BodyFrame bodyFrame, ulong trackingId)

    {

        Body result = null;

        Body[] bodies = new Body[bodyFrame.BodyCount];

        bodyFrame.GetAndRefreshBodyData(bodies);

        foreach (var body in bodies)

        {

            if (body.IsTracked)

            {

                if (body.TrackingId == trackingId)

                {

                    result = body;

                    break;

                }

            }

        }

        return result;

    }

    private Mesh theGeometry;

    void Start()
    {  //this like InitializeHDFace()

        theGeometry = new Mesh();

        //SetViewCollectionStatus();

        sensor = KinectSensor.GetDefault();

        bodySource = sensor.BodyFrameSource;

        bodyReader = bodySource.OpenReader();

        bodyReader.FrameArrived += BodyReader_FrameArrived;

        highDefinitionFaceFrameSource = HighDefinitionFaceFrameSource.Create(sensor);

        highDefinitionFaceFrameSource.TrackingIdLost += HdFaceSource_TrackingIdLost;

        highDefinitionFaceFrameReader = highDefinitionFaceFrameSource.OpenReader();

        highDefinitionFaceFrameReader.FrameArrived += HdFaceReader_FrameArrived;

        CurrentFaceModel = FaceModel.Create();

        currentFaceAlignment = FaceAlignment.Create();

        sensor.Open();

        tempAus = new Dictionary<string, float>();
        actorBlendshapeNames = getBlendShapeNames(actorMesh);

    }

    private void OnApplicationQuit()
    {
        sensor.Close();

        bodyReader.FrameArrived -= BodyReader_FrameArrived;
        highDefinitionFaceFrameSource.TrackingIdLost -= HdFaceSource_TrackingIdLost;
        highDefinitionFaceFrameReader.FrameArrived -= HdFaceReader_FrameArrived;
    }

    private void InitializeMesh(MeshFilter faceMeshFilter, Mesh faceMesh)

    {

        faceMeshFilter.mesh.Clear();

        var vertices = this.currentFaceModel.CalculateVerticesForAlignment(this.currentFaceAlignment);

        Debug.Log("FaceModel.TriangleCount " + FaceModel.TriangleCount);

        var triangleIndices = FaceModel.TriangleIndices;

        var indices = new int[triangleIndices.Count];

        Vector3[] newVerts = new Vector3[vertices.Count];

        Vector2[] newUV = new Vector2[vertices.Count];

        Vector3[] newNormals = new Vector3[vertices.Count];

        for (int i = 0; i < vertices.Count; ++i)

        {

            newVerts[i] = new Vector3(vertices[i].X, vertices[i].Y, vertices[i].Z);

            newNormals[i] = -Vector3.forward;

        }

        faceMesh.vertices = newVerts;

        faceMesh.uv = newUV;

        for (int i = 0; i < triangleIndices.Count; i += 3)

        {

            uint index01 = triangleIndices[i];

            uint index02 = triangleIndices[i + 1];

            uint index03 = triangleIndices[i + 2];

            indices[i] = (int)index01;

            indices[i + 1] = (int)index02;

            indices[i + 2] = (int)index03;

            if (indices[i] >= triangleIndices.Count || indices[i] < 0)

            {

                Debug.Log("Out of Range index i " + i + " = " + indices[i]);

            }

            if (indices[i + 1] >= triangleIndices.Count || indices[i + 1] < 0)

            {

                Debug.Log("Out of Range index i+1 " + (i + 1) + " = " + indices[i + 1]);

            }

            if (indices[i + 2] >= triangleIndices.Count || indices[i + 2] < 0)

            {

                Debug.Log("Out of Range index i+2 " + (i + 2) + " = " + indices[i + 2]);

            }

        }

        faceMesh.triangles = indices;

        faceMesh.RecalculateNormals();

        faceMeshFilter.mesh = faceMesh;

    }

    private void UpdateMesh()

    {

        var vertices = this.currentFaceModel.CalculateVerticesForAlignment(this.currentFaceAlignment);

        //currentFaceModel.FaceShapeDeformations[FaceShapeDeformations.Eyes00]

        Vector3[] newVerts = new Vector3[vertices.Count];

        for (int i = 0; i < vertices.Count; ++i)

        {

            newVerts[i] = new Vector3(vertices[i].X, vertices[i].Y, vertices[i].Z);

        }

        this.theGeometry.vertices = newVerts;

    }

    void Update()
    {

        UpdateMesh();

    }

    private void BodyReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)

    {

        var frameReference = e.FrameReference;

        using (var frame = frameReference.AcquireFrame())

        {

            if (frame == null)

            {

                // We might miss the chance to acquire the frame, it will be null if it's missed

                return;

            }

            if (currentTrackedBody != null)

            {

                currentTrackedBody = FindBodyWithTrackingId(frame, CurrentTrackingId);

                if (currentTrackedBody != null)

                {

                    return;

                }

            }

            Body selectedBody = FindClosestBody(frame);

            if (selectedBody == null)

            {

                return;

            }

            currentTrackedBody = selectedBody;

            CurrentTrackingId = selectedBody.TrackingId;

            highDefinitionFaceFrameSource.TrackingId = CurrentTrackingId;

        }

    }

    private void HdFaceSource_TrackingIdLost(object sender, TrackingIdLostEventArgs e)

    {

        var lostTrackingID = e.TrackingId;

        if (CurrentTrackingId == lostTrackingID)

        {

            CurrentTrackingId = 0;

            currentTrackedBody = null;

            highDefinitionFaceFrameSource.TrackingId = 0;

        }

    }

    private string[] getBlendShapeNames(SkinnedMeshRenderer head)
    {
        Mesh m = head.sharedMesh;
        string[] arr;
        arr = new string[m.blendShapeCount];
        for (int i = 0; i < m.blendShapeCount; i++)
        {
            string s = m.GetBlendShapeName(i);
            print("Blend Shape: " + i + " " + s);
            arr[i] = s;
        }
        return arr;
    }

    private void UpdateActormeshBlendshapes(Dictionary<string, float> aus)
    {
        foreach (string key in aus.Keys)
        {
            foreach (BlendshapeMapping map in blendshapeMapping)
            {
                if (map.from.ToLower() == key.ToLower())
                {
                    float sourceVal = aus[key] + map.offset;
                    foreach (StringFloat sf in map.to)
                    {
                        int destIndex = -1;
                        for (int i=0; i< actorBlendshapeNames.Length; i++)
                        {
                            if (actorBlendshapeNames[i].ToLower() == sf.shapeName.ToLower())
                            {
                                destIndex = i;
                                break;
                            }
                        }
                        if (destIndex >= 0)
                        {
                            float destVal = sourceVal * sf.multiplier * 100f; // map from 0-1 to 0-100
                            if (destVal < 0f)
                                destVal = 0f;
                            actorMesh.SetBlendShapeWeight(destIndex, destVal);
                        }
                    }
                }
            }
        }
    }

    private void HdFaceReader_FrameArrived(object sender, HighDefinitionFaceFrameArrivedEventArgs e)

    {

        using (var frame = e.FrameReference.AcquireFrame())

        {

            // We might miss the chance to acquire the frame; it will be null if it's missed.

            // Also ignore this frame if face tracking failed.

            if (frame == null || !frame.IsFaceTracked)

            {

                return;

            }

            frame.GetAndRefreshFaceAlignmentResult(currentFaceAlignment);
            if (debugEnabled)
            {
                string aus = "";
                foreach (FaceShapeAnimations key in currentFaceAlignment.AnimationUnits.Keys)
                {
                    aus += key.ToString();
                    aus += currentFaceAlignment.AnimationUnits[key].ToString("0.00") + "; ";
                    Debug.Log(aus);
                }
            }

            if (actorMesh != null)
            {
                tempAus.Clear();
                foreach (FaceShapeAnimations key in currentFaceAlignment.AnimationUnits.Keys)
                {
                    tempAus.Add(key.ToString(), currentFaceAlignment.AnimationUnits[key]);
                }
                UpdateActormeshBlendshapes(tempAus);
            }
        }

    }

}