**I2C scanner debugger**

electrical side: tested with one of those red boards with USB on one side and 6pin connector on the other side
connect your device as follows - don’t forget about I2C pull-ups
SCL --- RTSpin -- R 4k7 --- VCC
SDA --- CTSpin -- R 4k7 --- VCC
remember to set jumper on FTDI board to correct voltage for your devices

software side:
needs D2XX drivers from FTDI, you may get it here: https://ftdichip.com/drivers/d2xx-drivers/
this version got compiled using dot net 3.5 - binary runs on xp and w7x32, i could not find working drivers for those os's

Released under MIT licence, Copyright (c) 2026 Alana

**Development note**
This project was developed with substantial assistance from ChatGPT (OpenAI).
The hardware setup, testing, debugging, protocol experiments, and project direction were done hands-on by the author. ChatGPT was used extensively for generating and iterating the C# implementation.
In other words: the code was AI-assisted, the oscilloscope probes were not.
