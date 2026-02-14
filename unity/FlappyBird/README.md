## Flappy Bird

Flappy Bird is a side-scrolling game where the player controls a bird and navigates through on-screen obstacles to earn points. The game focuses on timing, control, and sustained attention through simple and continuous interaction.

This game is part of a larger interactive games platform developed for geriatric engagement and rehabilitation.

---

### Interactive Hardware
- M5StickC
- Webcam
- Torch light or light pen

---

### Gameplay Overview
- The player controls a bird that moves forward automatically
- Obstacles appear continuously along the path
- The objective is to guide the bird through gaps between obstacles
- The game ends when the bird collides with an obstacle

---

### Therapeutic Intent
- Encourages controlled arm movement and coordination
- Supports timing and motor control through repetitive interaction
- Promotes sustained attention during continuous gameplay
- Allows repeated practice using simple and familiar mechanics

---

### Key Features
- Supports multiple control input methods for controlling the bird
- Bird control using computer vision via OpenCV
- Bird control using tilt input via M5StickC
- Optional mouse input for testing and fallback interaction
- Simple scoring based on successful obstacle navigation

---

### Controls
- **Computer Vision:** Press **T** to enable OpenCV input, then move a light pen or torch light in front of the camera to control the bird
- **Sensor-Based Input:** Tilt the M5StickC device up or down to control the bird’s vertical movement
- **Mouse:** Move the mouse to control the bird’s vertical position

---

### Technical Details
- Engine: Unity
- Unity Version: 6000.2.7f2 (Unity 6 – Tech Stream)
- Platform: PC
- Input: OpenCV (camera-based), M5StickC (IMU), Mouse

---

### How to Run
1. Download the repository or use **Code → Download ZIP**
2. Open **Unity Hub**
3. Add this folder (`unity/FlappyBird`) as a project
4. Open the **Scenes** folder and load the main scene into the Hierarchy
5. Press Play in the Unity Editor

---

### Camera Calibration (Low Exposure Setup)
To ensure the OpenCV tracking accurately picks up the light pen or torch without interference from background lights, you must manually lower your webcam's exposure. Using **OBS Studio** is the most reliable way to save these hardware settings:

1. **Open OBS Studio** and create a new **Scene**.
2. In the **Sources** box, click the `+` icon and select **Video Capture Device**.
3. Select your webcam and click **OK**.
4. Right-click on the **Video Capture Device** source and select **Properties**.
5. Scroll down and click the **Configure Video** button.
6. In the popup window, navigate to the **Camera Control** tab.
7. Find the **Exposure** slider:
    * **Untick** the "Auto" checkbox.
    * **Lower the slider** until the room appears dark and only the light source (torch/pen) is clearly visible as a bright point.
8. Click **Apply** and **OK**. You can now close OBS; the hardware settings will remain active for the Unity game.

---

### Notes
- For demonstration purposes, the M5StickC input method is primarily used.
- Camera-based input requires adequate lighting and minimal background interference for reliable tracking.
- Mouse input is provided primarily for testing and fallback purposes.
- The `Library` folder is excluded from version control and will be regenerated automatically by Unity on first launch.
