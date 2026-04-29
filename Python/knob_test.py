import serial
import time
import json
import winsound

arduino_port = "COM7"
baud_rate = 9600
position: int = 0

try:
    # Initialize serial connection
    ser = serial.Serial(arduino_port, baud_rate, timeout=1)
    time.sleep(2)  # Wait for connection to stabilize
    print(f"Connected to Arduino on {arduino_port}")

    while True:
        if ser.in_waiting > 0:
            # Read the line from serial, decode it, and strip whitespace
            line = ser.readline().decode('utf-8').strip()
            if line:
                print(f"Encoder Position: {line}")
                print(f"pos var: {position}")
                data = json.loads(line)
                position += data["delta"]

                if abs(position) == 10:
                    print("Target reached!")
                    winsound.PlaySound("C:/SimulatorPlatform/Python_venv/assets_TargetSound.wav", winsound.SND_FILENAME)
                    position = 0


except KeyboardInterrupt:
    print("\nClosing connection...")
    ser.close()
except Exception as e:
    print(f"Error: {e}")