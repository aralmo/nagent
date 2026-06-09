"""
Spotify Control Tool - Control Spotify playback using your own account

Usage:
    python tools/spotify.py [command] [arguments]

Commands:
    auth                - Start authentication flow (opens browser)
    status              - Get current playback status
    play                - Play/pause playback
    next                - Skip to next track
    prev                - Go to previous track
    volume <0-100>      - Set volume level
    search <query>      - Search for a track/artist/album
    play <query>        - Search and play a track
    play <query> --device <device_id>  - Search and play on specific device
    queue <query>       - Add track to queue
    devices             - List available devices
    playlists           - List current user's playlists

Authentication:
    First run 'python tools/spotify.py auth' to authenticate.
    This will create a .spotify_credentials.json file with your tokens.

Requirements:
    pip install spotipy requests

Setup:
    1. Go to https://developer.spotify.com/dashboard
    2. Create a new application
    3. Add 'http://localhost:8080/callback' to Redirect URIs
    4. Set SPOTIFY_CLIENT_ID and SPOTIFY_CLIENT_SECRET environment variables
       or edit this file to include them
"""

import os
import sys
import json
import spotipy
from spotipy.oauth2 import SpotifyOAuth
import webbrowser
from http.server import HTTPServer, BaseHTTPRequestHandler
import threading
import time
import re

# Configuration - Set your Spotify App credentials here
# Get these from https://developer.spotify.com/dashboard
SPOTIFY_CLIENT_ID = os.environ.get('SPOTIFY_CLIENT_ID', 'your_client_id_here')
SPOTIFY_CLIENT_SECRET = os.environ.get('SPOTIFY_CLIENT_SECRET', 'your_client_secret_here')
SPOTIFY_REDIRECT_URI = 'http://127.0.0.1:8080/callback'

# Scope of permissions needed
SPOTIFY_SCOPE = 'user-read-playback-state,user-modify-playback-state,user-library-read,user-library-modify,playlist-read-private,playlist-modify-private'

CREDENTIALS_FILE = os.path.expanduser('~/.spotify_credentials.json')


def get_spotify_client():
    """Get authenticated Spotify client."""
    if not os.path.exists(CREDENTIALS_FILE):
        print(json.dumps({"error": "Not authenticated. Run 'python tools/spotify.py auth' first."}))
        return None
    
    with open(CREDENTIALS_FILE, 'r') as f:
        credentials = json.load(f)
    
    sp_oauth = SpotifyOAuth(
        client_id=SPOTIFY_CLIENT_ID,
        client_secret=SPOTIFY_CLIENT_SECRET,
        redirect_uri=SPOTIFY_REDIRECT_URI,
        cache_path=CREDENTIALS_FILE
    )
    
    # Check if token is expired
    if credentials.get('expires_at', 0) < time.time():
        try:
            new_token = sp_oauth.refresh_access_token(credentials['refresh_token'])
            credentials['access_token'] = new_token['access_token']
            credentials['expires_at'] = time.time() + new_token['expires_in']
            with open(CREDENTIALS_FILE, 'w') as f:
                json.dump(credentials, f)
        except Exception as e:
            print(json.dumps({"error": f"Token refresh failed: {e}"}))
            return None
    
    return spotipy.Spotify(auth=credentials['access_token'])


