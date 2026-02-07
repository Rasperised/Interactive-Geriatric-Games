## Target Ball (Arc Version)

Target Ball (Arc Version) is a rehabilitation-focused game where players guide a ball along a curved arc path to reach on-screen targets. The game emphasises controlled arm tilt and smooth directional movement through sensor-based input.

This game is part of a larger interactive games platform developed for geriatric engagement and rehabilitation.

---

### Interactive Hardware
- M5StickC  
- Arm Movement Rehabilitation Device  

---

### Gameplay Overview
- A ball moves along a predefined curved arc on the screen  
- Targets are positioned at alternating ends of the arc  
- The player tilts the arm rehabilitation device to guide the ball  
- Each successful collision with a target registers a hit  
- The game continues until a predefined number of targets is reached  

---

### Therapeutic Intent
- Encourages controlled wrist and forearm movement  
- Supports directional coordination through arc-based motion  
- Promotes smooth, continuous movement rather than rapid actions  
- Allows repeated, low-pressure physical engagement  

---

### Key Features
- Arc-based ball movement controlled using tilt input  
- IMU-driven interaction using the M5StickC device  
- Alternating target positions to encourage balanced movement  
- Visual feedback upon successful target hits  

---

### Controls
- **Sensor-Based Input:** Grab the handle and tilt the arm rehabilitation device left or right to guide the ball along the arc  

---

### Hardware Setup (Brief)
- An M5StickC device is mounted onto the arm rehabilitation apparatus  
- The IMU captures arm tilt and orientation  
- Tilt direction and magnitude are mapped to the ball’s movement along the arc  
- The exact mounting position may be adjusted based on user comfort  

---

### Technical Details
- Engine: Unity  
- Unity Version: 6000.2.7f2 (Unity 6 – Tech Stream)  
- Platform: PC  
- Input: M5StickC (IMU-based tilt input)  

---

### How to Run
1. Download the repository or use **Code → Download ZIP**  
2. Open **Unity Hub**  
3. Add this folder (`unity/TargetBall/ArcVersion`) as a project  
4. Connect the M5StickC device  
5. Open the **Scenes** folder and load the main scene into the Hierarchy  
6. Press Play in the Unity Editor  

---

### Notes
- Sensor sensitivity may require minor adjustment depending on mounting orientation  
- Consistent arm positioning helps ensure smooth ball movement  
- The `Library` folder is excluded from version control and will be regenerated automatically by Unity on first launch  
