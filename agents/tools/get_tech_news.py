"""
Technology News Tool - Fetches and displays latest technology news

Usage:
    python tools/get_tech_news.py [options]
    
Options:
    --top N        Show top N articles (default: 10)
    --category C   News category (default: technology)
    --source S     RSS source URL (for RSS mode)
    --api-key K    NewsAPI.org API key
    --help         Show this help message
    
Examples:
    python tools/get_tech_news.py
    python tools/get_tech_news.py --top 5
    python tools/get_tech_news.py --source https://techcrunch.com/feed/
"""
import requests
import sys
import argparse
import json
from datetime import datetime
import xml.etree.ElementTree as ET

# NewsAPI configuration
NEWSAPI_URL = "https://newsapi.org/v2/top-headlines"

# Default RSS sources for technology news (using reliable feeds)
DEFAULT_RSS_SOURCES = [
    ("TechCrunch", "https://techcrunch.com/feed/"),
    ("Ars Technica", "https://arstechnica.com/rss/"),
    ("BBC Technology", "http://feeds.bbci.co.uk/news/technology/rss.xml"),
    ("The Verge", "https://www.theverge.com/rss/index.xml"),
    ("Wired", "https://www.wired.com/feed/rss"),
]


def get_api_key():
    """Get API key from environment variable."""
    import os
    return os.environ.get("NEWSAPI_KEY")


def fetch_newsapi_news(api_key: str = None, num_articles: int = 10) -> list:
    """
    Fetch latest technology news from NewsAPI.
    
    Args:
        api_key: NewsAPI.org API key (required)
        num_articles: Number of articles to fetch
    
    Returns:
        List of article dictionaries
    """
    if not api_key:
        api_key = get_api_key()
    
    if not api_key:
        raise ValueError(
            "API key is required for NewsAPI. Get a free API key at https://newsapi.org\n"
            "Set it via NEWSAPI_KEY environment variable or use RSS mode (default)."
        )
    
    params = {
        "category": "technology",
        "language": "en",
        "pageSize": num_articles,
        "apiKey": api_key
    }
    
    try:
        response = requests.get(NEWSAPI_URL, params=params, timeout=10)
        response.raise_for_status()
        data = response.json()
        
        if data.get("status") == "ok":
            return data.get("articles", [])
        else:
            error = data.get("message", "Unknown error")
            raise ValueError(f"API Error: {error}")
            
    except requests.RequestException as e:
        raise ConnectionError(f"Failed to fetch news: {e}")


def fetch_rss_news(source_url: str, num_articles: int = 5) -> list:
    """
    Fetch news from an RSS feed.
    
    Args:
        source_url: RSS feed URL
        num_articles: Number of articles to fetch
    
    Returns:
        List of article dictionaries
    """
    headers = {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'
    }
    
    try:
        response = requests.get(source_url, headers=headers, timeout=10)
        response.raise_for_status()
        
        root = ET.fromstring(response.content)
        
        articles = []
        for item in root.findall('.//item')[:num_articles]:
            title = item.find('title')
            link = item.find('link')
            description = item.find('description')
            pub_date = item.find('pubDate')
            
            article = {
                'title': title.text if title is not None else 'No title',
                'url': link.text if link is not None else '',
                'description': '',
                'source': {'name': 'RSS Feed'},
                'publishedAt': pub_date.text if pub_date is not None else ''
            }
            
            # Parse description (may contain HTML)
            if description is not None and description.text:
                # Remove HTML tags
                import re
                clean_desc = re.sub(r'<[^>]+>', '', description.text)
                article['description'] = clean_desc.strip()
            
            articles.append(article)
        
        return articles
        
    except ET.ParseError as e:
        raise ValueError(f"Failed to parse RSS feed: {e}")
    except requests.RequestException as e:
        raise ConnectionError(f"Failed to fetch RSS feed: {e}")


def setup_stdout_encoding():
    """Configure stdout for proper UTF-8 handling on Windows."""
    if hasattr(sys.stdout, 'reconfigure'):
        try:
            sys.stdout.reconfigure(encoding='utf-8')
        except (OSError, ValueError):
            pass


