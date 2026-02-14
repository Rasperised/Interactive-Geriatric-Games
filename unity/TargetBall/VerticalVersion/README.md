## Target Ball (Vertical Version)

Target Ball (Vertical Version) is a rehabilitation-focused game where players control a ball’s vertical movement to reach on-screen targets. The game emphasises controlled arm displacement using distance-based sensing.

This game is part of a larger interactive games platform developed for geriatric engagement and rehabilitation.

---

### Interactive Hardware
- Sharp GP2Y0A21YK0F IR Distance Sensor  
- M5StickC or ESP32 Microcontroller  
- Arm Movement Rehabilitation Device  

---

### Gameplay Overview
- A ball moves vertically on the screen  
- Targets appear at different vertical positions  
- The player moves their arm closer to or further from the distance sensor  
- The ball follows the detected vertical arm movement  
- Each successful collision with a target registers a hit  

---

### Therapeutic Intent
- Encourages controlled vertical arm movement  
- Supports reach and return motion during interaction  
- Promotes repeated, low-impact physical engagement  
- Allows self-paced gameplay without time pressure  

---

### Key Features
- Vertical ball movement controlled using a distance sensor  
- Real-time mapping between arm displacement and on-screen motion  
- Target-based interaction to encourage deliberate movement  
- Simple visual feedback upon target collision  

---

### Controls
- **Distance Sensor:** Grab the handle on the arm movement rehabilitation device and move it up and down to control the ball’s vertical position  

---

### Hardware Setup (Brief)
- For this setup, the Sharp IR distance sensor is connected directly to the M5StickC (ESP32 can also be used in alternative configurations)  
- The sensor detects changes in arm distance  
- Distance readings are transmitted to Unity via the M5StickC  
- The received values are mapped to the ball’s vertical movement  
- GPIO pin assignments depend on the specific M5StickC wiring configuration  

---

### Technical Details
- Engine: Unity  
- Unity Version: 6000.2.7f2 (Unity 6 – Tech Stream)  
- Platform: Windows
- Input: IR distance sensor (via M5StickC)  

---

### How to Run
1. Download the repository or use **Code → Download ZIP**  
2. Open **Unity Hub**  
3. Add this folder (`unity/TargetBall/VerticalVersion`) as a project  
4. Connect the IR distance sensor to the M5StickC or ESP32 microcontroller  
5. Open the **Scenes** folder and load the main scene into the Hierarchy  
6. Press Play in the Unity Editor  

---

### Notes
- Sensor placement should remain consistent to ensure accurate vertical tracking  
- Minor calibration may be required depending on sensor range and mounting position  
- The `Library` folder is excluded from version control and will be regenerated automatically by Unity on first launch  
