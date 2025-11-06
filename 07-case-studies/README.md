# System Design Case Studies

## 1. URL Shortener (Like Bit.ly)

### Requirements

#### Functional Requirements
- Shorten long URLs
- Redirect users to original URL
- Custom URL support
- URL expiration
- Analytics tracking

#### Non-Functional Requirements
- Highly available
- Low latency
- URL should not be guessable
- Scalable to handle high traffic

### System Design

#### API Design

```python
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
import hashlib
import base62

app = FastAPI()

class URLRequest(BaseModel):
    long_url: str
    custom_alias: str = None
    expiry_days: int = None

@app.post("/shorten")
async def shorten_url(request: URLRequest):
    if request.custom_alias:
        short_url = request.custom_alias
    else:
        # Generate short URL using MD5 and Base62
        hash_object = hashlib.md5(request.long_url.encode())
        hash_hex = hash_object.hexdigest()
        short_url = base62.encode(int(hash_hex[:8], 16))
    
    return {"short_url": f"http://short.url/{short_url}"}

@app.get("/{short_url}")
async def redirect_url(short_url: str):
    # Lookup original URL and redirect
    original_url = await get_original_url(short_url)
    if not original_url:
        raise HTTPException(status_code=404, detail="URL not found")
    return {"url": original_url}
```

#### Database Schema

```sql
-- URLs Table
CREATE TABLE urls (
    id SERIAL PRIMARY KEY,
    short_url VARCHAR(10) UNIQUE,
    long_url TEXT NOT NULL,
    user_id INTEGER,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    expires_at TIMESTAMP,
    click_count INTEGER DEFAULT 0
);

-- Analytics Table
CREATE TABLE clicks (
    id SERIAL PRIMARY KEY,
    url_id INTEGER REFERENCES urls(id),
    clicked_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    user_agent TEXT,
    ip_address VARCHAR(45),
    referrer TEXT
);

-- Create indexes
CREATE INDEX idx_short_url ON urls(short_url);
CREATE INDEX idx_clicks_url_id ON clicks(url_id);
```

## 2. Video Streaming Service (Like YouTube)

### Requirements

#### Functional Requirements
- Upload videos
- Stream videos
- User subscriptions
- Comments and likes
- Video search

#### Non-Functional Requirements
- High availability
- Low latency streaming
- Scalable storage
- Global accessibility

### System Design

#### Video Processing Pipeline

```python
from enum import Enum
from typing import List

class VideoStatus(Enum):
    UPLOADING = "uploading"
    PROCESSING = "processing"
    READY = "ready"
    FAILED = "failed"

class VideoProcessor:
    def __init__(self):
        self.storage_client = CloudStorageClient()
        self.transcoder = VideoTranscoder()
        self.cdn = CDNClient()

    async def process_video(self, video_id: str, file_path: str):
        try:
            # 1. Upload to cloud storage
            await self.storage_client.upload(video_id, file_path)

            # 2. Create multiple formats
            formats = ["720p", "1080p", "480p", "360p"]
            transcoding_tasks = []
            for format in formats:
                task = self.transcoder.transcode(video_id, format)
                transcoding_tasks.append(task)
            
            await asyncio.gather(*transcoding_tasks)

            # 3. Push to CDN
            await self.cdn.distribute(video_id)

            return {"status": "success", "formats": formats}
        except Exception as e:
            return {"status": "failed", "error": str(e)}
```

#### CDN Configuration

```nginx
# CDN Edge Server Configuration
http {
    proxy_cache_path /tmp/cache levels=1:2 keys_zone=video_cache:10m max_size=10g inactive=60m;

    server {
        listen 80;
        server_name cdn.example.com;

        location ~ ^/videos/(.+)$ {
            proxy_cache video_cache;
            proxy_cache_use_stale error timeout http_500 http_502 http_503 http_504;
            proxy_cache_valid 200 24h;
            proxy_cache_key $uri;
            proxy_pass http://origin.example.com;
        }
    }
}
```

## 3. Real-time Chat System (Like Slack)

### Requirements

#### Functional Requirements
- One-on-one messaging
- Group chats
- Online status
- Message history
- File sharing

#### Non-Functional Requirements
- Real-time delivery
- Message persistence
- Scalable to millions of users
- Low latency

### System Design

#### WebSocket Server

```python
import asyncio
import websockets
import json

class ChatServer:
    def __init__(self):
        self.connections = {}  # user_id -> websocket
        self.channels = {}     # channel_id -> set of user_ids

    async def register(self, websocket, user_id):
        self.connections[user_id] = websocket
        await self.broadcast_status(user_id, "online")

    async def unregister(self, user_id):
        del self.connections[user_id]
        await self.broadcast_status(user_id, "offline")

    async def handle_message(self, message_data):
        sender_id = message_data["sender_id"]
        channel_id = message_data["channel_id"]
        content = message_data["content"]

        # Store message in database
        await self.store_message(channel_id, sender_id, content)

        # Send to all users in channel
        if channel_id in self.channels:
            for user_id in self.channels[channel_id]:
                if user_id in self.connections:
                    await self.connections[user_id].send(
                        json.dumps(message_data)
                    )
```

