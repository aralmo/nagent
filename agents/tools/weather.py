"""
Weather tool for fetching current weather from wttr.in

Usage:
    python tools/weather.py <location> [format]
    
Examples:
    python tools/weather.py London
    python tools/weather.py New_York 2
    python tools/weather.py "New York" 1
    
Format options:
    1 - Weather icon + temperature (compact)
    2 - Weather icon + temperature + wind direction + wind speed
    3 - Location name + weather icon + temperature
    (default) - Full ASCII art weather report
"""
import requests
import sys
import codecs


def get_weather(location: str, format_type: str = None) -> str:
    """
    Get current weather for a location using wttr.in API.
    
    Args:
        location: The location to get weather for 
                  (e.g., "New York", "London", "Paris", "New_York")
        format_type: The format for the response:
                     - "1": Weather icon + temperature
                     - "2": Weather icon + temperature + wind direction + wind speed
                     - "3": Location name + weather icon + temperature
                     - None/default: Full ASCII art weather report
    
    Returns:
        Weather information as a string
    """
    if format_type:
        url = f"http://wttr.in/{location}?format={format_type}"
    else:
        url = f"http://wttr.in/{location}"
    
    try:
        r = requests.get(url, timeout=10)
        r.raise_for_status()
        return r.text
    except requests.RequestException as e:
        return f"Error fetching weather: {e}"


def main():
    """Command-line interface for the weather tool."""
    if len(sys.argv) < 2:
        print(__doc__)
        print("\nExamples:")
        print("  python tools/weather.py London")
        print("  python tools/weather.py New_York 2")
        sys.exit(1)
    
    location = sys.argv[1]
    format_type = sys.argv[2] if len(sys.argv) > 2 else None
    
    result = get_weather(location, format_type)
    
    # Handle Windows console encoding
    try:
        print(result)
    except UnicodeEncodeError:
        # Fallback: encode to utf-8, ignoring errors
        sys.stdout.buffer.write(result.encode('utf-8', errors='replace'))
        sys.stdout.buffer.write(b'\n')


if __name__ == "__main__":
    main()