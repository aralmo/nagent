import urllib.request
import xml.etree.ElementTree as ET

# Fetch TechCrunch news
try:
    content = urllib.request.urlopen('https://techcrunch.com/feed/').read().decode('utf-8')
    print("=== TECHCRUNCH NEWS ===\n")
    
    # Parse RSS
    root = ET.fromstring(content)
    items = root.findall('.//item')
    
    for i, item in enumerate(items[:8]):
        title = item.find('title').text
        link = item.find('link').text
        pub_date = item.find('pubDate').text
        desc = item.find('description').text
        
        print(f"{i+1}. {title}")
        print(f"   Date: {pub_date}")
        print(f"   Link: {link}\n")
        
except Exception as e:
    print(f"Error: {e}")