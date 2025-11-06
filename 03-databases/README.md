# Database Design in System Design

## Types of Databases

### 1. Relational Databases (SQL)

Popular options:
- PostgreSQL
- MySQL
- Oracle
- SQL Server

Key characteristics:
- ACID compliance
- Structured data
- Strong consistency
- Complex queries

### 2. NoSQL Databases

Types and examples:

#### Document Stores
- MongoDB
- CouchDB

#### Key-Value Stores
- Redis
- DynamoDB

#### Column-Family Stores
- Cassandra
- HBase

#### Graph Databases
- Neo4j
- ArangoDB

## Database Design Patterns

### 1. Sharding

```sql
-- Example of Hash-Based Sharding
CREATE TABLE users_shard_1 (
    user_id UUID PRIMARY KEY,
    username VARCHAR(50),
    email VARCHAR(100)
) PARTITION BY HASH (user_id);

-- Shard routing function
CREATE OR REPLACE FUNCTION get_shard_id(user_id UUID) 
RETURNS INTEGER AS $$
BEGIN
    RETURN abs(hash_bigint(user_id::text::bigint)) % 4;
END;
$$ LANGUAGE plpgsql;
```

### 2. Replication

```python
# Example MongoDB Replication Configuration
config = {
    '_id': 'rs0',
    'members': [
        {'_id': 0, 'host': 'mongodb0.example.net:27017'},
        {'_id': 1, 'host': 'mongodb1.example.net:27017'},
        {'_id': 2, 'host': 'mongodb2.example.net:27017'}
    ]
}

# Initialize the replica set
rs.initiate(config)
```

## Database Schema Design

### 1. Normalized Schema Example

```sql
-- User Management System

CREATE TABLE users (
    user_id SERIAL PRIMARY KEY,
    username VARCHAR(50) UNIQUE NOT NULL,
    email VARCHAR(100) UNIQUE NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE addresses (
    address_id SERIAL PRIMARY KEY,
    user_id INTEGER REFERENCES users(user_id),
    street VARCHAR(100),
    city VARCHAR(50),
    country VARCHAR(50),
    postal_code VARCHAR(20)
);

CREATE TABLE orders (
    order_id SERIAL PRIMARY KEY,
    user_id INTEGER REFERENCES users(user_id),
    total_amount DECIMAL(10,2),
    order_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

### 2. Denormalized Schema Example (NoSQL)

```javascript
// MongoDB Document Structure
{
  "user_id": "12345",
  "username": "john_doe",
  "email": "john@example.com",
  "addresses": [
    {
      "type": "home",
      "street": "123 Main St",
      "city": "San Francisco",
      "country": "USA"
    },
    {
      "type": "work",
      "street": "456 Market St",
      "city": "San Francisco",
      "country": "USA"
    }
  ],
  "orders": [
    {
      "order_id": "ORD001",
      "total_amount": 99.99,
      "order_date": "2025-11-05T10:00:00Z"
    }
  ]
}
```

## Database Optimization Techniques

### 1. Indexing Strategies

```sql
-- B-Tree Index
CREATE INDEX idx_username ON users(username);

-- Composite Index
CREATE INDEX idx_user_email_username ON users(email, username);

-- Partial Index
CREATE INDEX idx_active_users ON users(user_id) 
WHERE status = 'active';
```

### 2. Query Optimization

```sql
-- Using EXPLAIN ANALYZE
EXPLAIN ANALYZE
SELECT u.username, COUNT(o.order_id) as order_count
FROM users u
LEFT JOIN orders o ON u.user_id = o.user_id
GROUP BY u.username
HAVING COUNT(o.order_id) > 5;
```

## Database Scaling Patterns

### 1. Read Replicas

```plaintext
[Primary DB] ---> [Read Replica 1]
            ---> [Read Replica 2]
            ---> [Read Replica 3]
```

### 2. Connection Pooling

```python
# Example using psycopg2 pool
from psycopg2 import pool

connection_pool = pool.SimpleConnectionPool(
    minconn=5,
    maxconn=20,
    database="mydb",
    user="user",
    password="password",
    host="localhost"
)
```

## Database Monitoring

### Key Metrics to Monitor

1. Performance Metrics:
   - Query response time
   - Throughput
   - Cache hit ratio
   - Index usage

2. Resource Metrics:
   - CPU usage
   - Memory usage
   - Disk I/O
   - Connection count

3. Error Metrics:
   - Failed queries
   - Deadlocks
   - Replication lag
   - Connection timeouts

## Backup and Recovery

### 1. Backup Strategies

```bash
# PostgreSQL backup example
pg_dump dbname > backup.sql

# MongoDB backup example
mongodump --db=mydb --out=/backup/
```

### 2. Point-in-Time Recovery

```sql
-- Enable WAL archiving in PostgreSQL
ALTER SYSTEM SET archive_mode = on;
ALTER SYSTEM SET archive_command = 'cp %p /archive/%f';
```

## Best Practices

1. Security:
   - Use prepared statements
   - Implement proper access control
   - Regular security audits
   - Encrypt sensitive data

2. Performance:
   - Regular index maintenance
   - Query optimization
   - Connection pooling
   - Proper caching strategy

3. Maintenance:
   - Regular backups
   - Database cleanup
   - Schema updates
   - Performance monitoring