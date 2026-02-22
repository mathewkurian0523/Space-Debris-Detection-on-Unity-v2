# Space Debris Detection – Inference Pipeline

This document explains the complete real-time inference pipeline implemented in Unity using YOLOv8 (ONNX) and Microsoft ONNX Runtime.

---

## 1. System Overview

The detection system follows this pipeline:

Unity Camera → RenderTexture → Preprocessing → ONNX Runtime → YOLOv8 Output Decoding → Non-Max Suppression → GUI Rendering

The model runs in real-time and performs object detection on frames captured from a Unity scene simulating space debris.

---

## 2. Frame Capture

- A Unity Camera renders the scene.
- The camera output is written to a RenderTexture.
- The RenderTexture is read into a CPU-accessible Texture2D using `ReadPixels()`.

This allows pixel data to be extracted for model input.

---

## 3. Preprocessing

The model expects input in the format:

[1, 3, H, W] (NCHW format)

Steps performed:

1. Read RGB pixel data from Texture2D
2. Normalize pixel values (divide by 255.0)
3. Convert to planar format:
   - All R values first
   - Then G values
   - Then B values

This matches the input format required by YOLOv8 exported to ONNX.

---

## 4. ONNX Runtime Inference

The ONNX model is loaded using:

Microsoft.ML.OnnxRuntime

An `InferenceSession` is created from the model bytes.

Each frame:

- A DenseTensor<float> is constructed
- The tensor is passed into `session.Run()`
- Output tensor is retrieved

Inference runs on CPU.

---

## 5. YOLOv8 Output Format

The raw output tensor shape is:

[1, 84, 8400]

Where:

- 4 values → bounding box (cx, cy, w, h)
- Remaining values → class probabilities
- 8400 → total candidate boxes

For each candidate box:

1. Find highest class probability
2. Apply confidence threshold
3. Convert center coordinates to corner format
4. Scale to screen resolution

---

## 6. Non-Max Suppression (NMS)

To remove overlapping boxes:

1. Sort boxes by confidence
2. Select highest-confidence box
3. Remove boxes with IoU > threshold
4. Repeat

IoU (Intersection over Union):

IoU = Intersection Area / Union Area

This ensures only the best bounding box is kept for each detected object.

---

## 7. Rendering

Final bounding boxes are drawn using Unity’s `OnGUI()` system.

The Y-axis is flipped to match Unity’s screen coordinate system.

Each box displays:

Debris: confidence_score

---

## 8. Current Limitations

- `ReadPixels()` introduces CPU-GPU sync overhead.
- Inference runs every frame (can be optimized with coroutine).
- No tracking between frames.
- CPU-only inference.

---

## 9. Possible Improvements

- Use AsyncGPUReadback instead of ReadPixels
- Run inference at fixed interval
- Add debris tracking (Kalman filter)
- Compare ONNX Runtime vs Unity Barracuda
- Move NMS to GPU

---

## Conclusion

This project demonstrates a fully manual real-time ONNX inference pipeline inside Unity, including tensor preprocessing, output decoding, and custom Non-Max Suppression.