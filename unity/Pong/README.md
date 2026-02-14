## Pong

Pong is a ball-and-paddle game adapted to support multiple input methods, including computer vision and sensor-based control. The game focuses on hand–eye coordination and controlled movement through simple, responsive interaction.

This game is part of a larger interactive games platform developed for geriatric engagement and rehabilitation.

---

### Interactive Hardware
- Light pen or torch  
- M5StickC  

---

### Gameplay Overview
- A ball moves continuously across the screen  
- One paddle is controlled by the player, while the opposing paddle is controlled by an AI opponent  
- The player controls their paddle to intercept and return the ball  
- The game continues until either side reaches a predefined number of points  

---

### Therapeutic Intent
- Encourages hand–eye coordination  
- Supports controlled, goal-directed arm movement  
- Promotes sustained attention through continuous interaction  
- Allows repeated practice using simple and familiar mechanics  

---

### Key Features
- Supports multiple control input methods  
- Paddle control using computer vision via OpenCV  
- Paddle control using tilt input via the M5StickC device  
- Optional keyboard control for testing and fallback interaction  
- Three difficulty levels:
  - Easy  
  - Medium  
  - Hard  

---

### Controls
- **Computer Vision:** Press **T** to enable OpenCV input, then move a light pen or torch in front of the camera to control the paddle  
- **Sensor-Based Input:** Tilt the M5StickC device left or right to move the paddle  
- **Keyboard:** Use the A and D keys to control paddle movement  

---

### Technical Details
- Engine: Unity  
- Unity Version: 6000.2.7f2 (Unity 6 – Tech Stream)  
- Platform: PC  
- Input: OpenCV (camera-based), M5StickC (IMU), Keyboard  

---

### How to Run
1. Download the repository or use **Code → Download ZIP**  
2. Open **Unity Hub**  
3. Add this folder (`unity/Pong`) as a project  
4. Connect the required input device (camera or M5StickC)  
5. Open the **Scenes** folder and load the main scene into the Hierarchy  
6. Press Play in the Unity Editor

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
- Keyboard input is provided primarily for testing and fallback purposes  
- For camera-based input, webcam exposure should be set to the lowest possible value  
- A clean background with minimal lighting interference is recommended for reliable light tracking  
- The `Library` folder is excluded from version control and will be regenerated automatically by Unity on first launch  
