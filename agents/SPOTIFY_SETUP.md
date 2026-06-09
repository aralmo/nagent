# Spotify Integration Setup Guide

This guide explains how to set up the Spotify tool to control your Spotify account.

## Prerequisites

1. A Spotify account
2. Spotify Desktop app running (for playback to work)
3. Python with `spotipy` installed (already installed in this environment)

## Step 1: Create a Spotify Developer Application

1. Go to https://developer.spotify.com/dashboard
2. Log in with your Spotify account
3. Click "Create an App"
4. Fill in the details:
   - **App name**: Spotify Control Tool
   - **App description**: Control Spotify playback programmatically
   - **Redirect URIs**: Add `http://localhost:8080/callback`
5. Click "Save"

## Step 2: Get Your Client Credentials

After creating the app, you'll see:
- **Client ID**: A long string of letters and numbers
- **Client Secret**: Another long string (click "Show client secret" to reveal)

## Step 3: Configure Credentials

You have two options:

### Option A: Environment Variables (Recommended)

Set these environment variables:
```bash
set SPOTIFY_CLIENT_ID=your_client_id_here
set SPOTIFY_CLIENT_SECRET=your_client_secret_here
```

### Option B: Edit the Python file

Open `tools/spotify.py` and replace the placeholder values:
```python
SPOTIFY_CLIENT_ID = 'your_actual_client_id'
SPOTIFY_CLIENT_SECRET = 'your_actual_client_secret'
```

## Step 4: Authenticate

Run the authentication command:
```bash
python tools/spotify.py auth
```

This will:
1. Open your browser to Spotify's authorization page
2. Ask you to grant permission to the app
3. Redirect back to the local server
4. Save your credentials to `~/.spotify_credentials.json`

## Available Commands

| Command | Description |
|---------|-------------|
| `python tools/spotify.py auth` | Start authentication flow |
| `python tools/spotify.py status` | Get current playback status |
| `python tools/spotify.py play` | Play/pause (toggle) |
| `python tools/spotify.py play <query>` | Search and play a track |
| `python tools/spotify.py pause` | Pause playback |
| `python tools/spotify.py next` | Skip to next track |
| `python tools/spotify.py prev` | Previous track |
| `python tools/spotify.py volume <0-100>` | Set volume |
| `python tools/spotify.py search <query>` | Search for tracks |
| `python tools/spotify.py queue <query>` | Add track to queue |
| `python tools/spotify.py devices` | List playback devices |
| `python tools/spotify.py playlists` | List your playlists |

## Using with Custom Tools

The tools are already configured in `custom-tools.json`. You can use them like:
- `spotify-auth` - Authenticate
- `spotify-status` - Check what's playing
- `spotify-play` - Play music
- `spotify-next` - Skip track
- `spotify-playlists` - List your playlists
- etc.

## Troubleshooting

### "Not authenticated" error
Run `python tools/spotify.py auth` first.

### "No active device" error
Make sure Spotify desktop app is running and you're logged in.

### "Invalid credentials" error
Double-check your Client ID and Client Secret.

### Browser doesn't open automatically
Copy the URL from the console output and paste it in your browser manually.