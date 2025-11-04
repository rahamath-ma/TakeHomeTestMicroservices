# 🧩 TakeHomeTest – Microservices Sample (.NET 8, Kafka, Outbox Pattern)

## 📘 Overview
This repository demonstrates a simple **event-driven microservices** architecture using .NET 8, Kafka (via Redpanda), and an **Outbox pattern** for reliable messaging.

It contains two bounded contexts:

| Service | Port | Responsibility |
|----------|------|----------------|
| **UserService** | 8080 | Manages users and publishes `users.created` events |
| **OrderService** | 8081 | Creates orders and consumes `users.created` events to validate users |

Each service runs independently with its own in-memory database and uses Serilog for logging and health checks.

---

## 🏗️ Architecture
```
+-------------+         Kafka Topic         +---------------+
| UserService | --►  users.created  ───────► | OrderService  |
|  (Publisher)|                              | (Consumer)    |
+-------------+                              +---------------+
       │                                              │
       │    POST /users                               │
       │    GET  /users/{id}                          │
       │                                              │
       └──────────────────────────────► Outbox Pattern │
                                         (BackgroundDispatcher)
```

### 🔹 Technologies
- .NET 8, ASP.NET Core Minimal API
- Kafka / Redpanda via Confluent.Kafka
- Entity Framework Core (In-Memory DB)
- Serilog logging
- Outbox pattern for reliable event publishing
- Background consumers for cross-service communication

---

## ⚙️ Running the solution

### 1️⃣ Docker Compose
```bash
docker compose down --volumes 
docker compose build --no-cache
docker compose up
```

### 2️⃣ Local development
```bash
cd UserService
dotnet run --urls=http://localhost:8080

cd ../OrderService
dotnet run --urls=http://localhost:8081
```

### 3️⃣ Swagger UI
| Service | URL |
|----------|-----|
| UserService | http://localhost:8080/swagger |
| OrderService | http://localhost:8081/swagger |

---

## 🚀 API Usage

### Create User
```http
POST /users
{
  "name": "Abu",
  "email": "abu@example.com"
}
```

### Create Order
```http
POST /orders
{
  "userId": "GUID-from-created-user",
  "product": "Book",
  "quantity": 1,
  "price": 10
}
```

---

## 🧠 Validation Logic
- Validates known users via Kafka-consumed cache.
- Prevents duplicate orders (same user, product, price, and quantity) by adding IdempotencyKey

---

## 🧪 Automated Tests
```bash
dotnet test
```
Includes xUnit unit tests under `/Tests` verifying:
- Unknown users are rejected.
- Valid users can create orders.
- Duplicate orders are blocked.

---

## 🌐 GitHub Setup
```bash
git init
git add .
git commit -m "Initial commit – microservices demo"
git branch -M main
git remote add origin https://github.com/rahamath-ma/TakeHomeTestMicroservices.git
git push -u origin main
```
## 🤖 AI Tools & Development Approach (if you choose to use them)

AI Tools used **GitHub Copilot**