def start_auth_flow():
    """Start OAuth authentication flow."""
    if not SPOTIFY_CLIENT_ID or SPOTIFY_CLIENT_ID == 'your_client_id_here':
        print(json.dumps({"error": "Please set SPOTIFY_CLIENT_ID environment variable or edit spotify.py"}))
        return
    
    if not SPOTIFY_CLIENT_SECRET or SPOTIFY_CLIENT_SECRET == 'your_client_secret_here':
        print(json.dumps({"error": "Please set SPOTIFY_CLIENT_SECRET environment variable or edit spotify.py"}))
        return
    
    sp_oauth = SpotifyOAuth(
        client_id=SPOTIFY_CLIENT_ID,
        client_secret=SPOTIFY_CLIENT_SECRET,
        redirect_uri=SPOTIFY_REDIRECT_URI,
        scope=SPOTIFY_SCOPE
    )
    
    auth_url = sp_oauth.get_authorize_url()
    print(json.dumps({"message": "Opening browser for authentication...", "url": auth_url}))
    webbrowser.open(auth_url)
    
    # Start a local server to handle the callback
    class CallbackHandler(BaseHTTPRequestHandler):
        def do_GET(self):
            if 'code' in self.path:
                code = self.path.split('code=')[1].split('&')[0]
                token_info = sp_oauth.get_access_token(code)
                
                # Save credentials
                credentials = {
                    'access_token': token_info['access_token'],
                    'refresh_token': token_info['refresh_token'],
                    'expires_at': time.time() + token_info['expires_in']
                }
                with open(CREDENTIALS_FILE, 'w') as f:
                    json.dump(credentials, f)
                
                self.send_response(200)
                self.send_header('Content-type', 'text/html')
                self.end_headers()
                self.wfile.write(b'<html><body><h1>Authentication successful!</h1><p>You can close this window.</p></body></html>')
            else:
                self.send_response(400)
                self.end_headers()
    
    server = HTTPServer(('localhost', 8080), CallbackHandler)
    print(json.dumps({"message": "Waiting for authentication callback..."}))
    server.handle_request()
    print(json.dumps({"message": "Authentication complete! Credentials saved."}))


def get_status():
    """Get current playback status."""
    sp = get_spotify_client()
    if not sp:
        return
    
    try:
        playback = sp.current_playback()
        if playback and playback.get('is_playing'):
            track = playback['item']
            status = {
                "status": "playing",
                "track": track['name'],
                "artist": ", ".join([a['name'] for a in track['artists']]),
                "album": track['album']['name'],
                "progress_ms": playback['progress_ms'],
                "duration_ms": track['duration_ms'],
                "volume_percent": playback['device']['volume_percent'],
                "device": playback['device']['name'],
                "device_id": playback['device']['id']
            }
        else:
            status = {"status": "paused"}
        print(json.dumps(status, indent=2))
    except Exception as e:
        print(json.dumps({"error": str(e)}))


def control_playback(action, device_id=None):
    """Control playback (play/pause)."""
    sp = get_spotify_client()
    if not sp:
        return
    
    try:
        if action == 'play':
            if device_id:
                sp.start_playback(device_id=device_id)
            else:
                sp.start_playback()
            print(json.dumps({"message": "Playback started"}))
        elif action == 'pause':
            sp.pause_playback()
            print(json.dumps({"message": "Playback paused"}))
    except Exception as e:
        print(json.dumps({"error": str(e)}))


def skip_track(direction='forward'):
    """Skip to next or previous track."""
    sp = get_spotify_client()
    if not sp:
        return
    
    try:
        if direction == 'next':
            sp.next_track()
            print(json.dumps({"message": "Skipped to next track"}))
        else:
            sp.previous_track()
            print(json.dumps({"message": "Went to previous track"}))
    except Exception as e:
        print(json.dumps({"error": str(e)}))


def set_volume(volume):
    """Set volume level (0-100)."""
    try:
        volume = int(volume)
        if volume < 0 or volume > 100:
            print(json.dumps({"error": "Volume must be between 0 and 100"}))
            return
        
        sp = get_spotify_client()
        if not sp:
            return
        
        sp.volume(volume)
        print(json.dumps({"message": f"Volume set to {volume}"}))
    except ValueError:
        print(json.dumps({"error": "Volume must be a number"}))


def search_and_play(query, device_id=None):
    """Search for a track and play it."""
    sp = get_spotify_client()
    if not sp:
        return
    
    try:
        results = sp.search(q=query, type='track', limit=1)
        tracks = results['tracks']['items']
        
        if not tracks:
            print(json.dumps({"error": "No tracks found"}))
            return
        
        track_uri = tracks[0]['uri']
        if device_id:
            sp.start_playback(device_id=device_id, uris=[track_uri])
        else:
            sp.start_playback(uris=[track_uri])
        print(json.dumps({
            "message": f"Playing: {tracks[0]['name']} by {tracks[0]['artists'][0]['name']}",
            "device_id": device_id
        }))
    except Exception as e:
        print(json.dumps({"error": str(e)}))


