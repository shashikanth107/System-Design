# Microservices Architecture

## Introduction

Microservices architecture is a design approach where an application is built as a collection of small, independent services that communicate through well-defined APIs.

## Core Concepts

### 1. Service Independence

Each microservice should:
- Be independently deployable
- Have its own database
- Be loosely coupled
- Have a single responsibility

### 2. Communication Patterns

#### Synchronous Communication

```python
# REST API Example
from fastapi import FastAPI
from pydantic import BaseModel

app = FastAPI()

class Order(BaseModel):
    order_id: str
    user_id: str
    items: list[dict]

@app.post("/orders/")
async def create_order(order: Order):
    # Process order
    return {"status": "success", "order_id": order.order_id}
```

#### Asynchronous Communication

```python
# Message Queue Example using RabbitMQ
import pika

class OrderProcessor:
    def __init__(self):
        self.connection = pika.BlockingConnection(
            pika.ConnectionParameters('localhost')
        )
        self.channel = self.connection.channel()
        self.channel.queue_declare(queue='orders')

    def publish_order(self, order):
        self.channel.basic_publish(
            exchange='',
            routing_key='orders',
            body=json.dumps(order)
        )

    def process_orders(self):
        def callback(ch, method, properties, body):
            order = json.loads(body)
            # Process order
            print(f"Processing order: {order['order_id']}")

        self.channel.basic_consume(
            queue='orders',
            on_message_callback=callback,
            auto_ack=True
        )
        self.channel.start_consuming()
```

## Service Discovery

### 1. Using Consul

```yaml
# consul-config.yml
services:
  - name: order-service
    port: 8080
    check:
      http: "http://localhost:8080/health"
      interval: "10s"
      timeout: "1s"
```

```python
# Service registration
import consul

class ServiceRegistry:
    def __init__(self):
        self.consul = consul.Consul()

    def register_service(self, name, address, port):
        self.consul.agent.service.register(
            name=name,
            address=address,
            port=port,
            check=consul.Check.http(
                f"http://{address}:{port}/health",
                interval="10s"
            )
        )

    def get_service(self, name):
        _, services = self.consul.health.service(name, passing=True)
        return services
```

## API Gateway Pattern

```python
# API Gateway using FastAPI
from fastapi import FastAPI, HTTPException
import httpx

app = FastAPI()

class APIGateway:
    def __init__(self):
        self.services = {
            'orders': 'http://order-service:8080',
            'users': 'http://user-service:8081',
            'products': 'http://product-service:8082'
        }
        self.client = httpx.AsyncClient()

    async def forward_request(self, service, path, method, data=None):
        if service not in self.services:
            raise HTTPException(status_code=404, detail="Service not found")

        url = f"{self.services[service]}{path}"
        response = await self.client.request(method, url, json=data)
        return response.json()

@app.post("/api/orders/{path:path}")
async def order_proxy(path: str, data: dict = None):
    gateway = APIGateway()
    return await gateway.forward_request('orders', f"/{path}", "POST", data)
```

## Circuit Breaker Pattern

```python
from enum import Enum
import time

class CircuitState(Enum):
    CLOSED = "CLOSED"
    OPEN = "OPEN"
    HALF_OPEN = "HALF_OPEN"

class CircuitBreaker:
    def __init__(self, failure_threshold=5, reset_timeout=60):
        self.failure_threshold = failure_threshold
        self.reset_timeout = reset_timeout
        self.state = CircuitState.CLOSED
        self.failures = 0
        self.last_failure_time = None

    async def call(self, func, *args, **kwargs):
        if self.state == CircuitState.OPEN:
            if time.time() - self.last_failure_time >= self.reset_timeout:
                self.state = CircuitState.HALF_OPEN
            else:
                raise Exception("Circuit is OPEN")

        try:
            result = await func(*args, **kwargs)
            if self.state == CircuitState.HALF_OPEN:
                self.state = CircuitState.CLOSED
                self.failures = 0
            return result
        except Exception as e:
            self.failures += 1
            self.last_failure_time = time.time()
            if self.failures >= self.failure_threshold:
                self.state = CircuitState.OPEN
            raise e
```

