#include <ClickEncoder.h>
#include <TimerOne.h>

#define ENCODER_CLK A0 // Change A0 to, for example, A5 if you want to use analog pin 5 instead
#define ENCODER_DT A1
#define ENCODER_SW A2

ClickEncoder *encoder; // variable representing the rotary encoder
int16_t last, value; // variables for current and last rotation value

void timerIsr() {
  encoder->service();
}

void setup() {
  Serial.begin(9600); // Opens the serial connection used for communication with the PC. 
  encoder = new ClickEncoder(ENCODER_DT, ENCODER_CLK, ENCODER_SW); // Initializes the rotary encoder with the mentioned pins

  Timer1.initialize(1000); // Initializes the timer, which the rotary encoder uses to detect rotation
  Timer1.attachInterrupt(timerIsr); 
  last = -1;
} 

void loop() {  
  value += encoder->getValue();

  // This part of the code is responsible for the actions when you rotate the encoder
  if (value != last) { // New value is different than the last one, that means to encoder was rotated
    if(last<value) { // Detecting the direction of rotation
      Serial.println("{\"delta\": 1}");
    }else{
      Serial.println("{\"delta\": -1}");
    }
    last = value; // Refreshing the "last" varible for the next loop with the current value
  }
}

        
