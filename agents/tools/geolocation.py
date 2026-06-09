"""
Geolocation tool for fetching current location based on internet connection

Usage:
    python tools/geolocation.py
    
Returns:
    JSON with location information including:
    - ip: Your public IP address
    - city: City name
    - region: Region/State
    - country: Country
    - latitude: Latitude coordinate
    - longitude: Longitude coordinate
    - postal_code: Postal/ZIP code
    - timezone: Timezone
"""

import requests
import sys
import json


def get_location() -> dict:
    """
    Get current location based on internet connection using whereami.com API.
    
    Returns:
        Dictionary with location information
    """
    url = "https://ipv4.whereami.com/api/whoami.php"
    
    try:
        r = requests.get(url, timeout=10)
        r.raise_for_status()
        
        data = r.json()
        
        # Extract relevant location information
        location_info = {
            "ip": data.get("ip"),
            "ip_version": data.get("ip_version"),
            "city": data.get("geo", {}).get("city"),
            "region": data.get("geo", {}).get("region"),
            "region_code": data.get("geo", {}).get("region_code"),
            "country": data.get("geo", {}).get("country"),
            "country_code": data.get("geo", {}).get("country_code"),
            "postal_code": data.get("geo", {}).get("postal"),
            "latitude": data.get("geo", {}).get("latitude"),
            "longitude": data.get("geo", {}).get("longitude"),
            "timezone": data.get("geo", {}).get("timezone"),
            "isp": data.get("geo", {}).get("isp"),
            "organization": data.get("geo", {}).get("org"),
            "asn": data.get("geo", {}).get("asn"),
            "reverse_dns": data.get("geo", {}).get("reverse_dns"),
            "is_mobile": data.get("geo", {}).get("is_mobile"),
            "is_proxy": data.get("geo", {}).get("is_proxy"),
            "is_hosting": data.get("geo", {}).get("is_hosting"),
            "server_time": data.get("server_time")
        }
        
        return location_info
        
    except requests.RequestException as e:
        return {"error": f"Error fetching location: {e}"}


def main():
    """Command-line interface for the geolocation tool."""
    result = get_location()
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()