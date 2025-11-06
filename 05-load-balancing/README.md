# Load Balancing in System Design

## Introduction

Load balancing is the process of distributing network traffic across multiple servers to ensure high availability and reliability by preventing any single server from becoming overwhelmed.

## Load Balancing Algorithms

### 1. Round Robin

```python
class RoundRobinLoadBalancer:
    def __init__(self, servers):
        self.servers = servers
        self.current = 0

    def get_server(self):
        server = self.servers[self.current]
        self.current = (self.current + 1) % len(self.servers)
        return server

    def remove_server(self, server):
        self.servers.remove(server)
        if self.current >= len(self.servers):
            self.current = 0

    def add_server(self, server):
        self.servers.append(server)
```

### 2. Weighted Round Robin

```python
class WeightedRoundRobinLoadBalancer:
    def __init__(self, servers_with_weights):
        self.servers = []
        for server, weight in servers_with_weights:
            self.servers.extend([server] * weight)
        self.current = 0

    def get_server(self):
        server = self.servers[self.current]
        self.current = (self.current + 1) % len(self.servers)
        return server
```

### 3. Least Connections

```python
class LeastConnectionsLoadBalancer:
    def __init__(self, servers):
        self.servers = {server: 0 for server in servers}

    def get_server(self):
        server = min(self.servers.items(), key=lambda x: x[1])[0]
        self.servers[server] += 1
        return server

    def release_connection(self, server):
        if server in self.servers:
            self.servers[server] -= 1
```

### 4. IP Hash

```python
class IPHashLoadBalancer:
    def __init__(self, servers):
        self.servers = servers

    def get_server(self, ip_address):
        hash_value = sum(int(octet) for octet in ip_address.split('.'))
        return self.servers[hash_value % len(self.servers)]
```

## Implementation Examples

### 1. Nginx Load Balancer Configuration

```nginx
http {
    upstream backend {
        # Round Robin (default)
        server backend1.example.com:8080;
        server backend2.example.com:8080;
        server backend3.example.com:8080;
    }

    # Weighted Round Robin
    upstream backend_weighted {
        server backend1.example.com:8080 weight=3;
        server backend2.example.com:8080 weight=2;
        server backend3.example.com:8080 weight=1;
    }

    # Least Connections
    upstream backend_least_conn {
        least_conn;
        server backend1.example.com:8080;
        server backend2.example.com:8080;
        server backend3.example.com:8080;
    }

    server {
        listen 80;
        server_name example.com;

        location / {
            proxy_pass http://backend;
        }
    }
}
```

### 2. HAProxy Configuration

```haproxy
global
    log /dev/log local0
    maxconn 4096

defaults
    log     global
    mode    http
    option  httplog
    option  dontlognull
    timeout connect 5000
    timeout client  50000
    timeout server  50000

frontend http-in
    bind *:80
    default_backend servers

backend servers
    balance roundrobin
    option httpchk HEAD / HTTP/1.1\r\nHost:\ localhost
    server server1 backend1.example.com:8080 check
    server server2 backend2.example.com:8080 check
    server server3 backend3.example.com:8080 check
```

## Health Checking

### 1. Active Health Checks

```python
class HealthChecker:
    def __init__(self, servers):
        self.servers = servers
        self.healthy_servers = servers.copy()
        self._start_health_checks()

    def _check_server_health(self, server):
        try:
            response = requests.get(f"http://{server}/health", timeout=5)
            return response.status_code == 200
        except:
            return False

    def _start_health_checks(self):
        def health_check_worker():
            while True:
                for server in self.servers:
                    is_healthy = self._check_server_health(server)
                    if is_healthy and server not in self.healthy_servers:
                        self.healthy_servers.append(server)
                    elif not is_healthy and server in self.healthy_servers:
                        self.healthy_servers.remove(server)
                time.sleep(30)  # Check every 30 seconds

        Thread(target=health_check_worker, daemon=True).start()
```

### 2. Passive Health Checks

```python
class PassiveHealthChecker:
    def __init__(self, threshold=5):
        self.failure_counts = {}
        self.threshold = threshold

    def record_failure(self, server):
        self.failure_counts[server] = self.failure_counts.get(server, 0) + 1
        if self.failure_counts[server] >= self.threshold:
            return False  # Server should be removed
        return True

    def record_success(self, server):
        self.failure_counts[server] = 0
```

## Session Persistence

### 1. Cookie-Based Persistence

```python
class CookieBasedLoadBalancer:
    def __init__(self, servers):
        self.servers = servers
        self.cookie_name = "SERVERID"

    def get_server(self, cookie=None):
        if cookie and cookie in self.servers:
            return cookie
        return random.choice(self.servers)

    def set_cookie(self, response, server):
        response.set_cookie(self.cookie_name, server, max_age=3600)
```

### 2. IP-Based Persistence

```python
class IPPersistentLoadBalancer:
    def __init__(self, servers):
        self.servers = servers
        self.ip_mappings = {}

    def get_server(self, ip_address):
        if ip_address in self.ip_mappings:
            return self.ip_mappings[ip_address]
        server = random.choice(self.servers)
        self.ip_mappings[ip_address] = server
        return server
```

## Monitoring and Metrics

### Key Metrics to Track

1. Performance Metrics:
   - Response time
   - Throughput
   - Connection count
   - Error rate

2. Health Metrics:
   - Server status
   - CPU usage
   - Memory usage
   - Network latency

3. Load Balancer Metrics:
   - Active connections
   - Connection rate
   - Request rate
   - Error rate

## Best Practices

1. High Availability:
   - Use multiple load balancers
   - Implement failover
   - Regular health checks
   - Proper monitoring

2. Performance:
   - Choose appropriate algorithm
   - Optimize configurations
   - Monitor resource usage
   - Regular maintenance

3. Security:
   - SSL termination
   - DDoS protection
   - Access control
   - Regular updates

## Disaster Recovery

### 1. Failover Configuration

```nginx
# Primary Load Balancer
stream {
    upstream backend {
        server backup_lb:80 backup;
        server primary_lb:80;
    }
}
```

### 2. Backup Procedures

```bash
#!/bin/bash
# Backup load balancer configuration
backup_dir="/etc/lb_backup"
date_stamp=$(date +%Y%m%d_%H%M%S)

# Backup configs
cp /etc/nginx/nginx.conf "${backup_dir}/nginx_${date_stamp}.conf"
cp /etc/haproxy/haproxy.cfg "${backup_dir}/haproxy_${date_stamp}.cfg"

# Compress backup
tar -czf "${backup_dir}/lb_backup_${date_stamp}.tar.gz" \
    "${backup_dir}/nginx_${date_stamp}.conf" \
    "${backup_dir}/haproxy_${date_stamp}.cfg"
```