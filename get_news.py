import urllib.request
import xml.etree.ElementTree as ET

# TechCrunch
try:
    content = urllib.request.urlopen('https://techcrunch.com/feed/', timeout=10).read().decode('utf-8')
    root = ET.fromstring(content)
    items = root.findall('.//item')
    print("=== TECHCRUNCH TOP STORIES ===\n")
    for item in items[:5]:
        title = item.find('title').text
        link = item.find('link').text
        pub_date = item.find('pubDate').text
        print(f"HEADLINE: {title}")
        print(f"SOURCE: TechCrunch")
        print(f"DATE: {pub_date}")
        print(f"LINK: {link}\n")
except Exception as e:
    print(f"TechCrunch error: {e}")