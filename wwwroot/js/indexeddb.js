const DB_NAME = 'SimplyJobApplication';
const DB_VERSION = 1;

let _db = null;

async function openDb() {
    if (_db) return _db;
    return new Promise((resolve, reject) => {
        const req = indexedDB.open(DB_NAME, DB_VERSION);
        req.onupgradeneeded = e => {
            const db = e.target.result;
            if (!db.objectStoreNames.contains('settings')) {
                db.createObjectStore('settings');
            }
            if (!db.objectStoreNames.contains('sessions')) {
                const s = db.createObjectStore('sessions', { keyPath: 'id' });
                s.createIndex('createdAt', 'createdAt');
            }
            if (!db.objectStoreNames.contains('files')) {
                const s = db.createObjectStore('files', { keyPath: 'id' });
                s.createIndex('lastUsedAt', 'lastUsedAt');
            }
        };
        req.onsuccess = e => { _db = e.target.result; resolve(_db); };
        req.onerror = e => reject(e.target.error);
    });
}

function tx(store, mode, fn) {
    return openDb().then(db => new Promise((resolve, reject) => {
        const t = db.transaction(store, mode);
        const r = fn(t.objectStore(store));
        r.onsuccess = () => resolve(r.result);
        r.onerror = () => reject(r.error);
    }));
}

export async function getSetting(key) {
    const v = await tx('settings', 'readonly', s => s.get(key));
    return (v === undefined || v === null) ? null : JSON.stringify(v);
}

export async function setSetting(key, value) {
    return tx('settings', 'readwrite', s => s.put(value, key));
}

export async function getAllSessions() {
    const v = await tx('sessions', 'readonly', s => s.getAll());
    return JSON.stringify(v ?? []);
}

export async function getSession(id) {
    const v = await tx('sessions', 'readonly', s => s.get(id));
    return (v === undefined || v === null) ? null : JSON.stringify(v);
}

export async function saveSession(session) {
    return tx('sessions', 'readwrite', s => s.put(session));
}

export async function deleteSession(id) {
    return tx('sessions', 'readwrite', s => s.delete(id));
}

export async function clearSessions() {
    return tx('sessions', 'readwrite', s => s.clear());
}

export async function getAllFileMeta() {
    const files = await tx('files', 'readonly', s => s.getAll());
    const meta = (files ?? []).map(f => ({ id: f.id, name: f.name, lastUsedAt: f.lastUsedAt, sessionCount: f.sessionCount }));
    return JSON.stringify(meta);
}

export async function getFile(id) {
    const v = await tx('files', 'readonly', s => s.get(id));
    return (v === undefined || v === null) ? null : JSON.stringify(v);
}

export async function saveFile(file) {
    return tx('files', 'readwrite', s => s.put(file));
}

export async function deleteFile(id) {
    return tx('files', 'readwrite', s => s.delete(id));
}

export async function clearFiles() {
    return tx('files', 'readwrite', s => s.clear());
}

export function downloadBlob(fileName, base64Data) {
    const bytes = Uint8Array.from(atob(base64Data), c => c.charCodeAt(0));
    const blob = new Blob([bytes], {
        type: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
    });
    const url = URL.createObjectURL(blob);
    const a = Object.assign(document.createElement('a'), { href: url, download: fileName });
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}
