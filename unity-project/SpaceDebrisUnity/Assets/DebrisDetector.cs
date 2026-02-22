using UnityEngine;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Collections.Generic;
using System.Linq;

public class DebrisDetector : MonoBehaviour
{
    [Header("AI Model")]
    // --- CHANGE: Assign your .onnx model as a TextAsset ---
    public TextAsset modelAsset;
    private InferenceSession session;

    [Header("Live Inference")]
    public Camera aiCamera;
    public RenderTexture aiCameraView;

    [Header("Detection Parameters")]
    [Range(0f, 1f)]
    public float confidenceThreshold = 0.5f;
    [Range(0f, 1f)]
    public float iouThreshold = 0.45f;

    public struct Detection
    {
        public Rect box;
        public float confidence;
        public int classId;
    }

    private List<Detection> finalDetections = new List<Detection>();
    private int modelInputWidth;
    private int modelInputHeight;
    private Texture2D readableTexture; // Used for converting RenderTexture

    void Start()
    {
        // Load the model from the byte data of the TextAsset
        byte[] modelBytes = modelAsset.bytes;
        session = new InferenceSession(modelBytes);

        // Get model input dimensions from its metadata
        var inputMetadata = session.InputMetadata.First().Value;
        modelInputWidth = inputMetadata.Dimensions[3];
        modelInputHeight = inputMetadata.Dimensions[2];

        // Create a helper texture for processing the camera view
        readableTexture = new Texture2D(modelInputWidth, modelInputHeight, TextureFormat.RGB24, false);

        Debug.Log($"ONNX Runtime session created. Input: {modelInputWidth}x{modelInputHeight}");
    }

    void Update()
    {
        // 1. Prepare the input data from the camera view
        var inputData = PreprocessInput(aiCameraView);
        var inputTensor = new DenseTensor<float>(inputData, session.InputMetadata.First().Value.Dimensions);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(session.InputMetadata.Keys.First(), inputTensor)
        };

        // 2. Run the model and get the results
        using (var results = session.Run(inputs))
        {
            // 3. Process the output tensor to find detections
            var outputTensor = results.First().AsTensor<float>();
            ProcessOutput(outputTensor);
        }
    }

    // This helper function converts the camera's RenderTexture into the float array the model needs
    private float[] PreprocessInput(RenderTexture rt)
    {
        RenderTexture.active = rt;
        // Copy the RenderTexture data to a Texture2D that we can read from
        readableTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        readableTexture.Apply();
        RenderTexture.active = null;

        Color32[] pixels = readableTexture.GetPixels32();
        float[] floatArray = new float[pixels.Length * 3];

        // Convert the pixel data into the required planar format (RRR...GGG...BBB...)
        for (int i = 0; i < pixels.Length; i++)
        {
            floatArray[i] = pixels[i].r / 255.0f;
            floatArray[i + pixels.Length] = pixels[i].g / 255.0f;
            floatArray[i + pixels.Length * 2] = pixels[i].b / 255.0f;
        }

        return floatArray;
    }

    // This function decodes the model's output to find bounding boxes
    void ProcessOutput(Tensor<float> output)
    {
        var temporaryDetections = new List<Detection>();

        // YOLOv8 raw output shape is [1, 84, 8400]
        int classCount = output.Dimensions[1] - 4;
        int boxCount = output.Dimensions[2];

        float scaleX = (float)aiCameraView.width / modelInputWidth;
        float scaleY = (float)aiCameraView.height / modelInputHeight;

        for (int i = 0; i < boxCount; i++)
        {
            float maxScore = 0;
            int bestClassId = -1;
            for (int j = 0; j < classCount; j++)
            {
                float currentScore = output[0, 4 + j, i];
                if (currentScore > maxScore)
                {
                    maxScore = currentScore;
                    bestClassId = j;
                }
            }

            if (maxScore > confidenceThreshold)
            {
                float cx = output[0, 0, i];
                float cy = output[0, 1, i];
                float w = output[0, 2, i];
                float h = output[0, 3, i];

                float x = cx - w / 2;
                float y = cy - h / 2;

                temporaryDetections.Add(new Detection
                {
                    box = new Rect(x * scaleX, y * scaleY, w * scaleX, h * scaleY),
                    confidence = maxScore,
                    classId = bestClassId
                });
            }
        }

        finalDetections = NonMaxSuppression(temporaryDetections, iouThreshold);
    }

    // This function filters overlapping boxes to keep only the best one
    private List<Detection> NonMaxSuppression(List<Detection> boxes, float iouThresh)
    {
        if (boxes.Count == 0) return boxes;
        var sortedBoxes = boxes.OrderByDescending(b => b.confidence).ToList();
        var selectedBoxes = new List<Detection>();
        while (sortedBoxes.Count > 0)
        {
            var bestBox = sortedBoxes[0];
            selectedBoxes.Add(bestBox);
            sortedBoxes.RemoveAt(0);
            for (int i = sortedBoxes.Count - 1; i >= 0; i--)
            {
                var boxA = bestBox.box;
                var boxB = sortedBoxes[i].box;
                float interArea = Mathf.Max(0, Mathf.Min(boxA.xMax, boxB.xMax) - Mathf.Max(boxA.xMin, boxB.xMin)) * Mathf.Max(0, Mathf.Min(boxA.yMax, boxB.yMax) - Mathf.Max(boxA.yMin, boxB.yMin));
                float unionArea = boxA.width * boxA.height + boxB.width * boxB.height - interArea;
                float iou = interArea / unionArea;
                if (iou > iouThresh) { sortedBoxes.RemoveAt(i); }
            }
        }
        return selectedBoxes;
    }

    // This function draws the final detection boxes on the screen
    void OnGUI()
    {
        GUI.color = Color.red; // Changed color to make it clear this is a new version
        foreach (var det in finalDetections)
        {
            // Flip the Y-coordinate for Unity's top-left UI system
            Rect flippedRect = new Rect(det.box.x, aiCameraView.height - det.box.y - det.box.height, det.box.width, det.box.height);
            GUI.Box(flippedRect, $"Debris: {det.confidence:0.00}");
        }
    }

    void OnDestroy()
    {
        // Clean up the session when the object is destroyed
        session?.Dispose();
    }
}
