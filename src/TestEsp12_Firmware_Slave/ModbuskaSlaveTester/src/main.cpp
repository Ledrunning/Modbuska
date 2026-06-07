#include <Arduino.h>
#include <ModbusRTU.h>

#define SLAVE_ID 1
ModbusRTU mb;

// The NodeMCU's built-in LED is usually blue and is connected to pin D4 (GPIO2)
#define MY_LED LED_BUILTIN

void setup()
{
    // Set the LED pin to output
    pinMode(MY_LED, OUTPUT);
    // Note: the ESP8266's built-in LED is activated by a LOW signal,
    // so we set it to HIGH to turn it off initially.
    digitalWrite(MY_LED, HIGH);

    // Инициализируем штатный USB-UART
    Serial.begin(115200, SERIAL_8N1);
    mb.begin(&Serial);
    mb.slave(SLAVE_ID);

    // Allocate 16 registers in memory for each type
    for (int i = 0; i < 16; i++)
    {
        mb.addCoil(i, false);        // Addresses of flags 0...15 (default: false)
        mb.addIsts(i, (i % 2 == 0)); // Addresses of inputs 0...15 (alternating true/false)
        mb.addIreg(i, i * 100);      // Addresses of input registers 0...15 (0, 100, 200...)
        mb.addHreg(i, i * 10);       // Addresses of storage registers 0...15 (0, 10, 20...)
    }
}

void loop()
{
    // Processing Modbus packets (reading/writing registers using the library)
    mb.task();

    // --- LED TESTING PROCEDURE ---

    // Read the data written by the PC to Coil at address 0 (FC05/FC15 test)
    bool ledTriggeredByCoil = mb.Coil(0);

    // Read the value written by the PC to the Holding Register at address 0 (test FC06/FC16)
    uint16_t ledTriggeredByHreg = mb.Hreg(0);

    // If a 1 is stored in Coil 0 OR the number 555 is stored in Holding Register 0
    if (ledTriggeredByCoil == true || ledTriggeredByHreg == 555)
    {
        digitalWrite(MY_LED, LOW); // Turn on the LED
    }
    else
    {
        digitalWrite(MY_LED, HIGH); // Turn off the LED
    }

    yield(); // Mandatory reset for ESP8266 stability
}