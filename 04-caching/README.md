# Caching in System Design

## Introduction to Caching

Caching is a technique used to store frequently accessed data in a faster storage layer to improve system performance and reduce load on backend services.

## Caching Strategies

### 1. Cache-Aside (Lazy Loading)

```python
class CacheAside:
    def __init__(self, cache, database):
        self.cache = cache
        self.database = database

    def get_user(self, user_id):
        # Try to get from cache first
        user = self.cache.get(f"user:{user_id}")
        if user is not None:
            return user

        # If not in cache, get from database
        user = self.database.get_user(user_id)
        if user is not None:
            # Store in cache for future requests
            self.cache.set(f"user:{user_id}", user, expire=3600)
        return user
```

### 2. Write-Through

```python
class WriteThrough:
    def __init__(self, cache, database):
        self.cache = cache
        self.database = database

    def update_user(self, user_id, user_data):
        # Update database first
        self.database.update_user(user_id, user_data)
        # Then update cache
        self.cache.set(f"user:{user_id}", user_data, expire=3600)
```

### 3. Write-Behind (Write-Back)

```python
class WriteBehind:
    def __init__(self, cache, database):
        self.cache = cache
        self.database = database
        self.write_queue = Queue()
        self._start_background_worker()

    def update_user(self, user_id, user_data):
        # Update cache immediately
        self.cache.set(f"user:{user_id}", user_data, expire=3600)
        # Queue the database update
        self.write_queue.put((user_id, user_data))

    def _start_background_worker(self):
        def worker():
            while True:
                user_id, user_data = self.write_queue.get()
                self.database.update_user(user_id, user_data)
                self.write_queue.task_done()

        Thread(target=worker, daemon=True).start()
```

## Redis Implementation Examples

### 1. Basic Redis Operations

```python
import redis

# Initialize Redis connection
redis_client = redis.Redis(
    host='localhost',
    port=6379,
    db=0,
    decode_responses=True
)

# String operations
redis_client.set('user:1', 'John Doe', ex=3600)  # Expires in 1 hour
redis_client.get('user:1')

# Hash operations
redis_client.hset('user:2', mapping={
    'name': 'Jane Doe',
    'email': 'jane@example.com',
    'age': '30'
})

# List operations
redis_client.lpush('recent_users', 'user:1', 'user:2')
redis_client.lrange('recent_users', 0, -1)

# Set operations
redis_client.sadd('active_users', 'user:1', 'user:2')
redis_client.smembers('active_users')
```

### 2. Redis as a Cache

```python
class RedisCache:
    def __init__(self, redis_client):
        self.redis = redis_client

    def get_or_set(self, key, callback, expire=3600):
        # Try to get from cache
        value = self.redis.get(key)
        if value is not None:
            return value

        # If not in cache, get from callback
        value = callback()
        if value is not None:
            # Store in cache
            self.redis.set(key, value, ex=expire)
        return value
```

## Cache Invalidation Strategies

### 1. Time-Based Invalidation

```python
def set_with_ttl(cache, key, value, ttl):
    cache.set(key, value, ex=ttl)
```

### 2. Event-Based Invalidation

```python
class EventBasedCache:
    def __init__(self, cache):
        self.cache = cache

    def invalidate_user_cache(self, user_id):
        # Invalidate user data
        self.cache.delete(f"user:{user_id}")
        # Invalidate related data
        self.cache.delete(f"user:{user_id}:posts")
        self.cache.delete(f"user:{user_id}:followers")
```

### 3. Version-Based Invalidation

```python
class VersionedCache:
    def __init__(self, cache):
        self.cache = cache

    def get_with_version(self, key, version):
        return self.cache.get(f"{key}:v{version}")

    def set_with_version(self, key, value, version, expire=3600):
        # Set new version
        self.cache.set(f"{key}:v{version}", value, ex=expire)
        # Update current version
        self.cache.set(f"{key}:current_version", version)
```

## Cache Consistency Patterns

### 1. Read-Through Cache

```python
class ReadThroughCache:
    def __init__(self, cache, database):
        self.cache = cache
        self.database = database

    def get_user(self, user_id):
        def database_callback():
            return self.database.get_user(user_id)

        return self.cache.get_or_set(
            key=f"user:{user_id}",
            callback=database_callback,
            expire=3600
        )
```

### 2. Refresh-Ahead Pattern

```python
class RefreshAheadCache:
    def __init__(self, cache, database):
        self.cache = cache
        self.database = database

    def setup_refresh_trigger(self, key, callback, refresh_threshold=0.75):
        """
        Refresh cache when TTL reaches threshold
        """
        ttl = self.cache.ttl(key)
        if ttl and ttl < (3600 * refresh_threshold):
            value = callback()
            self.cache.set(key, value, ex=3600)
```

## Best Practices

1. Cache Design:
   - Use appropriate cache size
   - Choose suitable TTL values
   - Implement cache warming
   - Monitor cache hit/miss rates

2. Error Handling:
   - Handle cache failures gracefully
   - Implement circuit breakers
   - Use fallback mechanisms
   - Monitor cache health

3. Performance Optimization:
   - Use batch operations
   - Implement compression
   - Monitor memory usage
   - Optimize cache keys

## Monitoring and Maintenance

### Key Metrics to Monitor

1. Performance Metrics:
   - Hit rate
   - Miss rate
   - Response time
   - Eviction rate

2. Resource Metrics:
   - Memory usage
   - Network bandwidth
   - CPU usage
   - Connection count

3. Error Metrics:
   - Connection failures
   - Timeouts
   - Evictions
   - Out of memory errors