/*
  A serialUART controlled light(LED)

  Turn set of LED on or off through computer user interface
  LED is connected to Pin 2 to 9, USB port/cable is connected to the PC
  to allowed serial communication serial via UART protocol.

  each LED is addressed with LXY,
  where L indicate LED,
  X = Led No (1 - 8), and and Y is 1 or 0 (On/Off)

  The project can be expanded using shiftregister.

  Created September 9th, 2021
  by, Olayide AJAYI
  ebena107@gmail.com
  http://ebena.com.ng
*/


char buffer[8];



int Led1 = 2;
int Led2 = 3;
int Led3 = 4;
int Led4 = 5;
int Led5 = 6;
int Led6 = 7;
int Led7 = 8;
int Led8 = 9;

void setup() {
  Serial.begin(9600);
  Serial.flush();

  //setup pins
  pinMode(Led1, OUTPUT);
  pinMode(Led2, OUTPUT);
  pinMode(Led3, OUTPUT);
  pinMode(Led4, OUTPUT);
  pinMode(Led5, OUTPUT);
  pinMode(Led6, OUTPUT);
  pinMode(Led7, OUTPUT);
  pinMode(Led8, OUTPUT);

  // Ensure all LED are off
  switchAllOff();

}

void loop() {
  // byte  val;
  // read from port 1, send to port 0:
  if (Serial.available() > 0) {
    int index = 0;
    delay(100);
    int numChar = Serial.available();
    if (numChar > 5) {
      numChar = 5;
    }
    while (numChar--) {
      buffer[index++] = Serial.read();
    }
    splitString(buffer);
  }
}

void splitString(char* data) {
  char* param;
  param = strtok (data, " ,");
  while (param != NULL) {
    checkMessage(param);
    param = strtok (NULL, " ,");
  }

  //clear the text and serial buffers
  for (int x = 0; x < 6; x++) {
    buffer[x] = '\0';
  }
  Serial.flush();
}


void checkMessage(char* data) {
  // Serial.write(data+0);
  if ((data[0] == 'l') || (data[0] == 'L')) {
    switch (data[1]) {
      case '1' : switchLed(Led1, data); break;
      case '2' : switchLed(Led2, data); break;
      case '3' : switchLed(Led3, data); break;
      case '4' : switchLed(Led4, data); break;
      case '5' : switchLed(Led5, data); break;
      case '6' : switchLed(Led6, data); break;
      case '7' : switchLed(Led7, data); break;
      case '8' : switchLed(Led8, data); break;
      default: break;
    }
  } else  if ((data[0] == 'x') || (data[0] == 'X')) {
    switch (data[1]) {
      case '1': switchAllOn(); break;
      case '0': switchAllOff(); break;
      default: break;
    }
  }
}


void switchLed(int led, char* data) {
  if (data[2] == '0')
  {
    digitalWrite(led, LOW);
  }
  else  if (data[2] == '1')
  {
    digitalWrite(led, HIGH);
  }

  Serial.write(data + 1);
  delay(100);
  Serial.flush();
}

// Switch all LED are OFF
void switchAllOff() {
  String str;
  
  digitalWrite(Led1, LOW);
  digitalWrite(Led2, LOW);
  digitalWrite(Led3, LOW);
  digitalWrite(Led4, LOW);
  digitalWrite(Led5, LOW);
  digitalWrite(Led6, LOW);
  digitalWrite(Led7, LOW);
  digitalWrite(Led8, LOW);

  for (int i =1; i <= 8; i++) {
    str = String("");
   str += i;
    str += 0;
     Serial.println(str);
   Serial.flush();
    delay(1100);
    Serial.flush();
  }
 
}

// Switch all LED are ON
void switchAllOn() {
  String str;
  
  digitalWrite(Led1, HIGH);
  digitalWrite(Led2, HIGH);
  digitalWrite(Led3, HIGH);
  digitalWrite(Led4, HIGH);
  digitalWrite(Led5, HIGH);
  digitalWrite(Led6, HIGH);
  digitalWrite(Led7, HIGH);
  digitalWrite(Led8, HIGH);

  for (int i = 1; i <= 8; i++) {
    str = String("");
    str += i;
    str += 1;
   // str += "\n";
  
    Serial.println(str);
    Serial.flush();
    delay(1100);
    Serial.flush();
  }
  
}