## Database Per Service Pattern

### 1. Service with Dedicated Database

```python
# Order Service with its own database
from sqlalchemy import create_engine, Column, Integer, String
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm import sessionmaker

Base = declarative_base()

class Order(Base):
    __tablename__ = 'orders'
    
    id = Column(Integer, primary_key=True)
    user_id = Column(String)
    status = Column(String)
    
class OrderService:
    def __init__(self):
        self.engine = create_engine('postgresql://user:pass@localhost/orders_db')
        Session = sessionmaker(bind=self.engine)
        self.session = Session()
        
    def create_order(self, user_id):
        order = Order(user_id=user_id, status='pending')
        self.session.add(order)
        self.session.commit()
        return order.id
```

## Event Sourcing Pattern

```python
from datetime import datetime

class Event:
    def __init__(self, event_type, data, timestamp=None):
        self.event_type = event_type
        self.data = data
        self.timestamp = timestamp or datetime.now()

class EventStore:
    def __init__(self):
        self.events = []

    def append(self, event):
        self.events.append(event)

    def get_events(self, start_time=None):
        if start_time:
            return [e for e in self.events if e.timestamp >= start_time]
        return self.events

class OrderAggregate:
    def __init__(self, event_store):
        self.event_store = event_store
        self.state = {}

    def apply_event(self, event):
        if event.event_type == "OrderCreated":
            self.state["order_id"] = event.data["order_id"]
            self.state["status"] = "created"
        elif event.event_type == "OrderPaid":
            self.state["status"] = "paid"
        elif event.event_type == "OrderShipped":
            self.state["status"] = "shipped"

    def rebuild_state(self):
        self.state = {}
        for event in self.event_store.get_events():
            self.apply_event(event)
```

## Service Mesh Pattern

### Using Istio

```yaml
# istio-config.yaml
apiVersion: networking.istio.io/v1alpha3
kind: VirtualService
metadata:
  name: order-service
spec:
  hosts:
  - order-service
  http:
  - route:
    - destination:
        host: order-service
        subset: v1
      weight: 90
    - destination:
        host: order-service
        subset: v2
      weight: 10
```

## Monitoring and Observability

### 1. Distributed Tracing

```python
from opentelemetry import trace
from opentelemetry.ext import jaeger

class OrderService:
    def __init__(self):
        jaeger_exporter = jaeger.JaegerSpanExporter(
            agent_host_name="localhost",
            agent_port=6831
        )
        trace.get_tracer_provider().add_span_processor(
            jaeger_exporter
        )
        self.tracer = trace.get_tracer(__name__)

    async def process_order(self, order_id):
        with self.tracer.start_as_current_span("process_order") as span:
            span.set_attribute("order_id", order_id)
            # Process order
            result = await self._internal_process(order_id)
            return result
```

### 2. Metrics Collection

```python
from prometheus_client import Counter, Histogram
import time

# Metrics
REQUEST_COUNT = Counter(
    'request_count', 
    'App Request Count',
    ['method', 'endpoint', 'http_status']
)
REQUEST_LATENCY = Histogram(
    'request_latency_seconds', 
    'Request latency',
    ['method', 'endpoint']
)

# FastAPI middleware
@app.middleware("http")
async def metrics_middleware(request, call_next):
    start_time = time.time()
    response = await call_next(request)
    latency = time.time() - start_time
    
    REQUEST_COUNT.labels(
        method=request.method,
        endpoint=request.url.path,
        http_status=response.status_code
    ).inc()
    
    REQUEST_LATENCY.labels(
        method=request.method,
        endpoint=request.url.path
    ).observe(latency)
    
    return response
```

## Best Practices

1. Service Design:
   - Single Responsibility
   - Domain-Driven Design
   - Independent Deployability
   - Proper Service Boundaries

2. Data Management:
   - Data Ownership
   - Database per Service
   - Event-Driven Architecture
   - CQRS Pattern

3. Operational Excellence:
   - Automated Deployment
   - Continuous Integration
   - Monitoring and Logging
   - Service Documentation

4. Resilience:
   - Circuit Breakers
   - Rate Limiting
   - Retry Policies
   - Fallback Mechanisms