def display_news(articles: list, num_articles: int = 10, source_name: str = "Technology News"):
    """Display news articles in a formatted way."""
    setup_stdout_encoding()
    
    print("=" * 70)
    print(f"TOP TECHNOLOGY NEWS - {source_name.upper()}")
    print("=" * 70)
    print(f"Fetched at: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"Total articles: {len(articles)}")
    print("=" * 70)
    
    if not articles:
        print("\nNo articles found.")
        return
    
    for i, article in enumerate(articles[:num_articles], 1):
        title = article.get("title", "No title")
        description = article.get("description", "")
        url = article.get("url", "")
        source = article.get("source", {}).get("name", "Unknown")
        published_at = article.get("publishedAt", "")
        
        # Format published date
        published_str = published_at if published_at else "N/A"
        if published_at and len(published_at) > 30:
            published_str = published_at[:30]
        
        print(f"\n[{i}] {title}")
        print(f"    Source: {source} | Published: {published_str}")
        if description:
            # Truncate long descriptions
            if len(description) > 200:
                description = description[:200] + "..."
            print(f"    {description}")
        if url:
            print(f"    URL: {url}")
        print("-" * 70)


def parse_int_arg(value):
    """
    Parse an integer argument, handling quoted strings.
    
    This handles cases where the tool framework passes the value
    as a quoted string like '"10"' instead of a raw integer.
    """
    if isinstance(value, int):
        return value
    # Strip any surrounding quotes and convert to int
    return int(str(value).strip().strip('"').strip("'"))


def main():
    """Command-line interface for the tech news tool."""
    parser = argparse.ArgumentParser(
        description="Fetch and display latest technology news",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__
    )
    # Use type=str for --top to handle quoted values, then convert manually
    parser.add_argument(
        "--top", "-t",
        type=str,
        default="10",
        help="Number of top articles to display (default: 10)"
    )
    parser.add_argument(
        "--api-key",
        type=str,
        default=None,
        help="NewsAPI.org API key (or set NEWSAPI_KEY env var)"
    )
    parser.add_argument(
        "--source", "-s",
        type=str,
        default=None,
        help="RSS feed URL (enables RSS mode)"
    )
    parser.add_argument(
        "--all-sources",
        action="store_true",
        help="Fetch from all default RSS sources"
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Output as JSON"
    )
    
    args = parser.parse_args()
    
    # Handle potential quoted values for --top argument
    # This is a workaround for tool frameworks that quote string parameters
    try:
        num_articles = parse_int_arg(args.top)
    except (ValueError, TypeError) as e:
        print(f"Error: Invalid value for --top: {args.top}", file=sys.stderr)
        sys.exit(1)
    
    try:
        api_key = args.api_key or get_api_key()
        
        if args.source:
            # Single RSS source mode
            articles = fetch_rss_news(args.source, num_articles)
            display_news(articles, num_articles, "RSS Source")
            
        elif args.all_sources:
            # Fetch from all default RSS sources
            all_articles = []
            for name, url in DEFAULT_RSS_SOURCES:
                try:
                    articles = fetch_rss_news(url, num_articles // len(DEFAULT_RSS_SOURCES) + 1)
                    for a in articles:
                        a['source'] = {'name': name}
                    all_articles.extend(articles)
                except Exception:
                    continue
            
            if args.json:
                print(json.dumps(all_articles[:num_articles], indent=2, ensure_ascii=False))
            else:
                display_news(all_articles[:num_articles], num_articles, "Multiple Sources")
            
        elif api_key:
            # NewsAPI mode
            articles = fetch_newsapi_news(api_key, num_articles)
            if args.json:
                print(json.dumps(articles, indent=2, ensure_ascii=False))
            else:
                display_news(articles, num_articles)
            
        else:
            # Default: use RSS mode with default sources
            all_articles = []
            for name, url in DEFAULT_RSS_SOURCES:
                try:
                    articles = fetch_rss_news(url, num_articles)
                    for a in articles:
                        a['source'] = {'name': name}
                    all_articles.extend(articles)
                except Exception as e:
                    print(f"Warning: Could not fetch from {name}: {e}", file=sys.stderr)
            
            if not all_articles:
                raise ValueError("Could not fetch news from any source. Please provide an API key or valid RSS source.")
            
            if args.json:
                print(json.dumps(all_articles[:num_articles], indent=2, ensure_ascii=False))
            else:
                display_news(all_articles[:num_articles], num_articles, "Technology News")
            
    except ValueError as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)
    except ConnectionError as e:
        print(f"Connection Error: {e}", file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        print(f"Unexpected error: {e}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()