def search_query(query):
    """Search for tracks, artists, albums."""
    sp = get_spotify_client()
    if not sp:
        return
    
    try:
        results = sp.search(q=query, type='track,artist,album', limit=10)
        
        output = {
            "tracks": [{"name": t['name'], "artist": t['artists'][0]['name'], "uri": t['uri']} 
                      for t in results['tracks']['items']],
            "artists": [{"name": a['name'], "uri": a['uri']} 
                       for a in results['artists']['items']],
            "albums": [{"name": a['name'], "artist": a['artists'][0]['name'], "uri": a['uri']} 
                      for a in results['albums']['items']]
        }
        print(json.dumps(output, indent=2))
    except Exception as e:
        print(json.dumps({"error": str(e)}))


def add_to_queue(query):
    """Add a track to the queue."""
    sp = get_spotify_client()
    if not sp:
        return
    
    try:
        results = sp.search(q=query, type='track', limit=1)
        tracks = results['tracks']['items']
        
        if not tracks:
            print(json.dumps({"error": "No tracks found"}))
            return
        
        track_uri = tracks[0]['uri']
        sp.add_to_queue(track_uri)
        print(json.dumps({
            "message": f"Added to queue: {tracks[0]['name']} by {tracks[0]['artists'][0]['name']}"
        }))
    except Exception as e:
        print(json.dumps({"error": str(e)}))


def list_devices():
    """List available playback devices."""
    sp = get_spotify_client()
    if not sp:
        return
    
    try:
        devices = sp.devices()
        output = {
            "devices": [{"id": d['id'], "name": d['name'], "type": d['type'], 
                        "is_active": d['is_active'], "volume_percent": d['volume_percent']}
                       for d in devices['devices']]
        }
        print(json.dumps(output, indent=2))
    except Exception as e:
        print(json.dumps({"error": str(e)}))


def list_playlists(limit=50):
    """List current user's playlists."""
    sp = get_spotify_client()
    if not sp:
        return
    
    try:
        playlists = sp.current_user_playlists(limit=limit)
        output = {
            "total": playlists['total'],
            "playlists": [
                {
                    "name": p['name'],
                    "id": p['id'],
                    "description": p.get('description', ''),
                    "tracks_total": p['tracks']['total'],
                    "is_public": p['public'],
                    "uri": p['external_urls']['spotify']
                }
                for p in playlists['items']
            ]
        }
        print(json.dumps(output, indent=2))
    except Exception as e:
        print(json.dumps({"error": str(e)}))


def main():
    """Main CLI interface."""
    if len(sys.argv) < 2:
        print(__doc__)
        return
    
    command = sys.argv[1].lower()
    
    if command == 'auth':
        start_auth_flow()
    elif command == 'status':
        get_status()
    elif command == 'play':
        # Check for --device flag
        device_id = None
        query_parts = []
        
        i = 2
        while i < len(sys.argv):
            if sys.argv[i] == '--device' and i + 1 < len(sys.argv):
                device_id = sys.argv[i + 1]
                i += 2
            else:
                query_parts.append(sys.argv[i])
                i += 1
        
        query = ' '.join(query_parts)
        if query:
            search_and_play(query, device_id)
        else:
            control_playback('play', device_id)
    elif command == 'pause':
        control_playback('pause')
    elif command == 'next':
        skip_track('next')
    elif command == 'prev':
        skip_track('prev')
    elif command == 'volume':
        if len(sys.argv) > 2:
            set_volume(sys.argv[2])
        else:
            print(json.dumps({"error": "Usage: python tools/spotify.py volume <0-100>"}))
    elif command == 'search':
        if len(sys.argv) > 2:
            search_query(' '.join(sys.argv[2:]))
        else:
            print(json.dumps({"error": "Usage: python tools/spotify.py search <query>"}))
    elif command == 'queue':
        if len(sys.argv) > 2:
            add_to_queue(' '.join(sys.argv[2:]))
        else:
            print(json.dumps({"error": "Usage: python tools/spotify.py queue <query>"}))
    elif command == 'devices':
        list_devices()
    elif command == 'playlists':
        list_playlists()
    else:
        print(json.dumps({"error": f"Unknown command: {command}"}))


if __name__ == "__main__":
    main()