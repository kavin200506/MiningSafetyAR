---
library_name: pytorch
license: apache-2.0
tags:
- real_time
- bu_auto
- android
pipeline_tag: object-detection

---

![](https://qaihub-public-assets.s3.us-west-2.amazonaws.com/qai-hub-models/models/mobile_facenet/web-assets/model_demo.png)

# MobileFaceNet: Optimized for Qualcomm Devices

MobileFaceNet is an efficient CNN that maps a 112x112 face image to a compact 128-dimensional embedding. Two embeddings are compared via cosine similarity to determine whether they belong to the same person, achieving 99.48% accuracy on the LFW benchmark. The model uses depthwise-separable convolutions and inverted residual blocks (MobileNetV2-style) to stay under 1M parameters, making it well-suited for real-time face verification on mobile and edge devices. Trained with ArcFace loss on MS-Celeb-1M.

This is based on the implementation of MobileFaceNet found [here](https://github.com/foamliu/MobileFaceNet).
This repository contains pre-exported model files optimized for Qualcomm® devices. You can use the [Qualcomm® AI Hub Models](https://github.com/qualcomm/ai-hub-models/blob/v0.62.1/src/qai_hub_models/models/mobile_facenet) library to export with custom configurations. More details on model performance across various devices, can be found [here](#performance-summary).

Qualcomm AI Hub Models uses [Qualcomm AI Hub Workbench](https://workbench.aihub.qualcomm.com) to compile, profile, and evaluate this model. [Sign up](https://myaccount.qualcomm.com/signup) to run these models on a hosted Qualcomm® device.

## Getting Started
There are two ways to deploy this model on your device:

### Option 1: Download Pre-Exported Models

Below are pre-exported model assets ready for deployment.

| Runtime | Precision | Chipset | SDK Versions | Download |
|---|---|---|---|---|
| ONNX | float | Universal | QAIRT 2.45, ONNX Runtime 1.27.1 | [Download](https://qaihub-public-assets.s3.us-west-2.amazonaws.com/qai-hub-models/models/mobile_facenet/releases/v0.62.1/mobile_facenet-onnx-float.zip)
| ONNX | w8a16 | Universal | QAIRT 2.45, ONNX Runtime 1.27.1 | [Download](https://qaihub-public-assets.s3.us-west-2.amazonaws.com/qai-hub-models/models/mobile_facenet/releases/v0.62.1/mobile_facenet-onnx-w8a16.zip)
| QNN_DLC | float | Universal | QAIRT 2.45 | [Download](https://qaihub-public-assets.s3.us-west-2.amazonaws.com/qai-hub-models/models/mobile_facenet/releases/v0.62.1/mobile_facenet-qnn_dlc-float.zip)
| QNN_DLC | w8a16 | Universal | QAIRT 2.45 | [Download](https://qaihub-public-assets.s3.us-west-2.amazonaws.com/qai-hub-models/models/mobile_facenet/releases/v0.62.1/mobile_facenet-qnn_dlc-w8a16.zip)
| TFLITE | float | Universal | QAIRT 2.45 | [Download](https://qaihub-public-assets.s3.us-west-2.amazonaws.com/qai-hub-models/models/mobile_facenet/releases/v0.62.1/mobile_facenet-tflite-float.zip)

For more device-specific assets and performance metrics, visit **[MobileFaceNet on Qualcomm® AI Hub](https://aihub.qualcomm.com/models/mobile_facenet)**.


### Option 2: Export with Custom Configurations

Use the [Qualcomm® AI Hub Models](https://github.com/qualcomm/ai-hub-models/blob/v0.62.1/src/qai_hub_models/models/mobile_facenet) Python library to compile and export the model with your own:
- Custom weights (e.g., fine-tuned checkpoints)
- Custom input shapes
- Target device and runtime configurations

This option is ideal if you need to customize the model beyond the default configuration provided here.

See our repository for [MobileFaceNet on GitHub](https://github.com/qualcomm/ai-hub-models/blob/v0.62.1/src/qai_hub_models/models/mobile_facenet) for usage instructions.

## Model Details

**Model Type:** Model_use_case.object_detection

**Model Stats:**
- Embedding dimension: 128
- Input resolution: 112x112
- Model checkpoint: mobilefacenet.pt
- Model size (float): 4MB
- Number of parameters: 1M

## Performance Summary
| Model | Runtime | Precision | Chipset | Inference Time (ms) | Peak Memory Range (MB) | Primary Compute Unit
|---|---|---|---|---|---|---
| MobileFaceNet | ONNX | float | Snapdragon® X2 Elite | 0.584 ms | 1 - 1 MB | NPU
| MobileFaceNet | ONNX | float | Snapdragon® X Elite | 1.048 ms | 0 - 0 MB | NPU
| MobileFaceNet | ONNX | float | Snapdragon® 8 Gen 3 Mobile | 0.705 ms | 0 - 51 MB | NPU
| MobileFaceNet | ONNX | float | Snapdragon® 8 Gen 1 Mobile | 1.514 ms | 0 - 54 MB | NPU
| MobileFaceNet | ONNX | float | Qualcomm® Dragonwing™ IQ-8275 | 1.472 ms | 0 - 4 MB | NPU
| MobileFaceNet | ONNX | float | Qualcomm® Dragonwing™ QCS8550 (Proxy) | 1.002 ms | 0 - 5 MB | NPU
| MobileFaceNet | ONNX | float | Qualcomm® QCS8450 | 1.514 ms | 0 - 54 MB | NPU
| MobileFaceNet | ONNX | float | Qualcomm® Dragonwing™ IQ-9075 | 1.38 ms | 0 - 3 MB | NPU
| MobileFaceNet | ONNX | float | Qualcomm® Dragonwing™ IQ-X7181 | 1.048 ms | 0 - 0 MB | NPU
| MobileFaceNet | ONNX | float | Qualcomm® Dragonwing™ Q-8750 | 0.564 ms | 0 - 31 MB | NPU
| MobileFaceNet | ONNX | float | Snapdragon® 8 Elite Mobile | 0.564 ms | 0 - 31 MB | NPU
| MobileFaceNet | ONNX | float | Snapdragon® 8 Elite Gen 5 Mobile | 0.505 ms | 0 - 32 MB | NPU
| MobileFaceNet | ONNX | w8a16 | Snapdragon® X2 Elite | 0.443 ms | 1 - 1 MB | NPU
| MobileFaceNet | ONNX | w8a16 | Snapdragon® X Elite | 0.823 ms | 0 - 0 MB | NPU
| MobileFaceNet | ONNX | w8a16 | Snapdragon® 8 Gen 3 Mobile | 0.577 ms | 0 - 53 MB | NPU
| MobileFaceNet | ONNX | w8a16 | Snapdragon® 8 Gen 1 Mobile | 1.092 ms | 0 - 58 MB | NPU
| MobileFaceNet | ONNX | w8a16 | Qualcomm® Dragonwing™ QCS6490 | 2.966 ms | 0 - 3 MB | NPU
| MobileFaceNet | ONNX | w8a16 | Qualcomm® Dragonwing™ IQ-8275 | 0.874 ms | 0 - 4 MB | NPU
| MobileFaceNet | ONNX | w8a16 | Qualcomm® Dragonwing™ QCS8550 (Proxy) | 0.799 ms | 0 - 4 MB | NPU
| MobileFaceNet | ONNX | w8a16 | Qualcomm® QCS8450 | 1.092 ms | 0 - 58 MB | NPU
| MobileFaceNet | ONNX | w8a16 | Qualcomm® Dragonwing™ IQ-9075 | 0.911 ms | 0 - 3 MB | NPU
| MobileFaceNet | ONNX | w8a16 | Qualcomm® Dragonwing™ IQ-X7181 | 0.823 ms | 0 - 0 MB | NPU
| MobileFaceNet | ONNX | w8a16 | Qualcomm® Dragonwing™ Q-6690 | 5.412 ms | 0 - 158 MB | NPU
| MobileFaceNet | ONNX | w8a16 | Qualcomm® Dragonwing™ Q-7790 | 0.925 ms | 0 - 41 MB | NPU
| MobileFaceNet | ONNX | w8a16 | Qualcomm® Dragonwing™ Q-8750 | 0.457 ms | 0 - 45 MB | NPU
| MobileFaceNet | ONNX | w8a16 | Snapdragon® 8 Elite Mobile | 0.457 ms | 0 - 45 MB | NPU
| MobileFaceNet | ONNX | w8a16 | Snapdragon® 8 Elite Gen 5 Mobile | 0.369 ms | 0 - 41 MB | NPU
| MobileFaceNet | ONNX | w8a16 | Snapdragon® 7 Gen 4 Mobile | 0.925 ms | 0 - 41 MB | NPU
| MobileFaceNet | QNN_DLC | float | Snapdragon® X2 Elite | 0.68 ms | 0 - 0 MB | NPU
| MobileFaceNet | QNN_DLC | float | Snapdragon® X Elite | 1.314 ms | 0 - 0 MB | NPU
| MobileFaceNet | QNN_DLC | float | Snapdragon® 8 Gen 3 Mobile | 0.792 ms | 0 - 46 MB | NPU
| MobileFaceNet | QNN_DLC | float | Snapdragon® 8 Gen 1 Mobile | 1.645 ms | 0 - 50 MB | NPU
| MobileFaceNet | QNN_DLC | float | Qualcomm® Dragonwing™ IQ-8275 | 1.471 ms | 0 - 3 MB | NPU
| MobileFaceNet | QNN_DLC | float | Qualcomm® Dragonwing™ QCS8550 (Proxy) | 1.132 ms | 0 - 2 MB | NPU
| MobileFaceNet | QNN_DLC | float | Qualcomm® SA8775P | 1.677 ms | 0 - 33 MB | NPU
| MobileFaceNet | QNN_DLC | float | Qualcomm® SA8650P | 1.677 ms | 0 - 33 MB | NPU
| MobileFaceNet | QNN_DLC | float | Qualcomm® SA8255P | 1.677 ms | 0 - 33 MB | NPU
| MobileFaceNet | QNN_DLC | float | Qualcomm® QCS8450 | 1.645 ms | 0 - 50 MB | NPU
| MobileFaceNet | QNN_DLC | float | Qualcomm® Dragonwing™ IQ-9075 | 1.498 ms | 0 - 2 MB | NPU
| MobileFaceNet | QNN_DLC | float | Qualcomm® Dragonwing™ IQ-X7181 | 1.314 ms | 0 - 0 MB | NPU
| MobileFaceNet | QNN_DLC | float | Qualcomm® Dragonwing™ Q-8750 | 0.602 ms | 0 - 31 MB | NPU
| MobileFaceNet | QNN_DLC | float | Qualcomm® SA7255P | 4.475 ms | 0 - 30 MB | NPU
| MobileFaceNet | QNN_DLC | float | Qualcomm® SA8295P | 2.018 ms | 0 - 29 MB | NPU
| MobileFaceNet | QNN_DLC | float | Snapdragon® 8 Elite Mobile | 0.602 ms | 0 - 31 MB | NPU
| MobileFaceNet | QNN_DLC | float | Snapdragon® 8 Elite Gen 5 Mobile | 0.479 ms | 0 - 32 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Snapdragon® X2 Elite | 0.592 ms | 0 - 0 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Snapdragon® X Elite | 1.156 ms | 0 - 0 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Snapdragon® 8 Gen 3 Mobile | 0.689 ms | 0 - 47 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Snapdragon® 8 Gen 1 Mobile | 1.228 ms | 0 - 52 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Qualcomm® Dragonwing™ QCS6490 | 3.644 ms | 0 - 2 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Qualcomm® Dragonwing™ IQ-8275 | 0.983 ms | 0 - 2 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Qualcomm® Dragonwing™ QCS8550 (Proxy) | 0.992 ms | 0 - 11 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Qualcomm® SA8775P | 1.205 ms | 0 - 38 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Qualcomm® SA8650P | 1.205 ms | 0 - 38 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Qualcomm® SA8255P | 1.205 ms | 0 - 38 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Qualcomm® QCS8450 | 1.228 ms | 0 - 52 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Qualcomm® Dragonwing™ IQ-9075 | 1.084 ms | 0 - 2 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Qualcomm® Dragonwing™ IQ-X7181 | 1.156 ms | 0 - 0 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Qualcomm® Dragonwing™ Q-6690 | 6.133 ms | 0 - 149 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Qualcomm® Dragonwing™ Q-7790 | 1.078 ms | 0 - 35 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Qualcomm® Dragonwing™ Q-8750 | 0.492 ms | 0 - 36 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Qualcomm® SA7255P | 2.244 ms | 0 - 35 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Qualcomm® SA8295P | 1.622 ms | 0 - 34 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Snapdragon® 8 Elite Mobile | 0.492 ms | 0 - 36 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Snapdragon® 8 Elite Gen 5 Mobile | 0.39 ms | 0 - 37 MB | NPU
| MobileFaceNet | QNN_DLC | w8a16 | Snapdragon® 7 Gen 4 Mobile | 1.078 ms | 0 - 35 MB | NPU
| MobileFaceNet | TFLITE | float | Snapdragon® 8 Gen 3 Mobile | 0.701 ms | 0 - 47 MB | NPU
| MobileFaceNet | TFLITE | float | Snapdragon® 8 Gen 1 Mobile | 1.483 ms | 0 - 49 MB | NPU
| MobileFaceNet | TFLITE | float | Qualcomm® Dragonwing™ IQ-8275 | 1.44 ms | 0 - 6 MB | NPU
| MobileFaceNet | TFLITE | float | Qualcomm® Dragonwing™ QCS8550 (Proxy) | 0.973 ms | 0 - 2 MB | NPU
| MobileFaceNet | TFLITE | float | Qualcomm® SA8775P | 1.508 ms | 0 - 36 MB | NPU
| MobileFaceNet | TFLITE | float | Qualcomm® SA8650P | 1.508 ms | 0 - 36 MB | NPU
| MobileFaceNet | TFLITE | float | Qualcomm® SA8255P | 1.508 ms | 0 - 36 MB | NPU
| MobileFaceNet | TFLITE | float | Qualcomm® QCS8450 | 1.483 ms | 0 - 49 MB | NPU
| MobileFaceNet | TFLITE | float | Qualcomm® Dragonwing™ IQ-9075 | 1.336 ms | 0 - 5 MB | NPU
| MobileFaceNet | TFLITE | float | Qualcomm® Dragonwing™ Q-8750 | 0.572 ms | 0 - 30 MB | NPU
| MobileFaceNet | TFLITE | float | Qualcomm® SA7255P | 4.451 ms | 0 - 32 MB | NPU
| MobileFaceNet | TFLITE | float | Qualcomm® SA8295P | 1.793 ms | 0 - 29 MB | NPU
| MobileFaceNet | TFLITE | float | Snapdragon® 8 Elite Mobile | 0.572 ms | 0 - 30 MB | NPU
| MobileFaceNet | TFLITE | float | Snapdragon® 8 Elite Gen 5 Mobile | 0.504 ms | 0 - 31 MB | NPU

## License
* The license for the original implementation of MobileFaceNet can be found
  [here](https://github.com/foamliu/MobileFaceNet/blob/master/LICENSE).

## References
* [Source Model Implementation](https://github.com/foamliu/MobileFaceNet)

## Community
* Join [our AI Hub Slack community](https://aihub.qualcomm.com/community/slack) to collaborate, post questions and learn more about on-device AI.
* For questions or feedback please [reach out to us](mailto:ai-hub-support@qti.qualcomm.com).
