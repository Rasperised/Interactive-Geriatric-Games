## Flower Field

Flower Field is an interactive game where players trigger on-screen flowers using distance-based hand or arm movement. The game focuses on spatial awareness and controlled movement through simple, sensor-driven interaction.

This game is part of a larger interactive games platform developed for geriatric engagement and rehabilitation.

---

### Interactive Hardware
- Sharp GP2Y0A21YK0F IR Distance Sensor
- ESP32 Microcontroller

---
### Gameplay Overview
- Flowers are arranged horizontally across the screen
- The player activates flowers by moving their hand or arm within range of distance sensors
- Each sensor corresponds to a specific on-screen flower
- The game encourages exploration and repeated interaction across the full horizontal range

---

### Therapeutic Intent
- Encourages controlled upper-limb movement
- Supports spatial awareness and left-to-right reach
- Promotes repeated, low-pressure physical engagement
- Allows self-paced interaction without time constraints

---

### Key Features
- Distance sensor–based interaction for triggering on-screen elements
- Multiple sensors mapped to different flower positions
- Clear left-to-right spatial mapping between sensors and on-screen flowers
- Simple visual feedback when flowers are activated

---

### Controls
- **Distance Sensors:** Move the hand or arm closer to a sensor to activate the corresponding flower

---

### Hardware Setup (Brief)
- Multiple distance sensors are arranged horizontally
- The first distance sensor corresponds to the leftmost flower on screen
- The fifth distance sensor corresponds to the rightmost flower on screen
- Sensor-to-flower mapping follows a left-to-right order
- GPIO pin assignments depend on the specific hardware connections used

---

### Technical Details
- Engine: Unity
- Unity Version: 6000.2.7f2 (Unity 6 – Tech Stream)
- Platform: PC
- Input: Distance sensors (via microcontroller)

---

### How to Run
1. Download the repository or use **Code → Download ZIP**
2. Open **Unity Hub**
3. Add this folder (`unity/FlowerField`) as a project
4. Connect the distance sensors to the microcontroller
5. Open the **Scenes** folder and load the main scene into the Hierarchy
6. Press Play in the Unity Editor

---

### Notes
- Sensor positioning should remain consistent to preserve correct left-to-right mapping.
- Minor calibration may be required depending on sensor placement and distance range.
- The `Library` folder is excluded from version control and will be regenerated automatically by Unity on first launch.