#### Database Schema

```sql
-- Users Table
CREATE TABLE users (
    user_id SERIAL PRIMARY KEY,
    username VARCHAR(50) UNIQUE,
    status VARCHAR(20),
    last_seen TIMESTAMP
);

-- Channels Table
CREATE TABLE channels (
    channel_id SERIAL PRIMARY KEY,
    name VARCHAR(50),
    type VARCHAR(20),  -- 'direct' or 'group'
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Channel Members Table
CREATE TABLE channel_members (
    channel_id INTEGER REFERENCES channels(channel_id),
    user_id INTEGER REFERENCES users(user_id),
    joined_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (channel_id, user_id)
);

-- Messages Table
CREATE TABLE messages (
    message_id SERIAL PRIMARY KEY,
    channel_id INTEGER REFERENCES channels(channel_id),
    sender_id INTEGER REFERENCES users(user_id),
    content TEXT,
    sent_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    edited_at TIMESTAMP
);

-- Create indexes
CREATE INDEX idx_messages_channel ON messages(channel_id);
CREATE INDEX idx_messages_sender ON messages(sender_id);
CREATE INDEX idx_channel_members ON channel_members(user_id, channel_id);
```

## 4. E-commerce Platform (Like Amazon)

### Requirements

#### Functional Requirements
- Product catalog
- Shopping cart
- Order processing
- Payment integration
- User reviews

#### Non-Functional Requirements
- High availability
- Consistent transactions
- Scalable inventory
- Fast search

### System Design

#### Product Search Service

```python
from elasticsearch import Elasticsearch
from fastapi import FastAPI, Query

app = FastAPI()
es = Elasticsearch()

class ProductSearch:
    def __init__(self):
        self.es = Elasticsearch()

    async def search_products(self, query: str, filters: dict = None):
        search_body = {
            "query": {
                "bool": {
                    "must": [
                        {
                            "multi_match": {
                                "query": query,
                                "fields": ["name^3", "description", "category"]
                            }
                        }
                    ]
                }
            },
            "aggs": {
                "categories": {
                    "terms": {"field": "category.keyword"}
                },
                "price_ranges": {
                    "range": {
                        "field": "price",
                        "ranges": [
                            {"to": 50},
                            {"from": 50, "to": 100},
                            {"from": 100}
                        ]
                    }
                }
            }
        }

        if filters:
            for field, value in filters.items():
                search_body["query"]["bool"]["filter"] = [
                    {"term": {field: value}}
                ]

        return await self.es.search(
            index="products",
            body=search_body
        )
```

#### Order Processing System

```python
from enum import Enum
from datetime import datetime
import asyncio

class OrderStatus(Enum):
    CREATED = "created"
    PAYMENT_PENDING = "payment_pending"
    PAID = "paid"
    PREPARING = "preparing"
    SHIPPED = "shipped"
    DELIVERED = "delivered"
    CANCELLED = "cancelled"

class OrderProcessor:
    def __init__(self):
        self.payment_service = PaymentService()
        self.inventory_service = InventoryService()
        self.notification_service = NotificationService()

    async def process_order(self, order_id: str, user_id: str, items: List[dict]):
        try:
            # 1. Check inventory
            if not await self.inventory_service.check_availability(items):
                raise Exception("Items not available")

            # 2. Calculate total
            total = sum(item["price"] * item["quantity"] for item in items)

            # 3. Process payment
            payment_result = await self.payment_service.process_payment(
                user_id, total
            )

            if payment_result["status"] == "success":
                # 4. Update inventory
                await self.inventory_service.update_inventory(items)

                # 5. Update order status
                await self.update_order_status(
                    order_id, 
                    OrderStatus.PAID
                )

                # 6. Send notification
                await self.notification_service.notify_user(
                    user_id,
                    f"Order {order_id} has been confirmed"
                )

                return {"status": "success", "order_id": order_id}
            else:
                raise Exception("Payment failed")

        except Exception as e:
            await self.update_order_status(
                order_id, 
                OrderStatus.CANCELLED
            )
            return {"status": "failed", "error": str(e)}
```

## Best Practices from Case Studies

1. System Design:
   - Start with clear requirements
   - Design for scalability
   - Plan for failure
   - Consider global users

2. Data Management:
   - Choose right database type
   - Plan for data distribution
   - Implement caching
   - Consider data consistency

3. Performance:
   - Use CDN for static content
   - Implement caching layers
   - Optimize database queries
   - Use asynchronous processing

4. Monitoring:
   - Track key metrics
   - Set up alerts
   - Monitor user experience
   - Regular performance testing