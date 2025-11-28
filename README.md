# **End-to-End gRPC Streaming with Bidirectional Backpressure (gRPC + Channels) with .NET 10**

This project demonstrates a complete **end-to-end bidirectional gRPC streaming pipeline** in **.NET 10**, featuring real backpressure on both the server and the client.

Unlike typical gRPC samples (which stream unbounded and rely only on TCP buffers), this architecture enforces **application-level backpressure** using both:

* bounded channels
* controlled processing rates
* simulated slow consumers
* WriteAsync blocking
* natural TCP congestion
* client-side throttling

The result is a **stable, self-regulated streaming system** with no leaks, no growing latency, and predictable flow control.

---

## 🚧 The Problem We Wanted to Solve

By default, gRPC duplex streams allow clients to send messages **much faster** than a server can process:

* client → streams hundreds of messages per second
* server → processes slower
* TCP & gRPC buffers → fill up
* memory starts growing
* WriteAsync eventually blocks
* system becomes unstable

We needed:

✔ Server-side backpressure

✔ Client-side slowdown when server is overwhelmed

✔ A bounded internal buffer

✔ A predictable, self-throttling streaming pipeline

---

## 🎯 What We Implemented

### **Server**

* A **bounded Channel<TelemetryEvent>** (capacity 20)
* WriteAsync that **blocks** whenever the channel is full
* A simulated “slow processor”
* ACKs returned to the client for each event
* Natural TCP backpressure (gRPC transport)

### **Client**

* Large payloads (50–600 KB) to stress the pipeline
* Continuous streaming of events
* Measurement of WriteAsync pauses
* **Random delays** (simulated workload) → letting the server drain
* Reading ACKs as they come
* Achieving natural bidirectional flow control

---

## 📊 What the Logs Show

### **Server logs**

* The server’s WriteAsync pauses at predictable intervals
* Channel fills to 20 and drains back down
* No memory growth
* No dropping packets
* No runaway latency

### **Client logs**

* Client WriteAsync periodically pauses → exactly what we want
* Total paused time remains bounded
* The client slows down naturally when the server is saturated
* No uncontrolled flooding

Both logs confirm the same behavior:

👉 **Backpressure exists and flows in both directions.**

---

## 🟩 Final Architecture: Bidirectional Backpressure

This architecture includes:

### **✔ Physical backpressure (TCP)**

The OS network stack slows the sender when the receiver is overloaded.

### **✔ Logical backpressure (gRPC WriteAsync)**

If gRPC internal buffers fill, WriteAsync blocks.

### **✔ Internal backpressure (bounded Channel)**

The server uses a bounded channel to control ingestion.

### **✔ Bounded incoming & outgoing buffers**

Neither client nor server can overrun the pipeline.

---

## 🟦 Final Result

**A fully automatic, stable, self-regulating streaming system:**

🟩 **REAL bidirectional backpressure**

🟩 **No memory leaks**

🟩 **No unbounded latency**

🟩 **No message loss**

🟩 **Safe throttling on both client & server**

🟩 **Predictable channel behavior**

🟩 **TCP-level congestion control working together with app-level control**

This is the ideal architecture for:

* telemetry ingestion
* analytics pipelines
* IoT streaming
* logging pipelines
* real-time monitoring systems
* high-volume producer → slow consumer scenarios