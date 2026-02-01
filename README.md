# MusicApiDownloader

A small command-line utility to download and keep Yandex.Music playlists locally, verify saved playlists for changes, and schedule periodic checks. Built for .NET 7 / .NET 8.

---

## Quick summary

- Download entire playlists (tracks saved as .mp3).
- Keep a playlist metadata file (PlaylistInfo.json) beside downloaded tracks.
- Verify and synchronize local copies with the remote playlist (remove deleted tracks, try to recover missing ones).
- Schedule automatic verification + download using Windows Task Scheduler.
- Saves last used access token and save path to AppData (UserData.json) unless run in incognito.

---

## Files produced / used

- PlaylistInfo.json — stored inside the playlist folder (SavePath). Contains playlist metadata and per-track status.
- UserData.json — stored in %AppData%\<application-folder> and used to persist last token and save path.
- Downloaded tracks — saved to the specified folder; existing files are skipped.

---

## Commands & options

All commands accept common options described below.

Common options (ArgumentsBase)
- -t, --token <token>  
  Access token. If provided, it will be saved for future runs (unless using incognito). See token docs above.

- -s, --silent  
  Hide console output. Final summary and change notifications are shown as a message box (if any track changed).

- -i, --incog  
  Do not save the provided access token to persistent storage (UserData.json).

Path-related option (PathArgument)
- -p, --path <path>  
  Path to the folder where playlist is (for verify/status) or where tracks will be saved (for download). The last used path is stored and reused on subsequent runs (unless omitted).

Download (DownloadArguments)
- download (verb) — Downloads the whole playlist.
- -l, --list <id|uuid> (required)  
  Playlist identifier: either numeric id or uuid (uid). When the old format with numeric id is used, you can specify -u to indicate the user owner.
- -u, --user <username>  
  (Optional) Playlist owner username — used when passing a numeric playlist id instead of uuid.

Verify (VerifyArguments)
- verify (verb) — Compare the current online playlist with the saved PlaylistInfo.json. Detects removed, lost, recovered and new tracks; attempts to re-download missing tracks.

Status (StatusArguments)
- status (verb) — Print the full saved state of the playlist (grouped by track status).

Schedule (ScheduleArguments)
- schedule (verb) — Create / remove a Windows scheduled task that runs `verify` daily.
- -m, --time <HH:mm> (default: "12:00")  
  Time of day to perform checks.
- -d, --interval <days> (default: 1)  
  Days interval between checks.
- -r, --remove  
  Remove the scheduled task (ignores other schedule options).

---

## How it works (behavior details)

1. Authorization
   - The app uses the provided access token to authorize the Yandex.Music client.
   - If -t/--token is provided it will be saved to AppData for future runs (unless -i/--incog is used).

2. Download
   - `download` fetches playlist metadata and populates PlaylistInfo.json if it doesn't exist.
   - For each track the downloader:
     - Queries the API for the track (with retry logic).
     - Skips saving if the file already exists.
     - Saves available tracks as .mp3 files.
     - Marks each track in PlaylistInfo.json with a status (Valid, Unavailable, Undownloadable, Unexist, Unknown).
   - At the end PlaylistInfo.json is written and a summary is printed (or shown as a message box in silent mode).

3. Verify
   - Loads PlaylistInfo.json from the folder specified by -p/--path (or the stored last path).
   - Compares saved track IDs with the current playlist from the server.
   - Removes local files for tracks removed from the playlist.
   - Attempts to re-download tracks that are not valid.
   - Produces a VerifyReport (saved inside the save folder) and prints summary / shows message box if -s is used.

4. Status
   - Reads PlaylistInfo.json and prints grouped track statuses with counts and per-track lines.

5. Scheduling
   - On Windows the `schedule` command registers a daily Task Scheduler job that runs the app with `verify -p "<savePath>" -s`.
   - The app will warn you if the executable path changes — scheduled tasks reference the absolute executable path.
   - `schedule -r` removes the task.

6. Retry policy
   - Network operations and downloads are retried several times (configured in code). This helps transient network/API failures.

---

## Examples

- Download a playlist (using saved token and default save path):
  dotnet run -- download -l a1b2c3d4-... 

- Download and specify path + token (also saves both):
  dotnet run -- download -l a1b2c3 -p "C:\Music\MyPlaylist" -t <your-token>

- Verify a saved playlist (with silent message box on changes):
  dotnet run -- verify -p "C:\Music\MyPlaylist" -s

- Show stored playlist status:
  dotnet run -- status -p "C:\Music\MyPlaylist"

- Schedule daily verification at 03:30:
  dotnet run -- schedule -p "C:\Music\MyPlaylist" -m 03:30 -d 1

- Remove scheduled task:
  dotnet run -- schedule -r

Notes:
- If you omit -p, the last used save path from UserData.json will be used. The first time you must provide -p and/or -t.
- Use -i to avoid saving your token to disk.

---

## Troubleshooting

- "Access token must be specified at least once" — pass -t <token> or ensure token is saved in AppData from a previous run.
- "Cannot find playlist info file" — run `download` at least once for that folder, or point verify/status to the correct folder with -p.
- Scheduling fails or task does not run — ensure the path to the executable will remain unchanged and that you have sufficient privileges to register scheduled tasks.

---

## Building

- Build in Visual Studio 2022 or use dotnet CLI:
  dotnet build
  dotnet run -- [verb] [options]

---

## License

This project is licensed under the GNU Affero General Public License v3. See LICENSE.txt for details.

---

If you want, I can:
- Add translated (Russian) README,
- Add a short usage banner printed by the app,
- Or generate a small example PowerShell script to create scheduled tasks manually.