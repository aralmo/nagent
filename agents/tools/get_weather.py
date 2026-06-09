import urllib.request
import sys

def get_weather(location):
    url = f'http://wttr.in/{location}'
    try:
        response = urllib.request.urlopen(url)
        data = response.read().decode('utf-8')
        return data
    except Exception as e:
        return f"Error: {str(e)}"

if __name__ == "__main__":
    location = sys.argv[1] if len(sys.argv) > 1 else "Brunete"
    print(get_weather(location))