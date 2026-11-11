using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Splines;

using UnityEditor;

public class SplineToBoxColliders : MonoBehaviour
{
    [Header("Source")]
    public SplineContainer splineContainer;

    [Header("Generation")]
    [Min(1)]
    public int boxCount = 100;

    public float wallHeight = 2f;
    public float wallThickness = 0.4f;

    [Tooltip("Adds a small overlap between neighboring boxes.")]
    public float overlap = 0.05f;

    [Header("Vertical Position")]
    public float yOffset = 0f;

    [Header("Output")]
    public string parentName = "GeneratedWallColliders";

    public bool isTrigger = false;

    public void Generate()
    {
        if (splineContainer == null)
        {
            Debug.LogError("No SplineContainer assigned.");
            return;
        }

        Clear();

        GameObject parentObject = new GameObject(parentName);
        parentObject.transform.SetParent(transform);
        parentObject.transform.localPosition = Vector3.zero;
        parentObject.transform.localRotation = Quaternion.identity;
        parentObject.transform.localScale = Vector3.one;

        Transform parent = parentObject.transform;

        Spline spline = splineContainer.Spline;

        for (int i = 0; i < boxCount; i++)
        {
            float t1 = i / (float)boxCount;
            float t2 = (i + 1) / (float)boxCount;

            float3 splineP1 = spline.EvaluatePosition(t1);
            float3 splineP2 = spline.EvaluatePosition(t2);

            Vector3 p1 = splineContainer.transform.TransformPoint(
                new Vector3(splineP1.x, splineP1.y, splineP1.z)
            );

            Vector3 p2 = splineContainer.transform.TransformPoint(
                new Vector3(splineP2.x, splineP2.y, splineP2.z)
            );

            Vector3 direction = p2 - p1;

            // For walls we only care about horizontal direction.
            Vector3 horizontalDirection =
                new Vector3(direction.x, 0f, direction.z);

            float length = horizontalDirection.magnitude;

            if (length < 0.001f)
                continue;

            Vector3 center = (p1 + p2) * 0.5f;
            center.y += yOffset;

            GameObject boxObject =
                new GameObject($"WallBox_{i:000}");

            boxObject.transform.SetParent(parent);
            boxObject.transform.position = center;

            // Z axis of the box follows the spline.
            boxObject.transform.rotation =
                Quaternion.LookRotation(
                    horizontalDirection.normalized,
                    Vector3.up
                );

            BoxCollider box =
                boxObject.AddComponent<BoxCollider>();

            box.center = Vector3.zero;

            box.size = new Vector3(
                wallThickness,
                wallHeight,
                length + overlap
            );

            box.isTrigger = isTrigger;

            boxObject.layer = gameObject.layer;
        }

        Debug.Log(
            $"Generated {boxCount} spline-following box colliders."
        );
    }

    public void Clear()
    {
        Transform oldParent = transform.Find(parentName);

        if (oldParent == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(oldParent.gameObject);
        else
            Destroy(oldParent.gameObject);
#else
        Destroy(oldParent.gameObject);
#endif
    }
}

#if UNITY_EDITOR


[CustomEditor(typeof(SplineToBoxColliders))]
public class SplineToBoxCollidersEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);

        SplineToBoxColliders generator =
            (SplineToBoxColliders)target;

        if (GUILayout.Button("Generate Box Colliders"))
        {
            generator.Generate();
        }

        if (GUILayout.Button("Clear Box Colliders"))
        {
            generator.Clear();
        }
    }
}

#endif
