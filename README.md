# OPC Tag Monitor/Writer & Automate Data Logging

Task 1 (Screen 1): Automated Tag Monitoring & Logging
-  tags are fetched from MXOPC DA using a C# API and exposed to the frontend built with React (Vite).
- The system automatically logs tag data into a SQL Server table at a configurable interval (default: every 5 seconds).
- Users can modify tag values from the frontend, which updates both the OPC tags and the database accordingly.
- The entire process is automated, with the flexibility to adjust the logging interval as needed.

Task 2 (Screen 2): Trigger-Based Data Logging
- Similar functionality is implemented; however, data logging is not time-based.
- Logging is controlled by Boolean OPC tags, acting as triggers.
- When a trigger tag changes state (e.g., goes HIGH), the corresponding data is captured and stored in the database.

## Features

- View live OPC tag values in real time.
- Write values to tags from the UI.
- Separate Page 1 and Page 2 routing.
- Special trigger tag support with delayed write while typing.
- Auto-refresh of live values.
- Automatic logging of edited values after a delay.
- Clean, responsive UI with modern styling.
- Automatic and triggered based data logging.

## Tech Stack

- React
- React Router
- JavaScript
- CSS
- REST API backend in C#

## Project Structure

```bash
dotnet-sql-manual/
├── backend/
│   ├── Controllers/
│   ├── Models/
│   │   ├── ManualTagItem.cs
│   │   ├── OpcTagConfig.cs
│   │   ├── Page2TagResult.cs
│   │   ├── TagData.cs
│   │   └── ToggleTagRequest.cs
│   ├── Services/
│   │   ├── OpcAutoLoggingService.cs
│   │   ├── OpcRuntimeService.cs
│   │   ├── ManualLogService.cs
│   │   ├── OpcStore.cs
│   │   ├── Page2TriggerLoggingService.cs
│   │   └── Page2TriggerMonitorService.cs
│   ├── Program.cs
│   ├── tags.json
│   ├── appsettings.json
│   └── backend.csproj
├── frontend/
│   ├── public/
│   ├── src/
│   │   ├── App.jsx
│   │   ├── Page2.jsx
│   │   ├── App.css
│   │   ├── main.jsx
│   │   └── ...
│   ├── package.json
│   └── vite.config.js
├── dotnet-sql-manual.sln
└── README.md
```

## Prerequisites

Before running the project, make sure you have:

- Node.js installed
- The backend API running
- The correct API base URL configured

## Setup

1. Clone the repository.
2. Install dependencies.

```bash
npm install
```

3. Start the React app:

```bash
npm run dev
```

or if you are using Create React App:

```bash
npm start
```

## Backend API

This project expects a backend server running at:

```bash
http://localhost:5058
```

Make sure the backend exposes the required endpoints:

- `GET /api/tags`
- `GET /api/tags/live`
- `POST /api/tags/write`
- `POST /api/tags/manual-save`
- `GET /api/page2/tags`
- `GET /api/page2/live`
- `GET /api/page2/trigger-status`
- `POST /api/page2/write`

## How It Works

### Page 1
- Loads the first 5 tags from the API.
- Refreshes live values every second.
- Allows editing and writing tag values.
- Automatically saves edited values after a delay.

### Page 2
- Loads Page 2 tags from the API.
- Refreshes live values and trigger status every second.
- Shows a special trigger tag when it gets high then tag values will get logged into table.
- Delays writing the trigger tag while the user is typing.

## Routing

The app uses React Router:

- `/` → Page 1
- `/page2` → Page 2

## Important Notes

- The trigger tag is debounced so it waits briefly while the user is typing.
- Live values are refreshed periodically.
- Edited values are preserved while a tag is marked dirty.
- The backend must be running for the app to work correctly.

## Styling

The UI uses a modern card-based layout with:

- soft background colors,
- rounded corners,
- subtle shadows,
- improved spacing,
- responsive button placement.

## Troubleshooting

### API not loading
- Check that the backend is running on the correct port.
- Verify the API routes return valid JSON.

### Write not working
- Confirm the tag name is valid.
- Check backend logs for write errors.
- Ensure the OPC server connection is active.

### Routing issues
- Make sure `BrowserRouter` wraps the app in `main.jsx` or `index.jsx`.
- Confirm `useNavigate()` is used inside routed components.


## Author

Abhishek Adhalkar
