import { useEffect, useRef, useState } from "react";
import { Routes, Route, useNavigate } from "react-router-dom";
import Page2 from "./Page2";
import "./App.css";

const API_BASE = "http://localhost:5058";
const LOG_DELAY = 5000;

function Page1() {
  const [tagNames, setTagNames] = useState([]);
  const [liveValues, setLiveValues] = useState({});
  const [editValues, setEditValues] = useState({});
  const [loading, setLoading] = useState(true);
  const [autoRefresh] = useState(true);
  const [message, setMessage] = useState("");

  const editValuesRef = useRef({});
  const intervalRef = useRef(null);
  const dirtyTagsRef = useRef(new Set());
  const logTimersRef = useRef({});
  const lastLoggedValuesRef = useRef({});
  const navigate = useNavigate();

  useEffect(() => {
    editValuesRef.current = editValues;
  }, [editValues]);

  useEffect(() => {
    loadInitialTags();

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
      }

      Object.values(logTimersRef.current).forEach(clearTimeout);
    };
  }, []);

  useEffect(() => {
    if (!autoRefresh || tagNames.length === 0) return;

    if (intervalRef.current) {
      clearInterval(intervalRef.current);
    }

    intervalRef.current = setInterval(() => {
      refreshLiveValues();
    }, 1000);

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
        intervalRef.current = null;
      }
    };
  }, [autoRefresh, tagNames]);

  async function loadInitialTags() {
    try {
      setLoading(true);
      const res = await fetch(`${API_BASE}/api/tags`);
      const data = await res.json();

      const firstFive = data.slice(0, 5);
      const names = firstFive.map((x) => x.tagName);

      const liveMap = {};
      const editMap = {};

      firstFive.forEach((x) => {
        liveMap[x.tagName] = x.value ?? "";
        editMap[x.tagName] = x.value ?? "";
      });

      setTagNames(names);
      setLiveValues(liveMap);
      setEditValues(editMap);
      editValuesRef.current = editMap;
      dirtyTagsRef.current.clear();
    } catch (error) {
      setMessage("Failed to load tags.");
    } finally {
      setLoading(false);
    }
  }

  async function refreshLiveValues() {
    try {
      const res = await fetch(`${API_BASE}/api/tags/live`);
      const data = await res.json();

      const liveMap = {};
      const nextEditMap = { ...editValuesRef.current };

      data.slice(0, 5).forEach((x) => {
        const tagName = x.tagName;
        const value = x.value ?? "";

        liveMap[tagName] = value;

        if (!dirtyTagsRef.current.has(tagName)) {
          nextEditMap[tagName] = value;
        }
      });

      setLiveValues(liveMap);
      setEditValues(nextEditMap);
      editValuesRef.current = nextEditMap;
    } catch (error) {
      console.error("Live refresh failed", error);
    }
  }

  async function writeTagValue(tagName, value) {
    try {
      const res = await fetch(`${API_BASE}/api/tags/write`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ tagName, value })
      });

      if (!res.ok) {
        const result = await res.json();
        throw new Error(result.error || `Write failed for ${tagName}`);
      }
    } catch (error) {
      console.error(`Write failed for ${tagName}:`, error);
      setMessage(`❌ Write failed for ${tagName}`);
    }
  }

  async function logSingleTag(tagName, value) {
    try {
      const res = await fetch(`${API_BASE}/api/tags/manual-save`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify([{ tagName, value }])
      });

      if (!res.ok) {
        const result = await res.json();
        throw new Error(result.error || `Log failed for ${tagName}`);
      }

      lastLoggedValuesRef.current[tagName] = value;
      dirtyTagsRef.current.delete(tagName);
      setMessage(`✅ Auto-saved ${tagName}`);
      await refreshLiveValues();
    } catch (error) {
      console.error(`Log failed for ${tagName}:`, error);
      setMessage(`❌ Log failed for ${tagName}`);
    }
  }

  function scheduleAutoLog(tagName, value) {
    if (logTimersRef.current[tagName]) {
      clearTimeout(logTimersRef.current[tagName]);
    }

    logTimersRef.current[tagName] = setTimeout(async () => {
      const latestValue = editValuesRef.current[tagName] ?? "";

      if (
        latestValue !== "" &&
        lastLoggedValuesRef.current[tagName] !== latestValue
      ) {
        await logSingleTag(tagName, latestValue);
      }
    }, LOG_DELAY);
  }

  function handleEditChange(tagName, newValue) {
    dirtyTagsRef.current.add(tagName);

    setEditValues((prev) => {
      const updated = {
        ...prev,
        [tagName]: newValue
      };
      editValuesRef.current = updated;
      return updated;
    });

    setMessage("");
    writeTagValue(tagName, newValue);
    scheduleAutoLog(tagName, newValue);
  }

  if (loading) {
    return <div className="app">Loading...</div>;
  }

  return (
    <div className="app">
      <div className="header">
        <div>
          <h1>OPC Tags</h1>
          <p>Live monitoring and write control</p>
        </div>

      <div className="header-actions">
        <button className="nav-button primary" onClick={() => navigate("/page2")}>
          Go to Page 2
        </button>
      </div>
    </div>

      <div className="tags-row">
        {tagNames.map((tagName) => (
          <div className="tag" key={tagName}>
            <div className="tag-name">{tagName}</div>

            <input
              className="value-input"
              value={editValues[tagName] ?? ""}
              onChange={(e) => handleEditChange(tagName, e.target.value)}
              placeholder="0"
            />
          </div>
        ))}
      </div>

      {message && (
        <div className="save-row">
          <div className="status">{message}</div>
        </div>
      )}
    </div>
  );
}

function App() {
  return (
    <Routes>
      <Route path="/" element={<Page1 />} />
      <Route path="/page2" element={<Page2 />} />
    </Routes>
  );
}

export default App;