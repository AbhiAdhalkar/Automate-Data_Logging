import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";

const API_BASE = "http://localhost:5058";
const TRIGGER_TAG = "ML_MTB_Andon.ShiftTimeSetting.Test.TRIGGER";
const TRIGGER_WRITE_DELAY = 1000;
const POLL_DELAY = 1000;

const Page2 = () => {
  const [tagNames, setTagNames] = useState([]);
  const [liveValues, setLiveValues] = useState({});
  const [editValues, setEditValues] = useState({});
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");
  const [triggerStatus, setTriggerStatus] = useState(false);
  const [triggerValue, setTriggerValue] = useState("");

  const editValuesRef = useRef({});
  const dirtyTagsRef = useRef(new Set());
  const writeTimersRef = useRef({});
  const pollTimeoutRef = useRef(null);
  const isMountedRef = useRef(false);
  const isPollingRef = useRef(false);
  const navigate = useNavigate();

  useEffect(() => {
    editValuesRef.current = editValues;
  }, [editValues]);

  useEffect(() => {
    isMountedRef.current = true;

    loadInitialTags();

    const handleVisibilityChange = () => {
      if (!document.hidden && tagNames.length > 0) {
        startPolling();
      }
    };

    document.addEventListener("visibilitychange", handleVisibilityChange);

    return () => {
      isMountedRef.current = false;

      if (pollTimeoutRef.current) {
        clearTimeout(pollTimeoutRef.current);
      }

      Object.values(writeTimersRef.current).forEach(clearTimeout);
      document.removeEventListener("visibilitychange", handleVisibilityChange);
    };
  }, []);

  useEffect(() => {
    if (tagNames.length === 0) return;
    startPolling();

    return () => {
      if (pollTimeoutRef.current) {
        clearTimeout(pollTimeoutRef.current);
        pollTimeoutRef.current = null;
      }
    };
  }, [tagNames]);

  function scheduleNextPoll() {
    if (!isMountedRef.current) return;
    if (document.hidden) return;

    pollTimeoutRef.current = setTimeout(async () => {
      await pollOnce();
    }, POLL_DELAY);
  }

  async function pollOnce() {
    if (!isMountedRef.current || document.hidden || isPollingRef.current) return;

    isPollingRef.current = true;

    try {
      await loadLiveAndTrigger();
    } finally {
      isPollingRef.current = false;
      scheduleNextPoll();
    }
  }

  function startPolling() {
    if (pollTimeoutRef.current) {
      clearTimeout(pollTimeoutRef.current);
      pollTimeoutRef.current = null;
    }

    scheduleNextPoll();
  }

  async function loadInitialTags() {
    try {
      setLoading(true);

      const tagsRes = await fetch(`${API_BASE}/api/page2/tags`);
      const tagsData = await tagsRes.json();

      const names = tagsData.map((x) => x.tagName ?? x.TagName);
      setTagNames(names);

      await loadLiveAndTrigger();
    } catch (error) {
      console.error(error);
      setMessage("Failed to load page 2 tags.");
    } finally {
      if (isMountedRef.current) {
        setLoading(false);
      }
    }
  }

  async function loadLiveAndTrigger() {
    try {
      const [liveRes, triggerRes] = await Promise.all([
        fetch(`${API_BASE}/api/page2/live`),
        fetch(`${API_BASE}/api/page2/trigger-status`)
      ]);

      const liveData = await liveRes.json();
      const triggerJson = await triggerRes.json();

      const liveMap = {};
      const nextEditMap = { ...editValuesRef.current };

      liveData.forEach((x) => {
        const tagName = x.tagName ?? x.TagName;
        const value = x.value ?? x.Value ?? "";

        liveMap[tagName] = value;

        if (!dirtyTagsRef.current.has(tagName)) {
          nextEditMap[tagName] = value;
        }
      });

      if (!isMountedRef.current) return;

      setLiveValues(liveMap);
      setEditValues(nextEditMap);
      editValuesRef.current = nextEditMap;

      setTriggerStatus(triggerJson.isLogging ?? triggerJson.IsLogging ?? false);
      setTriggerValue(triggerJson.triggerValue ?? triggerJson.TriggerValue ?? "");
    } catch (error) {
      console.error("Page2 refresh failed", error);
    }
  }

  async function writeTagValue(tagName, value) {
    try {
      const res = await fetch(`${API_BASE}/api/page2/write`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ tagName, value })
      });

      const result = await res.json();

      if (!res.ok) {
        throw new Error(result.error || `Write failed for ${tagName}`);
      }

      dirtyTagsRef.current.delete(tagName);
      setMessage(`✅ Written ${tagName}`);
      await loadLiveAndTrigger();
    } catch (error) {
      console.error(`Write failed for ${tagName}`, error);
      setMessage(`❌ Write failed for ${tagName}`);
    }
  }

  function scheduleTriggerWrite(tagName, value) {
    if (writeTimersRef.current[tagName]) {
      clearTimeout(writeTimersRef.current[tagName]);
    }

    writeTimersRef.current[tagName] = setTimeout(async () => {
      const latestValue = editValuesRef.current[tagName] ?? "";
      await writeTagValue(tagName, latestValue);
    }, TRIGGER_WRITE_DELAY);
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

    if (tagName === TRIGGER_TAG) {
      scheduleTriggerWrite(tagName, newValue);
      return;
    }

    writeTagValue(tagName, newValue);
  }

  if (loading) {
    return <div className="app">Loading page 2...</div>;
  }

  return (
    <div className="app">
      <div className="header">
        <div>
          <h1>Page 2 OPC Tags</h1>
          <p>Live monitoring, writing, and trigger-based logging</p>
        </div>

        <div className="header-actions">
          <button className="nav-button primary" onClick={() => navigate("/")}>
            Back to Page 1
          </button>
        </div>
      </div>

      <div className="content">
        <div className="status-bar">
          <div className="status">
            Trigger Active: {triggerStatus ? "✅ LOGGING" : "⏸️ Waiting..."}
          </div>
          <div className="status-chip">Value: {triggerValue}</div>
        </div>
      </div>

      <div className="tags-row">
        {tagNames.map((tagName) => (
          <div className="tag" key={tagName}>
            <div className="tag-header">
              <div className="tag-name">{tagName}</div>
              <div className="live-value">{String(liveValues[tagName] ?? "")}</div>
            </div>

            <span className="input-label">Write value</span>

            <input
              className="value-input"
              value={editValues[tagName] ?? ""}
              onChange={(e) => handleEditChange(tagName, e.target.value)}
              placeholder="Enter value"
            />
          </div>
        ))}
      </div>

      {message && (
        <div className="save-row">
          <div className="message">{message}</div>
        </div>
      )}
    </div>
  );
};

export default Page2;