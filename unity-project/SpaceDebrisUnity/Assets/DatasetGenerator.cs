using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class DatasetGenerator : MonoBehaviour
{
    [Header("Capture Settings")]
    public Camera captureCamera;
    public List<GameObject> debrisObjects;
    public int imagesToGenerate = 1000;
    public int classId = 0;

    [Header("Randomization Settings")]
    public Transform earthTransform;
    public float satelliteOrbitRadius = 8.0f; // Controls the camera's orbit
    // spawnCenter & spawnVolume are now relative to the camera
    public Vector3 spawnCenter = new Vector3(0, 0, 15); // How far IN FRONT of the camera the debris cluster is
    public Vector3 spawnVolume = new Vector3(10, 10, 10); // Size of the debris cluster

    [Header("Output Settings")]
    public string savePath = "Dataset";
    private int imageWidth;
    private int imageHeight;

    void Start()
    {
        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }
        imageWidth = captureCamera.targetTexture.width;
        imageHeight = captureCamera.targetTexture.height;
        StartCoroutine(GenerateDatasetCoroutine());
    }

    private IEnumerator GenerateDatasetCoroutine()
    {
        Debug.Log("Starting dataset generation...");
        for (int i = 0; i < imagesToGenerate; i++)
        {
            RandomizeScene();
            yield return new WaitForEndOfFrame();
            string imageName = $"image_{i:D4}";
            CaptureImage(imageName);
            CalculateAndSaveLabels(imageName);
            if ((i + 1) % 100 == 0)
            {
                Debug.Log($"Generated {i + 1} / {imagesToGenerate} images...");
            }
        }
        Debug.Log("Dataset generation complete!");
    }

    void RandomizeScene()
    {
        // --- Place the Camera (Satellite) in Orbit ---
        Vector3 earthCenter = earthTransform.position;

        // 1. Get a random direction from Earth for the satellite's position
        Vector3 satelliteDirection = Random.onUnitSphere;
        Vector3 satellitePosition = earthCenter + satelliteDirection * satelliteOrbitRadius;
        captureCamera.transform.position = satellitePosition;

        // 2. Point the camera towards the horizon, not straight down
        Vector3 forwardDirection = Vector3.Cross(satelliteDirection, Vector3.up).normalized;
        if (forwardDirection == Vector3.zero) forwardDirection = Vector3.forward; // Edge case for poles
        captureCamera.transform.rotation = Quaternion.LookRotation(forwardDirection, satelliteDirection);


        // --- Place Debris in Front of the Camera ---
        Vector3 clusterCenter = captureCamera.transform.position + captureCamera.transform.forward * spawnCenter.z;

        foreach (var debris in debrisObjects)
        {
            Vector3 randomOffset =
                (captureCamera.transform.right * Random.Range(-spawnVolume.x / 2, spawnVolume.x / 2)) +
                (captureCamera.transform.up * Random.Range(-spawnVolume.y / 2, spawnVolume.y / 2)) +
                (captureCamera.transform.forward * Random.Range(-spawnVolume.z / 2, spawnVolume.z / 2));

            debris.transform.position = clusterCenter + randomOffset;
            debris.transform.rotation = Random.rotation;
        }
    }

    // (CaptureImage and CalculateAndSaveLabels methods remain unchanged)

    void CaptureImage(string imageName)
    {
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = captureCamera.targetTexture;
        captureCamera.Render();
        Texture2D image = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
        image.Apply();
        RenderTexture.active = currentRT;
        byte[] bytes = image.EncodeToPNG();
        Destroy(image);
        File.WriteAllBytes(Path.Combine(savePath, imageName + ".png"), bytes);
    }

    void CalculateAndSaveLabels(string imageName)
    {
        string labelContent = "";
        foreach (var debris in debrisObjects)
        {
            Renderer renderer = debris.GetComponent<Renderer>();
            if (renderer == null) continue;
            Bounds bounds = renderer.bounds;
            Vector3[] corners = new Vector3[8];
            corners[0] = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);
            corners[1] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
            corners[2] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
            corners[3] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
            corners[4] = new Vector3(bounds.max.x, bounds.max.y, bounds.max.z);
            corners[5] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z);
            corners[6] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);
            corners[7] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            bool isVisible = false;
            for (int j = 0; j < 8; j++)
            {
                Vector3 screenPoint = captureCamera.WorldToScreenPoint(corners[j]);
                if (screenPoint.z > 0)
                {
                    isVisible = true;
                    min.x = Mathf.Min(min.x, screenPoint.x);
                    min.y = Mathf.Min(min.y, screenPoint.y);
                    max.x = Mathf.Max(max.x, screenPoint.x);
                    max.y = Mathf.Max(max.y, screenPoint.y);
                }
            }
            if (isVisible && max.x > 0 && min.x < imageWidth && max.y > 0 && min.y < imageHeight)
            {
                min.x = Mathf.Clamp(min.x, 0, imageWidth);
                max.x = Mathf.Clamp(max.x, 0, imageWidth);
                min.y = Mathf.Clamp(min.y, 0, imageHeight);
                max.y = Mathf.Clamp(max.y, 0, imageHeight);
                float boxWidth = max.x - min.x;
                float boxHeight = max.y - min.y;
                float centerX = min.x + boxWidth / 2;
                float centerY = min.y + boxHeight / 2;
                float normCenterX = centerX / imageWidth;
                float normCenterY = centerY / imageHeight;
                float normWidth = boxWidth / imageWidth;
                float normHeight = boxHeight / imageHeight;
                labelContent += $"{classId} {normCenterX} {normCenterY} {normWidth} {normHeight}\n";
            }
        }
        if (!string.IsNullOrEmpty(labelContent))
        {
            File.WriteAllText(Path.Combine(savePath, imageName + ".txt"), labelContent);
        }
    }
}